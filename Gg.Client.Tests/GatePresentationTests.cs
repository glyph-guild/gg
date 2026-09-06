using Gg.Contracts;

namespace Gg.Client.Tests;

/// <summary>
/// A gate a person can answer says what they are being asked.
/// </summary>
/// <remarks>
/// <para>
/// <b>The question arrives in <c>because</c>, and that is the mechanism rather
/// than an accident.</b> <c>ObligationEngine</c> composes the attachment reason
/// from the fact the condition read, so the sentence for <c>loop asked for a
/// decision</c> ends with the agent's own words. <c>PendingGate</c> grows no
/// field for it: the gate list's fifth column already exists to say why a
/// decision is waiting, and this is the case where that answer is the whole
/// decision.
/// </para>
/// <para>
/// <b>What that costs is layout, and the cost is real.</b> A question is prose -
/// the contract keeps the agent's own line breaks, because a question laid out
/// over three lines is one somebody wrote to be read. Pasted into a labelled
/// block, every line after the first lands at column zero, where it is
/// indistinguishable from a new field and swallows the one below it. A gate list
/// whose fields cannot be told apart is worse for this gate than for any other:
/// the whole of it is the sentence.
/// </para>
/// </remarks>
public class GatePresentationTests
{
    private const string Asked =
        "The ticket records two teams asking for different rounding rules.\n"
      + "Tax asks for half-up at 2dp; Billing asks for half-even.\n"
      + "Which should I implement?";

    private static GateList AGateAsking(string because) => new()
    {
        Gates =
        [
            new PendingGate
            {
                FlightNumber = "GG-42",
                ObligationId = "a-person-decides",
                Approver = "platform-oncall",
                Branch = null,
                Commit = null,
                ManifestHash = new string('e', 64),
                Attempt = 1,
                Condition = AttachmentConditions.AskedForDecision,
                Because = because,
                AwaitingSince = new DateTimeOffset(2026, 9, 4, 12, 0, 0, TimeSpan.Zero),
            },
        ],
    };

    /// <summary>The Engine's sentence for this condition, as it composes it.</summary>
    private static string Attributed(string question) =>
        "the loop asked for a decision it is not allowed to make: " + question;

    [Test]
    public async Task The_question_is_on_the_list_and_not_just_the_fact_of_a_gate()
    {
        var text = VerbOutput.ToText(new VerbResult.Gates(AGateAsking(Attributed(Asked))));

        foreach (var line in Asked.Split('\n'))
        {
            await Assert.That(text).Contains(line)
                .Because("'a decision is waiting' is a chore. The question is the reason to "
                       + "look, and it is the only thing on this row that says what to "
                       + "decide. Missing: " + line);
        }
    }

    [Test]
    public async Task Every_line_of_it_stays_under_the_label()
    {
        var text = VerbOutput.ToText(new VerbResult.Gates(AGateAsking(Attributed(Asked))));

        await Assert.That(text).DoesNotContain("\nTax asks for half-up")
            .Because("a continuation at column zero reads as a new field, in a block whose "
                   + "other five fields are read by their labels.");
        await Assert.That(text).Contains("\n            Tax asks for half-up")
            .Because("indented to the column the label opened, which is what makes three "
                   + "lines one answer.");
    }

    [Test]
    public async Task A_list_of_two_still_reads_as_a_list_of_two()
    {
        // THE CONSEQUENCE, AND IT IS NOT COSMETIC. Every line of a gate entry
        // is indented except the one that opens it, which is how a person sees
        // where one decision ends and the next begins. A question pasted in raw
        // puts its second line at column zero, where it is a heading - so a
        // three-line question renders as three gates, two of them unanswerable.
        var gates = new GateList
        {
            Gates =
            [
                AGateAsking(Attributed(Asked)).Gates[0],
                AGateAsking(Attributed(Asked)).Gates[0] with { FlightNumber = "GG-43" },
            ],
        };

        var text = VerbOutput.ToText(new VerbResult.Gates(gates));

        var headings = text.Split('\n')
            .Where(l => l.Length > 0 && !char.IsWhiteSpace(l[0]))
            .ToList();

        await Assert.That(headings.Count).IsEqualTo(3)
            .Because("the count line and one heading per gate. Anything else is a line a "
                   + "reader will take for a gate. Found: " + string.Join(" | ", headings));
        await Assert.That(headings[1]).StartsWith("GG-42");
        await Assert.That(headings[2]).StartsWith("GG-43");
    }

