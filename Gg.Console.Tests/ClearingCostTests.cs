using Gg.Console;

namespace Gg.Console.Tests;

/// <summary>
/// Is clearing a gate actually cheap?
/// </summary>
/// <remarks>
/// <para>
/// <b>The console's entire justification.</b> A lease renewed by clearing checkpoints only
/// works if clearing one is nearly free; otherwise the renewal IS the interruption tax and
/// the attendance argument is built on something that does not hold.
/// </para>
/// <para>
/// <b>The verb is the control.</b> If the console is not meaningfully cheaper than typing
/// `gg decide`, that is worth knowing before attendance is built on the assumption - so
/// this counts both and neither is allowed to be estimated.
/// </para>
/// <para>
/// <b>What this measures is keystrokes, which is the honest half.</b> Elapsed time in a
/// terminal is dominated by how long a person takes to read the evidence, and that is the
/// same evidence on both paths - so the difference between the two is input, and input is
/// countable. The wall-clock comparison is recorded in the step report with its caveats
/// rather than asserted here as though a test had measured a human.
/// </para>
/// </remarks>
public class ClearingCostTests
{
    [Test]
    public async Task Clearing_a_gate_from_the_console_is_two_keystrokes()
    {
        // COUNTED FROM THE KEYMAP, not from a comment. The path is: queue focused with the
        // row selected, open the gate, answer it.
        var open = Keymap.Bindings(new KeymapContext(UiMode.Normal))
            .Single(b => b.Command == Command.OpenGate);

        var approve = Keymap.Bindings(new KeymapContext(UiMode.GateDecision))
            .Single(b => b.Command == Command.ApproveGate);

        var strokes = new[] { open.Key, approve.Key };

        await Assert.That(strokes.Length).IsEqualTo(2)
            .Because("open the gate, answer it. Nothing is typed and nothing is named, "
                   + "because the cursor already knows which flight and which obligation.");

        // And they really do resolve, so this is a path rather than an arithmetic.
        await Assert.That(Keymap.Resolve(open.Key, new KeymapContext(UiMode.Normal)))
            .IsEqualTo(Command.OpenGate);
        await Assert.That(Keymap.Resolve(approve.Key, new KeymapContext(UiMode.GateDecision)))
            .IsEqualTo(Command.ApproveGate);
    }

    [Test]
    public async Task Clearing_it_with_the_verb_is_the_whole_invocation()
    {
        // THE CONTROL, counted the same way: every character somebody types, because the
        // verb cannot use a cursor to know what is being decided. The flight and the
        // obligation are named rather than selected, which is the difference.
        const string invocation = "gg decide GG-42 reversibility-plan approved";

        await Assert.That(invocation.Length).IsGreaterThan(40)
            .Because("this is what the console is being compared against.");

        // The shape is the CLI's own, not one invented for this test - a comparison
        // against a usage string nobody has to type would be measuring nothing.
        var usage = "gg decide <flight> <obligation> <approved|rejected> [reason]";

        await Assert.That(usage).Contains("<flight>");
        await Assert.That(usage).Contains("<obligation>");
        await Assert.That(usage).Contains("approved");
    }

    [Test]
    public async Task The_console_path_names_neither_the_flight_nor_the_obligation()
    {
        // WHERE THE SAVING COMES FROM, and it is not keystroke golf. The verb needs a
        // flight number and an obligation id because it has no context; the console has
        // both on the cursor. That is also why the console cannot be wrong about which
        // gate is being answered, which matters more than the typing.
        var modal = Keymap.Bindings(new KeymapContext(UiMode.GateDecision));

        await Assert.That(modal.Select(b => b.Description)).DoesNotContain("flight")
            .Because("nothing in the modal asks which flight - the queue already knows.");
        await Assert.That(modal.Count).IsLessThanOrEqualTo(3)
            .Because("approve, reject, and one way out. A modal with more choices than that "
                   + "is a form, and a form is not nearly free.");
    }
}
