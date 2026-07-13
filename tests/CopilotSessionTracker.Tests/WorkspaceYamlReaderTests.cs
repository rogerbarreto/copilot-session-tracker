using CopilotSessionTracker.Core;

namespace CopilotSessionTracker.Tests;

public sealed class WorkspaceYamlReaderTests
{
    [Fact]
    public void Read_ParsesWorkspaceYamlFields()
    {
        var directory = Path.Combine(Path.GetTempPath(), "CopilotSessionTracker.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var yamlPath = Path.Combine(directory, "workspace.yaml");

        try
        {
            File.WriteAllText(
                yamlPath,
                """
                id: f2253d1e-4580-4d95-b572-131b476aad0a
                cwd: D:\repo\work\semantic-kernel-msrc-119552-openapi-operation-selection
                repository: microsoft/semantic-kernel
                branch: rogerbarreto/openapi-operation-selection-hardening-119552
                name: MSRC 119552 - OpenAPI Operation Selection
                user_named: true
                created_at: 2026-07-02T11:49:17.813Z
                updated_at: 2026-07-13T15:24:16.670Z
                """);

            var metadata = WorkspaceYamlReader.Read(yamlPath);

            Assert.Equal("MSRC 119552 - OpenAPI Operation Selection", metadata.Name);
            Assert.True(metadata.UserNamed);
            Assert.Equal(@"D:\repo\work\semantic-kernel-msrc-119552-openapi-operation-selection", metadata.Cwd);
            Assert.Equal("microsoft/semantic-kernel", metadata.Repository);
            Assert.Equal("rogerbarreto/openapi-operation-selection-hardening-119552", metadata.Branch);
            Assert.Equal("2026-07-02T11:49:17.813Z", metadata.CreatedAt);
            Assert.Equal("2026-07-13T15:24:16.670Z", metadata.UpdatedAt);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void ReadFromSessionFolder_UsesSessionStateLayout()
    {
        var sessionStateDir = Path.Combine(Path.GetTempPath(), "CopilotSessionTracker.Tests", Guid.NewGuid().ToString("N"));
        var sessionId = "abc12345-6789-4abc-8def-0123456789ab";
        var sessionDir = Path.Combine(sessionStateDir, sessionId);
        Directory.CreateDirectory(sessionDir);

        try
        {
            File.WriteAllText(
                Path.Combine(sessionDir, "workspace.yaml"),
                """
                name: Agent Framework Development Session
                user_named: true
                """);

            var metadata = WorkspaceYamlReader.ReadFromSessionFolder(sessionStateDir, sessionId);

            Assert.Equal("Agent Framework Development Session", metadata.Name);
            Assert.True(metadata.UserNamed);
        }
        finally
        {
            Directory.Delete(sessionStateDir, recursive: true);
        }
    }
}