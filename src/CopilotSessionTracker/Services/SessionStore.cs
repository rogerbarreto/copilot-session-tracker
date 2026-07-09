using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using CopilotSessionTracker.Models;
using Microsoft.Data.Sqlite;

namespace CopilotSessionTracker.Services;

/// <summary>
/// Reads local Copilot CLI session data from ~/.copilot. The set of "local" sessions is
/// defined by the folders under ~/.copilot/session-state; metadata and conversation turns
/// are enriched from the ~/.copilot/session-store.db SQLite database.
/// </summary>
public sealed class SessionStore
{
    private readonly string _copilotRoot;
    private readonly string _sessionStateDir;
    private readonly string _dbPath;

    public SessionStore()
    {
        _copilotRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".copilot");
        _sessionStateDir = Path.Combine(_copilotRoot, "session-state");
        _dbPath = Path.Combine(_copilotRoot, "session-store.db");
    }

    public bool DatabaseExists => File.Exists(_dbPath);

    public string SessionStateDir => _sessionStateDir;

    /// <summary>
    /// Returns every local session (a folder under session-state), newest activity first.
    /// </summary>
    public IReadOnlyList<SessionInfo> LoadSessions()
    {
        var localIds = EnumerateLocalSessionIds();
        if (localIds.Count == 0)
        {
            return Array.Empty<SessionInfo>();
        }

        var fromDb = LoadFromDatabase(localIds);

        var results = new List<SessionInfo>(localIds.Count);
        foreach (var id in localIds)
        {
            if (fromDb.TryGetValue(id, out var info))
            {
                results.Add(info);
            }
            else
            {
                // Folder without a database row (e.g. a brand-new session) — read workspace.yaml.
                results.Add(LoadFromWorkspaceYaml(id));
            }
        }

        return results
            .OrderByDescending(s => s.LastActivityAt ?? DateTimeOffset.MinValue)
            .ToList();
    }

    /// <summary>
    /// Returns the most recent <paramref name="count"/> round trips for a session,
    /// ordered chronologically (oldest first) so they read top-to-bottom.
    /// </summary>
    public IReadOnlyList<ConversationTurn> LoadRecentTurns(string sessionId, int count = 5)
    {
        if (!DatabaseExists)
        {
            return Array.Empty<ConversationTurn>();
        }

        var turns = new List<ConversationTurn>();
        using var snapshot = DatabaseSnapshot.Create(_dbPath);
        using var connection = new SqliteConnection(snapshot.ConnectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT turn_index, user_message, assistant_response, timestamp
            FROM turns
            WHERE session_id = $id
            ORDER BY turn_index DESC
            LIMIT $count;
            """;
        command.Parameters.AddWithValue("$id", sessionId);
        command.Parameters.AddWithValue("$count", count);

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            turns.Add(new ConversationTurn
            {
                TurnIndex = reader.GetInt32(0),
                UserMessage = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                AssistantResponse = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                Timestamp = ParseTimestamp(reader.IsDBNull(3) ? null : reader.GetString(3)),
            });
        }

        turns.Reverse();
        return turns;
    }

    private List<string> EnumerateLocalSessionIds()
    {
        if (!Directory.Exists(_sessionStateDir))
        {
            return new List<string>();
        }

        return Directory.EnumerateDirectories(_sessionStateDir)
            .Where(IsResumableSession)
            .Select(Path.GetFileName)
            .Where(name => !string.IsNullOrEmpty(name))
            .Select(name => name!)
            .ToList();
    }

    /// <summary>
    /// A session folder represents a real, locally-resumable interactive session only if it
    /// contains an <c>events.jsonl</c> conversation log. Folders holding just a
    /// <c>workspace.yaml</c> (plus empty checkpoints/files/research) are stub/marker folders
    /// pre-registered by a sync process — they have no local conversation and
    /// <c>copilot --resume</c> rejects them with "No session, task, or name matched".
    /// </summary>
    private static bool IsResumableSession(string folderPath) =>
        File.Exists(Path.Combine(folderPath, "events.jsonl"));

    private Dictionary<string, SessionInfo> LoadFromDatabase(IReadOnlyCollection<string> localIds)
    {
        var map = new Dictionary<string, SessionInfo>(StringComparer.OrdinalIgnoreCase);
        if (!DatabaseExists)
        {
            return map;
        }

        var localSet = new HashSet<string>(localIds, StringComparer.OrdinalIgnoreCase);

        using var snapshot = DatabaseSnapshot.Create(_dbPath);
        using var connection = new SqliteConnection(snapshot.ConnectionString);
        connection.Open();

        var turnStats = LoadTurnStats(connection);

        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT id, cwd, repository, branch, summary, created_at, updated_at
            FROM sessions;
            """;

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var id = reader.GetString(0);
            if (!localSet.Contains(id))
            {
                continue;
            }

            turnStats.TryGetValue(id, out var stats);
            var summary = reader.IsDBNull(4) ? null : reader.GetString(4);
            map[id] = new SessionInfo
            {
                Id = id,
                Name = string.IsNullOrWhiteSpace(summary) ? "(unnamed session)" : summary!,
                WorkingDirectory = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                Repository = reader.IsDBNull(2) ? null : reader.GetString(2),
                Branch = reader.IsDBNull(3) ? null : reader.GetString(3),
                CreatedAt = ParseTimestamp(reader.IsDBNull(5) ? null : reader.GetString(5)),
                UpdatedAt = ParseTimestamp(reader.IsDBNull(6) ? null : reader.GetString(6)),
                LastInteractionAt = stats.LastTimestamp,
                TurnCount = stats.Count,
            };
        }

        return map;
    }

    private readonly record struct TurnStats(int Count, DateTimeOffset? LastTimestamp);

    private static Dictionary<string, TurnStats> LoadTurnStats(SqliteConnection connection)
    {
        var stats = new Dictionary<string, TurnStats>(StringComparer.OrdinalIgnoreCase);
        using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT session_id, COUNT(*), MAX(timestamp) FROM turns GROUP BY session_id;";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            if (!reader.IsDBNull(0))
            {
                var last = ParseTimestamp(reader.IsDBNull(2) ? null : reader.GetString(2));
                stats[reader.GetString(0)] = new TurnStats(reader.GetInt32(1), last);
            }
        }

        return stats;
    }

    private SessionInfo LoadFromWorkspaceYaml(string id)
    {
        var yamlPath = Path.Combine(_sessionStateDir, id, "workspace.yaml");
        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (File.Exists(yamlPath))
        {
            foreach (var line in File.ReadLines(yamlPath))
            {
                var separator = line.IndexOf(':');
                if (separator <= 0)
                {
                    continue;
                }

                var key = line[..separator].Trim();
                var value = line[(separator + 1)..].Trim().Trim('"');
                if (!fields.ContainsKey(key))
                {
                    fields[key] = value;
                }
            }
        }

        fields.TryGetValue("cwd", out var cwd);
        fields.TryGetValue("repository", out var repository);
        fields.TryGetValue("branch", out var branch);
        fields.TryGetValue("created_at", out var created);
        fields.TryGetValue("updated_at", out var updated);

        return new SessionInfo
        {
            Id = id,
            Name = "(unnamed session)",
            WorkingDirectory = cwd ?? string.Empty,
            Repository = string.IsNullOrWhiteSpace(repository) ? null : repository,
            Branch = string.IsNullOrWhiteSpace(branch) ? null : branch,
            CreatedAt = ParseTimestamp(created),
            UpdatedAt = ParseTimestamp(updated),
            TurnCount = 0,
        };
    }

    private static DateTimeOffset? ParseTimestamp(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return DateTimeOffset.TryParse(
            value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var parsed)
            ? parsed
            : null;
    }
}
