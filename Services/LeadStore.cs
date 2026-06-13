using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;

namespace SahlAI.Api.Services;

public record LeadSummary(string SessionId, string? Name, string? Phone, string FirstSeen, string LastSeen, int MessageCount);
public record StoredMessage(string Role, string Text, string CreatedAt);
public record VisitStats(int Total, int Today, int Week, int ChatSessions);
public record SourceCount(string Source, int Count);
public record VisitRow(string CreatedAt, string? Source, string? Ip, string? UserAgent, string? Referrer);

/// <summary>
/// Captures every chat visitor and their messages into a SQLite database.
/// Best-effort extraction of name + phone from the conversation text.
/// On Azure Linux App Service the DB lives under /home (persistent storage).
/// </summary>
public interface ILeadStore
{
    void LogTurn(string sessionId, string userText, string botReply);
    IReadOnlyList<LeadSummary> GetLeads();
    IReadOnlyList<StoredMessage> GetMessages(string sessionId);
    void LogVisit(string? source, string? path, string? referrer, string? userAgent, string? ip);
    VisitStats GetVisitStats();
    IReadOnlyList<SourceCount> GetVisitsBySource();
    IReadOnlyList<VisitRow> GetRecentVisits(int limit);
}

public class SqliteLeadStore : ILeadStore
{
    private readonly string _connString;
    private readonly object _writeLock = new();

    // UAE & international phone numbers: +9715........, 05........, or 7-15 digit groups.
    private static readonly Regex PhoneRx =
        new(@"(\+?\d[\d\s\-\(\)]{6,16}\d)", RegexOptions.Compiled);
    private static readonly Regex NameRx =
        new(@"(?:my name is|my name'?s|i am|i'?m|this is|name is|name'?s|name\s*[:\-])\s+([A-Za-z][A-Za-z .'\-]{0,40})",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Words that signal the name has ended — they must never be captured as part of it.
    private static readonly HashSet<string> NameStopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "and", "my", "is", "the", "a", "an", "im", "i", "this", "that",
        "phone", "number", "mobile", "cell", "contact", "whatsapp", "email", "mail",
        "no", "num", "call", "reach", "here", "from", "at", "on", "in",
        "looking", "interested", "searching", "trying", "planning", "keen", "want", "need",
        "buy", "buying", "rent", "renting", "sell", "selling", "new", "just"
    };

    public SqliteLeadStore()
    {
        var home = Environment.GetEnvironmentVariable("HOME");
        var dataDir = !string.IsNullOrEmpty(home)
            ? Path.Combine(home, "data")
            : Path.Combine(AppContext.BaseDirectory, "data");
        Directory.CreateDirectory(dataDir);
        var dbPath = Path.Combine(dataDir, "sahlai.db");
        _connString = $"Data Source={dbPath}";
        Init();
    }

    private SqliteConnection Open()
    {
        var c = new SqliteConnection(_connString);
        c.Open();
        return c;
    }

    private void Init()
    {
        lock (_writeLock)
        {
            using var c = Open();
            using var cmd = c.CreateCommand();
            cmd.CommandText = """
                PRAGMA journal_mode=WAL;
                CREATE TABLE IF NOT EXISTS Leads (
                    SessionId    TEXT PRIMARY KEY,
                    Name         TEXT,
                    Phone        TEXT,
                    FirstSeen    TEXT NOT NULL,
                    LastSeen     TEXT NOT NULL,
                    MessageCount INTEGER NOT NULL DEFAULT 0
                );
                CREATE TABLE IF NOT EXISTS Messages (
                    Id        INTEGER PRIMARY KEY AUTOINCREMENT,
                    SessionId TEXT NOT NULL,
                    Role      TEXT NOT NULL,
                    Text      TEXT NOT NULL,
                    CreatedAt TEXT NOT NULL
                );
                CREATE INDEX IF NOT EXISTS IX_Messages_Session ON Messages(SessionId);
                CREATE TABLE IF NOT EXISTS Visits (
                    Id        INTEGER PRIMARY KEY AUTOINCREMENT,
                    Source    TEXT,
                    Path      TEXT,
                    Referrer  TEXT,
                    UserAgent TEXT,
                    Ip        TEXT,
                    CreatedAt TEXT NOT NULL
                );
                CREATE INDEX IF NOT EXISTS IX_Visits_Created ON Visits(CreatedAt);
                """;
            cmd.ExecuteNonQuery();
        }
    }

    public void LogTurn(string sessionId, string userText, string botReply)
    {
        var now = DateTime.UtcNow.ToString("u");
        var name = ExtractName(userText);
        var phone = ExtractPhone(userText);

        lock (_writeLock)
        {
            using var c = Open();
            using var tx = c.BeginTransaction();

            InsertMessage(c, sessionId, "user", userText, now);
            InsertMessage(c, sessionId, "assistant", botReply, now);

            using (var up = c.CreateCommand())
            {
                up.CommandText = """
                    INSERT INTO Leads (SessionId, Name, Phone, FirstSeen, LastSeen, MessageCount)
                    VALUES ($s, $n, $p, $now, $now, 1)
                    ON CONFLICT(SessionId) DO UPDATE SET
                        LastSeen = $now,
                        MessageCount = MessageCount + 1,
                        Name  = COALESCE(Name, $n),
                        Phone = COALESCE(Phone, $p);
                    """;
                up.Parameters.AddWithValue("$s", sessionId);
                up.Parameters.AddWithValue("$n", (object?)name ?? DBNull.Value);
                up.Parameters.AddWithValue("$p", (object?)phone ?? DBNull.Value);
                up.Parameters.AddWithValue("$now", now);
                up.ExecuteNonQuery();
            }

            tx.Commit();
        }
    }

