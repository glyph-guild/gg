using Gg.Client;

namespace Gg.Console.Tests;

/// <summary>
/// A console nobody is signed in on offers to sign in.
/// </summary>
/// <remarks>
/// <para>
/// <b>What it did instead: named a command it had taken the terminal away
/// from.</b> Open <c>gg</c> with no session and every read refuses, the queue
/// is empty, and the model carries <i>"Not signed in. Run gg login."</i> — an
/// instruction to type something into the terminal this console is drawing on.
/// The only way to follow it was to quit, and nothing on the screen said so.
/// </para>
/// <para>
/// <b>Decided by the LOADER, which is the boot and the refresh both.</b> A
/// session that expires while somebody is looking at the console is the same
/// fact arriving later; deciding this anywhere else would answer it at boot
/// only, and the refresh key would go on emptying the console for a reason it
/// could no longer name.
/// </para>
/// </remarks>
public class SigningInFromTheConsoleTests
{
    private sealed class NoSession : ISessionStore
    {
        public StoredSession? Read() => null;
        public void Write(StoredSession value) { }
        public void Clear() { }
    }

    /// <summary>A session this machine holds, whether or not it still works.</summary>
    private sealed class HasSession : ISessionStore
    {
        public StoredSession? Read() => new()
        {
            SessionToken = "a-token",
            ExpiresAt = new DateTimeOffset(2099, 1, 1, 0, 0, 0, TimeSpan.Zero),
            TenantId = Guid.Empty.ToString(),
            PrincipalDisplay = "somebody",
        };

        public void Write(StoredSession value) { }
        public void Clear() { }
    }

    private sealed class NoStore : ICredentialStore
    {
        public string Root => "(no store)";
        public string Protection => "nothing is stored";
        public string PathFor(string locator) => throw new InvalidOperationException("no store here");
        public void Write(string locator, string secret) => throw new InvalidOperationException("no store here");
        public string? Read(string locator) => null;
        public bool Remove(string locator) => false;
    }

    private sealed class NeverAsked : ISecretPrompt
    {
        public string ReadSecret(string prompt) =>
            throw new InvalidOperationException("the console does not prompt for secrets.");

        public string ReadLine(string prompt) =>
            throw new InvalidOperationException("the console does not prompt for secrets.");
    }

    /// <summary>A console reading through the verbs, with the session it is given.</summary>
    private static ConsoleData Reading(ISessionStore sessions)
    {
        // A port nothing listens on, so nothing here reaches the network. Which
        // failure the load takes is decided by the SESSION: the verbs refuse
        // before they ask, so no session is a refusal and a session is a
        // connection that cannot be made.
        var http = new HttpClient { BaseAddress = new Uri("http://127.0.0.1:1/") };
        var client = new ControlPlaneClient(http);

        return new ConsoleData(
            new FlightCommands(client, sessions),
            new CredentialCommands(client, sessions, new NoStore(), new NeverAsked()),
            new TakeCommands(client, sessions),
            new IdentityCommands(client, sessions),
            new EnvelopeCommands(client, sessions));
    }

    /// <summary>
    /// A console this machine holds no session for.
    /// </summary>
    /// <remarks>
    /// Shared, because the property beside it in
    /// <c>SigningInIsTheShellsWorkTests</c> is about the same refusal and a
    /// second copy of this wiring is a second thing to keep in step.
    /// </remarks>
    internal static ConsoleData SignedOut() => Reading(new NoSession());

    [Test]
    public async Task A_console_nobody_is_signed_in_on_asks_them_to()
    {
        var state = await ConsoleStart.LoadAsync(SignedOut());

        await Assert.That(state.Mode).IsEqualTo(UiMode.SignIn)
            .Because("an empty queue and a diagnosis naming a command in a terminal this "
                   + "console has taken over is not an answer a person can act on.");
    }

    [Test]
    public async Task A_control_plane_nobody_can_reach_is_not_a_person_who_is_signed_out()
    {
        // THE DISCRIMINATION THIS WHOLE ARM RESTS ON. Both failures empty the
        // console and both are caught in the same place. Only one of them is
        // about a credential - and offering to sign in when a cable is out
        // sends a person to re-authenticate against a service that would refuse
        // them for a reason that has nothing to do with who they are.
        var state = await ConsoleStart.LoadAsync(Reading(new HasSession()), "somebody");

        await Assert.That(state.Mode).IsEqualTo(UiMode.Normal)
            .Because("this machine holds a session; what it cannot do is reach anybody to "
                   + "use it, and no amount of signing in fixes that.");
    }

    [Test]
    public async Task Being_asked_to_sign_in_does_not_cost_the_reason()
    {
        // The modal is what a person does about it; the diagnosis is what
        // happened. A console that swapped one for the other would be a console
        // that forgot why it is empty the moment it offered a way out.
        var state = await ConsoleStart.LoadAsync(SignedOut());

        await Assert.That(state.Diagnosis).IsNotNull();
        await Assert.That(state.Queue).IsEmpty();
    }
}
