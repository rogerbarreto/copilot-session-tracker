using System;
using System.Collections.Generic;
using System.Linq;

namespace CopilotSessionTracker.Core;

/// <summary>
/// Decides whether a session should be hidden based on its working directory (cwd).
/// A session is ignored when its working directory equals, or lives under, any of the
/// configured ignore roots. Matching is case-insensitive and tolerant of mixed path
/// separators and trailing slashes, so users can paste paths however they like.
/// </summary>
public static class SessionDirectoryFilter
{
    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="workingDirectory"/> is the same
    /// folder as, or a descendant of, any entry in <paramref name="ignoredRoots"/>. Blank
    /// working directories and blank ignore entries never match.
    /// </summary>
    public static bool IsIgnored(string? workingDirectory, IEnumerable<string>? ignoredRoots)
    {
        if (ignoredRoots is null)
        {
            return false;
        }

        var normalizedCwd = Normalize(workingDirectory);
        if (normalizedCwd.Length == 0)
        {
            return false;
        }

        foreach (var root in ignoredRoots)
        {
            var normalizedRoot = Normalize(root);
            if (normalizedRoot.Length == 0)
            {
                continue;
            }

            if (normalizedCwd.Equals(normalizedRoot, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // Descendant match: the cwd must start with "<root>\" so that
            // "C:\foo" does not match "C:\foobar".
            if (normalizedCwd.StartsWith(normalizedRoot + '\\', StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Splits raw multi-line text (one path per line) into a clean list of ignore roots:
    /// trims each line, drops blank lines, and de-duplicates case-insensitively while
    /// preserving the first occurrence and its original order.
    /// </summary>
    public static IReadOnlyList<string> ParseRoots(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Array.Empty<string>();
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<string>();

        foreach (var rawLine in text.Split('\r', '\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0)
            {
                continue;
            }

            if (seen.Add(Normalize(line)))
            {
                result.Add(line);
            }
        }

        return result;
    }

    /// <summary>Joins ignore roots back into newline-separated text for editing.</summary>
    public static string JoinRoots(IEnumerable<string>? roots) =>
        roots is null ? string.Empty : string.Join(Environment.NewLine, roots.Where(r => !string.IsNullOrWhiteSpace(r)));

    /// <summary>
    /// Canonical comparison form: trims surrounding whitespace and quotes, converts forward
    /// slashes to backslashes, and strips trailing separators. Returns an empty string for
    /// blank input.
    /// </summary>
    private static string Normalize(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        var trimmed = path.Trim().Trim('"').Trim();
        var unified = trimmed.Replace('/', '\\');

        // Strip trailing separators, but keep a root like "C:\" intact.
        var end = unified.Length;
        while (end > 0 && unified[end - 1] == '\\')
        {
            end--;
        }

        // Preserve a drive-root trailing slash (e.g. "C:\").
        if (end == 2 && unified.Length >= 3 && unified[1] == ':')
        {
            end = 3;
        }

        return unified[..end];
    }
}