    private static void InsertMessage(SqliteConnection c, string session, string role, string text, string now)
    {
        using var cmd = c.CreateCommand();
        cmd.CommandText = "INSERT INTO Messages (SessionId, Role, Text, CreatedAt) VALUES ($s, $r, $t, $now);";
        cmd.Parameters.AddWithValue("$s", session);
        cmd.Parameters.AddWithValue("$r", role);
        cmd.Parameters.AddWithValue("$t", text);
        cmd.Parameters.AddWithValue("$now", now);
        cmd.ExecuteNonQuery();
    }

    public IReadOnlyList<LeadSummary> GetLeads()
    {
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT SessionId, Name, Phone, FirstSeen, LastSeen, MessageCount FROM Leads ORDER BY LastSeen DESC;";
        using var r = cmd.ExecuteReader();
        var list = new List<LeadSummary>();
        while (r.Read())
            list.Add(new LeadSummary(
                r.GetString(0),
                r.IsDBNull(1) ? null : r.GetString(1),
                r.IsDBNull(2) ? null : r.GetString(2),
                r.GetString(3), r.GetString(4), r.GetInt32(5)));
        return list;
    }

    public IReadOnlyList<StoredMessage> GetMessages(string sessionId)
    {
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT Role, Text, CreatedAt FROM Messages WHERE SessionId = $s ORDER BY Id;";
        cmd.Parameters.AddWithValue("$s", sessionId);
        using var r = cmd.ExecuteReader();
        var list = new List<StoredMessage>();
        while (r.Read())
            list.Add(new StoredMessage(r.GetString(0), r.GetString(1), r.GetString(2)));
        return list;
    }

    public void LogVisit(string? source, string? path, string? referrer, string? userAgent, string? ip)
    {
        var now = DateTime.UtcNow.ToString("u");
        lock (_writeLock)
        {
            using var c = Open();
            using var cmd = c.CreateCommand();
            cmd.CommandText = "INSERT INTO Visits (Source, Path, Referrer, UserAgent, Ip, CreatedAt) VALUES ($src,$p,$r,$ua,$ip,$now);";
            cmd.Parameters.AddWithValue("$src", (object?)source ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$p", (object?)path ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$r", (object?)referrer ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$ua", (object?)userAgent ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$ip", (object?)ip ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$now", now);
            cmd.ExecuteNonQuery();
        }
    }

    public VisitStats GetVisitStats()
    {
        var todayCut = DateTime.UtcNow.Date.ToString("u");
        var weekCut = DateTime.UtcNow.AddDays(-7).ToString("u");
        using var c = Open();
        int total = CountSince(c, null);
        int today = CountSince(c, todayCut);
        int week = CountSince(c, weekCut);

        using var lc = c.CreateCommand();
        lc.CommandText = "SELECT COUNT(*) FROM Leads;";
        int chats = Convert.ToInt32(lc.ExecuteScalar());

        return new VisitStats(total, today, week, chats);
    }

    private static int CountSince(SqliteConnection c, string? cutoff)
    {
        using var cmd = c.CreateCommand();
        if (cutoff is null)
            cmd.CommandText = "SELECT COUNT(*) FROM Visits;";
        else
        {
            cmd.CommandText = "SELECT COUNT(*) FROM Visits WHERE CreatedAt >= $cut;";
            cmd.Parameters.AddWithValue("$cut", cutoff);
        }
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    public IReadOnlyList<SourceCount> GetVisitsBySource()
    {
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT COALESCE(NULLIF(Source,''),'(direct)') AS s, COUNT(*) FROM Visits GROUP BY s ORDER BY COUNT(*) DESC;";
        using var r = cmd.ExecuteReader();
        var list = new List<SourceCount>();
        while (r.Read())
            list.Add(new SourceCount(r.GetString(0), r.GetInt32(1)));
        return list;
    }

    public IReadOnlyList<VisitRow> GetRecentVisits(int limit)
    {
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT CreatedAt, Source, Ip, UserAgent, Referrer FROM Visits ORDER BY Id DESC LIMIT $lim;";
        cmd.Parameters.AddWithValue("$lim", limit);
        using var r = cmd.ExecuteReader();
        var list = new List<VisitRow>();
        while (r.Read())
            list.Add(new VisitRow(
                r.GetString(0),
                r.IsDBNull(1) ? null : r.GetString(1),
                r.IsDBNull(2) ? null : r.GetString(2),
                r.IsDBNull(3) ? null : r.GetString(3),
                r.IsDBNull(4) ? null : r.GetString(4)));
        return list;
    }

    private static string? ExtractName(string text)
    {
        var m = NameRx.Match(text);
        if (!m.Success) return null;

        var words = new List<string>();
        foreach (var raw in m.Groups[1].Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var w = raw.Trim('.', ',', '!', '?', ':', ';');
            if (w.Length == 0) continue;
            if (NameStopWords.Contains(w)) break;          // stop at connective / contact words
            words.Add(char.ToUpperInvariant(w[0]) + w[1..]); // normalise casing (kalaiyarasi -> Kalaiyarasi)
            if (words.Count == 3) break;                    // names rarely exceed 3 tokens
        }

        var name = string.Join(' ', words);
        return string.IsNullOrWhiteSpace(name) ? null : name;
    }

    private static string? ExtractPhone(string text)
    {
        foreach (Match m in PhoneRx.Matches(text))
        {
            var digits = new string(m.Value.Where(char.IsDigit).ToArray());
            if (digits.Length is >= 7 and <= 15)
                return m.Value.Trim();
        }
        return null;
    }
}
