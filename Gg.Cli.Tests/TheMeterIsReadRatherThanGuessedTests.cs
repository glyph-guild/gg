using System.Text.Json;
using Gg.Local;

namespace Gg.Cli.Tests;

/// <summary>
/// The share of a plan that is spent, taken from the meter that keeps it.
/// </summary>
/// <remarks>
/// <para>
/// <b>Because a ceiling cannot be discovered and was being invented.</b> A
/// percentage needs a denominator, nothing on a machine reports a plan's token
/// ceiling, so <c>allowance-limits</c> asked a person to type one. The obvious
/// way to get that number — measure the tokens, read a percentage out of the
/// executor, divide — is wrong, and the file proves it: the executor's cache
/// holds a <b>fixed window with a reset instant</b> while this ledger sums a
/// <b>rolling</b> one, so the division puts two different spans over each
/// other.
/// </para>
/// <para>
/// <b>So the share is not derived at all. It is read.</b> The executor caches
/// what the provider's own meter said — <c>five_hour</c> and <c>seven_day</c>
/// utilisation, each with the instant its window resets. That is the
/// authoritative number, it needs no denominator, and it is right even when
/// nobody ever configured a ceiling.
/// </para>
/// <para>
/// <b>Two facts, and neither is allowed to impersonate the other.</b> The
/// tokens are this ledger's measurement over its own rolling window; the share
/// is the meter's statement about the meter's window. Both cross, each says
/// which span it describes, and <see cref="MeasuredWindow.Fraction"/> — the
/// share derived from a typed ceiling — stays exactly what it was.
/// </para>
/// <para>
/// <b>A guest again, under the same rule as the transcripts.</b> That file is
/// another tool's and it carries an account uuid. The reader takes two numbers
/// and an instant per window and retains nothing else, so a reading can cross
/// to a control plane that must never learn whose account this is.
/// </para>
/// </remarks>
public class TheMeterIsReadRatherThanGuessedTests
{
    private const string AccountUuid = "00bb6968-542e-4726-8197-8138d5bf73d5";

    private static readonly DateTimeOffset Now =
        new(2026, 9, 12, 3, 58, 0, TimeSpan.Zero);

    /// <summary>The executor's cache, in the shape it really has on disk.</summary>
    private sealed class Meter : IDisposable
    {
        public Meter(string? body)
        {
            Root = Directory.CreateTempSubdirectory("gg-meter").FullName;
            Path = System.IO.Path.Combine(Root, ".claude.json");

            if (body is not null)
            {
                File.WriteAllText(Path, body);
            }
        }

        public string Root { get; }

        public string Path { get; }

        public void Dispose()
        {
            try { Directory.Delete(Root, recursive: true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    private static string Cached(
        int fiveHour = 9, int sevenDay = 40, string fetchedAtMs = "1789160129633") => $$"""
        {
          "hasCompletedOnboarding": true,
          "cachedUsageUtilization": {
            "fetchedAtMs": {{fetchedAtMs}},
            "accountUuid": "{{AccountUuid}}",
            "utilization": {
              "five_hour": {
                "utilization": {{fiveHour}},
                "resets_at": "2026-09-12T05:10:00.553211+00:00",
                "limit_dollars": null
              },
              "seven_day": {
                "utilization": {{sevenDay}},
                "resets_at": "2026-09-15T00:00:00.553230+00:00",
                "limit_dollars": null
              },
              "seven_day_opus": null,
              "extra_usage": { "is_enabled": true, "monthly_limit": 10000 }
            }
          }
        }
        """;

    [Test]
    public async Task A_share_is_read_from_the_meter_with_no_ceiling_configured()
    {
        using var meter = new Meter(Cached());

        var read = AllowanceMeter.Read(meter.Path);

        await Assert.That(read.Share(AllowanceLedger.Session)).IsEqualTo(0.09)
            .Because("the meter says nine percent of the five-hour window, and a share "
                   + "read from the meter needs no ceiling at all - which is the whole "
                   + "point, because a ceiling cannot be discovered.");

        await Assert.That(read.Share(AllowanceLedger.Week)).IsEqualTo(0.40)
            .Because("seven_day is this ledger's week.");
    }

    [Test]
    public async Task It_says_when_the_meters_window_resets_because_it_is_not_this_ones()
    {
        using var meter = new Meter(Cached());

        var read = AllowanceMeter.Read(meter.Path);

        await Assert.That(read.ResetsAt(AllowanceLedger.Session))
            .IsEqualTo(new DateTimeOffset(2026, 9, 12, 5, 10, 0, TimeSpan.Zero)
                .AddMilliseconds(553.211))
            .Because("the meter's window is FIXED and ends at an instant; this ledger's is "
                   + "rolling. Carrying the reset is what stops a reader taking the share "
                   + "as a statement about the rolling window beside it.");
    }

    [Test]
    public async Task It_holds_no_account_identity()
    {
        using var meter = new Meter(Cached());

        var read = AllowanceMeter.Read(meter.Path);

        var everything = JsonSerializer.Serialize(read);

        await Assert.That(everything).DoesNotContain(AccountUuid, StringComparison.OrdinalIgnoreCase)
            .Because("an allowance is a name somebody chose and never an identity. That "
                   + "file names the account the plan belongs to, and a reading crosses to "
                   + "a control plane that must never learn it.");

        await Assert.That(everything).DoesNotContain("extra_usage", StringComparison.OrdinalIgnoreCase)
            .Because("two numbers and an instant per window, and nothing else - the same "
                   + "rule the transcripts are read under.");
    }

    [Test]
    public async Task An_absent_meter_reports_nothing_rather_than_zero()
    {
        using var meter = new Meter(body: null);

        var read = AllowanceMeter.Read(meter.Path);

        await Assert.That(read.Share(AllowanceLedger.Session)).IsNull()
            .Because("nought percent reads as a plan nobody has touched, and 'the executor "
                   + "never cached one' is a different fact. The same rule Limit already "
                   + "follows.");
        await Assert.That(read.FetchedAt).IsNull();
    }

    [Test]
    public async Task A_meter_that_cannot_be_read_is_stepped_over_in_silence()
    {
        using var meter = new Meter("{ this is not json");

        var read = AllowanceMeter.Read(meter.Path);

        await Assert.That(read.Share(AllowanceLedger.Week)).IsNull()
            .Because("another tool owns that file and may change its shape without telling "
                   + "anybody. A reading that threw would take the token counts down with "
                   + "it, and those are right regardless.");
    }

    [Test]
    public async Task It_says_when_the_meter_was_fetched_so_staleness_is_visible()
    {
        using var meter = new Meter(Cached());

        var read = AllowanceMeter.Read(meter.Path);

        await Assert.That(read.FetchedAt)
            .IsEqualTo(DateTimeOffset.FromUnixTimeMilliseconds(1789160129633))
            .Because("the real one on this machine was seven hours old and described a "
                   + "window that had already reset. A share with no age cannot be told "
                   + "from a current one.");

        await Assert.That(read.FetchedAt < Now).IsTrue();
    }
}
