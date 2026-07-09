using System;

namespace CopilotSessionTracker.Models;

/// <summary>
/// One recorded round trip (user message + assistant response) within a session.
/// </summary>
public sealed class ConversationTurn
{
    public int TurnIndex { get; init; }

    public string UserMessage { get; init; } = string.Empty;

    public string AssistantResponse { get; init; } = string.Empty;

    public DateTimeOffset? Timestamp { get; init; }

    public string TimestampDisplay =>
        Timestamp is null ? string.Empty : Timestamp.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");

    public string Header => $"Turn {TurnIndex + 1}   {TimestampDisplay}".Trim();

    public string UserDisplay => string.IsNullOrWhiteSpace(UserMessage) ? "(empty)" : UserMessage.Trim();

    public string AssistantDisplay =>
        string.IsNullOrWhiteSpace(AssistantResponse) ? "(no response recorded)" : AssistantResponse.Trim();
}
