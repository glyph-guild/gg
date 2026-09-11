using System.Globalization;
using System.Reflection;
using System.Text.Json;
using Gg.Local;

namespace Gg.Cli.Tests;

/// <summary>
/// What this machine has spent, read from the executor's own transcripts.
/// </summary>
/// <remarks>
/// <para>
/// <b>The number has to come from somewhere, and there is only one place.</b>
/// Nothing on this machine records a percentage of a limit, so a window is
/// summed from the executor's transcripts and divided by a limit somebody
/// configured. That makes the ledger a reader of another tool's files — a
/// guest — and the two rules below both follow from being one.
/// </para>
/// <para>
/// <b>It holds no message content, and that is a boundary rather than a
/// tidiness.</b> Those transcripts are the customer's code, their prompts and
/// their agent's reasoning. The ledger extracts a timestamp, a model name and
/// four token counts, and retains nothing else — so a reading can cross to a
/// control plane that must never see any of it. A malformed line is stepped
/// over in silence for the same reason: a diagnostic quoting the line it could
/// not read would carry the content out in the one path nobody tests.
/// </para>
/// <para>
/// <b>A window is bounded by timestamps.</b> Transcript records carry an
/// <c>apiBlockIndex</c> that reads exactly like a session-window marker and is
/// not one — measured on this machine, blocks 0 to 3 all start within the same
/// two minutes of one session. Summing by it would report four windows where
/// there is one.
/// </para>
/// </remarks>
public class AnAllowanceIsMeasuredFromTranscriptsTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 11, 12, 0, 0, TimeSpan.Zero);

    [Test]
    public async Task It_holds_no_message_content()
    {
        const string needle = "CUSTOMER-SECRET-do-not-copy-7f3a";

        using var transcripts = new Transcripts();
        transcripts.Write(
            "a-project",
            Spent(Now.AddMinutes(-10), output: 169, text: needle),

            // A LINE THE LEDGER CANNOT READ, which is the path a diagnostic
            // would leak through. It is another tool's file and may hold
            // anything at all, including a half-written line.
            "{\"message\": {\"content\": \"" + needle + "\", ");

        var measured = AllowanceLedger.Read(
            "mine", transcripts.Root, AllowanceLimits.None, Now);

        await Assert.That(measured.Windows).IsNotEmpty()
            .Because("the well-formed record has to have been read, or this test "
                   + "passes by measuring nothing at all.");

        foreach (var held in Strings(measured))
        {
            await Assert.That(held).DoesNotContain(needle, StringComparison.Ordinal)
                .Because($"the reading crosses to a control plane that must never "
                       + $"see customer content, and this carries it: {held}");
        }
    }

    [Test]
    public async Task A_window_is_bounded_by_timestamps_and_not_by_the_block_index()
    {
        using var transcripts = new Transcripts();

        // ONE BLOCK INDEX, TWO WINDOWS. Both records claim block 3; one is an
        // hour old and one is nine, so only the timestamps can tell them apart.
        transcripts.Write(
            "a-project",
            Spent(Now.AddHours(-1), output: 100, block: 3),
            Spent(Now.AddHours(-9), output: 500, block: 3));

        var measured = AllowanceLedger.Read(
            "mine", transcripts.Root, AllowanceLimits.None, Now);

        await Assert.That(Window(measured, AllowanceLedger.Session).Tokens).IsEqualTo(100L)
            .Because("the nine-hour-old record is outside a five-hour session and the "
                   + "block index says it is in the same one.");

        await Assert.That(Window(measured, AllowanceLedger.Week).Tokens).IsEqualTo(600L)
            .Because("both are inside the week.");
    }

    [Test]
    public async Task An_absent_limit_is_unknown_rather_than_nothing_used()
    {
        using var transcripts = new Transcripts();
        transcripts.Write("a-project", Spent(Now.AddMinutes(-1), output: 4242));

        var measured = AllowanceLedger.Read(
            "mine", transcripts.Root, AllowanceLimits.None, Now);

        var session = Window(measured, AllowanceLedger.Session);

        await Assert.That(session.Tokens).IsEqualTo(4242L)
            .Because("what was spent is known whether or not anybody said what the "
                   + "ceiling is.");
        await Assert.That(session.Limit).IsNull();
        await Assert.That(session.Fraction).IsNull()
            .Because("a missing denominator reported as 0% reads as plenty left, which "
                   + "is the one wrong answer that looks like a right one.");
    }

    [Test]
    public async Task A_limit_that_was_configured_gives_the_window_a_fraction()
    {
        using var transcripts = new Transcripts();
        transcripts.Write("a-project", Spent(Now.AddMinutes(-1), output: 250));

        var limits = AllowanceLimits.Read("session=1000,week=50000");

        var measured = AllowanceLedger.Read("mine", transcripts.Root, limits, Now);
        var session = Window(measured, AllowanceLedger.Session);

        await Assert.That(session.Limit).IsEqualTo(1000L);
        await Assert.That(session.Fraction).IsEqualTo(0.25);
    }

    /// <summary>One transcript record, in the shape the executor writes.</summary>
    /// <remarks>
    /// Built here rather than copied from a real file, because a real one is
    /// somebody's work. The members named are the only ones the ledger reads.
    /// </remarks>
    private static string Spent(
        DateTimeOffset at,
        long output,
        long input = 0,
        long cacheRead = 0,
        long cacheWrite = 0,
        int block = 0,
        string text = "some work happened here")
    {
        var usage = new Dictionary<string, object>
        {
            ["input_tokens"] = input,
            ["output_tokens"] = output,
            ["cache_read_input_tokens"] = cacheRead,
            ["cache_creation_input_tokens"] = cacheWrite,
        };

        var message = new Dictionary<string, object>
        {
            ["model"] = "claude-opus-5",
            ["content"] = text,
            ["usage"] = usage,
        };

        return JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["type"] = "assistant",
            ["timestamp"] = at.ToString("o", CultureInfo.InvariantCulture),
            ["apiBlockIndex"] = block,
            ["message"] = message,
        });
    }

    private static MeasuredWindow Window(MeasuredAllowance measured, string kind) =>
        measured.Windows.Single(w => w.Kind == kind);

    /// <summary>Every string the reading can carry, however deeply.</summary>
    private static IEnumerable<string> Strings(object? held)
    {
        switch (held)
        {
            case null:
                yield break;
            case string text:
                yield return text;
                yield break;
            case System.Collections.IEnumerable many and not string:
                foreach (var one in many)
                {
                    foreach (var found in Strings(one)) { yield return found; }
                }

                yield break;
        }

        var type = held.GetType();
        if (type.IsPrimitive || type.IsEnum || held is DateTimeOffset or DateTime or decimal)
        {
            yield break;
        }

        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.GetIndexParameters().Length > 0) { continue; }

            foreach (var found in Strings(property.GetValue(held))) { yield return found; }
        }
    }

    /// <summary>A transcript directory in the executor's layout.</summary>
    private sealed class Transcripts : IDisposable
    {
        public Transcripts() =>
            Root = Directory.CreateTempSubdirectory("gg-allowance").FullName;

        public string Root { get; }

        public void Write(string project, params string[] lines)
        {
            var directory = Path.Combine(Root, project);
            Directory.CreateDirectory(directory);
            File.WriteAllLines(
                Path.Combine(directory, $"{Guid.NewGuid():N}.jsonl"), lines);
        }

        public void Dispose()
        {
            try { Directory.Delete(Root, recursive: true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
