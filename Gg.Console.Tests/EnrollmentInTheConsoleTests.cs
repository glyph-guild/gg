using Gg.Console;
using Gg.Contracts;

namespace Gg.Console.Tests;

/// <summary>
/// The tenant's enrollment tokens, read and revoked from the console - and
/// never minted there (slice forty-six, step 6).
/// </summary>
/// <remarks>
/// <b>Minting is absent by decision, not by omission.</b> Its one output is a
/// secret shown once, and a console repaints: a screen share, a scrollback and
/// a screenshot all keep what it painted. A terminal prints it once and moves
/// on, which is the whole difference.
/// </remarks>
public class EnrollmentInTheConsoleTests
{
    private sealed class ScriptedUi(params Func<AppState, UiOutcome>[] script) : IUiSession
    {
        private readonly Queue<Func<AppState, UiOutcome>> _script = new(script);

        public UiOutcome Run(AppState state) => _script.Dequeue()(state);
    }

    private static EnrollmentTokenSummary AToken(string id = "01a0bca3-80f9-7659-8333-3f31d32f3413") =>
        new()
        {
            TokenId = id,
            Profile = "dev-worker",
            UsesLeft = 1,
            ExpiresAt = new DateTimeOffset(2026, 9, 20, 3, 27, 7, TimeSpan.Zero),
            Ownership = RunnerOwnerships.Tenant,
            MintedBy = "Kevin",
            MintedAt = new DateTimeOffset(2026, 9, 20, 2, 27, 7, TimeSpan.Zero),
        };

    private static AppState OnTheFleet() => new() { ActiveTab = TabId.Runners };

    [Test]
    public async Task The_key_is_offered_on_the_fleet_and_nowhere_else()
    {
        var onTheFleet = Keymap.Bindings(KeymapContext.For(OnTheFleet()));
        var elsewhere = Keymap.Bindings(KeymapContext.For(new AppState { ActiveTab = TabId.Flights }));

        await Assert.That(onTheFleet.Any(b => b.Command == Command.ShowFleetTokens)).IsTrue();
        await Assert.That(elsewhere.Any(b => b.Command == Command.ShowFleetTokens)).IsFalse()
            .Because("a token is about machines joining, so it belongs beside the machines.");
    }

    [Test]
    public async Task Opening_it_reads_the_tokens_once()
    {
        var actions = new ConsoleDoubles.Records
        {
            Tokens = new EnrollmentTokenList { Tokens = [AToken()] },
        };
        var ui = new ScriptedUi(
            s => new UiOutcome(Command.ShowFleetTokens, s),
            s => new UiOutcome(Command.Quit, s));

        var final = new ConsoleLoop(ui, new ConsoleDoubles.NoEditor(), actions: actions)
            .Run(OnTheFleet());

        await Assert.That(final.Mode).IsEqualTo(UiMode.FleetTokens);
        await Assert.That(final.Tokens!.Tokens).Count().IsEqualTo(1);
    }

    [Test]
    public async Task The_list_says_what_each_is_for_and_never_a_secret()
    {
        var state = OnTheFleet() with
        {
            Mode = UiMode.FleetTokens,
            Tokens = new EnrollmentTokenList { Tokens = [AToken()] },
        };

        var said = PaneText.Modal(state);

        await Assert.That(said).Contains("dev-worker", StringComparison.Ordinal);
        await Assert.That(said).Contains("gg fleet enroll", StringComparison.Ordinal)
            .Because("minting is not here, so the list says where it is done instead of "
                   + "leaving somebody looking for a key that is missing on purpose.");

        // THE TYPE IS THE GUARANTEE, not the renderer: the listed shape has no
        // member a secret could travel in, which is why this is safe to draw.
        await Assert.That(typeof(EnrollmentTokenSummary).GetProperties()
                .Select(p => p.Name))
            .DoesNotContain("Token");
    }

    [Test]
    public async Task Revoking_asks_first_and_says_what_it_costs()
    {
        var state = OnTheFleet() with
        {
            Mode = UiMode.FleetTokens,
            Tokens = new EnrollmentTokenList { Tokens = [AToken()] },
        };

        var asked = Reducer.Reduce(state, Command.AskToRevokeToken);

        await Assert.That(asked.Mode).IsEqualTo(UiMode.ConfirmRevoke);

        var question = PaneText.Modal(asked);
        await Assert.That(question).Contains("cannot be undone", StringComparison.Ordinal);
        await Assert.That(question).Contains("already enrolled with it are untouched", StringComparison.Ordinal)
            .Because("a person revoking needs to know it does not reach the machines that "
                   + "already used it, or they will go looking for what else broke.");
    }

    [Test]
    public async Task Answering_yes_revokes_the_one_under_the_cursor_and_reads_the_list_again()
    {
        var actions = new ConsoleDoubles.Records
        {
            Tokens = new EnrollmentTokenList { Tokens = [AToken(), AToken("01a0bca3-0000-7000-8000-000000000002")] },
        };
        var ui = new ScriptedUi(
            s => new UiOutcome(Command.RevokeToken, s),
            s => new UiOutcome(Command.Quit, s));

        var final = new ConsoleLoop(ui, new ConsoleDoubles.NoEditor(), actions: actions)
            .Run(OnTheFleet() with
            {
                Mode = UiMode.ConfirmRevoke,
                Tokens = actions.Tokens,
                TokenSelected = 1,
            });

        await Assert.That(actions.Revoked).IsEquivalentTo(
            (string[])["01a0bca3-0000-7000-8000-000000000002"]);
        await Assert.That(final.LastDecision!).Contains("untouched", StringComparison.Ordinal);
        await Assert.That(final.Mode).IsEqualTo(UiMode.FleetTokens)
            .Because("the question is answered, and what is left is the list it was asked over.");
    }

    [Test]
    public async Task Nothing_in_the_console_mints_one()
    {
        // NOT "IT IS NOT BOUND" - THERE IS NOTHING TO BIND. A console that had
        // the command and left it off a keymap is one key away from painting a
        // secret that a screen share, a scrollback and a screenshot all keep.
        await Assert.That(Enum.GetNames<Command>()
                .Any(n => n.Contains("Mint", StringComparison.Ordinal)
                       || n.Contains("Enroll", StringComparison.Ordinal)))
            .IsFalse()
            .Because("minting is the terminal's: it prints the secret once and moves on.");

        var reachable = Enum.GetValues<UiMode>()
            .SelectMany(mode => Keymap.Bindings(new KeymapContext(mode) { Showing = TabId.Runners }))
            .Select(b => b.Description)
            .ToList();

        await Assert.That(reachable.Any(d => d.Contains("mint", StringComparison.OrdinalIgnoreCase)))
            .IsFalse();
    }
}
