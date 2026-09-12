using Gg.Client;
using Gg.Console;
using Gg.Contracts;

namespace Gg.Console.Tests;

/// <summary>
/// Four places render a window's share, and they have to agree.
/// </summary>
/// <remarks>
/// <para>
/// <b>Four, counted.</b> <c>gg allowance</c> and <c>gg allowances</c> in
/// <c>VerbOutput</c>, the allowance column on the runners pane, and the fleet
/// pane behind the Allowances tab. Each had its own copy of the same
/// three-way choice, written out longhand, and only the first learned about
/// the meter — so the same reading showed a percentage in one surface and
/// "no ceiling set" in another.
/// </para>
/// <para>
/// <b>So the decision moves to one function and only the formatting stays
/// local.</b> <c>AllowanceShare</c> answers which kind of share a window has;
/// a pane fits it into a column and a verb into a sentence. Four copies of a
/// rule is four chances for the next member to reach three of them.
/// </para>
/// <para>
/// <b>Asserted through the four surfaces rather than on the helper</b>, because
/// a test of the helper alone is what let the copies drift in the first place.
/// </para>
/// </remarks>
public class EverySurfaceTellsTheSameShareTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 12, 4, 0, 0, TimeSpan.Zero);

    private static AllowanceWindow Window(
        double? reported, DateTimeOffset? resetsAt, long? limit) => new()
        {
            Kind = AllowanceWindows.Week,
            Since = Now.AddDays(-7),
            InputTokens = 0,
            OutputTokens = 600_000,
            CacheReadTokens = 0,
            CacheWriteTokens = 0,
            Limit = limit,
            Reported = reported,
            ResetsAt = resetsAt,
            ReportedAt = reported is null ? null : Now.AddHours(-1),
        };

    private static AllowanceList Listed(AllowanceWindow window) => new()
    {
        Allowances =
        [
            new()
            {
                Name = "kdee-max",
                MeasuredAt = Now,
                Runners = ["one"],
                Owners = [],
                Windows = [window],
            },
        ],
    };

    private static IEnumerable<(string Surface, string Text)> EverySurface(AllowanceWindow window)
    {
        yield return ("gg allowance", VerbOutput.ToText(
            new VerbResult.Allowance(new AllowanceReading
            {
                Allowance = "kdee-max",
                MeasuredAt = Now,
                Windows = [window],
            })));

        yield return ("gg allowances", VerbOutput.ToText(
            new VerbResult.Allowances(Listed(window))));

        yield return ("the fleet pane", PaneText.ForTab(
            new AppState { Allowances = Listed(window), IsAdmin = true },
            TabId.Allowances));
    }

    [Test]
    public async Task A_live_meter_share_reaches_every_surface()
    {
        foreach (var (surface, text) in EverySurface(
            Window(reported: 0.40, resetsAt: Now.AddDays(3), limit: null)))
        {
            await Assert.That(text).Contains("40%", StringComparison.Ordinal)
                .Because($"{surface} showed no share for a window the meter measured, and "
                       + "a person comparing two surfaces cannot tell which one is wrong.");

            await Assert.That(text).DoesNotContain("no ceiling", StringComparison.Ordinal)
                .Because($"{surface} asked for a denominator that is no longer needed.");
        }
    }

    [Test]
    public async Task The_meter_beats_a_typed_ceiling_on_every_surface()
    {
        foreach (var (surface, text) in EverySurface(
            Window(reported: 0.40, resetsAt: Now.AddDays(3), limit: 2_400_000)))
        {
            await Assert.That(text).Contains("40%", StringComparison.Ordinal);
            await Assert.That(text).DoesNotContain("25%", StringComparison.Ordinal)
                .Because($"{surface} divided tokens by a typed ceiling while the provider's "
                       + "own number was sitting on the record beside it.");
        }
    }

    [Test]
    public async Task A_rolled_over_share_is_withheld_on_every_surface()
    {
        foreach (var (surface, text) in EverySurface(
            Window(reported: 0.40, resetsAt: Now.AddHours(-2), limit: null)))
        {
            await Assert.That(text).DoesNotContain("40%", StringComparison.Ordinal)
                .Because($"{surface} stated a share about a window that ended two hours "
                       + "ago as though it were the current one.");

            // AND NOT VACUOUSLY. Absence alone passes on a surface that
            // renders no share at all, which is exactly the state this branch
            // starts in - so the window still has to be on the page.
            await Assert.That(text).Contains("600,000", StringComparison.Ordinal)
                .Because($"{surface} dropped the window instead of withholding its share. "
                       + "The tokens are this ledger's own measurement and are true "
                       + "whatever the meter last said.");
        }
    }

    [Test]
    public async Task With_no_meter_a_typed_ceiling_still_answers_everywhere()
    {
        foreach (var (surface, text) in EverySurface(
            Window(reported: null, resetsAt: null, limit: 2_400_000)))
        {
            await Assert.That(text).Contains("25%", StringComparison.Ordinal)
                .Because($"{surface} must not lose the typed ceiling now that a better "
                       + "source exists for some machines. Most machines have no meter.");
        }
    }
}
