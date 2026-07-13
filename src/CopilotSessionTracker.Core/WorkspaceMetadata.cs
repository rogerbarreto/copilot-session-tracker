namespace CopilotSessionTracker.Core;

public readonly record struct WorkspaceMetadata(
    string? Name,
    bool UserNamed,
    string? Cwd,
    string? Repository,
    string? Branch,
    string? CreatedAt,
    string? UpdatedAt);