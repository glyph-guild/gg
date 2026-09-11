using Gg.Console;
using Gg.Contracts;

namespace Gg.Console.Tests;

/// <summary>
/// Keeping a share of your own allowance back, from the console.
/// </summary>
/// <remarks>
/// <para>
/// <b>CHOSEN, NEVER TYPED.</b> Nothing in this console is written by typing
/// into a widget — the reason <c>config set</c> is a command-line verb and the
/// help page hands the whole file to <c>$EDITOR</c>. A floor is a number, so
/// the console offers a few shares and a key for each, exactly as
/// <c>ComposeChoice</c> offers two composers. An arbitrary percentage is what
/// <c>gg allowances floor</c> is for.
/// </para>
/// <para>
/// <b>Only on your own allowance.</b> A floor is its owners' to set, and the
/// control plane refuses anybody else — so offering the key on somebody else's
/// machine would be advertising a refusal. <c>RunnerIsOurs</c> already draws
/// that line for the runner modal and draws it here.
/// </para>
/// <para>
/// <b>And the write happens between sessions</b>, like every other write here:
/// the command ends the UI session and the shell performs it with the terminal
/// provably free.
/// </para>
/// </remarks>
public class AShareIsKeptByAKeypressTests
{
    private static AppState OnTheRunnersTab(bool ours = true) => new()
    {
        ActiveTab = TabId.Runners,
        // ALWAYS ME. `Yours` compares this console's principal with the
        // runner's registrant, so moving BOTH would have made somebody else's
        // machine theirs-and-mine at once - which is how the first version of
        // this fixture asserted nothing.
        PrincipalId = "me",
        Runners = new RunnerList
        {
            Runners =
            [
                new()
                {
                    RunnerId = "11111111-1111-1111-1111-111111111111",
                    Label = "a-laptop",
                    State = RunnerStates.Idle,
                    RegisteredBy = "me",
                    RegisteredByPrincipalId = ours ? "me" : "somebody-else",
                },
            ],
        },
        Allowances = new AllowanceList
        {
            Allowances =
            [
                new()
                {
                    Name = "kdee-max",
                    MeasuredAt = DateTimeOffset.UnixEpoch,
                    Runners = ["11111111-1111-1111-1111-111111111111"],
                    Owners = ["me"],
                    Windows = [],
                },
            ],
        },
    };

    [Test]
    public async Task The_key_is_offered_on_your_own_allowance_and_not_on_anybody_elses()
    {
        var mine = Keymap.Resolve(KeyStroke.Char('o'), KeymapContext.For(OnTheRunnersTab()));

        await Assert.That(mine).IsEqualTo(Command.AskToKeepAShare)
            .Because("a floor is its owners' to set, and this is one of them.");

        var theirs = Keymap.Resolve(
            KeyStroke.Char('o'), KeymapContext.For(OnTheRunnersTab(ours: false)));

        await Assert.That(theirs).IsNull()
            .Because("the control plane refuses anybody else, so offering the key here "
                   + "would be advertising a refusal - which is worse than not offering "
                   + "it, because a person presses it and concludes the console is "
                   + "broken.");
    }

    [Test]
    public async Task The_modal_offers_shares_and_a_way_to_keep_nothing()
    {
        var choosing = OnTheRunnersTab() with { Mode = UiMode.FloorChoice };
        var context = KeymapContext.For(choosing);

        await Assert.That(Keymap.Resolve(KeyStroke.Char('1'), context))
            .IsEqualTo(Command.KeepATenth);
        await Assert.That(Keymap.Resolve(KeyStroke.Char('2'), context))
            .IsEqualTo(Command.KeepAQuarter);
        await Assert.That(Keymap.Resolve(KeyStroke.Char('3'), context))
            .IsEqualTo(Command.KeepAHalf);

        await Assert.That(Keymap.Resolve(KeyStroke.Char('0'), context))
            .IsEqualTo(Command.KeepNothing)
            .Because("clearing a floor is a choice somebody makes on purpose, and it needs "
                   + "a key of its own - escaping is 'I did not mean to open this'.");

        await Assert.That(Keymap.Resolve(KeyStroke.Esc, context))
            .IsEqualTo(Command.CloseModal)
            .Because("one way out of every modal, the same key everywhere, which is what "
                   + "makes it findable without being learned.");
    }

    [Test]
    public async Task Choosing_a_share_ends_the_session_because_a_write_happens_outside_one()
    {
        foreach (var chosen in new[]
                 {
                     Command.KeepATenth, Command.KeepAQuarter,
                     Command.KeepAHalf, Command.KeepNothing,
                 })
        {
            await Assert.That(ShellCommands.Handled).Contains(chosen)
                .Because("a write happens between sessions with the terminal provably "
                       + "free, which is the same arrangement $EDITOR has always used. A "
                       + "pure reduction here would never open anything - the defect "
                       + "ComposeInEditor already recorded.");
        }
    }

    [Test]
    public async Task The_modal_says_which_allowance_it_is_about()
    {
        var text = PaneText.Modal(OnTheRunnersTab() with { Mode = UiMode.FloorChoice });

        await Assert.That(text).Contains("kdee-max", StringComparison.Ordinal)
            .Because("two machines can share one allowance and one person can lend "
                   + "several, so a modal that asked 'how much to keep?' without naming "
                   + "the subscription would be a question nobody can answer safely.");
    }
}
