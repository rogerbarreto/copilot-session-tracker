namespace CopilotSessionTracker.Core;

/// <summary>
/// Picks the display name Copilot CLI shows. User-renamed sessions keep their title in
/// <c>workspace.yaml</c> (<c>user_named: true</c>) while <c>session-store.db</c>'s
/// <c>summary</c> drifts with the latest conversation snippet.
/// </summary>
public static class SessionNameResolver
{
    public const string UnnamedSession = "(unnamed session)";

    public static string Resolve(string sessionId, string? dbSummary, WorkspaceMetadata workspace)
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

        return UnnamedSession;
    }

    public static bool IsUsableYamlName(string? name, string sessionId)
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
}