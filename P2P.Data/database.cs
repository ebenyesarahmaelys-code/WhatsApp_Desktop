using System.Globalization;
using System.Net;
using LanChat.Models;
using Microsoft.Data.Sqlite;

namespace LanChat.Data;

/// <summary>
/// COUCHE BASE DE DONNÉES (SQLite).
///
/// Principes :
///  1. Une connexion COURTE par opération (le pool de SQLite la réutilise) -> thread-safe sans verrou.
///  2. Requêtes TOUJOURS paramétrées ($param) -> impossible d'injecter du SQL.
///  3. Validation en C# ET contraintes CHECK en SQL -> deux lignes de défense.
///  4. Dates stockées en texte ISO-8601 UTC -> triables alphabétiquement, indépendantes du fuseau.
///  5. Id de message = GUID + "ON CONFLICT DO NOTHING" -> un message reçu deux fois n'est stocké qu'une fois.
/// </summary>
public sealed class Database
{
    private readonly string _connectionString;

    public Database(string filePath)
    {
        // Crée le dossier du fichier .db s'il n'existe pas encore.
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(filePath))!);

        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = filePath,
            Mode = SqliteOpenMode.ReadWriteCreate // crée le fichier au premier lancement
        }.ToString();

        Initialize();
    }

    // ---------------------------------------------------------------------
    //  Infrastructure
    // ---------------------------------------------------------------------

    /// <summary>Ouvre une connexion et active les réglages qui sont PAR CONNEXION.</summary>
    private SqliteConnection Open()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        using var cmd = connection.CreateCommand();
        // foreign_keys : sans ça, SQLite IGNORE les clés étrangères.
        // busy_timeout : si la base est occupée, attendre 5 s au lieu d'échouer tout de suite.
        cmd.CommandText = "PRAGMA foreign_keys = ON; PRAGMA busy_timeout = 5000;";
        cmd.ExecuteNonQuery();
        return connection;
    }

    /// <summary>Crée les tables si elles n'existent pas (idempotent : on peut l'appeler à chaque lancement).</summary>
    private void Initialize()
    {
        using var connection = Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            PRAGMA journal_mode = WAL;     -- lecteurs et écrivain ne se bloquent plus ; persistant dans le fichier
            PRAGMA synchronous = NORMAL;   -- bon compromis vitesse / sécurité avec WAL

            CREATE TABLE IF NOT EXISTS Settings (
                SettingKey   TEXT PRIMARY KEY,
                SettingValue TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS Peers (
                Id          TEXT PRIMARY KEY CHECK (length(Id) = 36),
                Name        TEXT NOT NULL CHECK (length(Name) BETWEEN 1 AND 50),
                LastIp      TEXT,
                Port        INTEGER NOT NULL CHECK (Port BETWEEN 1 AND 65535),
                LastSeenUtc TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS Messages (
                Id        TEXT PRIMARY KEY CHECK (length(Id) = 36),
                PeerId    TEXT NOT NULL REFERENCES Peers(Id) ON DELETE CASCADE,
                IsMine    INTEGER NOT NULL CHECK (IsMine IN (0, 1)),
                Content   TEXT NOT NULL CHECK (length(Content) BETWEEN 1 AND 4000),
                SentAtUtc TEXT NOT NULL,
                Status    INTEGER NOT NULL CHECK (Status IN (0, 1, 2))
            );

            -- Afficher une conversation = chercher par PeerId puis trier par date : cet index le rend instantané.
            CREATE INDEX IF NOT EXISTS IX_Messages_Peer_Date ON Messages (PeerId, SentAtUtc);

            -- Index partiel : ne contient QUE les messages à renvoyer (très petit, très rapide).
            CREATE INDEX IF NOT EXISTS IX_Messages_Pending ON Messages (PeerId) WHERE IsMine = 1 AND Status = 0;
            """;
        cmd.ExecuteNonQuery();
    }

    private static string ToDb(DateTime d) => d.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

    private static DateTime FromDb(string s) =>
        DateTime.Parse(s, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

    private static void RequireId(string id, string paramName)
    {
        if (Sanitizer.NormalizeId(id) != id)
            throw new ArgumentException("Identifiant invalide (GUID normalisé attendu).", paramName);
    }

    // ---------------------------------------------------------------------
    //  Réglages (mon identité)
    // ---------------------------------------------------------------------

    /// <summary>Lit un réglage ; s'il n'existe pas, le crée avec la valeur fournie par 'factory'.</summary>
    public string GetOrCreateSetting(string key, Func<string> factory)
    {
        using var connection = Open();

        using (var insert = connection.CreateCommand())
        {
            insert.CommandText =
                "INSERT INTO Settings (SettingKey, SettingValue) VALUES ($k, $v) ON CONFLICT(SettingKey) DO NOTHING";
            insert.Parameters.AddWithValue("$k", key);
            insert.Parameters.AddWithValue("$v", factory());
            insert.ExecuteNonQuery();
        }

        using var select = connection.CreateCommand();
        select.CommandText = "SELECT SettingValue FROM Settings WHERE SettingKey = $k";
        select.Parameters.AddWithValue("$k", key);
        return (string)select.ExecuteScalar()!;
    }

    // ---------------------------------------------------------------------
    //  Contacts
    // ---------------------------------------------------------------------

    /// <summary>Ajoute un contact ou met à jour son nom / IP / port / dernière vue (UPSERT).</summary>
    public void UpsertPeer(Peer peer)
    {
        RequireId(peer.Id, nameof(peer));
        var name = Sanitizer.Name(peer.Name) ?? throw new ArgumentException("Nom invalide.", nameof(peer));
        if (peer.LastIp is null || !IPAddress.TryParse(peer.LastIp, out _))
            throw new ArgumentException("Adresse IP invalide.", nameof(peer));
        if (peer.Port is < 1 or > 65535)
            throw new ArgumentException("Port invalide.", nameof(peer));

        using var connection = Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO Peers (Id, Name, LastIp, Port, LastSeenUtc)
            VALUES ($id, $name, $ip, $port, $seen)
            ON CONFLICT(Id) DO UPDATE SET
                Name = excluded.Name,
                LastIp = excluded.LastIp,
                Port = excluded.Port,
                LastSeenUtc = excluded.LastSeenUtc
            """;
        cmd.Parameters.AddWithValue("$id", peer.Id);
        cmd.Parameters.AddWithValue("$name", name);
        cmd.Parameters.AddWithValue("$ip", peer.LastIp);
        cmd.Parameters.AddWithValue("$port", peer.Port);
        cmd.Parameters.AddWithValue("$seen", ToDb(peer.LastSeenUtc));
        cmd.ExecuteNonQuery();
    }

    public List<Peer> GetPeers()
    {
        using var connection = Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT Id, Name, LastIp, Port, LastSeenUtc FROM Peers ORDER BY Name COLLATE NOCASE";
        using var reader = cmd.ExecuteReader();
        var list = new List<Peer>();
        while (reader.Read()) list.Add(ReadPeer(reader));
        return list;
    }

    public Peer? GetPeer(string id)
    {
        RequireId(id, nameof(id));
        using var connection = Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT Id, Name, LastIp, Port, LastSeenUtc FROM Peers WHERE Id = $id";
        cmd.Parameters.AddWithValue("$id", id);
        using var reader = cmd.ExecuteReader();
        return reader.Read() ? ReadPeer(reader) : null;
    }

    private static Peer ReadPeer(SqliteDataReader r) => new(
        r.GetString(0),
        r.GetString(1),
        r.IsDBNull(2) ? null : r.GetString(2),
        r.GetInt32(3),
        FromDb(r.GetString(4)));

    // ---------------------------------------------------------------------
    //  Messages
    // ---------------------------------------------------------------------

    /// <summary>
    /// Enregistre un message. Renvoie true s'il est NOUVEAU, false si cet Id existait déjà (doublon réseau).
    /// ON CONFLICT DO NOTHING ne protège QUE contre les doublons : une donnée invalide (CHECK) lève toujours une erreur.
    /// </summary>
    public bool InsertMessage(ChatMessage m)
    {
        RequireId(m.Id, nameof(m));
        RequireId(m.PeerId, nameof(m));
        var content = Sanitizer.Message(m.Content) ?? throw new ArgumentException("Message vide.", nameof(m));

        using var connection = Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO Messages (Id, PeerId, IsMine, Content, SentAtUtc, Status)
            VALUES ($id, $peer, $mine, $content, $sent, $status)
            ON CONFLICT(Id) DO NOTHING
            """;
        cmd.Parameters.AddWithValue("$id", m.Id);
        cmd.Parameters.AddWithValue("$peer", m.PeerId);
        cmd.Parameters.AddWithValue("$mine", m.IsMine ? 1 : 0);
        cmd.Parameters.AddWithValue("$content", content);
        cmd.Parameters.AddWithValue("$sent", ToDb(m.SentAtUtc));
        cmd.Parameters.AddWithValue("$status", (int)m.Status);
        return cmd.ExecuteNonQuery() == 1; // 1 ligne insérée = nouveau ; 0 = doublon ignoré
    }

    /// <summary>Les 'limit' derniers messages d'une conversation, du plus ancien au plus récent.</summary>
    public List<ChatMessage> GetHistory(string peerId, int limit = 200)
    {
        RequireId(peerId, nameof(peerId));
        limit = Math.Clamp(limit, 1, 1000);

        using var connection = Open();
        using var cmd = connection.CreateCommand();
        // Sous-requête : on prend les N plus RÉCENTS (DESC + LIMIT), puis on les remet dans l'ordre chronologique.
        cmd.CommandText = """
            SELECT Id, PeerId, IsMine, Content, SentAtUtc, Status FROM (
                SELECT rowid AS rid, * FROM Messages
                WHERE PeerId = $peer
                ORDER BY SentAtUtc DESC, rowid DESC
                LIMIT $limit
            ) ORDER BY SentAtUtc ASC, rid ASC
            """;
        cmd.Parameters.AddWithValue("$peer", peerId);
        cmd.Parameters.AddWithValue("$limit", limit);
        return ReadMessages(cmd);
    }

    /// <summary>Messages que j'ai écrits et dont la livraison n'est pas encore confirmée.</summary>
    public List<ChatMessage> GetPending(string peerId)
    {
        RequireId(peerId, nameof(peerId));
        using var connection = Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT Id, PeerId, IsMine, Content, SentAtUtc, Status FROM Messages
            WHERE PeerId = $peer AND IsMine = 1 AND Status = 0
            ORDER BY SentAtUtc ASC
            LIMIT 100
            """;
        cmd.Parameters.AddWithValue("$peer", peerId);
        return ReadMessages(cmd);
    }

    /// <summary>Change le statut, mais uniquement vers l'avant (Status &lt; nouveau) : on ne "rétrograde" jamais.</summary>
    public bool UpdateStatus(string messageId, MessageStatus status)
    {
        RequireId(messageId, nameof(messageId));
        using var connection = Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "UPDATE Messages SET Status = $s WHERE Id = $id AND Status < $s";
        cmd.Parameters.AddWithValue("$s", (int)status);
        cmd.Parameters.AddWithValue("$id", messageId);
        return cmd.ExecuteNonQuery() == 1;
    }

    /// <summary>Exécute un SELECT de messages (commande déjà paramétrée) et convertit les lignes en objets.</summary>
    private static List<ChatMessage> ReadMessages(SqliteCommand cmd)
    {
        using var reader = cmd.ExecuteReader();
        var list = new List<ChatMessage>();
        while (reader.Read())
        {
            list.Add(new ChatMessage(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetInt32(2) == 1,
                reader.GetString(3),
                FromDb(reader.GetString(4)),
                (MessageStatus)reader.GetInt32(5)));
        }
        return list;
    }
}
