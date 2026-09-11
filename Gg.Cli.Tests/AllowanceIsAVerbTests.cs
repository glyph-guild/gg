using Gg.Cli;
using Gg.Client;
using Gg.Contracts;

namespace Gg.Cli.Tests;

/// <summary>
/// Reading what this machine has spent, from this machine.
/// </summary>
/// <remarks>
/// <para>
/// <b>A verb, because otherwise the slice measures something nobody can
/// read.</b> The reading crosses to a control plane and a fleet view comes
/// later; until then the only reader would be the control plane's own storage,
/// which is the assembled-and-discarded shape this codebase has met before. A
/// person on the machine doing the spending is the first person entitled to
/// the number.
/// </para>
/// <para>
/// <b>Local, like <c>gg config show</c> and unlike <c>gg flights</c>.</b> It
/// contacts nothing: the transcripts are on this disk and the ceilings are in
/// this file, so it answers on a plane.
/// </para>
/// <para>
/// <b>No ceiling is said in words, never as a percentage.</b> Nothing on a
/// machine records a subscription's limits, and a window rendered as 0% would
/// read as plenty left.
/// </para>
/// </remarks>
public class AllowanceIsAVerbTests
{
    [Test]
    public async Task It_parses()
    {
        await Assert.That(CliArgs.Parse(["allowance"])).IsTypeOf<CliAction.Allowance>();

        var json = CliArgs.Parse(["allowance", "--json"]);
        await Assert.That(json).IsTypeOf<CliAction.Allowance>();
        await Assert.That(((CliAction.Allowance)json).Json).IsTrue();
    }

    [Test]
    public async Task It_is_on_the_usage_page()
    {
        // THROUGH THE REFUSAL, which is where the usage actually reaches a
        // person: the list is private and Unknown is what prints it.
        var refused = CliArgs.Parse(["definitely-not-a-verb"]);

        await Assert.That(refused).IsTypeOf<CliAction.Unknown>();
        await Assert.That(((CliAction.Unknown)refused).Message)
            .Contains("gg allowance", StringComparison.Ordinal)
            .Because("a verb nobody can find is one nobody runs, and this one is how a "
                   + "person checks the number their fleet is about to act on.");
    }

    [Test]
    public async Task It_renders_what_was_spent_against_what_was_configured()
    {
        var text = VerbOutput.ToText(new VerbResult.Allowance(new AllowanceReading
        {
            Allowance = "kdee-max",
            MeasuredAt = DateTimeOffset.UnixEpoch,
            Windows =
            [
                new()
                {
                    Kind = AllowanceWindows.Session,
                    Since = DateTimeOffset.UnixEpoch,
                    InputTokens = 400,
                    OutputTokens = 12000,
                    CacheReadTokens = 5_000_000,
                    CacheWriteTokens = 0,
                    Limit = 88000,
                },
            ],
        }));

        await Assert.That(text).Contains("kdee-max", StringComparison.Ordinal);
        await Assert.That(text).Contains("session", StringComparison.Ordinal);
        await Assert.That(text).Contains("14%", StringComparison.Ordinal)
            .Because("12,400 of 88,000. The percentage is the thing a person came for.");
    }

    [Test]
    public async Task A_window_with_no_ceiling_says_so_rather_than_showing_a_percentage()
    {
        var text = VerbOutput.ToText(new VerbResult.Allowance(new AllowanceReading
        {
            Allowance = "kdee-max",
            MeasuredAt = DateTimeOffset.UnixEpoch,
            Windows =
            [
                new()
                {
                    Kind = AllowanceWindows.Week, Since = DateTimeOffset.UnixEpoch,
                    InputTokens = 42, OutputTokens = 4200,
                    CacheReadTokens = 90_000, CacheWriteTokens = 0,
                },
            ],
        }));

        await Assert.That(text).Contains("4,242", StringComparison.Ordinal)
            .Because("what was spent is known whether or not anybody said the ceiling.");
        await Assert.That(text).DoesNotContain("%", StringComparison.Ordinal)
            .Because("0% reads as plenty left, which is the one wrong answer that looks "
                   + "like a right one.");
        await Assert.That(text).Contains("allowance-limits", StringComparison.Ordinal)
            .Because("it names the setting that would fix it, where somebody reading the "
                   + "gap is standing.");
    }
}
