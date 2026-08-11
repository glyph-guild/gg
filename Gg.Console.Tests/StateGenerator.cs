using Gg.Contracts;
using Gg.Contracts.Description;

namespace Gg.Console.Tests;

/// <summary>
/// Builds arbitrary <see cref="AppState"/> values, for properties rather than
/// examples.
/// </summary>
/// <remarks>
/// <para>
/// A hand-written serialization test covers exactly the fields somebody
/// remembered. That is the same defect as a poison twin pointed at the wrong
/// key and the same defect as a cross-tenant test with one tenant: the shape
/// of the check makes it look total when it is a sample.
/// </para>
/// <para>
/// Seeded, so a failure names the seed that produced it and can be replayed.
/// A generator whose failures cannot be reproduced is a flake with better
/// manners.
/// </para>
/// </remarks>
internal static class StateGenerator
{
    /// <summary>Every mode and pane, so no generated run can miss one by luck.</summary>
    internal static IReadOnlyList<UiMode> Modes { get; } = Enum.GetValues<UiMode>();

    internal static IReadOnlyList<PaneId> Panes { get; } = Enum.GetValues<PaneId>();

    internal static AppState Next(Random random)
    {
        var queue = Enumerable.Range(0, random.Next(0, 6))
            .Select(_ => NextRow(random))
            .ToList();

        var live = Enumerable.Range(0, random.Next(0, 8))
            .Select(_ => NextLine(random))
            .ToList();

        return new AppState
        {
            Mode = Pick(random, Modes),
            FocusedPane = Pick(random, Panes),
            Queue = queue,
            SelectedRow = queue.Count == 0 ? 0 : random.Next(0, queue.Count),
            Flight = random.Next(2) == 0 ? null : NextSummary(random),
            FlightLog = random.Next(2) == 0 ? null : NextLog(random),
            Runners = random.Next(2) == 0 ? null : NextRunners(random),
            EvidenceVisible = random.Next(2) == 0,
            LiveVisible = random.Next(2) == 0,
            Frozen = random.Next(2) == 0,
            Live = live,
            Held = Enumerable.Range(0, random.Next(0, 4)).Select(_ => NextLine(random)).ToList(),
            AttachFacts = Enumerable.Range(0, random.Next(0, 4))
                .Select(_ => new LiveAttachFact
                {
                    FlightId = NextId(random),
                    Attached = random.Next(2) == 0,
                    AttachCount = random.Next(0, 5),
                })
                .ToList(),
            Notes = NextText(random),
            Diagnosis = random.Next(3) == 0 ? NextText(random) : null,
        };
    }

    private static QueueRow NextRow(Random random) => new()
    {
        FlightId = NextId(random),
        FlightNumber = FlightRef.Format(random.Next(1, 9999)),
        Name = NextText(random),
        Reason = Pick(random, Enum.GetValues<QueueReason>()),
        Since = NextInstant(random),
        UnreadArrivals = random.Next(0, 4),
    };

    private static StreamLine NextLine(Random random) => new()
    {
        Kind = Pick(random, Enum.GetValues<StreamLineKind>()),
        Text = NextText(random),
        At = NextInstant(random),
    };

    private static FlightSummary NextSummary(Random random) => new()
    {
        FlightId = NextId(random),
        FlightNumber = FlightRef.Format(random.Next(1, 9999)),
        Name = NextText(random),
        Intent = new FlightIntent { Kind = FlightIntentKinds.Text, Text = NextText(random) },
        CreatedAt = NextInstant(random),
        RunnerProtocolVersion = random.Next(1, 4),
        FactVocabularyVersion = "0.1.0",
        ConstitutionVersion = "1.0.0",
        EnvelopeVersion = "none",
        Facts = [],
    };

    private static FlightLog NextLog(Random random) => new()
    {
        FlightId = NextId(random),
        FlightNumber = FlightRef.Format(random.Next(1, 9999)),
        Entries = Enumerable.Range(0, random.Next(0, 4))
            .Select(_ => new FlightLogEntry
            {
                At = NextInstant(random),
                Kind = Pick(random, (string[])["lease-granted", "lease-expired", "lease-released"]),
                Detail = NextText(random),
            })
            .ToList(),
    };

    private static RunnerList NextRunners(Random random) => new()
    {
        Runners = Enumerable.Range(0, random.Next(0, 3))
            .Select(_ => new RunnerSummary
            {
                RunnerId = NextId(random),
                Label = NextText(random),
                State = Pick(random, RunnerStates.All),
                CurrentFlightId = random.Next(2) == 0 ? null : NextId(random),
                CurrentFlightNumber = random.Next(2) == 0 ? null : FlightRef.Format(random.Next(1, 99)),
                LastHeartbeatAt = random.Next(2) == 0 ? null : NextInstant(random),
            })
            .ToList(),
    };

    private static T Pick<T>(Random random, IReadOnlyList<T> values) => values[random.Next(values.Count)];

    private static string NextId(Random random)
    {
        var bytes = new byte[16];
        random.NextBytes(bytes);
        return new Guid(bytes).ToString();
    }

    /// <summary>
    /// Text with the things that break naive serialization in it.
    /// </summary>
    /// <remarks>
    /// Newlines, quotes, backslashes and non-ASCII, because a round trip that
    /// only ever saw "flight-1" proves nothing about a flight named after a
    /// branch somebody created on a Tuesday.
    /// </remarks>
    private static string NextText(Random random)
    {
        var pieces = (string[])
        [
            "fix the login bug", "", "  ", "line one\nline two", "quote \" and \\ backslash",
            "日本語のフライト", "Grüße", "tab\there", "100% — done!", "{\"looks\":\"like json\"}",
        ];
        return pieces[random.Next(pieces.Length)];
    }

    private static DateTimeOffset NextInstant(Random random) =>
        new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero).AddSeconds(random.Next(0, 60_000_000));
}
