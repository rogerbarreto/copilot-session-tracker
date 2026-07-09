using System;
using System.IO;
using Microsoft.Data.Sqlite;

namespace CopilotSessionTracker.Services;

/// <summary>
/// Creates a throwaway copy of a live SQLite database (including its -wal and -shm
/// sidecar files) so it can be read consistently without contending with the Copilot
/// CLI process that may be actively writing to it. The copy is deleted on dispose.
/// </summary>
internal sealed class DatabaseSnapshot : IDisposable
{
    private readonly string _directory;

    private DatabaseSnapshot(string directory, string dbCopyPath)
    {
        _directory = directory;
        ConnectionString = new SqliteConnectionStringBuilder
        {
            DataSource = dbCopyPath,
            Mode = SqliteOpenMode.ReadWrite, // read-write on the *copy* so WAL data is visible
            Pooling = false,
        }.ToString();
    }

    public string ConnectionString { get; }

    public static DatabaseSnapshot Create(string dbPath)
    {
        var directory = Path.Combine(
            Path.GetTempPath(), "CopilotSessionTracker", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        var target = Path.Combine(directory, "session-store.db");
        CopyShared(dbPath, target);
        CopyShared(dbPath + "-wal", target + "-wal");
        CopyShared(dbPath + "-shm", target + "-shm");

        return new DatabaseSnapshot(directory, target);
    }

    private static void CopyShared(string source, string target)
    {
        if (!File.Exists(source))
        {
            return;
        }

        // FileShare.ReadWrite lets us copy while the CLI still holds the file open.
        using var input = new FileStream(
            source, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var output = new FileStream(
            target, FileMode.Create, FileAccess.Write, FileShare.None);
        input.CopyTo(output);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try
        {
            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, recursive: true);
            }
        }
        catch (IOException)
        {
            // Best effort: a temp copy left behind is harmless.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
