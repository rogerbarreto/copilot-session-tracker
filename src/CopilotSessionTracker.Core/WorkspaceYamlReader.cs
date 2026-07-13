namespace CopilotSessionTracker.Core;

public static class WorkspaceYamlReader
{
    public static WorkspaceMetadata Read(string yamlPath)
    {
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

        return new WorkspaceMetadata(
            string.IsNullOrWhiteSpace(name) ? null : name,
            string.Equals(userNamed, "true", StringComparison.OrdinalIgnoreCase),
            string.IsNullOrWhiteSpace(cwd) ? null : cwd,
            string.IsNullOrWhiteSpace(repository) ? null : repository,
            string.IsNullOrWhiteSpace(branch) ? null : branch,
            string.IsNullOrWhiteSpace(created) ? null : created,
            string.IsNullOrWhiteSpace(updated) ? null : updated);
    }

    public static WorkspaceMetadata ReadFromSessionFolder(string sessionStateDir, string sessionId) =>
        Read(Path.Combine(sessionStateDir, sessionId, "workspace.yaml"));
}