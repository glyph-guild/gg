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
                WhenUnmet = Reason.For(ReasonKinds.NoRunnerAdvertises, ["environment=aurora"]),
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
    public async Task A_withheld_requirement_renders_as_withheld_and_says_by_what()
    {
        // THE FOURTH SATISFIER, AT THE ONE PLACE A PERSON READS IT. The switch
        // here falls through to the raw wire value for anything it does not
        // know, so a fourth value would have printed
        // 'withheld-by-declaration' beside three sentences in English - not a
        // crash, and not something a reader would trust either.
        var withheld = new Checklist
        {
            EnvelopeVersion = "v3",
            FlightNumber = null,
            Environment = "aspire-payments",
            Repository = null,
            RequiredLabels = ["environment=aspire-payments"],
            Items =
            [
                new ChecklistItem
                {
                    Requirement = "environment=aspire-payments",
                    Verification = "a live runner's advertised labels contain it",
                    Satisfier = ChecklistSatisfiers.Withheld,
                    WhenUnmet = Reason.For(
                        ReasonKinds.RunnerReserved, ["environment=aspire-payments", "Dana"]),
                    Disposition = LabelDispositions.Stated,
                },
            ],
        };

        var text = VerbOutput.ToText(new VerbResult.Plan(withheld));

        await Assert.That(text).Contains("withheld");
        await Assert.That(text).DoesNotContain("withheld-by-declaration")
            .Because("the other three satisfiers are rendered as sentences, and one raw wire "
                   + "value among them reads as a rendering that gave up.");
        await Assert.That(text).Contains("Dana")
            .Because("the satisfier says a person is holding it and the reason says WHICH "
                   + "person - a withheld row naming nobody sends the reader nowhere.");
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
