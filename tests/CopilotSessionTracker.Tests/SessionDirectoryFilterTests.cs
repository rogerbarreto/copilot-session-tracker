using System;
using CopilotSessionTracker.Core;

namespace CopilotSessionTracker.Tests;

public sealed class SessionDirectoryFilterTests
{
    private static readonly string[] Roots =
    {
        @"C:\Users\rbarreto\OneDrive - Microsoft\Documents\Microsoft Scout",
        @"C:\Users\rbarreto\OneDrive - Microsoft\Documents\Clawpilot",
    };

    [Fact]
    public void IsIgnored_ExactMatch_IsHidden()
    {
        Assert.True(SessionDirectoryFilter.IsIgnored(
            @"C:\Users\rbarreto\OneDrive - Microsoft\Documents\Microsoft Scout", Roots));
    }

    [Fact]
    public void IsIgnored_Descendant_IsHidden()
    {
        Assert.True(SessionDirectoryFilter.IsIgnored(
            @"C:\Users\rbarreto\OneDrive - Microsoft\Documents\Clawpilot\src\app", Roots));
    }

    [Fact]
    public void IsIgnored_UnrelatedDirectory_IsVisible()
    {
        Assert.False(SessionDirectoryFilter.IsIgnored(@"D:\repo\community\copilot-session-tracker", Roots));
    }

    [Fact]
    public void IsIgnored_SiblingWithSharedPrefix_IsNotAccidentallyHidden()
    {
        // "...\Microsoft Scout" must not match "...\Microsoft Scoutmaster".
        Assert.False(SessionDirectoryFilter.IsIgnored(
            @"C:\Users\rbarreto\OneDrive - Microsoft\Documents\Microsoft Scoutmaster", Roots));
    }

    [Theory]
    [InlineData(@"C:/Users/rbarreto/OneDrive - Microsoft/Documents/Clawpilot")]
    [InlineData(@"C:\Users\rbarreto\OneDrive - Microsoft\Documents\Clawpilot\")]
    [InlineData(@"c:\users\rbarreto\onedrive - microsoft\documents\clawpilot")]
    public void IsIgnored_TolerantOfSeparatorsTrailingSlashAndCase(string cwd)
    {
        Assert.True(SessionDirectoryFilter.IsIgnored(cwd, Roots));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void IsIgnored_BlankWorkingDirectory_IsVisible(string? cwd)
    {
        Assert.False(SessionDirectoryFilter.IsIgnored(cwd, Roots));
    }

    [Fact]
    public void IsIgnored_NullOrEmptyRoots_IsVisible()
    {
        Assert.False(SessionDirectoryFilter.IsIgnored(@"C:\anything", null));
        Assert.False(SessionDirectoryFilter.IsIgnored(@"C:\anything", Array.Empty<string>()));
    }

    [Fact]
    public void IsIgnored_BlankRootEntries_AreSkipped()
    {
        var roots = new[] { "  ", "", @"C:\block" };
        Assert.True(SessionDirectoryFilter.IsIgnored(@"C:\block\sub", roots));
        Assert.False(SessionDirectoryFilter.IsIgnored(@"C:\other", roots));
    }

    [Fact]
    public void ParseRoots_TrimsBlanksAndDeduplicatesCaseInsensitively()
    {
        var text = "C:\\a\n C:\\b \n\n C:\\A \nC:\\b\\";
        var roots = SessionDirectoryFilter.ParseRoots(text);

        Assert.Equal(2, roots.Count);
        Assert.Equal(@"C:\a", roots[0]);
        Assert.Equal(@"C:\b", roots[1]);
    }

    [Theory]
    [InlineData("C:\\a\rC:\\b\rC:\\c")]        // WinUI multiline TextBox uses \r
    [InlineData("C:\\a\r\nC:\\b\r\nC:\\c")]    // CRLF
    [InlineData("C:\\a\nC:\\b\nC:\\c")]        // LF
    public void ParseRoots_SplitsOnAnyNewlineStyle(string text)
    {
        var roots = SessionDirectoryFilter.ParseRoots(text);

        Assert.Equal(new[] { @"C:\a", @"C:\b", @"C:\c" }, roots);
    }

    [Fact]
    public void ParseRoots_StripsQuotesForComparisonButKeepsFirstSeen()
    {
        var roots = SessionDirectoryFilter.ParseRoots("\"C:\\quoted path\"");
        Assert.Single(roots);
        // A quoted path still matches an unquoted cwd.
        Assert.True(SessionDirectoryFilter.IsIgnored(@"C:\quoted path\sub", roots));
    }

    [Fact]
    public void JoinRoots_RoundTripsThroughParse()
    {
        var original = new[] { @"C:\a", @"C:\b\c" };
        var joined = SessionDirectoryFilter.JoinRoots(original);
        var parsed = SessionDirectoryFilter.ParseRoots(joined);

        Assert.Equal(original, parsed);
    }
}
