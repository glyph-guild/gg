using Gg.Contracts.Authoring;
using static Gg.Contracts.Tests.McpFixtures;

namespace Gg.Contracts.Tests;

/// <summary>
/// Root may list the models a person may move one flight to; absent means
/// nobody may (slice seventy-six).
/// </summary>
/// <remarks>
/// <para>
/// <b>The owner's ask, 2026-10-11:</b> <i>"during a flight i would like the
/// ability to override the model on the fly - but it needs to be enabled in
/// the root airspace."</i> Slice seventy-five made the model the work kind's
/// decision; this is the tenant's decision about what a person may do to one
/// flight at a keystroke.
/// </para>
/// <para>
/// <b>A list, not a switch.</b> A switch that admitted any name would let that
/// keystroke put a flight on a model nobody reviewed the cost of. The list is
/// the shape <c>environments:</c> already has: root only, null is off, empty
/// is refused, growing is a widening.
/// </para>
/// <para>
/// <b>The person's bound, not the loop's.</b> A loop's own <c>model:</c> is
/// held to shape and not to this list, as slice seventy-five decided; nothing
/// here changes what a work kind may name.
/// </para>
/// </remarks>
public class RootListsTheModelsAPersonMayChooseTests
{
    private static Envelope RootListing(IReadOnlyList<string>? models) =>
        Root(servers: null) with { Models = models };

    [Test]
    public async Task A_list_is_rendered_and_read_back_unchanged()
    {
        var written = EnvelopeText.Render(RootListing(["claude-opus-5-5", "claude-sonnet-5-5"]));

        await Assert.That(written).Contains("models:")
            .And.Contains("- claude-opus-5-5")
            .And.Contains("- claude-sonnet-5-5")
            .Because("the member has lines in the text form, or show hides it. Written:\n" + written);

        var read = EnvelopeYaml.Parse(written);
        await Assert.That(read.Diagnosis).IsNull().Because("Diagnosis: " + read.Diagnosis);
        await Assert.That(read.Envelope!.Models).IsEquivalentTo(["claude-opus-5-5", "claude-sonnet-5-5"]);
        await Assert.That(EnvelopeText.Render(read.Envelope!)).IsEqualTo(written);
    }

    [Test]
    public async Task One_name_is_a_scalar_like_the_other_bounds()
    {
        var written = EnvelopeText.Render(RootListing(["claude-opus-5-5"]));

        await Assert.That(written).Contains("models: claude-opus-5-5")
            .Because("one name renders as a scalar, the rule environments: and repositories: "
                   + "follow so a one-item list does not rewrite a person's file. Written:\n" + written);

        var read = EnvelopeYaml.Parse(written);
        await Assert.That(read.Diagnosis).IsNull().Because("Diagnosis: " + read.Diagnosis);
        await Assert.That(read.Envelope!.Models).IsEquivalentTo(["claude-opus-5-5"]);
    }

    [Test]
    public async Task Absent_stays_absent()
    {
        var written = EnvelopeText.Render(RootListing(null));

        await Assert.That(written.Contains("models:", StringComparison.Ordinal)).IsFalse()
            .Because("absent is off, which is every tenant today, and none of them should "
                   + "have to say so. Written:\n" + written);

        var read = EnvelopeYaml.Parse(written);
        await Assert.That(read.Diagnosis).IsNull().Because("Diagnosis: " + read.Diagnosis);
        await Assert.That(read.Envelope!.Models).IsNull();
    }

    [Test]
    public async Task An_empty_list_is_refused()
    {
        var refusal = Envelope.Validate(RootListing([]), Roles.Root);

        await Assert.That(refusal).IsNotNull()
            .Because("a declared, empty list would enable a door that admits nobody, which is "
                   + "the inversion the other bounds refuse.");
        await Assert.That(refusal!).Contains("models");
    }

    [Test]
    public async Task A_blank_or_spaced_name_is_refused()
    {
        foreach (var bad in new[] { "", "   ", "claude opus", "opus\n" })
        {
            var refusal = Envelope.Validate(RootListing(["claude-opus-5-5", bad]), Roles.Root);

            await Assert.That(refusal).IsNotNull()
                .Because($"'{bad}' is not a model's name, by the rule a loop's model: is held to.");
            await Assert.That(refusal!).Contains("models");
        }
    }

    [Test]
    public async Task A_work_kind_may_not_declare_it()
    {
        var refusal = Envelope.Validate(
            Kind(mcp: null) with { Models = ["claude-opus-5-5"] }, Roles.WorkKind);

        await Assert.That(refusal).IsNotNull()
            .Because("what a person may choose for any flight here is the tenant's statement, "
                   + "and a narrowing speaks for one work kind.");
        await Assert.That(refusal!).Contains("models").And.Contains(Roles.Root);
    }

    [Test]
    public async Task Roots_list_survives_composition_and_a_kind_naming_one_is_refused()
    {
        var composition = Compose(RootListing(["claude-opus-5-5"]), Kind(mcp: null));

        await Assert.That(composition.Refused).IsNull().Because("Refused: " + composition.Refused);
        await Assert.That(composition.Composed!.Models).IsEquivalentTo(["claude-opus-5-5"])
            .Because("the composer is hand-written and a root-only member it does not mention "
                   + "rides from the base, which is the work kind - so root's list would vanish "
                   + "for every flight, exactly how learned: went missing.");

        var disagreeing = Compose(
            RootListing(["claude-opus-5-5"]),
            Kind(mcp: null) with { Models = ["claude-sonnet-5-5"] });

        await Assert.That(disagreeing.Refused).IsNotNull()
            .Because("two layers listing different models is a mistake, not an intersection.");
        await Assert.That(disagreeing.Refused!).Contains("models");
    }

    [Test]
    public async Task Growing_the_list_is_a_widening_and_shrinking_it_is_not()
    {
        var one = RootListing(["claude-opus-5-5"]);
        var two = RootListing(["claude-opus-5-5", "claude-sonnet-5-5"]);

        var widened = EnvelopeDirection.Widening(one, two);
        await Assert.That(widened).IsNotNull()
            .Because("a person may now put flights somewhere they could not before, which "
                   + "takes the gate root designates.");
        await Assert.That(widened!.Field).IsEqualTo("models");

        await Assert.That(EnvelopeDirection.Widening(RootListing(null), one)).IsNotNull()
            .Because("listing anything where nothing was listed turns the door on.");
        await Assert.That(EnvelopeDirection.Widening(two, one)).IsNull()
            .Because("shrinking narrows, and a narrowing applies without a gate.");
        await Assert.That(EnvelopeDirection.Widening(two, two)).IsNull();
    }

    [Test]
    public async Task It_is_on_the_wire_surface()
    {
        await Assert.That(Description.ProtocolSurface.JsonMembers[typeof(Envelope)]).Contains("models")
            .Because("an applied document is stored as this type's JSON, and a member the "
                   + "surface does not declare is one the control plane drops on the way in.");
    }
}
