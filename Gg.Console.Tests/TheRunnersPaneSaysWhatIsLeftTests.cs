using Gg.Console;
using Gg.Contracts;

namespace Gg.Console.Tests;

/// <summary>
/// What the runners pane says about the allowance each machine spends from.
/// </summary>
/// <remarks>
/// <para>
/// <b>The FLEET's allowances, not this machine's.</b> `gg allowance` reads the
/// transcripts on the disk it runs on; this reads what every machine has
/// reported, over the read surface, including machines this laptop has never
/// seen. They are different questions with the same word in them, and a pane
/// that answered the first while being labelled the second would be the
/// two-cursor defect this console has already met once — a pane and its title
/// answering "which flight" from different places.
/// </para>
/// <para>
/// <b>A column on the runners pane rather than a tab of its own.</b> An
/// allowance is a property of the machines that spend from it, and the
/// question a person has while looking at a fleet is <i>which of these can
/// still work</i>. A tab would make them look at two things and join them
/// themselves.
/// </para>
/// <para>
/// <b>No ceiling is said in words, never as a percentage</b> — the rule the
/// verb already follows, arriving at the second surface that renders one. A
/// window with no configured limit rendered as 0% reads as plenty left.
/// </para>
/// </remarks>
public class TheRunnersPaneSaysWhatIsLeftTests
{
    private static AppState WithAllowances(params AllowanceSummary[] allowances) =>
        new()
        {
            Runners = new RunnerList
            {
                Runners =
                [
                    new()
                    {
                        RunnerId = "11111111-1111-1111-1111-111111111111",
                        Label = "a-laptop",
                        State = RunnerStates.Idle,
                        RegisteredBy = "somebody",
                    },
                ],
            },
            Allowances = new AllowanceList { Allowances = [.. allowances] },
        };

    private static AllowanceSummary Summary(
        long tokens, long? limit, string runner = "11111111-1111-1111-1111-111111111111") => new()
        {
            Name = "kdee-max",
            MeasuredAt = DateTimeOffset.UnixEpoch,
            Runners = [runner],
            Windows =
            [
                new()
                {
                    Kind = AllowanceWindows.Session,
                    Since = DateTimeOffset.UnixEpoch,
                    InputTokens = 0,
                    OutputTokens = tokens,
                    CacheReadTokens = 9_999_999,
                    CacheWriteTokens = 0,
                    Limit = limit,
                },
            ],
        };

    [Test]
    public async Task A_runner_says_how_much_of_its_allowance_is_gone()
    {
        var pane = PaneText.Runners(WithAllowances(Summary(tokens: 22_000, limit: 88_000)));

        await Assert.That(pane).Contains("kdee-max", StringComparison.Ordinal)
            .Because("two machines can share one allowance, so the name is what tells "
                   + "somebody whether they are looking at one pool or two.");

        await Assert.That(pane).Contains("25%", StringComparison.Ordinal)
            .Because("22,000 of 88,000 - and the cache reads are not in it, which is why "
                   + "the fixture carries ten million of them.");
    }

    [Test]
    public async Task A_runner_nobody_gave_a_ceiling_says_so_rather_than_showing_a_share()
    {
        var pane = PaneText.Runners(WithAllowances(Summary(tokens: 22_000, limit: null)));

        await Assert.That(pane).DoesNotContain("%", StringComparison.Ordinal)
            .Because("0% reads as plenty left, which is the one wrong answer that looks "
                   + "like a right one.");
    }

    [Test]
    public async Task A_runner_no_allowance_mentions_says_nothing_about_one()
    {
        var pane = PaneText.Runners(WithAllowances(
            Summary(tokens: 1, limit: 2, runner: "22222222-2222-2222-2222-222222222222")));

        await Assert.That(pane).DoesNotContain("kdee-max", StringComparison.Ordinal)
            .Because("an allowance belongs to the machines that reported it. Showing it "
                   + "beside a machine that did not is how a person comes to believe "
                   + "their laptop is spending somebody else's subscription.");
    }

    [Test]
    public async Task A_console_that_has_read_no_allowances_still_renders_the_pane()
    {
        var bare = new AppState
        {
            Runners = new RunnerList
            {
                Runners =
                [
                    new()
                    {
                        RunnerId = "11111111-1111-1111-1111-111111111111",
                        Label = "a-laptop",
                        State = RunnerStates.Idle,
                        RegisteredBy = "somebody",
                    },
                ],
            },
        };

        await Assert.That(PaneText.Runners(bare)).Contains("a-laptop", StringComparison.Ordinal)
            .Because("a control plane one version behind serves no allowances at all, and "
                   + "the pane a person opens to see their fleet must not go blank over "
                   + "a column that is extra.");
    }
}
