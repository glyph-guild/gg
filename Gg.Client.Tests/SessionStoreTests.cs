namespace Gg.Client.Tests;

/// <summary>
/// The session file. Permissions are the whole point: this holds a live
/// credential.
/// </summary>
public class SessionStoreTests
{
    private static string TempPath() =>
        Path.Combine(Path.GetTempPath(), $"gg-session-{Guid.NewGuid():N}", "session.json");

    [Test]
    public async Task WritesAndReadsBackASession()
    {
        var store = new FileSessionStore(TempPath());
        var session = new StoredSession
        {
            SessionToken = "token",
            ExpiresAt = new DateTimeOffset(2026, 8, 9, 12, 0, 0, TimeSpan.Zero),
            TenantId = "tenant",
            PrincipalDisplay = "someone",
        };

        store.Write(session);

        await Assert.That(store.Read()).IsEqualTo(session);
        store.Clear();
    }

    [Test]
    public async Task TheFileIsOwnerReadWriteOnly()
    {
        if (OperatingSystem.IsWindows())
        {
            return; // Unix mode bits do not apply.
        }

        var store = new FileSessionStore(TempPath());
        store.Write(new StoredSession
        {
            SessionToken = "token",
            ExpiresAt = DateTimeOffset.UtcNow,
            TenantId = "tenant",
            PrincipalDisplay = "someone",
        });

        var mode = File.GetUnixFileMode(store.FilePath);

        await Assert.That(mode).IsEqualTo(UnixFileMode.UserRead | UnixFileMode.UserWrite)
            .Because("0600 - group and other must not be able to read a live session credential.");
        store.Clear();
    }

    [Test]
    public async Task ReadingWhenNothingIsStoredReturnsNull()
    {
        await Assert.That(new FileSessionStore(TempPath()).Read()).IsNull();
    }

    [Test]
    public async Task ACorruptFileReadsAsNoSessionRatherThanThrowing()
    {
        var path = TempPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "{ not json");

        await Assert.That(new FileSessionStore(path).Read()).IsNull()
            .Because("the developer should be told to log in again, not handed a parse error.");
    }

    [Test]
    public async Task TheDefaultPathSitsUnderTheConfigDirectory()
    {
        await Assert.That(FileSessionStore.DefaultPath()).Contains("good-grief");
    }
}
