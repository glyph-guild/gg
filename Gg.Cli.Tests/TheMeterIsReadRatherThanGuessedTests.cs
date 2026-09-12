using System.Text.Json;
using Gg.Local;

// THE NEIGHBOUR'S FIXTURES, named rather than copied. A duplicated test
// opener is what one pty flake turned out to be, and these two are the same
// shape of thing: a temp tree and a record written into it.
using Transcripts = Gg.Cli.Tests.AnAllowanceIsMeasuredFromTranscriptsTests.Transcripts;

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
    public async Task The_ledger_carries_the_meters_share_beside_its_own_count()
    {
        using var meter = new Meter(Cached());
        using var transcripts = new Transcripts();

        transcripts.Write("a-project", AnAllowanceIsMeasuredFromTranscriptsTests.Spent(Now.AddMinutes(-10), output: 169));

        var read = AllowanceLedger.Read(
            "kdee-max", transcripts.Root, AllowanceLimits.None, Now, meter.Path);

        var session = read.Windows.Single(w => w.Kind == AllowanceLedger.Session);

        await Assert.That(session.Reported).IsEqualTo(0.09)
            .Because("this is the number a person asked for, and it arrives without "
                   + "anybody typing a ceiling.");

        await Assert.That(session.Tokens).IsGreaterThan(0)
            .Because("the count is still this ledger's own, over its own rolling window.");

        await Assert.That(session.Fraction).IsNull()
            .Because("Fraction is the share derived from a TYPED ceiling and there is no "
                   + "ceiling here. Two provenances, and neither may impersonate the "
                   + "other - a reader has to be able to tell a measured share from a "
                   + "divided one.");
    }

    [Test]
    public async Task A_machine_with_no_meter_reads_exactly_as_it_did_before()
    {
        using var meter = new Meter(body: null);
        using var transcripts = new Transcripts();

        transcripts.Write("a-project", AnAllowanceIsMeasuredFromTranscriptsTests.Spent(Now.AddMinutes(-10), output: 169));

        var read = AllowanceLedger.Read(
            "kdee-max", transcripts.Root,
            AllowanceLimits.Read("session=1000,week=9000"), Now, meter.Path);

        var session = read.Windows.Single(w => w.Kind == AllowanceLedger.Session);

        await Assert.That(session.Reported).IsNull();
        await Assert.That(session.Fraction).IsNotNull()
            .Because("the typed ceiling still answers where the meter cannot, which is "
                   + "every machine that is not running this executor.");
    }

    [Test]
    public async Task The_pane_shows_the_meters_share_and_says_it_is_the_meters()
    {
        var text = Gg.Client.VerbOutput.ToText(
            new Gg.Client.VerbResult.Allowance(new Gg.Contracts.AllowanceReading
            {
                Allowance = "kdee-max",
                MeasuredAt = Now,
                Windows =
                [
                    new()
                    {
                        Kind = Gg.Contracts.AllowanceWindows.Session,
                        Since = Now.AddHours(-5),
                        InputTokens = 124,
                        OutputTokens = 21_142,
                        CacheReadTokens = 1_349_033,
                        CacheWriteTokens = 131_473,
                        Reported = 0.09,
                        ResetsAt = new DateTimeOffset(2026, 9, 12, 5, 10, 0, TimeSpan.Zero),
                    },
                ],
            }));

        await Assert.That(text).Contains("9%", StringComparison.Ordinal)
            .Because("this is the number somebody asked for, and no ceiling was typed to "
                   + "get it.");

        await Assert.That(text).DoesNotContain("no ceiling", StringComparison.Ordinal)
            .Because("that sentence asks a person to configure a denominator, and the "
                   + "whole point is that one is no longer needed here.");

        await Assert.That(text).Contains("resets", StringComparison.OrdinalIgnoreCase)
            .Because("the share describes the METER's window, which ends at an instant "
                   + "and is not the rolling one the tokens beside it were counted over. "
                   + "A share with no reset invites exactly that conflation.");
    }

    [Test]
    public async Task Where_both_exist_the_meter_wins_because_the_other_is_a_guess()
    {
        var text = Gg.Client.VerbOutput.ToText(
            new Gg.Client.VerbResult.Allowance(new Gg.Contracts.AllowanceReading
            {
                Allowance = "kdee-max",
                MeasuredAt = Now,
                Windows =
                [
                    new()
                    {
                        Kind = Gg.Contracts.AllowanceWindows.Week,
                        Since = Now.AddDays(-7),
                        InputTokens = 0,
                        OutputTokens = 600_000,
                        CacheReadTokens = 0,
                        CacheWriteTokens = 0,

                        // A typed ceiling that would say 25%, and a meter that
                        // says 40%. They disagree because they measure
                        // different spans, and the provider's is the one that
                        // decides whether work stops.
                        Limit = 2_400_000,
                        Reported = 0.40,
                    },
                ],
            }));

        await Assert.That(text).Contains("40%", StringComparison.Ordinal);
        await Assert.That(text).DoesNotContain("25%", StringComparison.Ordinal)
            .Because("a typed ceiling is somebody's guess at a number the provider knows. "
                   + "Showing both would ask a person to arbitrate between them.");
    }

    [Test]
    public async Task A_share_whose_window_already_reset_is_not_offered_as_the_current_one()
    {
        // THE REAL FILE ON THIS MACHINE, on the night this was written: the
        // meter was fetched at 20:55Z and its five-hour window reset at
        // 00:10Z, so by 04:08Z the pane was printing "9% spent" about a window
        // that had ended four hours earlier. The number was not wrong; the
        // sentence around it was.
        var text = Gg.Client.VerbOutput.ToText(
            new Gg.Client.VerbResult.Allowance(new Gg.Contracts.AllowanceReading
            {
                Allowance = "kdee-max",
                MeasuredAt = Now,
                Windows =
                [
                    new()
                    {
                        Kind = Gg.Contracts.AllowanceWindows.Session,
                        Since = Now.AddHours(-5),
                        InputTokens = 0,
                        OutputTokens = 21_142,
                        CacheReadTokens = 0,
                        CacheWriteTokens = 131_473,
                        Reported = 0.09,
                        ResetsAt = Now.AddHours(-4),
                        ReportedAt = Now.AddHours(-7),
                    },
                ],
            }));

        await Assert.That(text).DoesNotContain("9% spent", StringComparison.Ordinal)
            .Because("the share is about a window that has ended. What the CURRENT window "
                   + "has spent is unknown, and a percentage stated plainly is a claim "
                   + "about it.");

        await Assert.That(text).Contains("reset", StringComparison.OrdinalIgnoreCase)
            .Because("saying nothing would leave a person wondering why a machine with a "
                   + "meter shows no share. The reason is actionable - the executor has "
                   + "not refreshed - and it is one sentence.");
    }

    [Test]
    public async Task A_live_share_says_how_old_it_is()
    {
        var text = Gg.Client.VerbOutput.ToText(
            new Gg.Client.VerbResult.Allowance(new Gg.Contracts.AllowanceReading
            {
                Allowance = "kdee-max",
                MeasuredAt = Now,
                Windows =
                [
                    new()
                    {
                        Kind = Gg.Contracts.AllowanceWindows.Week,
                        Since = Now.AddDays(-7),
                        InputTokens = 0,
                        OutputTokens = 600_000,
                        CacheReadTokens = 0,
                        CacheWriteTokens = 0,
                        Reported = 0.40,

                        // Still open - a calendar anchor three days out - so
                        // the share IS about the current window. It is also
                        // seven hours old, which the rollover rule above
                        // cannot catch and a reader still needs.
                        ResetsAt = Now.AddDays(3),
                        ReportedAt = Now.AddHours(-7),
                    },
                ],
            }));

        await Assert.That(text).Contains("40%", StringComparison.Ordinal);
        await Assert.That(text).Contains("as of", StringComparison.OrdinalIgnoreCase)
            .Because("a share is only as current as the last time the executor asked, and "
                   + "an hours-old number presented bare cannot be told from a live one.");
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
