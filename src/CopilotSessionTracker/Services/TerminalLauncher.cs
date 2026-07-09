using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using CopilotSessionTracker.Models;

namespace CopilotSessionTracker.Services;

/// <summary>
/// Launches a standalone terminal window that resumes a Copilot CLI session in
/// "yolo" mode (all permissions granted). Windows Terminal (wt.exe) is preferred;
/// falls back to cmd.exe.
/// </summary>
public static class TerminalLauncher
{
    public static void OpenSession(SessionInfo session)
    {
        ArgumentNullException.ThrowIfNull(session);

        var copilot = ResolveExecutable("copilot") ?? "copilot";
        var workingDirectory = ResolveWorkingDirectory(session.WorkingDirectory);

        // Interactive copilot invocation: resume the session and grant all permissions.
        var copilotArgs = $"--resume={session.Id} --yolo";

        var windowsTerminal = ResolveExecutable("wt");
        ProcessStartInfo startInfo;

        if (windowsTerminal is not null)
        {
            // wt.exe -d <dir> <copilot> --resume=<id> --yolo
            var arguments = "-d " + Quote(workingDirectory) + " " + Quote(copilot) + " " + copilotArgs;
            startInfo = new ProcessStartInfo
            {
                FileName = windowsTerminal,
                Arguments = arguments,
                UseShellExecute = true,
            };
        }
        else
        {
            // cmd.exe /k <copilot> --resume=<id> --yolo   (in a fresh, visible console window)
            startInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/k " + Quote(copilot) + " " + copilotArgs,
                WorkingDirectory = workingDirectory,
                UseShellExecute = true,
            };
        }

        Process.Start(startInfo);
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
    /// Finds an executable on PATH (trying .exe/.cmd/.bat). Returns the full path or null.
    /// </summary>
    private static string? ResolveExecutable(string name)
    {
        var extensions = new[] { ".exe", ".cmd", ".bat", "" };
        var pathValue = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        var directories = pathValue.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);

        foreach (var directory in directories)
        {
            foreach (var extension in extensions)
            {
                string full;
                try
                {
                    full = Path.Combine(directory.Trim(), name + extension);
                }
                catch (ArgumentException)
                {
                    continue;
                }

                if (File.Exists(full))
                {
                    return full;
                }
            }
        }

        // Windows Terminal is commonly reachable only via the app-execution alias.
        if (string.Equals(name, "wt", StringComparison.OrdinalIgnoreCase))
        {
            var alias = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Microsoft", "WindowsApps", "wt.exe");
            if (File.Exists(alias))
            {
                return alias;
            }
        }

        return null;
    }

    private static string Quote(string value) => "\"" + value + "\"";
}
