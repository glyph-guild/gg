namespace Gg.Client.Tests;

/// <summary>
/// In an agent session a person delegated, gg's verbs and tool servers act through the token
/// the machine put in the environment, and never write it down (ADR-0039 Amendment 2,
/// Decision 13).
/// </summary>
[NotInParallel("environment")]
public class ADelegatedSessionComesFromTheEnvironmentTests
{
    private static string File() => Path.Combine(Directory.CreateTempSubdirectory("gg-session-").FullName, "session.json");

    [Test]
    public async Task The_token_in_the_environment_is_the_session()
    {
        var store = SessionStores.For(variable => variable == SessionStores.TokenVariable ? "t0k3n" : null, File());

        await Assert.That(store.Read()?.SessionToken).IsEqualTo("t0k3n");
    }

    [Test]
    public async Task Without_one_the_signed_in_file_is_the_session()
    {
        var path = File();
        new FileSessionStore(path).Write(new StoredSession
        {
            SessionToken = "from-file", ExpiresAt = DateTimeOffset.MaxValue, TenantId = "t", PrincipalDisplay = "Kevin",
        });

        var store = SessionStores.For(_ => null, path);

        await Assert.That(store.Read()?.SessionToken).IsEqualTo("from-file");
    }

    [Test]
    public async Task A_delegated_session_is_never_written_down()
    {
        var path = File();
        var store = SessionStores.For(variable => variable == SessionStores.TokenVariable ? "t0k3n" : null, path);

        await Assert.That(() => store.Write(new StoredSession
        {
            SessionToken = "other", ExpiresAt = DateTimeOffset.MaxValue, TenantId = "t", PrincipalDisplay = "x",
        })).Throws<InvalidOperationException>();
        store.Clear();

        await Assert.That(System.IO.File.Exists(path)).IsFalse()
            .Because("signing in or out inside a delegated session would persist or destroy the wrong thing.");
    }
}
