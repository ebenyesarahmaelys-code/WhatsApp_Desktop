using System.Globalization;
using System.Net;
using LanChat.Models;
using Microsoft.Data.Sqlite;

namespace LanChat.Data;

public sealed class Database
{
    private readonly string _connectionString;

    public Database(string filePath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(filePath))!);

        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = filePath,
            Mode = SqliteOpenMode.ReadWriteCreate
        }.ToString();

        Initialize();
    }

    private SqliteConnection Open()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "PRAGMA foreign_keys = ON; PRAGMA busy_timeout = 5000;";
        cmd.ExecuteNonQuery();
        return connection;
    }

    private void Initialize()
    {
        using var connection = Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            PRAGMA journal_mode = WAL;
            PRAGMA synchronous = NORMAL;

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

            CREATE INDEX IF NOT EXISTS IX_Messages_Peer_Date ON Messages (PeerId, SentAtUtc);
            CREATE INDEX IF NOT EXISTS IX_Messages_Pending ON Messages (PeerId) WHERE IsMine = 1 AND Status = 0;
            """;
        cmd.ExecuteNonQuery();
    }

    private static string ToDb(DateTime d) => d.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
    private static DateTime FromDb(string s) => DateTime.Parse(s, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

    private static void RequireId(string id, string paramName)
    {
        if (Sanitizer.NormalizeId(id) != id)
            throw new ArgumentException("Identifiant invalide.", paramName);
    }

    public string GetOrCreateSetting(string key, Func<string> factory)
    {
        using var connection = Open();

        using (var insert = connection.CreateCommand())
        {
            insert.CommandText = "INSERT INTO Settings (SettingKey, SettingValue) VALUES ($k, $v) ON CONFLICT(SettingKey) DO NOTHING";
            insert.Parameters.AddWithValue("$k", key);
            insert.Parameters.AddWithValue("$v", factory());
            insert.ExecuteNonQuery();
        }

        using var select = connection.CreateCommand();
        select.CommandText = "SELECT SettingValue FROM Settings WHERE SettingKey = $k";
        select.Parameters.AddWithValue("$k", key);
        return (string)select.ExecuteScalar()!;
    }
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
        return cmd.ExecuteNonQuery() == 1;
    }

    public List<ChatMessage> GetHistory(string peerId, int limit = 200)
    {
        RequireId(peerId, nameof(peerId));
        limit = Math.Clamp(limit, 1, 1000);

        using var connection = Open();
        using var cmd = connection.CreateCommand();
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
