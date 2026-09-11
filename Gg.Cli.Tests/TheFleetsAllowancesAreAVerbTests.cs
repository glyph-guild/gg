using Gg.Cli;
using Gg.Client;
using Gg.Contracts;

namespace Gg.Cli.Tests;

/// <summary>
/// Asking what the whole fleet has left, from a terminal.
/// </summary>
/// <remarks>
/// <para>
/// <b>Plural, and a different question from <c>gg allowance</c>.</b> That one
/// reads the transcripts on the disk it runs on and contacts nothing; this
/// asks the control plane what every machine reported, including machines
/// this one has never seen. Two verbs because they are two questions — and
/// the console only ever asks the second.
/// </para>
/// <para>
/// <b>And because the renderer had no caller.</b> The console reads this
/// through the projection rather than through the text, so without a verb
/// <c>VerbOutput</c>'s fleet rendering was written, tested by nothing, and
/// reachable from nowhere — the assembled-and-discarded shape, in the small.
/// </para>
/// <para>
/// <b>The machines are listed under the allowance</b>, never the other way
/// round. The allowance is what is counted and two machines may share one; a
/// list keyed on machines would print the same pool twice and invite somebody
/// to add the halves.
/// </para>
/// </remarks>
public class TheFleetsAllowancesAreAVerbTests
{
    [Test]
    public async Task It_parses_and_is_not_the_singular_one()
    {
        await Assert.That(CliArgs.Parse(["allowances"])).IsTypeOf<CliAction.Allowances>();

        await Assert.That(CliArgs.Parse(["allowance"])).IsTypeOf<CliAction.Allowance>()
            .Because("one letter apart and a round trip apart: the singular contacts "
                   + "nothing and the plural needs a session.");

        var json = CliArgs.Parse(["allowances", "--json"]);
        await Assert.That(((CliAction.Allowances)json).Json).IsTrue();
    }

    [Test]
    public async Task It_is_on_the_usage_page()
    {
        var refused = CliArgs.Parse(["definitely-not-a-verb"]);

        await Assert.That(((CliAction.Unknown)refused).Message)
            .Contains("gg allowances", StringComparison.Ordinal);
    }

    [Test]
    public async Task It_lists_the_machines_under_the_allowance_they_share()
    {
        var text = VerbOutput.ToText(new VerbResult.Allowances(new AllowanceList
        {
            Allowances =
            [
                new()
                {
                    Name = "kdee-max",
                    MeasuredAt = DateTimeOffset.UnixEpoch,
                    Runners = ["one", "two"],
                    Windows =
                    [
                        new()
                        {
                            Kind = AllowanceWindows.Week,
                            Since = DateTimeOffset.UnixEpoch,
                            InputTokens = 0,
                            OutputTokens = 600_000,
                            CacheReadTokens = 40_000_000,
                            CacheWriteTokens = 0,
                            Limit = 2_400_000,
                        },
                    ],
                },
            ],
        }));

        await Assert.That(text).Contains("kdee-max", StringComparison.Ordinal);
        await Assert.That(text).Contains("2 machines", StringComparison.Ordinal)
            .Because("one line per allowance and the machines counted on it, because the "
                   + "allowance is the thing that has a ceiling.");
        await Assert.That(text).Contains("25%", StringComparison.Ordinal)
            .Because("600,000 of 2,400,000, with forty million cache reads excluded.");
    }

    [Test]
    public async Task A_fleet_that_reports_none_says_how_one_starts_reporting()
    {
        var text = VerbOutput.ToText(
            new VerbResult.Allowances(new AllowanceList { Allowances = [] }));

        await Assert.That(text).Contains("allowance", StringComparison.OrdinalIgnoreCase)
            .Because("empty is the ordinary state of a fleet nobody has configured, and "
                   + "the answer a person needs is which setting turns it on - not a "
                   + "blank.");
    }
}
