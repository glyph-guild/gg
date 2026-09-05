namespace Gg.Console.Tests;

/// <summary>
/// Step 4's panes went through the ratchets rather than around them.
/// </summary>
/// <remarks>
/// <para>
/// <b>An exemption list is a good instrument and a bad resting place.</b> Step 1
/// built two of them and left every step-4 read on both: a verb result with no
/// <c>Apply</c> arm, and a field no production path assigns. The cheap way to
/// finish step 4 would have been to leave the entries and reword them.
/// </para>
/// <para>
/// So this asserts the ABSENCE of those entries, which is the only thing that
/// distinguishes a pane that was built from one that was described. Every
/// deletion below happened because a ratchet went red on its staleness half -
/// the half that quietly rots, because nothing fails when a list describes a
/// past.
/// </para>
/// </remarks>
public class EveryNewPaneIsProjectedTests
{
    [Test]
    public async Task No_step_four_result_is_waiting_for_an_arm()
    {
        // NOT RunnerLabels, and the difference is the point. The other four are
        // panes that were built, so their entries went. That one is a result
        // kind the console can no longer RECEIVE - step 6 deleted the wrapper,
        // because it and RunnersAsync are one request under two names - so its
        // entry stops being a gap and becomes a decision, reworded rather than
        // deleted.
        string[] built = ["Plan", "Why", "EnvelopeShown", "Identity"];
        var parked = built.Where(ProjectionParityTests.Exempt.ContainsKey).ToArray();

        await Assert.That(parked).IsEmpty()
            .Because("a result kind on this list renders nothing, and the renderer above it "
                   + "is unreachable code that looks like a feature. Found: "
                   + string.Join(", ", parked));
    }

    [Test]
    public async Task No_field_step_four_renders_is_waiting_to_be_assigned()
    {
        string[] filled = ["Notices", "Attribution", "Checklist", "Envelope", "Runners"];
        var parked = filled.Where(StateAssignmentTests.Exempt.ContainsKey).ToArray();

        await Assert.That(parked).IsEmpty()
            .Because("a field nothing assigns renders its default for ever. Found: "
                   + string.Join(", ", parked));
    }

    [Test]
    public async Task The_one_field_still_parked_is_the_one_with_no_producing_verb()
    {
        // AND IT IS A DIFFERENT KIND OF ENTRY, which is why it survives a step
        // whose subject was emptying this list. `Payload` cannot be filled by
        // calling something: ConsoleData offers no read that returns a
        // GateEvidencePayload, and `why` answers a FlightAttribution instead.
        // It needs a READ TO EXIST first - or the field and its renderer both
        // go, which is a decision nobody has taken.
        await Assert.That(StateAssignmentTests.Exempt.Keys).IsEquivalentTo((string[])["Payload"])
            .Because("every other entry was a pane somebody had not built yet, and this one "
                   + "is a pane nothing could build.");
    }
}
