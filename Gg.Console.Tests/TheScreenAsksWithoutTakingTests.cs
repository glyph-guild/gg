using Gg.Client;
using Gg.Contracts;

namespace Gg.Console.Tests;

/// <summary>
/// Who may take the answer to the wait, and who may only look at it.
/// </summary>
/// <remarks>
/// <para>
/// <b>Two callers, one answer, and only one of them folds it.</b> The screen
/// ticks once a second to decide whether to END the session; the loop folds
/// what came back once the terminal is free. <see cref="SignInSession.Arrived"/>
/// is deliberately consume-once — <c>TheConsoleSignsInForRealTests</c> pins
/// that, because holding the answer would have the screen end a second session
/// over the top of the console the first one just signed in.
/// </para>
/// <para>
/// <b>So the screen cannot be the one to ask it.</b> A person approved in a
/// browser, the tick took the answer to see that it was there, threw it away
/// as a bool, and ended the session; the loop then asked and was told nothing
/// had landed, so it returned the state unchanged and the modal went on
/// showing the code. Signing in never happened and nothing said why.
/// </para>
/// <para>
/// <b>The comment was already right; the code was not.</b>
/// <c>ConsoleScreen</c>'s timer says "The result is not consumed by asking" and
/// that sentence is what this file makes true.
/// </para>
/// </remarks>
public class TheScreenAsksWithoutTakingTests
{
    private const string Handle = "the-device-code-nobody-else-may-hold";

    private static DeviceAuthorizationStarted Authorization() => new()
    {
        DeviceCode = Handle,
        UserCode = "WDJB-MJHT",
        VerificationUri = "https://example.test/device",
        PollIntervalSeconds = 1,
        ExpiresAt = new DateTimeOffset(2026, 9, 6, 14, 32, 0, TimeSpan.Zero),
    };

    private static Task<SignInStep> AtOnce(Func<SignInStep> work) =>
        Task.FromResult(work());

    [Test]
    public async Task Asking_whether_it_landed_does_not_take_the_answer_from_the_loop()
    {
        // THE BUG, AT THE OBJECT. The screen looks; the loop folds. Both happen
        // for one approval, in that order, and the second one has to still find
        // something there.
        var session = new SignInSession(
            Authorization,
            _ => new SignInResult { SignedIn = true, Said = "Signed in as somebody." },
            AtOnce);

        session.Start();

        await Assert.That(session.Landed()).IsTrue()
            .Because("the wait finished, which is the whole of what the tick needs to know.");

        await Assert.That(session.Arrived()?.SignedIn).IsTrue()
            .Because("looking is not taking - the loop folds this, and it is the only "
                   + "thing that ever signs the console in.");
    }

    [Test]
    public async Task Looking_before_anything_started_is_not_landed()
    {
        var session = new SignInSession(
            Authorization,
            _ => new SignInResult { SignedIn = true, Said = "Signed in as somebody." },
            AtOnce);

        await Assert.That(session.Landed()).IsFalse()
            .Because("null is 'not yet', and before Start there is not even a wait.");
    }

    [Test]
    public async Task Taking_it_twice_still_gives_nothing_the_second_time()
    {
        // THE PROPERTY THE SCREEN MUST NOT RELY ON, held here beside the one it
        // may - so a future reader sees both rules in one place rather than
        // inferring the second from the absence of a test.
        var session = new SignInSession(
            Authorization,
            _ => new SignInResult { SignedIn = true, Said = "Signed in as somebody." },
            AtOnce);

        session.Start();

        await Assert.That(session.Arrived()).IsNotNull();
        await Assert.That(session.Arrived()).IsNull()
            .Because("Arrived stays consume-once. Landed is the non-destructive look, "
                   + "and adding it does not soften this.");
    }

    [Test]
    public async Task Only_the_loop_takes_what_arrived()
    {
        // THE GUARD THAT WOULD HAVE CAUGHT IT. Nothing about the defect was
        // visible in Gg.Console: the screen took a Func<bool>, the loop folded
        // Arrived, and both were right on their own. It only became wrong where
        // the two were wired together, and the composition root is the one file
        // no test was reading.
        var offenders = new List<string>();

        foreach (var file in Directory.EnumerateFiles(
                     RepoRoot(), "*.cs", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(RepoRoot(), file);

            if (relative.Contains("Tests", StringComparison.Ordinal)
                || relative.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                || relative.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            {
                continue;
            }

            var text = await File.ReadAllTextAsync(file);

            if (!text.Contains(".Arrived()", StringComparison.Ordinal))
            {
                continue;
            }

            // SignInSession.cs declares and implements it; ConsoleLoop.cs folds
            // it. Any third file is a second place the answer can be spent.
            if (relative.EndsWith("SignInSession.cs", StringComparison.Ordinal)
                || relative.EndsWith("ConsoleLoop.cs", StringComparison.Ordinal))
            {
                continue;
            }

            offenders.Add(relative);
        }

        await Assert.That(offenders).IsEmpty()
            .Because("Arrived is consume-once, so every caller beyond the loop is an "
                   + "answer somebody else already spent. Ask Landed instead.");
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Gg.sln")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("no Gg.sln above the tests");
    }
}
