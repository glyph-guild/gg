using Gg.Local;

namespace Gg.Cli.Tests;

/// <summary>
/// Asking the executor to refresh the meter, rather than reporting a stale one.
/// </summary>
/// <remarks>
/// <para>
/// <b>Measured, not assumed: <c>claude -p "/usage"</c> refreshes the cache.</b>
/// On this machine it moved <c>fetchedAtMs</c> from 20:55Z to 07:50Z and the
/// weekly share from 40% to 48% — the number a person could see in their own
/// session while gg reported eleven hours behind it. On the agent host, where
/// <c>cachedUsageUtilization</c> was ABSENT entirely, the same command created
/// it.
/// </para>
/// <para>
/// <b>No credential, which is the whole reason this is allowed.</b> gg asks
/// the tool that already holds the subscription's credential and reads the
/// file it writes. Reading another tool's transcripts and cache is the guest
/// rule this ledger already follows; reading its <c>.credentials.json</c> and
/// calling an API with it would be a different thing entirely, and is what
/// every other way of getting this number would have required.
/// </para>
/// <para>
/// <b>ONCE PER ACCOUNT, not once per machine.</b> The share is a property of
/// the PLAN, so one fresh reading answers for every machine spending from it.
/// This type decides only whether a refresh is warranted here; which machine
/// does it for an allowance several of them share is a claim, and a claim is
/// the control plane's to hold.
/// </para>
/// <para>
/// <b>And on demand rather than on a timer.</b> A refresh spawns a process; a
/// read is a file. So the question is asked of the data — is this reading
/// behind its own window? — and the spawn only happens when the answer it
/// would otherwise give is wrong.
/// </para>
/// </remarks>
public class TheMeterIsRefreshedWhenItIsBehindTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 12, 7, 0, 0, TimeSpan.Zero);

    /// <summary>A meter whose five-hour window reset after it was read.</summary>
    private static MeteredShare Behind() => new()
    {
        Shares = new Dictionary<string, double>(StringComparer.Ordinal)
        {
            [AllowanceLedger.Session] = 0.09,
            [AllowanceLedger.Week] = 0.40,
        },
        Resets = new Dictionary<string, DateTimeOffset>(StringComparer.Ordinal)
        {
            [AllowanceLedger.Session] = Now.AddHours(-7),
            [AllowanceLedger.Week] = Now.AddDays(3),
        },
        FetchedAt = Now.AddHours(-10),
    };

    /// <summary>The same meter, read after the window it describes.</summary>
    private static MeteredShare Current() => new()
    {
        Shares = new Dictionary<string, double>(StringComparer.Ordinal)
        {
            [AllowanceLedger.Session] = 0.17,
            [AllowanceLedger.Week] = 0.48,
        },
        Resets = new Dictionary<string, DateTimeOffset>(StringComparer.Ordinal)
        {
            [AllowanceLedger.Session] = Now.AddHours(3),
            [AllowanceLedger.Week] = Now.AddDays(3),
        },
        FetchedAt = Now.AddMinutes(-2),
    };

    [Test]
    public async Task A_reading_taken_before_its_own_window_reset_is_behind()
    {
        await Assert.That(MeterRefresh.IsBehind(Behind(), Now)).IsTrue()
            .Because("a whole session's spending happened after that reading and none of "
                   + "it is in the number - so the WEEKLY share is behind too, by an "
                   + "amount nothing on this machine can estimate.");
    }

    [Test]
    public async Task A_reading_taken_inside_every_window_it_describes_is_not()
    {
        await Assert.That(MeterRefresh.IsBehind(Current(), Now)).IsFalse()
            .Because("a refresh spawns a process and a read is a file. Asking again when "
                   + "the answer is current is a process per beat for nothing.");
    }

    [Test]
    public async Task A_machine_with_no_meter_at_all_is_behind()
    {
        await Assert.That(MeterRefresh.IsBehind(MeteredShare.None, Now)).IsTrue()
            .Because("the agent host had no cachedUsageUtilization at all, because nothing "
                   + "had ever asked it for one. Absent is not `nothing to refresh' - it is "
                   + "the case a refresh exists for.");
    }

    [Test]
    public async Task It_asks_only_when_the_answer_would_otherwise_be_wrong()
    {
        var asked = 0;

        await MeterRefresh.EnsureCurrentAsync(
            Current(), Now, _ => { asked++; return Task.FromResult(true); });

        await Assert.That(asked).IsEqualTo(0);

        await MeterRefresh.EnsureCurrentAsync(
            Behind(), Now, _ => { asked++; return Task.FromResult(true); });

        await Assert.That(asked).IsEqualTo(1);
    }

    [Test]
    public async Task An_executor_that_cannot_answer_is_stepped_over_in_silence()
    {
        var read = await MeterRefresh.EnsureCurrentAsync(
            Behind(), Now, _ => throw new InvalidOperationException("no claude here"));

        await Assert.That(read).IsNotNull()
            .Because("a machine with no executor, or one nobody has signed in, still has "
                   + "token counts that are right - and the guest rule is that another "
                   + "tool's absence is never this one's failure.");
    }
}
