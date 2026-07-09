using System;
using System.Diagnostics;
using System.IO;
using CopilotSessionTracker.Models;

namespace CopilotSessionTracker.Services;

/// <summary>
/// Launches a standalone terminal window that runs a (configurable) Copilot CLI command
/// for a session. The command is defined by a template that supports the tokens
/// <c>{id}</c> (session id) and <c>{cwd}</c> (working directory). Windows Terminal
/// (wt.exe) is preferred; falls back to cmd.exe. In both cases the command runs inside a
/// shell (cmd /k) so the window stays open and any output/errors remain visible.
/// </summary>
public static class TerminalLauncher
{
    /// <summary>
    /// Default command template. Includes <c>--prefer-version 1.0.60</c> so the terminal
    /// resumes with a pinned CLI version; users can edit this in the app.
    /// </summary>
    public const string DefaultCommandTemplate = "copilot --resume={id} --yolo --prefer-version 1.0.60";

    public static void OpenSession(SessionInfo session, string? commandTemplate = null)
    {
        ArgumentNullException.ThrowIfNull(session);

        var command = BuildCommand(session, commandTemplate);
        var workingDirectory = ResolveWorkingDirectory(session.WorkingDirectory);

        var windowsTerminal = ResolveWindowsTerminal();
        ProcessStartInfo startInfo;

        if (windowsTerminal is not null)
        {
            // wt.exe -d <dir> cmd /k <command>
            var arguments = "-d " + Quote(workingDirectory) + " cmd /k " + Quote(command);
            startInfo = new ProcessStartInfo
            {
                FileName = windowsTerminal,
                Arguments = arguments,
                UseShellExecute = true,
            };
        }
        else
        {
            // cmd.exe /k <command>   (fresh, visible console window)
            startInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/k " + Quote(command),
                WorkingDirectory = workingDirectory,
                UseShellExecute = true,
            };
        }

        Process.Start(startInfo);
    }

    /// <summary>
    /// Substitutes the <c>{id}</c> and <c>{cwd}</c> tokens in the template. Falls back to
    /// the default template when none is supplied.
    /// </summary>
    public static string BuildCommand(SessionInfo session, string? commandTemplate)
    {
        var template = string.IsNullOrWhiteSpace(commandTemplate)
            ? DefaultCommandTemplate
            : commandTemplate.Trim();

        return template
            .Replace("{id}", session.Id, StringComparison.OrdinalIgnoreCase)
            .Replace("{cwd}", session.WorkingDirectory ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    private static string ResolveWorkingDirectory(string candidate)
    {
        if (!string.IsNullOrWhiteSpace(candidate) && Directory.Exists(candidate))
        {
            return candidate;
        }

        return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    }

    /// <summary>
    /// Locates <c>wt.exe</c> on PATH, or via the well-known app-execution alias. Returns
    /// the full path or null when Windows Terminal is unavailable.
    /// </summary>
    private static string? ResolveWindowsTerminal()
    {
        var pathValue = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (var directory in pathValue.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                var full = Path.Combine(directory.Trim(), "wt.exe");
                if (File.Exists(full))
                {
                    return full;
                }
            }
            catch (ArgumentException)
            {
                // Skip malformed PATH entries.
            }
        }

        var alias = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Microsoft", "WindowsApps", "wt.exe");
        return File.Exists(alias) ? alias : null;
    }

    private static string Quote(string value) => "\"" + value + "\"";
}
