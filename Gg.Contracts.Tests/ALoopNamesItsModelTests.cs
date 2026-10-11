using Gg.Contracts.Authoring;
using static Gg.Contracts.Tests.McpFixtures;

namespace Gg.Contracts.Tests;

/// <summary>
/// A loop may name the model it runs on; absent means the account's default
/// (slice seventy-five).
/// </summary>
/// <remarks>
/// <para>
/// <b>The work kind's decision, in the document a person reviews.</b> GG-1076
/// and GG-1081 (ado#18492, 2026-10-11) both ran on whatever the account
/// defaulted to, because nothing in gg or the airspace chose, and both stopped
/// at the layer a stronger model is likelier to read past. Which model
/// investigates is a decision about the work, so it belongs beside
/// <c>executor:</c> rather than in one runner's environment.
/// </para>
/// <para>
/// <b>A free string, held to shape and not to a list.</b> Model names change
/// faster than this contract, and a closed list here would make every new
/// model a contract release. What Validate holds it to is being a name: no
/// blank, no whitespace, and on no <c>human</c> loop, which has nothing to run
/// a model on.
/// </para>
/// <para>
/// <b>A change is a widening</b>, as every unordered scalar's is: "cannot be
/// shown to tighten" is the definition, so a model change takes the gate root
/// designates rather than applying silently.
/// </para>
/// </remarks>
public class ALoopNamesItsModelTests
{
    private static Envelope KindOn(string? model) => Kind(mcp: null) with
    {
        Loops = [Loop() with { Model = model }],
    };

    [Test]
    public async Task A_named_model_is_rendered_and_read_back_unchanged()
    {
        var written = EnvelopeText.Render(KindOn("claude-opus-5-5"));

        await Assert.That(written).Contains("model: claude-opus-5-5")
            .Because("the member has a line in the text form, or show hides it. Written:\n" + written);

        var read = EnvelopeYaml.Parse(written);
        await Assert.That(read.Diagnosis).IsNull().Because("Diagnosis: " + read.Diagnosis);
        await Assert.That(read.Envelope!.Loops.Single().Model).IsEqualTo("claude-opus-5-5");
        await Assert.That(EnvelopeText.Render(read.Envelope!)).IsEqualTo(written);
    }

    [Test]
    public async Task Absent_stays_absent()
    {
        var written = EnvelopeText.Render(KindOn(null));

        await Assert.That(written.Contains("model:", StringComparison.Ordinal)).IsFalse()
            .Because("absent is the account's default, and a written default would pin every "
                   + "document to the day it was rendered. Written:\n" + written);

        var read = EnvelopeYaml.Parse(written);
        await Assert.That(read.Diagnosis).IsNull().Because("Diagnosis: " + read.Diagnosis);
        await Assert.That(read.Envelope!.Loops.Single().Model).IsNull();
    }

    [Test]
    public async Task A_blank_or_spaced_model_is_refused_naming_the_loop()
    {
        foreach (var bad in new[] { "", "   ", "claude opus", "opus\n" })
        {
            var refusal = Envelope.Validate(KindOn(bad), Roles.WorkKind);

            await Assert.That(refusal).IsNotNull()
                .Because($"'{bad}' is not a model's name, and the client would fail the launch "
                       + "with less to say than this.");
            await Assert.That(refusal!).Contains("investigate").And.Contains("model");
        }
    }

    [Test]
    public async Task A_model_on_a_human_loop_is_refused()
    {
        var refusal = Envelope.Validate(
            Kind(mcp: null) with
            {
                Loops = [Loop() with { Executor = ExecutorRungs.Human, Moves = [], Model = "opus" }],
            },
            Roles.WorkKind);

        await Assert.That(refusal).IsNotNull()
            .Because("a human loop runs no model, so naming one is a document saying something "
                   + "untrue about itself.");
        await Assert.That(refusal!).Contains("human").And.Contains("model");
    }

    [Test]
    public async Task The_work_kinds_model_survives_composition()
    {
        var composition = Compose(Root(servers: null), KindOn("claude-opus-5-5"));

        await Assert.That(composition.Refused).IsNull().Because("Refused: " + composition.Refused);
        await Assert.That(composition.Composed!.Loops.Single().Model).IsEqualTo("claude-opus-5-5")
            .Because("loops ride from the work kind, and the model is the work kind's to choose.");
    }

    [Test]
    public async Task Changing_the_model_is_a_widening_and_so_is_changing_it_back()
    {
        var sonnet = KindOn("claude-sonnet-5");
        var opus = KindOn("claude-opus-5-5");

        var widened = EnvelopeDirection.Widening(sonnet, opus);
        await Assert.That(widened).IsNotNull()
            .Because("a model carries no order by strictness, so a change cannot be shown to "
                   + "tighten and takes the gate.");
        await Assert.That(widened!.Field).IsEqualTo("loops.investigate.model");
        await Assert.That(EnvelopeDirection.Widening(opus, sonnet)).IsNotNull();
        await Assert.That(EnvelopeDirection.Widening(opus, opus)).IsNull();
    }

    [Test]
    public async Task It_is_on_the_wire_surface_for_the_loop_and_the_lease()
    {
        await Assert.That(Description.ProtocolSurface.JsonMembers[typeof(Loop)]).Contains("model");
        await Assert.That(Description.ProtocolSurface.JsonMembers[typeof(LeaseLoop)]).Contains("model")
            .Because("the runner never sees an Envelope; a member the lease does not carry "
                   + "reaches no agent.");
    }
}
