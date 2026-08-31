namespace Gg.Contracts.Tests;

/// <summary>
/// The third intent kind: a work item, declared rather than addressed.
/// </summary>
/// <remarks>
/// <para>
/// <b>This kind was named in writing before it was built.</b>
/// <see cref="FlightIntentKinds"/> has said since slice two that <i>adding a
/// third — an issue, a pull request, a ticket — is a contract change, which is
/// what makes it visible; the FIELD to carry it already exists, which is what
/// makes it cheap.</i> This is that, and the two new members are the cheap half.
/// </para>
/// <para>
/// <b>A ticket is one payload carried in two fields.</b> That is the only thing
/// structurally new here: <c>text</c> and <c>uri</c> are each a payload in one
/// field, so "how many payloads is this" used to be a count of non-empty
/// strings. It is not any more, and a partial ticket has to be diagnosed as a
/// missing field rather than as <i>this one has neither</i> — which is the
/// wrong sentence and sends somebody looking in the wrong place.
/// </para>
/// <para>
/// <b>The provider is a free string, deliberately.</b> gg is public and
/// distributed and names no forge — <c>HttpsGitVcsAdapter</c> says so in those
/// words — and a closed provider vocabulary here would be the same mistake one
/// noun over. Which providers actually resolve is the control plane's knowledge
/// and the port's problem, and the port is not in this slice.
/// </para>
/// </remarks>
public class TicketIntentTests
{
    private static FlightIntent Ticket(string? provider = "azure-boards", string? id = "4471") =>
        new() { Kind = FlightIntentKinds.Ticket, Provider = provider, Id = id };

    [Test]
    public async Task A_work_item_is_an_intent()
    {
        await Assert.That(FlightIntent.Validate(Ticket())).IsNull()
            .Because("validation returns the diagnosis, and there is nothing wrong with this one.");
    }

    [Test]
    public async Task The_kind_is_advertised_beside_the_two_that_were_there()
    {
        await Assert.That(FlightIntentKinds.All).Contains(FlightIntentKinds.Ticket);
        await Assert.That(FlightIntentKinds.All.Count).IsEqualTo(3);

        // The two that already shipped keep their values. A vocabulary whose
        // existing members move is not an addition, it is a break wearing one.
        await Assert.That(FlightIntentKinds.Text).IsEqualTo("text");
        await Assert.That(FlightIntentKinds.Uri).IsEqualTo("uri");
    }

    // ---- the refusals, each naming what is wrong ----

    [Test]
    public async Task A_ticket_with_no_provider_is_refused_naming_the_provider()
    {
        var diagnosis = FlightIntent.Validate(Ticket(provider: null));

        await Assert.That(diagnosis).IsNotNull();
        await Assert.That(diagnosis!).Contains("provider")
            .Because("'invalid intent' tells whoever hit it nothing about which of the two "
                   + "fields they left out, and there are exactly two.");
        await Assert.That(diagnosis!).DoesNotContain("neither")
            .Because("a partial ticket IS a payload - an incomplete one. Reporting it as 'this "
                   + "one has neither' sends somebody looking for a missing intent rather than "
                   + "for a missing field, which is the wrong half of the document.");
    }

    [Test]
    public async Task A_ticket_with_no_id_is_refused_naming_the_id()
    {
        var diagnosis = FlightIntent.Validate(Ticket(id: null));

        await Assert.That(diagnosis).IsNotNull();
        await Assert.That(diagnosis!).Contains("id");
        await Assert.That(diagnosis!).DoesNotContain("neither");
    }

    [Test]
    public async Task Whitespace_is_not_a_provider_and_is_not_an_id()
    {
        // The same disguise the text kind already refuses. A flight whose entire
        // work item reference is a space is the empty case in a hat.
        await Assert.That(FlightIntent.Validate(Ticket(provider: "   "))).IsNotNull();
        await Assert.That(FlightIntent.Validate(Ticket(id: "   "))).IsNotNull();
    }

    [Test]
    public async Task A_ticket_carrying_text_as_well_is_refused_as_two_payloads()
    {
        // The existing one-payload sentence, extended to three fields rather
        // than a second sentence beside it - two sentences saying the same
        // thing is how the readers in two repositories come to disagree.
        var confused = new FlightIntent
        {
            Kind = FlightIntentKinds.Ticket,
            Provider = "azure-boards",
            Id = "4471",
            Text = "fix the login bug",
        };

        var diagnosis = FlightIntent.Validate(confused);

        await Assert.That(diagnosis).IsNotNull();
        await Assert.That(diagnosis!).Contains("one payload")
            .Because("this is the sentence that was already there, and it is the one that has "
                   + "to grow rather than be joined.");
    }

    [Test]
    public async Task A_ticket_carrying_a_uri_as_well_is_refused()
    {
        var confused = new FlightIntent
        {
            Kind = FlightIntentKinds.Ticket,
            Provider = "azure-boards",
            Id = "4471",
            Uri = "https://example.invalid/issues/7",
        };

        await Assert.That(FlightIntent.Validate(confused)).IsNotNull();
    }

    [Test]
    public async Task A_text_intent_that_also_names_a_provider_is_refused()
    {
        // The other direction, and the one a hand-written JSON body produces:
        // the kind says text, and somebody filled the ticket fields in too.
        var confused = new FlightIntent
        {
            Kind = FlightIntentKinds.Text,
            Text = "fix the login bug",
            Provider = "azure-boards",
            Id = "4471",
        };

        await Assert.That(FlightIntent.Validate(confused)).IsNotNull();
    }

    [Test]
    public async Task The_kind_and_the_populated_fields_must_agree()
    {
        // A kind that disagrees with the fields carrying the payload is how a
        // consumer renders a work item as prose, or tries to resolve free text.
        var mismatched = new FlightIntent
        {
            Kind = FlightIntentKinds.Ticket,
            Text = "fix the login bug",
        };

        await Assert.That(FlightIntent.Validate(mismatched)).IsNotNull();
    }

    [Test]
    public async Task A_ticket_carrying_nothing_at_all_is_refused()
    {
        await Assert.That(FlightIntent.Validate(new FlightIntent { Kind = FlightIntentKinds.Ticket }))
            .IsNotNull();
    }

    // ---- what the two that already shipped keep ----

    [Test]
    public async Task The_two_older_kinds_are_unchanged_by_this()
    {
        // THE REGRESSION HALF, and it is the one a new value in a closed
        // vocabulary actually threatens. Every flight ever opened is one of
        // these two, and they have to validate exactly as they did.
        await Assert.That(FlightIntent.Validate(
                new FlightIntent { Kind = FlightIntentKinds.Text, Text = "fix the login bug" }))
            .IsNull();
        await Assert.That(FlightIntent.Validate(
                new FlightIntent { Kind = FlightIntentKinds.Uri, Uri = "https://example.invalid/issues/7" }))
            .IsNull();
        await Assert.That(FlightIntent.Validate(
                new FlightIntent { Kind = FlightIntentKinds.Text, Text = "x", Uri = "https://example.invalid/y" }))
            .IsNotNull();
        await Assert.That(FlightIntent.Validate(new FlightIntent { Kind = "telepathy", Text = "x" }))
            .IsNotNull();
    }
}
