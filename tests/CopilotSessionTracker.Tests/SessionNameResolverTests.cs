using CopilotSessionTracker.Core;

namespace CopilotSessionTracker.Tests;

public sealed class SessionNameResolverTests
{
    private const string SessionId = "f2253d1e-4580-4d95-b572-131b476aad0a";

    [Fact]
    public void Resolve_UserNamedSession_PrefersYamlNameOverDriftingDbSummary()
    {
        var workspace = new WorkspaceMetadata(
            Name: "MSRC 119552 - OpenAPI Operation Selection",
            UserNamed: true,
            Cwd: null,
            Repository: null,
            Branch: null,
            CreatedAt: null,
            UpdatedAt: null);

        var resolved = SessionNameResolver.Resolve(
            SessionId,
            "I see the changes were implemented, should we consider other approaches",
            workspace);

        Assert.Equal("MSRC 119552 - OpenAPI Operation Selection", resolved);
    }

    [Fact]
    public void Resolve_AutoTitledSession_PrefersUsableYamlName()
    {
        var workspace = new WorkspaceMetadata(
            Name: "Understand Crash",
            UserNamed: false,
            Cwd: null,
            Repository: null,
            Branch: null,
            CreatedAt: null,
            UpdatedAt: null);

        var resolved = SessionNameResolver.Resolve(
            SessionId,
            "Copilot Booster is crashing, please investigate event viewer",
            workspace);

        Assert.Equal("Understand Crash", resolved);
    }

    [Fact]
    public void Resolve_UuidYamlName_FallsBackToDbSummary()
    {
        var workspace = new WorkspaceMetadata(
            Name: "02ecbfc3-2387-4d91-a085-6192ed527283",
            UserNamed: false,
            Cwd: null,
            Repository: null,
            Branch: null,
            CreatedAt: null,
            UpdatedAt: null);

        var resolved = SessionNameResolver.Resolve(
            "02ecbfc3-2387-4d91-a085-6192ed527283",
            "Investigate Dotnet CI Failures",
            workspace);

        Assert.Equal("Investigate Dotnet CI Failures", resolved);
    }

    [Fact]
    public void Resolve_PlaceholderYamlName_FallsBackToDbSummary()
    {
        var workspace = new WorkspaceMetadata(
            Name: "|-",
            UserNamed: false,
            Cwd: null,
            Repository: null,
            Branch: null,
            CreatedAt: null,
            UpdatedAt: null);

        var resolved = SessionNameResolver.Resolve(
            SessionId,
            "Classify this command",
            workspace);

        Assert.Equal("Classify this command", resolved);
    }

    [Fact]
    public void Resolve_NoNameSources_ReturnsUnnamedSession()
    {
        var workspace = default(WorkspaceMetadata);

        var resolved = SessionNameResolver.Resolve(SessionId, dbSummary: null, workspace);

        Assert.Equal(SessionNameResolver.UnnamedSession, resolved);
    }

    [Theory]
    [InlineData("MSRC 119552 - OpenAPI Operation Selection", true)]
    [InlineData("02ecbfc3-2387-4d91-a085-6192ed527283", false)]
    [InlineData("|-", false)]
    [InlineData(null, false)]
    public void IsUsableYamlName_ClassifiesExpectedValues(string? name, bool expected)
    {
        Assert.Equal(expected, SessionNameResolver.IsUsableYamlName(name, SessionId));
    }
}