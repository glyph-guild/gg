using Gg.Contracts;
using Gg.Local;

namespace Gg.Cli.Tests;

/// <summary>
/// Why a window carries four counts and a total that is not their sum.
/// </summary>
/// <remarks>
/// <para>
/// <b>Measured on a real machine, and it is not close.</b> Over one week of
/// this developer's transcripts — 37,209 assistant messages — the four counts
/// came out as: cache reads 17,660,352,647 (<b>99.0%</b>), cache writes
/// 144,720,335 (0.8%), output 30,999,990 (0.2%), input 74,396 (0.0%). An
/// unweighted sum is therefore a measurement of one thing: how much cached
/// context got re-read. That is the cheapest component a provider bills, and
/// summing it with the rest reported 18 billion tokens against a ceiling of
/// two million — 757,457%.
/// </para>
/// <para>
/// <b>So the total excludes cache reads, and that is a stated approximation
/// rather than a claim.</b> A provider's weighting is not published. What is
/// public is that a cache read is the cheap component and a cache write the
/// expensive one, so input, output and cache writes is the closest honest
/// yardstick — and the cache reads still cross beside it, so anybody who
/// learns better can recompute without a new contract.
/// </para>
/// <para>
/// <b>One derivation, however many readers</b> — <c>AttemptLedger</c>'s rule,
/// and the reason a total exists here at all. A window carrying only
/// components would let a console and a scheduler compute the number
/// differently and disagree about who gets work. One stated approximation
/// beats two private ones.
/// </para>
/// </remarks>
public class AWindowSaysWhatItCountedTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 11, 12, 0, 0, TimeSpan.Zero);

    [Test]
    public async Task The_total_leaves_out_the_component_that_is_ninety_nine_percent_of_it()
    {
        using var transcripts = new TranscriptFolder();
        transcripts.Write(Spent(Now.AddMinutes(-1),
            input: 20, output: 1, cacheRead: 300_000, cacheWrite: 4000));

        var window = AllowanceLedger
            .Read("mine", transcripts.Root, AllowanceLimits.None, Now)
            .Windows.Single(w => w.Kind == AllowanceLedger.Session);

        await Assert.That(window.Tokens).IsEqualTo(4021L)
            .Because("input, output and cache writes. Including the 300,000 cache reads "
                   + "would make this a measurement of context re-reading - on a real "
                   + "week it was 99.0% of the raw sum and reported 757,457% of a plan's "
                   + "ceiling.");
    }

    [Test]
    public async Task Every_count_still_crosses_so_the_approximation_can_be_checked()
    {
        using var transcripts = new TranscriptFolder();
        transcripts.Write(Spent(Now.AddMinutes(-1),
            input: 20, output: 1, cacheRead: 300_000, cacheWrite: 4000));

        var window = AllowanceLedger
            .Read("mine", transcripts.Root, AllowanceLimits.None, Now)
            .Windows.Single(w => w.Kind == AllowanceLedger.Session);

        await Assert.That(window.InputTokens).IsEqualTo(20L);
        await Assert.That(window.OutputTokens).IsEqualTo(1L);
        await Assert.That(window.CacheWriteTokens).IsEqualTo(4000L);
        await Assert.That(window.CacheReadTokens).IsEqualTo(300_000L)
            .Because("the EXCLUDED count is the one that most needs to be visible. A "
                   + "weighting nobody can see is one nobody can correct, and the "
                   + "provider's real one is not published.");
    }

    [Test]
    public async Task The_wire_window_says_the_same_four_things()
    {
        var window = new AllowanceWindow
        {
            Kind = AllowanceWindows.Session,
            Since = DateTimeOffset.UnixEpoch,
            InputTokens = 20,
            OutputTokens = 1,
            CacheReadTokens = 300_000,
            CacheWriteTokens = 4000,
        };

        await Assert.That(window.Tokens).IsEqualTo(4021L)
            .Because("the control plane must not re-derive this. Two readers computing "
                   + "one number differently is how a scheduler and a console come to "
                   + "disagree about who has headroom.");
    }

    private static string Spent(
        DateTimeOffset at, long input, long output, long cacheRead, long cacheWrite) =>
        System.Text.Json.JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["type"] = "assistant",
            ["timestamp"] = at.ToString("o", System.Globalization.CultureInfo.InvariantCulture),
            ["message"] = new Dictionary<string, object>
            {
                ["model"] = "claude-opus-5",
                ["usage"] = new Dictionary<string, object>
                {
                    ["input_tokens"] = input,
                    ["output_tokens"] = output,
                    ["cache_read_input_tokens"] = cacheRead,
                    ["cache_creation_input_tokens"] = cacheWrite,
                },
            },
        });

    private sealed class TranscriptFolder : IDisposable
    {
        public TranscriptFolder() =>
            Root = Directory.CreateTempSubdirectory("gg-allowance-counts").FullName;

        public string Root { get; }

        public void Write(params string[] lines)
        {
            var directory = Path.Combine(Root, "a-project");
            Directory.CreateDirectory(directory);
            File.WriteAllLines(Path.Combine(directory, $"{Guid.NewGuid():N}.jsonl"), lines);
        }

        public void Dispose()
        {
            try { Directory.Delete(Root, recursive: true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
