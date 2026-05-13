using System.IO;
using Microsoft.Data.Sqlite;
using System.Security.Cryptography;
using WaterCueWindows.Models;

namespace WaterCueWindows.Services;

public class DatabaseService
{
    private static readonly string DbDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) is { Length: > 0 } appData
            ? appData
            : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "WaterCue");

    private static readonly string DbPath = Path.Combine(DbDir, "hydration.sqlite");

    public static readonly DatabaseService Shared = new();

    private DatabaseService()
    {
        Directory.CreateDirectory(DbDir);
        CreateSchema();
    }

    private SqliteConnection OpenConnection()
    {
        var conn = new SqliteConnection($"Data Source={DbPath}");
        conn.Open();
        return conn;
    }

    private void CreateSchema()
    {
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS hydration_events (
                id                  INTEGER PRIMARY KEY AUTOINCREMENT,
                timestamp           REAL    NOT NULL,
                validated_by_ai     INTEGER NOT NULL DEFAULT 0,
                groq_model          TEXT,
                emergency_bypass    INTEGER NOT NULL DEFAULT 0,
                photo_hash          TEXT,
                reason              TEXT
            );
            CREATE INDEX IF NOT EXISTS idx_events_timestamp ON hydration_events(timestamp);
            """;
        cmd.ExecuteNonQuery();
    }

    public void Insert(HydrationEvent ev)
    {
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO hydration_events
                (timestamp, validated_by_ai, groq_model, emergency_bypass, photo_hash, reason)
            VALUES ($ts, $vai, $model, $emergency, $hash, $reason)
            """;
        cmd.Parameters.AddWithValue("$ts", ToEpoch(ev.Timestamp));
        cmd.Parameters.AddWithValue("$vai", ev.ValidatedByAI ? 1 : 0);
        cmd.Parameters.AddWithValue("$model", (object?)ev.GroqModel ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$emergency", ev.EmergencyBypass ? 1 : 0);
        cmd.Parameters.AddWithValue("$hash", (object?)ev.PhotoHash ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$reason", (object?)ev.Reason ?? DBNull.Value);
        cmd.ExecuteNonQuery();
    }

    public int CupsToday()
    {
        var startOfDay = ToEpoch(DateTime.Today.ToUniversalTime());
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT COUNT(*) FROM hydration_events
            WHERE timestamp >= $start AND emergency_bypass = 0
            """;
        cmd.Parameters.AddWithValue("$start", startOfDay);
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    public int CurrentStreak(int dailyGoal)
    {
        int streak = 0;
        var cursor = DateTime.Today;

        for (int i = 0; i < 365; i++)
        {
            var start = ToEpoch(cursor.ToUniversalTime());
            var end = ToEpoch(cursor.AddDays(1).ToUniversalTime());

            using var conn = OpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                SELECT COUNT(*) FROM hydration_events
                WHERE timestamp >= $start AND timestamp < $end AND emergency_bypass = 0
                """;
            cmd.Parameters.AddWithValue("$start", start);
            cmd.Parameters.AddWithValue("$end", end);
            var count = Convert.ToInt32(cmd.ExecuteScalar());

            if (count >= dailyGoal)
                streak++;
            else if (cursor.Date == DateTime.Today)
                { /* today incomplete — don't break streak */ }
            else
                break;

            cursor = cursor.AddDays(-1);
        }
        return streak;
    }

    public List<DailyCount> WeeklyCounts()
    {
        var sevenDaysAgo = ToEpoch(DateTime.UtcNow.AddDays(-7));
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT DATE(timestamp, 'unixepoch', 'localtime') AS day, COUNT(*) AS count
            FROM hydration_events
            WHERE emergency_bypass = 0 AND timestamp >= $start
            GROUP BY day ORDER BY day ASC
            """;
        cmd.Parameters.AddWithValue("$start", sevenDaysAgo);

        var results = new List<DailyCount>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            results.Add(new DailyCount(reader.GetString(0), reader.GetInt32(1)));
        return results;
    }

    public bool IsReplayedPhoto(string hash)
    {
        var oneHourAgo = ToEpoch(DateTime.UtcNow.AddHours(-1));
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT COUNT(*) FROM hydration_events
            WHERE photo_hash = $hash AND timestamp > $ago
            """;
        cmd.Parameters.AddWithValue("$hash", hash);
        cmd.Parameters.AddWithValue("$ago", oneHourAgo);
        return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
    }

    public static string PhotoHash(byte[] jpegData)
        => Convert.ToHexString(SHA256.HashData(jpegData)).ToLowerInvariant();

    private static double ToEpoch(DateTime dt)
        => new DateTimeOffset(dt, TimeSpan.Zero).ToUnixTimeMilliseconds() / 1000.0;
}

public record DailyCount(string Day, int Count);
