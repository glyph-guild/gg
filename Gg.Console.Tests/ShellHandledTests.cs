using System.Text.RegularExpressions;

namespace Gg.Console.Tests;

/// <summary>
/// A bound key does something, and the two halves of that agree.
/// </summary>
/// <remarks>
/// <para>
/// <b>Written after finding four dead keys.</b> <c>ConsoleScreen</c> ended the UI
/// session for exactly <c>Quit</c> and <c>OpenEditor</c>, named as literals;
/// <c>ConsoleLoop</c> had arms for <c>TakeFlight</c> and <c>HandBack</c> and threw
/// on anything else it was handed. Both lists were right about their own half and
/// neither knew about the other, so <c>t</c>, <c>h</c>, <c>a</c> and <c>r</c>
/// resolved to a command, reached the reducer, and returned the state unchanged.
/// Bound, advertised in the hint line, and inert.
/// </para>
/// <para>
/// <b>Structural, because the alternative needs a terminal.</b> Driving the real
/// screen means driving Terminal.Gui, and a test that cannot run in CI is a test
/// that does not hold the property. What can be checked without one is that the
/// declaration exists and that both sides read it - which is exactly what was
/// missing, since the bug was two correct lists rather than one wrong one.
/// </para>
/// <para>
/// <b>Three readers, not two.</b> <c>ModalEscapeTests</c> reimplements the screen's
/// dispatch to drive generated key sequences, so it held a third copy of the same
/// list. It reads the declaration too, or the property it proves is about a
/// console that no longer exists.
/// </para>
/// </remarks>
public class ShellHandledTests
{
    private static string Source(string project, string file)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Gg.sln")))
        {
            dir = dir.Parent;
        }

        return File.ReadAllText(Path.Combine(dir!.FullName, project, file));
    }

    [Test]
    public async Task The_four_keys_that_did_nothing_are_declared_as_the_shell_s()
    {
        // THE REGRESSION, named one command at a time so a future change that
        // dropped one fails on that one rather than on a count.
        foreach (var command in (Command[])
            [Command.TakeFlight, Command.HandBack, Command.ApproveGate, Command.RejectGate])
        {
            await Assert.That(ShellCommands.Handled).Contains(command)
                .Because($"{command} was bound to a key, advertised in the hint line, and did "
                       + "nothing: the screen never ended the session for it, so the loop never "
                       + "saw it.");
        }
    }

    [Test]
    public async Task Quit_is_still_the_shell_s()
    {
        // The one that always worked, asserted so a change that moved to the
        // declaration cannot quietly drop it.
        //
        // OpenEditor was asserted here too, as the other half of the pair that
        // motivated this file. It is GONE rather than dropped: the key wrote a
        // scratchpad nothing displayed, sent or kept, and `new flight` now
        // hands the terminal to the same editor for a reason somebody asked
        // for. See NoScratchpadKeyTests.
        await Assert.That(ShellCommands.Handled).Contains(Command.Quit);
        await Assert.That(ShellCommands.Handled).Contains(Command.OpenFlight)
            .Because("the terminal-release effect still has to be the shell's; it is just no "
                   + "longer a scratchpad.");
    }

    [Test]
    public async Task The_screen_ends_the_session_from_the_declaration_rather_than_a_literal_list()
    {
        // The half that was wrong. A literal list here is how this drifted, and it
        // will drift again the next time somebody adds a command - so what is
        // asserted is the ABSENCE of the literal, not the presence of a behaviour a
        // test without a terminal cannot see.
        var screen = Source("Gg.Console", Path.Combine("Views", "ConsoleScreen.cs"));

        await Assert.That(screen).Contains("ShellCommands.Handled")
            .Because("one declaration, read here, is what stops the screen and the shell "
                   + "disagreeing about which keys do something.");
        await Assert.That(screen).DoesNotContain("Command.Quit or Command.OpenEditor")
            .Because("that literal pair IS the defect: it named two commands correctly and "
                   + "silently excluded four others.");
    }

    [Test]
    public async Task The_shell_has_an_arm_for_every_command_it_is_handed()
    {
        // The other half, and the one that would have caught this from the far
        // side. ConsoleLoop throws on a command it does not handle, so a command in
        // the declaration with no arm is a crash rather than a dead key - louder,
        // but still a defect, and this is cheaper than finding out at runtime.
        var loop = Source("Gg.Console", "ConsoleLoop.cs");

        var missing = ShellCommands.Handled
            .Where(c => c != Command.Quit)
            .Where(c => !Regex.IsMatch(loop, $@"Command\.{c}\b"))
            .ToList();

        await Assert.That(missing).IsEmpty()
            .Because("the shell is handed these and has nowhere to put them, so it throws the "
                   + "sentence in its default arm. Found: " + string.Join(", ", missing));
    }

    [Test]
    public async Task The_generated_key_walk_reads_the_same_declaration()
    {
        // ModalEscapeTests held a third copy of the exit list. A property proven
        // over 500 generated sequences is worth exactly as much as the dispatch it
        // models, so it reads the declaration rather than restating it.
        var walk = Source("Gg.Console.Tests", "ModalEscapeTests.cs");

        await Assert.That(walk).Contains("ShellCommands.Handled");
        await Assert.That(walk).DoesNotContain("or Command.OpenEditor")
            .Because("a third literal copy of the list drifts exactly as the first two did.");
    }

    [Test]
    public async Task Nothing_the_shell_handles_is_also_changed_by_the_reducer()
    {
        // WHERE AN EFFECT LIVES, asserted once. The reducer NAMES ApproveGate and
        // RejectGate deliberately - to say out loud that answering posts and does
        // not decide locally - so naming is fine and CHANGING is not. A reducer that
        // also mutated state for a shell command would make the console's view and
        // the control plane's record two answers to one question.
        var reducer = Source("Gg.Console", Path.Combine("State", "Reducer.cs"));

        // LINE BY LINE, because a lookahead after `\s*` backtracks: `\s*` matches
        // zero spaces, the lookahead then sees " state," rather than "state," and
        // succeeds. My first version of this flagged three commands that were
        // written correctly, which is the wrong direction for a guard to fail in -
        // it would have been "fixed" by loosening it.
        var mutating = ShellCommands.Handled
            .Where(c => reducer.Split('\n')
                .Where(line => Regex.IsMatch(line, $@"Command\.{c}\b")
                            && line.Contains("=>", StringComparison.Ordinal))
                .Any(line => line[(line.IndexOf("=>", StringComparison.Ordinal) + 2)..].Trim()
                    is not ("state" or "state,")))
            .ToList();

        await Assert.That(mutating).IsEmpty()
            .Because("a shell command whose effect is also a state change has two effects, and "
                   + "the local one happens whether or not the remote one did. Found: "
                   + string.Join(", ", mutating));
    }
}
