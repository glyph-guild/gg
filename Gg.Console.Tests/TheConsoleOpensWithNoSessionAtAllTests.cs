using Gg.Client;

namespace Gg.Console.Tests;

/// <summary>
/// gg on a machine that has never signed in.
/// </summary>
/// <remarks>
/// <para>
/// <b>The whole sign-in modal is for this machine, and this machine could not
/// reach it.</b> <c>ConsoleStart</c> catches <see cref="NotSignedInException"/>
/// out of every read it makes and opens in <see cref="UiMode.SignIn"/> — but
/// the principal is computed as an ARGUMENT to that loader, so a throw there
/// happens before the call and outside the catch. What a person saw was not an
/// empty console: it was an unhandled exception, a stack trace, and no console
/// at all.
/// </para>
/// <para>
/// <b>Why it stayed hidden.</b> An EXPIRED session is a file that reads fine,
/// so the name is there and the boot proceeds; only the control plane refuses,
/// which is the case the modal was built and tested against. Nothing has no
/// file, and nothing is the state every new machine starts in.
/// </para>
/// </remarks>
public class TheConsoleOpensWithNoSessionAtAllTests
{
    /// <summary>A machine that has never signed in.</summary>
    private sealed class NoSession : ISessionStore
    {
        public StoredSession? Read() => null;

        public void Write(StoredSession session) =>
            throw new InvalidOperationException("nothing here writes one.");

        public void Clear()
        {
        }
    }

    [Test]
    public async Task Asking_the_verbs_for_a_name_is_what_throws()
    {
        // The behaviour under the defect, and it is the RIGHT behaviour: every
        // other caller of this is standing in a shell, where refusing with a
        // sentence naming `gg login` is exactly what should happen. What is
        // wrong is asking it where there is no shell to print it in.
        var takes = new TakeCommands(
            new ControlPlaneClient(new HttpClient { BaseAddress = new Uri("http://127.0.0.1:1/") }),
            new NoSession());

        await Assert.That(() => takes.Principal()).Throws<NotSignedInException>()
            .Because("this is the throw the console's boot walked into, and it stays - the "
                   + "fix is that the console stops asking, not that the verbs stop refusing.");
    }

    [Test]
    public async Task The_console_boots_without_asking_for_a_name_nobody_has()
    {
        var root = ConsoleSource.Text("Gg.Cli", "Program.cs");

        await Assert.That(root).DoesNotContain("takes.Principal()")
            .Because("it is evaluated as an argument to ConsoleStart.LoadAsync, so it throws "
                   + "BEFORE the loader runs and outside the catch that turns being signed "
                   + "out into the modal. Both call sites are on the path a machine with no "
                   + "session takes: one at boot, one on every reload.");
    }

    [Test]
    public async Task A_console_with_no_name_is_a_console_the_loader_already_allows()
    {
        // The anchor for what replaces it. Nobody signed in is not a missing
        // value to be invented at the boundary - the loader's own default is
        // the empty string, so handing it one is handing it what it expects.
        var loader = ConsoleSource.Text("Gg.Console", "ConsoleStart.cs");

        await Assert.That(loader).Contains("string principal = \"\"")
            .Because("if this ever becomes required, the boot has to answer the question "
                   + "some other way and this test should fail rather than the console.");

        var root = ConsoleSource.Text("Gg.Cli", "Program.cs");

        await Assert.That(root).Contains("ConsoleStart")
            .Because("liveness: a source assertion about a call that is no longer made "
                   + "anywhere passes forever and guards nothing.");
    }
}
