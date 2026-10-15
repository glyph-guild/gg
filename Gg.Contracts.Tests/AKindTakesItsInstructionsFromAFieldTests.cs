using Gg.Contracts;
using Gg.Contracts.Description;

namespace Gg.Contracts.Tests;

/// <summary>
/// <c>instructions-from:</c> parses, renders back, is inherited and replaced down a chain, is
/// work-kind only, and declaring one widens (S82.1-01, ADR-0041 Decision 10).
/// </summary>
public class AKindTakesItsInstructionsFromAFieldTests
{
    private const string Field = "instructions-from: Custom.ClaudeCodePlan\n";

    [Test]
    public async Task A_work_kind_reads_and_writes_back_the_field_its_instructions_come_from()
    {
        var read = WorkKindFixtures.Parsed(WorkKindFixtures.Base + "\n" + Field);
        await Assert.That(read.InstructionsFrom).IsEqualTo("Custom.ClaudeCodePlan");

        var written = EnvelopeText.Render(read);
        await Assert.That(written).Contains(Field);
        await Assert.That(WorkKindFixtures.Parsed(written).InstructionsFrom).IsEqualTo("Custom.ClaudeCodePlan");
        await Assert.That(WorkKindFixtures.Parsed(WorkKindFixtures.Base).InstructionsFrom).IsNull();
    }

    [Test]
    public async Task A_child_inherits_it_and_replaces_it_by_naming_its_own()
    {
        var parent = WorkKindFixtures.Parsed(WorkKindFixtures.Base + "\n" + Field);

        var silent = WorkKindChain.Resolve("investigate", parent,
            [("investigate-instructions", WorkKindFixtures.Extension(WorkKindFixtures.Child))]);
        await Assert.That(silent.Effective!.InstructionsFrom).IsEqualTo("Custom.ClaudeCodePlan");

        var child = WorkKindFixtures.Extension(WorkKindFixtures.Child + "\ninstructions-from: System.Description\n");
        var naming = WorkKindChain.Resolve("investigate", parent, [("investigate-instructions", child)]);
        await Assert.That(naming.Effective!.InstructionsFrom).IsEqualTo("System.Description");
        await Assert.That(EnvelopeText.Render(child)).Contains("instructions-from: System.Description\n");
    }

    [Test]
    public async Task Only_a_work_kind_declares_it_and_only_as_one_exact_path()
    {
        var valid = WorkKindFixtures.Parsed(WorkKindFixtures.Base + "\n" + Field);
        await Assert.That(Envelope.Validate(valid, Roles.WorkKind)).IsNull();

        await Assert.That(Envelope.Validate(valid with { Accepts = null, Produces = null }, Roles.Root))
            .Contains("instructions-from");
        await Assert.That(Envelope.Validate(valid with { InstructionsFrom = "Custom.*" }, Roles.WorkKind))
            .Contains("Custom.*");
        await Assert.That(Envelope.Validate(valid with { InstructionsFrom = "Custom Plan" }, Roles.WorkKind))
            .Contains("Custom Plan");
    }

    [Test]
    public async Task Declaring_or_changing_it_widens_and_removing_it_does_not()
    {
        var none = WorkKindFixtures.Parsed(WorkKindFixtures.Base);
        var one = none with { InstructionsFrom = "Custom.ClaudeCodePlan" };

        await Assert.That(EnvelopeDirection.Widening(none, one)).IsNotNull()
            .Because("a field's text reaching every flight of the kind is new influence over all of them.");
        await Assert.That(EnvelopeDirection.Widening(one, one with { InstructionsFrom = "System.Description" }))
            .IsNotNull();
        await Assert.That(EnvelopeDirection.Widening(one, none)).IsNull();
    }

    [Test]
    public async Task The_lease_and_the_surface_carry_it()
    {
        await Assert.That(ProtocolSurface.JsonMembers[typeof(Envelope)]).Contains("instructionsFrom");
        await Assert.That(ProtocolSurface.JsonMembers[typeof(EnvelopeExtension)]).Contains("instructionsFrom");
        await Assert.That(ProtocolSurface.JsonMembers[typeof(LeaseLoop)]).Contains("instructionsFrom");
    }
}