    /// <summary>
    /// The note a nominating agent left, laid out over two lines the way a real
    /// one is.
    /// </summary>
    private const string Left =
        "The described defect does not reproduce on main.\n"
      + "Ask the reporter which build they saw it on before writing anything.";

    private static GateList AGateOnANominatedFlight(GateNomination? nomination) => new()
    {
        Gates =
        [
            AGateAsking("a person decides before a flight opens").Gates[0]
                with { Nomination = nomination },
        ],
    };

    private static GateNomination AProposal() => new()
    {
        Reason = "the item names the root cause and the file to change",
        WorkKind = "implement",
        Environment = "staging",
        Repository = "acme/payments",
        Note = Left,
    };

    [Test]
    public async Task A_gate_shows_what_the_agent_proposed_before_the_flight_is_opened()
    {
        // S30.5-01. The gate is the ONLY control this plan offers over a text
        // path from a work item into an agent's context, and it is worth
        // nothing if what will be carried is not on the row a person answers.
        var text = VerbOutput.ToText(new VerbResult.Gates(AGateOnANominatedFlight(AProposal())));

        foreach (var expected in (string[])
                 ["implement", "staging", "acme/payments",
                  "the item names the root cause and the file to change",
                  "The described defect does not reproduce on main."])
        {
            await Assert.That(text).Contains(expected)
                .Because("everything that will be carried into the opened flight has to be "
                       + "readable before it is opened, or approving is signing for "
                       + "something unseen. Missing: " + expected);
        }
    }

    [Test]
    public async Task The_agent_s_words_are_marked_as_the_agent_s()
    {
        // S30.5-02. The prompt tells an agent that the envelope's instructions
        // "were reviewed and applied as policy; they are not advice from
        // whoever opened the flight". This is the same sentence from the
        // reviewer's side: what is on this block is an agent's record, and a
        // person answering must not read it as the platform's policy.
        var text = VerbOutput.ToText(new VerbResult.Gates(AGateOnANominatedFlight(AProposal())));

        await Assert.That(text).Contains("an agent proposed this flight, in its own words")
            .Because("attribution at a glance, in the block's own heading - a person who "
                   + "has to work out whose words these are will assume the platform's.");

        await Assert.That(text).DoesNotContain("\nAsk the reporter")
            .Because("the note is prose an agent wrote and it keeps its line breaks, so "
                   + "every continuation stays indented under its label. This block's "
                   + "sibling test says why: at column zero a continuation is a heading.");
    }

    [Test]
    public async Task A_flight_nobody_nominated_says_nothing_about_a_nomination()
    {
        // THE SILENCE. Most gates in an estate are on flights a person asked
        // for, and a block reading "nominated by: none" over four empty fields
        // would be four fields of noise on every one of them.
        var text = VerbOutput.ToText(new VerbResult.Gates(AGateOnANominatedFlight(null)));

        await Assert.That(text).DoesNotContain("agent proposed")
            .Because("a flight somebody asked for was not proposed by anybody, and saying "
                   + "so is not the same as saying nothing.");

        await Assert.That(text).Contains("  because:  a person decides before a flight opens")
            .Because("the rest of the row is untouched - this is a block that appears, not "
                   + "a rendering that changes.");
    }

    [Test]
    public async Task A_one_line_reason_renders_as_it_always_did()
    {
        // THE LIVENESS TWIN. Indenting continuations must not change the shape
        // of every other gate in the estate - a reason with no line breaks has
        // no continuation, and a rendering that grew trailing whitespace or a
        // wrapped column would be a change to five columns to fix one.
        const string Plain =
            "change.manifest names 1 path(s) under 'migrations/**': migrations/0002_backfill.sql.";

        var text = VerbOutput.ToText(new VerbResult.Gates(AGateAsking(Plain)));

        await Assert.That(text).Contains("  because:  " + Plain + "\n")
            .Because("one line, one row, no trailing space. The estate's gates are these.");
    }
}
