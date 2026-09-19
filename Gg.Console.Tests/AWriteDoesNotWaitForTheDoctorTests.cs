using Gg.Client;

namespace Gg.Console.Tests;

/// <summary>
/// The reload after a write reads what the write changed, and does not re-run
/// this machine's health check on the way.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every write paid for a doctor nobody asked for.</b> The composition
/// root's reload handed <c>ConsoleStart.LoadAsync</c> the doctor on every call,
/// so answering a gate, opening a flight or forgetting a credential re-ran a
/// dozen checks - a STUN gather bounded at five seconds and a WebRTC loopback
/// bounded at fifteen among them - between one UI session and the next, with
/// nothing on the screen. Measured at 1.2s on a machine where every probe
/// answers, and twenty when the two network ones stall.
/// </para>
/// <para>
/// <b>What it bought was the help modal's Doctor page</b>, the only reader of
/// the report. A write changes what the queue, the flights and the credentials
/// say; it does not change whether this machine can reach a STUN server. So
/// the report is taken when the console opens and when somebody signs in -
/// the one act that changes what most of its checks would answer - and a
/// reload that already holds one keeps it.
/// </para>
/// </remarks>
public class AWriteDoesNotWaitForTheDoctorTests
{
    private static readonly DoctorReport Held = Report("held");

    private static readonly DoctorReport Fresh = Report("fresh");

    [Test]
    public async Task A_reload_that_already_holds_a_report_does_not_ask_again()
    {
        var (data, _) = AConsolePlane.Console();
        var asked = 0;

        var after = await ConsoleStart.LoadAsync(
            data, "somebody", new AppState { Principal = "somebody", Doctor = Held },
            doctor: _ =>
            {
                asked++;
                return Task.FromResult(Fresh);
            });

        await Assert.That(asked).IsEqualTo(0)
            .Because("a write does not change what the health check would answer, and the "
                   + "check is seconds of a blank screen between one session and the next.");
        await Assert.That(after.Doctor).IsEqualTo(Held);
    }

    [Test]
    public async Task A_reload_with_no_report_asks()
    {
        // THE ANCHOR. The boot starts with no report, and so does a console
        // whose sign-in just dropped the one it had - both have to ask, or the
        // page says "has not read a health report" for as long as it is open.
        var (data, _) = AConsolePlane.Console();
        var asked = 0;

        var after = await ConsoleStart.LoadAsync(
            data, "somebody", new AppState { Principal = "somebody" },
            doctor: _ =>
            {
                asked++;
                return Task.FromResult(Fresh);
            });

        await Assert.That(asked).IsEqualTo(1);
        await Assert.That(after.Doctor).IsEqualTo(Fresh);
    }

    [Test]
    public async Task A_reload_given_no_doctor_keeps_the_report_it_had()
    {
        // Null meant "this console does not ask", and the loader answered it by
        // writing null over whatever report the person already had - which the
        // page then describes as never having read one.
        var (data, _) = AConsolePlane.Console();

        var after = await ConsoleStart.LoadAsync(
            data, "somebody", new AppState { Principal = "somebody", Doctor = Held });

        await Assert.That(after.Doctor).IsEqualTo(Held)
            .Because("not asking again is not the same fact as never having asked.");
    }

    [Test]
    public async Task Signing_in_drops_the_report_so_the_reload_after_it_asks()
    {
        // A sign-in changes what the session, runner and credential checks
        // would answer - including when the same person signs in again after a
        // session ran out. So the report it made stale is dropped before the
        // reload, which is then the one that asks.
        DoctorReport? seen = Held;
        var loop = new ConsoleLoop(
            new ConsoleDoubles.TypesKeys(Command.SignIn),
            new ConsoleDoubles.NoEditor(),
            reload: current =>
            {
                seen = current.Doctor;
                return current;
            },
            signIn: new Arrives());

        _ = loop.Run(new AppState
        {
            Mode = UiMode.SignIn,
            Doctor = Held,
            SignIn = new PendingSignIn
            {
                UserCode = "WDJB-MJHT",
                VerificationUri = "https://example.test/device",
                ExpiresAt = new DateTimeOffset(2026, 9, 18, 14, 0, 0, TimeSpan.Zero),
            },
        });

        await Assert.That(seen).IsNull()
            .Because("the reload after a sign-in is the one that has to ask again.");
    }

    [Test]
    public async Task The_page_says_when_its_report_was_taken()
    {
        // A report that is no longer re-taken on every write can be older than
        // the console's other panes, and a page that does not say so reads as
        // a check made a moment ago.
        var text = PaneText.HelpDoctorText(new AppState { Doctor = Held });

        await Assert.That(text).Contains("when this console opened or somebody last signed in");
        await Assert.That(text).Contains("gg doctor");
    }

    [Test]
    public async Task The_empty_page_names_only_what_actually_asks()
    {
        // It said "again on r" - and `r` is the repositories tab. The page
        // promised a key that has never re-run the doctor.
        var text = PaneText.HelpDoctorText(new AppState());

        await Assert.That(text).DoesNotContain("on r;");
        await Assert.That(text).Contains("when it opens and when somebody signs in");
    }

    private static DoctorReport Report(string detail) => new()
    {
        Checks =
        [
            new DoctorCheck
            {
                Name = "session",
                Passed = true,
                Detail = detail,
                Blocking = true,
                Fixable = true,
            },
        ],
    };

    /// <summary>A sign-in whose authorization has already been approved.</summary>
    private sealed class Arrives : ISignInSession
    {
        public SignInStep Start() =>
            throw new InvalidOperationException("a code is already showing; nothing starts.");

        public bool Landed() => true;

        public SignInStep? Arrived() => new() { SignedIn = true, Said = "Signed in as somebody." };
    }
}
