using Gg.Contracts;

namespace Gg.Contracts.Tests;

/// <summary>
/// How spent a window is, decided once for both repositories.
/// </summary>
/// <remarks>
/// <para>
/// <b>Two repositories answer this question and they cannot reference each
/// other.</b> gg renders it on four surfaces; the control plane decides
/// whether a floor has been reached and which machine is least spent. The
/// only thing both hold is this package, so the rule belongs here — anywhere
/// else and it is written twice, which is exactly how the four renderers in
/// gg came to disagree.
/// </para>
/// <para>
/// <b>The preference is the rule.</b> The provider's own share wins where it
/// is about the window in force; a ceiling somebody typed answers where it is
/// not; and neither means neither, never nought.
/// </para>
/// <para>
/// <b>A rolled-over share must not gate work, and that is the case with
/// teeth.</b> The meter keeps fixed windows. If one has reset and the executor
/// has not asked since, its share describes a finished window — and a fleet
/// deciding whether somebody's subscription is spent would be reading last
/// window's number in both directions: halting on a plan that has just
/// refilled, or lending from one that is exhausted.
/// </para>
/// </remarks>
public class OneRuleSaysHowSpentAWindowIsTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 12, 4, 0, 0, TimeSpan.Zero);

    [Test]
    public async Task The_providers_share_wins_over_a_typed_ceiling()
    {
        // 600,000 of 2,400,000 is a quarter; the meter says forty percent.
        // They disagree because they measure different spans.
        var spent = AllowanceShares.Spent(
            reported: 0.40, resetsAt: Now.AddDays(3),
            tokens: 600_000, limit: 2_400_000, asOf: Now);

        await Assert.That(spent).IsEqualTo(0.40)
            .Because("a typed ceiling is a guess at a number the provider knows, and both "
                   + "repositories have to prefer the same one or a pane and a refusal "
                   + "will describe different plans.");
    }

    [Test]
    public async Task A_rolled_over_share_does_not_decide_anything()
    {
        var spent = AllowanceShares.Spent(
            reported: 0.90, resetsAt: Now.AddHours(-2),
            tokens: 600_000, limit: 2_400_000, asOf: Now);

        await Assert.That(spent).IsEqualTo(0.25)
            .Because("the meter's window ended two hours ago, so 90% is last window's "
                   + "number. Gating on it would halt a plan that has just refilled - and "
                   + "the typed ceiling, whatever else is wrong with it, is at least "
                   + "about now.");
    }

    [Test]
    public async Task With_no_meter_the_typed_ceiling_still_answers()
    {
        var spent = AllowanceShares.Spent(
            reported: null, resetsAt: null,
            tokens: 600_000, limit: 2_400_000, asOf: Now);

        await Assert.That(spent).IsEqualTo(0.25)
            .Because("most machines run no executor that keeps a meter, and that must not "
                   + "become a reason work stops.");
    }

    [Test]
    public async Task With_neither_it_answers_nothing_rather_than_nought()
    {
        var spent = AllowanceShares.Spent(
            reported: null, resetsAt: null, tokens: 600_000, limit: null, asOf: Now);

        await Assert.That(spent).IsNull()
            .Because("nought reads as a plan nobody has touched, which is the one answer a "
                   + "fleet deciding whether to spend somebody's subscription must never "
                   + "be handed by accident. A caller that cannot act on null has to say "
                   + "so itself.");
    }

    [Test]
    public async Task A_share_with_no_stated_reset_is_still_the_providers()
    {
        var spent = AllowanceShares.Spent(
            reported: 0.40, resetsAt: null, tokens: 600_000, limit: 2_400_000, asOf: Now);

        await Assert.That(spent).IsEqualTo(0.40)
            .Because("a meter that reports a share and no window is still measuring the "
                   + "plan. Withholding it would prefer the guess on a technicality.");
    }

    [Test]
    public async Task The_window_overload_and_the_raw_one_are_the_same_rule()
    {
        // THE POINT OF THE PAIR. gg holds AllowanceWindow and the control
        // plane's engine holds its own shape with the same three values, so
        // the convenience overload must not become a second rule.
        var window = new AllowanceWindow
        {
            Kind = AllowanceWindows.Week,
            Since = Now.AddDays(-7),
            InputTokens = 0,
            OutputTokens = 600_000,
            CacheReadTokens = 0,
            CacheWriteTokens = 0,
            Limit = 2_400_000,
            Reported = 0.40,
            ResetsAt = Now.AddHours(-2),
        };

        await Assert.That(AllowanceShares.Spent(window, Now))
            .IsEqualTo(AllowanceShares.Spent(
                window.Reported, window.ResetsAt, window.Tokens, window.Limit, Now));
    }
}
