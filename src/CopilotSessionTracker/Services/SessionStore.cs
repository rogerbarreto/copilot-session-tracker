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
            var workspace = ReadWorkspaceYaml(id);
            map[id] = new SessionInfo
            {
                Id = id,
                Name = ResolveSessionName(id, summary, workspace),
                WorkingDirectory = FirstNonEmpty(workspace.Cwd, reader.IsDBNull(1) ? null : reader.GetString(1)) ?? string.Empty,
                Repository = FirstNonEmpty(workspace.Repository, reader.IsDBNull(2) ? null : reader.GetString(2)),
                Branch = FirstNonEmpty(workspace.Branch, reader.IsDBNull(3) ? null : reader.GetString(3)),
                CreatedAt = ParseTimestamp(FirstNonEmpty(workspace.CreatedAt, reader.IsDBNull(5) ? null : reader.GetString(5))),
                UpdatedAt = ParseTimestamp(FirstNonEmpty(workspace.UpdatedAt, reader.IsDBNull(6) ? null : reader.GetString(6))),
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
        var workspace = ReadWorkspaceYaml(id);

        return new SessionInfo
        {
            Id = id,
            Name = ResolveSessionName(id, dbSummary: null, workspace),
            WorkingDirectory = workspace.Cwd ?? string.Empty,
            Repository = string.IsNullOrWhiteSpace(workspace.Repository) ? null : workspace.Repository,
            Branch = string.IsNullOrWhiteSpace(workspace.Branch) ? null : workspace.Branch,
            CreatedAt = ParseTimestamp(workspace.CreatedAt),
            UpdatedAt = ParseTimestamp(workspace.UpdatedAt),
            TurnCount = 0,
        };
    }

    private WorkspaceFields ReadWorkspaceYaml(string id)
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

        fields.TryGetValue("name", out var name);
        fields.TryGetValue("user_named", out var userNamed);
        fields.TryGetValue("cwd", out var cwd);
        fields.TryGetValue("repository", out var repository);
        fields.TryGetValue("branch", out var branch);
        fields.TryGetValue("created_at", out var created);
        fields.TryGetValue("updated_at", out var updated);

        return new WorkspaceFields(
            string.IsNullOrWhiteSpace(name) ? null : name,
            string.Equals(userNamed, "true", StringComparison.OrdinalIgnoreCase),
            string.IsNullOrWhiteSpace(cwd) ? null : cwd,
            string.IsNullOrWhiteSpace(repository) ? null : repository,
            string.IsNullOrWhiteSpace(branch) ? null : branch,
            string.IsNullOrWhiteSpace(created) ? null : created,
            string.IsNullOrWhiteSpace(updated) ? null : updated);
    }

    /// <summary>
    /// Picks the display name Copilot CLI shows. User-renamed sessions keep their title in
    /// <c>workspace.yaml</c> (<c>user_named: true</c>) while <c>session-store.db</c>'s
    /// <c>summary</c> drifts with the latest conversation snippet.
    /// </summary>
    private static string ResolveSessionName(string sessionId, string? dbSummary, WorkspaceFields workspace)
    {
        if (workspace.UserNamed && !string.IsNullOrWhiteSpace(workspace.Name))
        {
            return workspace.Name!;
        }

        if (IsUsableYamlName(workspace.Name, sessionId))
        {
            return workspace.Name!;
        }

        if (!string.IsNullOrWhiteSpace(dbSummary))
        {
            return dbSummary!;
        }

        if (!string.IsNullOrWhiteSpace(workspace.Name))
        {
            return workspace.Name!;
        }

        return "(unnamed session)";
    }

    private static bool IsUsableYamlName(string? name, string sessionId)
    {
        if (string.IsNullOrWhiteSpace(name) || name == "|-")
        {
            return false;
        }

        var trimmed = name.Trim().Trim('"');
        if (Guid.TryParse(trimmed, out _))
        {
            return false;
        }

        return !string.Equals(trimmed, sessionId, StringComparison.OrdinalIgnoreCase);
    }

    private static string? FirstNonEmpty(string? preferred, string? fallback) =>
        !string.IsNullOrWhiteSpace(preferred) ? preferred : fallback;

    private readonly record struct WorkspaceFields(
        string? Name,
        bool UserNamed,
        string? Cwd,
        string? Repository,
        string? Branch,
        string? CreatedAt,
        string? UpdatedAt);

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
