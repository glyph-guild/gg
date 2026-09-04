namespace Gg.Cli.Tests;

/// <summary>
/// <c>gg flights --intent a-tracker#4471</c> — everything for one work item.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is the half of ADR-0019 § 5 that needs no port.</b> The work item id
/// is control-plane-side from flight creation, before a runner is leased, so
/// <i>everything for this ticket</i> is a query over something already held.
/// Nothing here talks to a tracker, and nothing here could.
/// </para>
/// <para>
/// <b>Same token shape as <c>gg fly --ticket</c>, deliberately.</b> What a
/// person reads back off a flight is what they can type into this, and two
/// spellings of one pair would be two things to remember for no reason.
/// </para>
/// <para>
/// <b>VALIDATED here and SPLIT there, since the uri form arrived.</b> The parse
/// still refuses a token that is neither a work item nor an absolute uri — a
/// filter nobody could satisfy that answered with everything is a wide answer
/// to a narrow question — and then passes the token through whole. Splitting it
/// here as well would be a second spelling of a rule the control plane has to
/// apply anyway, and an id containing a separator is exactly where two
/// spellings drift.
/// </para>
/// <para>
/// <b>Not position-independent.</b> <c>--json</c> and <c>--all</c> are, because
/// they are bare flags a person types in either place. <c>--intent</c> takes a
/// value, and a value-taking flag pulled out by a pre-scan is how the value
/// gets mistaken for a verb.
/// </para>
/// </remarks>
public class FlightsByIntentTests
{
    [Test]
    public async Task Listing_by_work_item_carries_the_token_a_person_typed()
    {
        var action = CliArgs.Parse(["flights", "--intent", "a-tracker#4471"]);

        await Assert.That(action).IsTypeOf<CliAction.Flights>();
        await Assert.That(((CliAction.Flights)action).Intent).IsEqualTo("a-tracker#4471")
            .Because("validated here and split there: the shape is refused at the parse and "
                   + "the token travels whole, so the separator rule has one home.");
    }

    [Test]
    public async Task Listing_by_a_link_is_the_second_shape()
    {
        var action = CliArgs.Parse(
            ["flights", "--intent", "https://example.test/boards/1/items/812"]);

        await Assert.That(action).IsTypeOf<CliAction.Flights>();
        await Assert.That(((CliAction.Flights)action).Intent)
            .IsEqualTo("https://example.test/boards/1/items/812")
            .Because("a flight opened from a link is correlated by the link, as written. The "
                   + "control plane compares it ordinally, so anything normalised here would "
                   + "be a spelling nothing matches.");
    }

    [Test]
    public async Task The_plain_listing_is_unchanged()
    {
        // The regression half, and the one a new arm in a pattern match
        // threatens: every `gg flights` anybody has typed is this.
        var plain = (CliAction.Flights)CliArgs.Parse(["flights"]);

        await Assert.That(plain.Intent).IsNull();
        await Assert.That(plain.All).IsFalse();

        var everything = (CliAction.Flights)CliArgs.Parse(["flights", "--all"]);
        await Assert.That(everything.All).IsTrue();
        await Assert.That(everything.Intent).IsNull();
    }

    [Test]
    public async Task Correlating_across_every_flight_is_allowed()
    {
        // --all and --intent together, because "everything for this ticket"
        // usually MEANS everything - a correlation that silently showed only
        // the flights still in the air would answer a different question than
        // the one somebody asked.
        var both = (CliAction.Flights)CliArgs.Parse(["flights", "--all", "--intent", "a-tracker#4471"]);

        await Assert.That(both.All).IsTrue();
        await Assert.That(both.Intent).IsEqualTo("a-tracker#4471");
    }

    [Test]
    public async Task A_token_that_is_neither_shape_is_refused_by_the_parser()
    {
        // STILL REFUSED with the uri form in place, and that is the assertion
        // worth having: none of these is an absolute uri either, so adding a
        // second accepted shape did not quietly accept every typo. `TryCreate`
        // with UriKind.Absolute is what holds it - a relative reference is not
        // a uri for this purpose.
        foreach (var token in (string[])["a-tracker", "#4471", "a-tracker#", "#", "4471"])
        {
            await Assert.That(CliArgs.Parse(["flights", "--intent", token]))
                .IsTypeOf<CliAction.Unknown>()
                .Because($"'{token}' is neither a work item nor an absolute uri, and a filter "
                       + "nobody could satisfy that answered with everything would be a wide "
                       + "answer to a narrow question.");
        }
    }

    [Test]
    public async Task An_id_containing_the_separator_keeps_all_of_it()
    {
        // THE DRIFT THIS USED TO GUARD AGAINST IS GONE RATHER THAN GUARDED. The
        // token is validated here and split by the control plane, which splits
        // on the first separator - so an id carrying one keeps its tail because
        // there is only one place that decides, not because two places agree.
        var flights = (CliAction.Flights)CliArgs.Parse(["flights", "--intent", "jira#PROJ-1#2"]);

        await Assert.That(flights.Intent).IsEqualTo("jira#PROJ-1#2")
            .Because("truncating the tail here would send a filter for 'PROJ-1' and answer "
                   + "about a different work item, which is the failure this repository "
                   + "keeps finding one field at a time.");
    }

    [Test]
    public async Task The_usage_names_the_flag_and_its_shape()
    {
        var usage = ((CliAction.Unknown)CliArgs.Parse(["telepathy"])).Message;

        await Assert.That(usage).Contains("--intent");
    }
}
