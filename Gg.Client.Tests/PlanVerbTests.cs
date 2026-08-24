using Gg.Client;
using Gg.Contracts;

namespace Gg.Client.Tests;

/// <summary>
/// `gg plan` renders the checklist: what a flight would need, and who could
/// satisfy it.
/// </summary>
/// <remarks>
/// <para>
/// <b>Headless, like every verb since slice seven:</b> a `VerbResult`, three
/// arms in `VerbOutput`, no console write outside `Program.EmitAsync` -
/// so CI and a person's terminal are the same path, and `--json` is the wire
/// document unchanged.
/// </para>
/// <para>
/// <b>The satisfier column holds exactly two sentences in this slice</b> -
/// "already true via matching" and "nobody: declared capability gap" -
/// because strategies do not exist yet, and a rendered placeholder for
/// machinery that does not exist would be the checklist containing a promise.
/// </para>
/// </remarks>
public class PlanVerbTests
{
    private static Checklist TwoItems() => new()
    {
        EnvelopeVersion = "v3",
        FlightNumber = null,
        Environment = "aspire-payments",
        Repository = "acme/payments",
        RequiredLabels = ["environment=aspire-payments"],
        Items =
        [
            new ChecklistItem
            {
                Requirement = "environment=aspire-payments",
                Verification = "a live runner's advertised labels contain it",
                Satisfier = ChecklistSatisfiers.MatchingRunner,
                WhenUnmet = null,
                Disposition = LabelDispositions.Stated,
            },
            new ChecklistItem
            {
                Requirement = "environment=aurora",
                Verification = "a live runner's advertised labels contain it",
                Satisfier = ChecklistSatisfiers.Nobody,
                WhenUnmet = "waiting: no runner advertises environment=aurora",
                Disposition = LabelDispositions.Stated,
            },
        ],
    };

    [Test]
    public async Task The_satisfier_column_renders_its_two_sentences()
    {
        var text = VerbOutput.ToText(new VerbResult.Plan(TwoItems()));

        await Assert.That(text).Contains("already true via matching");
        await Assert.That(text).Contains("nobody: declared capability gap")
            .Because("a gap is a fact about the fleet, and 'nobody' is the honest satisfier.");
        await Assert.That(text).Contains("environment=aspire-payments");
        await Assert.That(text).Contains("waiting: no runner advertises environment=aurora");
    }

    [Test]
    public async Task The_disposition_is_one_word_on_the_row()
    {
        var text = VerbOutput.ToText(new VerbResult.Plan(TwoItems()));

        await Assert.That(text).Contains(LabelDispositions.Stated)
            .Because("the third of the disposition's three surfaces: the runner listing, the "
                   + "checklist, and the refusal all say the same word.");
    }

    [Test]
    public async Task Json_is_the_checklist_document_unchanged()
    {
        var json = VerbOutput.ToJson(new VerbResult.Plan(TwoItems()));

        await Assert.That(json).Contains("\"envelopeVersion\"");
        await Assert.That(json).Contains("\"requiredLabels\"");
        await Assert.That(json).Contains("already-true-via-matching")
            .Because("--json carries the wire vocabulary, not the rendered sentence.");
    }

    [Test]
    public async Task A_saved_payload_renders_the_same_as_the_live_one()
    {
        var live = new VerbResult.Plan(TwoItems());
        var reparsed = VerbOutput.Parse(live.Kind, VerbOutput.ToJson(live));

        await Assert.That(VerbOutput.ToText(reparsed)).IsEqualTo(VerbOutput.ToText(live))
            .Because("re-rendering a --json payload somebody sent us must show what they saw.");
    }
}
