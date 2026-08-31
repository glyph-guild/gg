namespace Gg.Cli.Tests;

/// <summary>
/// <c>gg flights --intent azure-boards#4471</c> — everything for one work item.
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
/// spellings of one pair would be two things to remember for no reason. The
/// splitting rule is the same too — first separator wins, so an id containing
/// one keeps its tail.
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
    public async Task Listing_by_work_item_parses_into_a_provider_and_an_id()
    {
        var action = CliArgs.Parse(["flights", "--intent", "azure-boards#4471"]);

        await Assert.That(action).IsTypeOf<CliAction.Flights>();

        var flights = (CliAction.Flights)action;
        await Assert.That(flights.Provider).IsEqualTo("azure-boards");
        await Assert.That(flights.Id).IsEqualTo("4471");
    }

    [Test]
    public async Task The_plain_listing_is_unchanged()
    {
        // The regression half, and the one a new arm in a pattern match
        // threatens: every `gg flights` anybody has typed is this.
        var plain = (CliAction.Flights)CliArgs.Parse(["flights"]);

        await Assert.That(plain.Provider).IsNull();
        await Assert.That(plain.Id).IsNull();
        await Assert.That(plain.All).IsFalse();

        var everything = (CliAction.Flights)CliArgs.Parse(["flights", "--all"]);
        await Assert.That(everything.All).IsTrue();
        await Assert.That(everything.Provider).IsNull();
    }

    [Test]
    public async Task Correlating_across_every_flight_is_allowed()
    {
        // --all and --intent together, because "everything for this ticket"
        // usually MEANS everything - a correlation that silently showed only
        // the flights still in the air would answer a different question than
        // the one somebody asked.
        var both = (CliAction.Flights)CliArgs.Parse(["flights", "--all", "--intent", "azure-boards#4471"]);

        await Assert.That(both.All).IsTrue();
        await Assert.That(both.Provider).IsEqualTo("azure-boards");
        await Assert.That(both.Id).IsEqualTo("4471");
    }

    [Test]
    public async Task A_token_that_is_not_two_things_is_refused_by_the_parser()
    {
        foreach (var token in (string[])["azure-boards", "#4471", "azure-boards#", "#"])
        {
            await Assert.That(CliArgs.Parse(["flights", "--intent", token]))
                .IsTypeOf<CliAction.Unknown>()
                .Because($"'{token}' names at most one of the two things a work item is, and "
                       + "the id alone does not say which tracker it is in.");
        }
    }

    [Test]
    public async Task An_id_containing_the_separator_keeps_all_of_it()
    {
        // The same rule as `gg fly --ticket`, asserted separately because it is
        // a second place the splitting is written and two spellings of one rule
        // is how they drift.
        var flights = (CliAction.Flights)CliArgs.Parse(["flights", "--intent", "jira#PROJ-1#2"]);

        await Assert.That(flights.Provider).IsEqualTo("jira");
        await Assert.That(flights.Id).IsEqualTo("PROJ-1#2");
    }

    [Test]
    public async Task The_usage_names_the_flag_and_its_shape()
    {
        var usage = ((CliAction.Unknown)CliArgs.Parse(["telepathy"])).Message;

        await Assert.That(usage).Contains("--intent");
    }
}
