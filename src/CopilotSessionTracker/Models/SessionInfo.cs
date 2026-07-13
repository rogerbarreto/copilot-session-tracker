using System;

namespace CopilotSessionTracker.Models;

/// <summary>
/// A local Copilot CLI session, sourced from a folder under ~/.copilot/session-state
/// and enriched with metadata from ~/.copilot/session-store.db.
/// </summary>
public sealed class SessionInfo
{
    public required string Id { get; init; }

    /// <summary>
    /// Human-friendly session name from <c>workspace.yaml</c> (what Copilot CLI shows), with
    /// <c>session-store.db</c> "summary" as a fallback for auto-titled sessions.
    /// </summary>
    public string Name { get; init; } = "(unnamed session)";

    public string WorkingDirectory { get; init; } = string.Empty;

    public string? Repository { get; init; }

    public string? Branch { get; init; }

    public DateTimeOffset? CreatedAt { get; init; }

    public DateTimeOffset? UpdatedAt { get; init; }

    /// <summary>
    /// Timestamp of the most recent recorded conversation turn (the last time the user
    /// actually interacted with this session). Null when the session has no turns.
    /// </summary>
    public DateTimeOffset? LastInteractionAt { get; init; }

    /// <summary>
    /// Best available "last worked on" time: the last conversation turn if present,
    /// otherwise the session's updated/created timestamp.
    /// </summary>
    public DateTimeOffset? LastActivityAt => LastInteractionAt ?? UpdatedAt ?? CreatedAt;

    /// <summary>Number of user/assistant round trips recorded for this session.</summary>
    public int TurnCount { get; init; }

    public string ShortId => Id.Length >= 8 ? Id[..8] : Id;

    public string CreatedDisplay => FormatLocal(CreatedAt);

    public string UpdatedDisplay => FormatLocal(UpdatedAt);

    public string LastActivityDisplay => FormatLocal(LastActivityAt);

    public string WorkingDirectoryDisplay =>
        string.IsNullOrWhiteSpace(WorkingDirectory) ? "(no working directory)" : WorkingDirectory;

    public string RepositoryDisplay => string.IsNullOrWhiteSpace(Repository) ? "—" : Repository!;

    /// <summary>Tooltip for the session cell, combining repository and full working directory.</summary>
    public string LocationTooltip =>
        string.IsNullOrWhiteSpace(Repository)
            ? WorkingDirectoryDisplay
            : $"Repository: {Repository}\nDirectory: {WorkingDirectoryDisplay}";

    private static string FormatLocal(DateTimeOffset? value) =>
        value is null ? "—" : value.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
}
