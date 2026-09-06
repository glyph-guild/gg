namespace Gg.Console.Tests;

/// <summary>
/// What the sign-in modal puts in front of a person, at each step.
/// </summary>
/// <remarks>
/// <para>
/// <b>A modal with no text is a bordered box.</b> <c>PaneText.Modal</c>
/// dispatches on the mode and returns <c>""</c> for anything it does not know,
/// so a mode can be added, own the keyboard, and draw nothing at all — which
/// looks exactly like the console having frozen.
/// </para>
/// <para>
/// <b>And the hint line is half of it.</b> The keys are advertised from the
/// context the screen builds, so a step whose context is not derived from the
/// model shows the other step's keys — the person reads <c>y sign in</c> under
/// a code they were asked to approve, presses it, and nothing happens.
/// </para>
/// </remarks>
public class TheSignInModalReadsTests
{
    private static readonly DateTimeOffset Expiry =
        new(2026, 9, 6, 14, 32, 0, TimeSpan.Zero);

    private static AppState Offered() => new() { Mode = UiMode.SignIn };

    private static AppState Started() => Offered() with
    {
        SignIn = new PendingSignIn
        {
            UserCode = "WDJB-MJHT",
            VerificationUri = "https://example.test/device",
            ExpiresAt = Expiry,
        },
    };

    [Test]
    public async Task The_offer_says_why_the_console_behind_it_is_empty()
    {
        // The queue says "nothing needs you" whether nothing needs you or
        // nobody could ask, and this modal is drawn over the second one. A
        // person who reads only the pane behind it learns the opposite of the
        // truth.
        var text = PaneText.Modal(Offered());

        await Assert.That(text).IsNotEmpty()
            .Because("a modal that owns the keyboard and draws nothing is indistinguishable "
                   + "from a console that has frozen.");
        await Assert.That(text).Contains("signed in");
    }

    [Test]
    public async Task Where_to_go_and_what_to_type_are_both_on_the_screen()
    {
        // The whole reason the shell fetches the code before waiting on it. A
        // device flow whose code is only ever printed to a terminal the console
        // then redraws over is a device flow nobody can complete.
        var text = PaneText.Modal(Started());

        await Assert.That(text).Contains("https://example.test/device");
        await Assert.That(text).Contains("WDJB-MJHT");
    }

    [Test]
    public async Task A_code_says_when_it_stops_working()
    {
        // A code with no expiry on it is one somebody comes back to after lunch
        // and concludes the product is broken.
        await Assert.That(PaneText.Modal(Started())).Contains("14:32");
    }

    [Test]
    public async Task What_the_last_attempt_said_is_where_it_will_be_read()
    {
        // Expired, declined, or pressed a moment early — the arm returns to the
        // offer, and the offer is the only thing on the screen. A reason
        // recorded on the model and drawn nowhere is a key that appears to have
        // done nothing.
        var text = PaneText.Modal(
            Offered() with { LastSignIn = "That code expired before it was approved." });

        await Assert.That(text).Contains("That code expired before it was approved.");
    }

    [Test]
    public async Task The_offer_names_no_command_to_type_in_a_terminal_it_has_taken()
    {
        // THE DEFECT THIS WHOLE SLICE IS ABOUT, asserted so it cannot come back
        // as a helpful addition. "Run gg login" was true, and the person could
        // not do it: gg had the terminal.
        await Assert.That(PaneText.Modal(Offered())).DoesNotContain("gg login");
        await Assert.That(PaneText.Modal(Started())).DoesNotContain("gg login");
    }

    [Test]
    public async Task The_keys_offered_are_derived_from_the_model()
    {
        // Both steps live in one mode, so the mode alone cannot say which keys
        // are live. A context built by hand beside the model is a second place
        // to remember, and this one has already been forgotten twice.
        await Assert.That(KeymapContext.For(Started()).SignInStarted).IsTrue();
        await Assert.That(KeymapContext.For(Offered()).SignInStarted).IsFalse();

        var live = Keymap.Bindings(KeymapContext.For(Started())).Select(b => b.Key).ToList();

        await Assert.That(live).Contains(
            Keymap.Bindings(new KeymapContext(UiMode.SignIn) { SignInStarted = true })
                .Single(b => b.Command == Command.SignIn).Key);
    }

    [Test]
    public async Task The_screen_derives_its_context_rather_than_rebuilding_one()
    {
        // ShellHandledTests' shape, for the same class of defect one type over.
        // The screen's hand-built context has dropped THREE members already -
        // Takeable, HandedBackable and RepositoriesVisible - so the help page
        // advertises keys the screen would not resolve and a hint reads "show"
        // for a pane that is already showing. Neither is visible today because
        // nothing in production sets the first two; both become visible the day
        // something does, which is the worst time to find out.
        var screen = ConsoleSource.Text("Gg.Console", Path.Combine("Views", "ConsoleScreen.cs"));

        await Assert.That(screen).Contains("KeymapContext.For(State)")
            .Because("one derivation, read by the screen, the help page and these tests, is "
                   + "what stops the advertised keys and the live ones drifting.");
    }

    [Test]
    public async Task The_derivation_reads_every_field_the_keymap_dispatches_on()
    {
        // The ratchet on the derivation itself. A member added to KeymapContext
        // and never read off the model is a binding that can only ever be
        // reached by a test - which is how three of them got here.
        var state = new AppState
        {
            Mode = UiMode.Normal,
            LiveVisible = true,
            Frozen = true,
            BrowseVisible = true,
            ChecklistVisible = true,
            EnvelopeVisible = true,
            RepositoriesVisible = true,
            TakeableTree = "/somewhere",
            TakenOver = true,
            SignIn = Started().SignIn,
        };

        var context = KeymapContext.For(state);

        var defaults = typeof(KeymapContext)
            .GetProperties()
            .Where(p => p.PropertyType == typeof(bool))
            .Where(p => !(bool)p.GetValue(context)!)
            .Select(p => p.Name)
            .ToList();

        await Assert.That(defaults).IsEmpty()
            .Because("every flag the keymap dispatches on is set on this model, so one still "
                   + "false is one the derivation does not read. Found: "
                   + string.Join(", ", defaults));
    }
}
