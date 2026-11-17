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
