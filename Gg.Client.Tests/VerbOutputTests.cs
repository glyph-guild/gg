using Gg.Contracts;
using Gg.Contracts.Description;

namespace Gg.Client.Tests;

/// <summary>
/// Human output is a RENDERING of the structured result, not a second code
/// path that happens to say the same things today.
/// </summary>
/// <remarks>
/// <para>
/// The vault requires that every verb has a console equivalent and every
/// console action a verb, and that both render the same structured result.
/// That holds by construction only if there is one result and one renderer;
/// the moment a verb writes text directly, the two surfaces start drifting and
/// nothing notices until they disagree in front of somebody.
/// </para>
/// <para>
/// So the property asserted here is the strong one: rendering the result and
/// rendering the result AFTER a round trip through JSON produce the same text.
/// A renderer reaching for anything the JSON does not carry - a status code, a
/// header, a field it forgot to serialize - fails this. Comparing the two
/// outputs by eye, or asserting that both are non-empty, would not.
/// </para>
/// </remarks>
public class VerbOutputTests
{
    private static FlightSummary AFlight(int number, string name) => new()
    {
        FlightId = "019fe815-6136-7518-bb57-b06d6d3f411a",
        FlightNumber = FlightRef.Format(number),
        Name = name,
        Intent = new FlightIntent { Kind = FlightIntentKinds.Text, Text = "fix the login bug" },
        CreatedAt = new DateTimeOffset(2026, 8, 9, 12, 0, 0, TimeSpan.Zero),
        RunnerProtocolVersion = 1,
        FactVocabularyVersion = "0.1.0",
        ConstitutionVersion = "1.0.0",
        EnvelopeVersion = "none",
        Attempts = 1,
        Facts = [],
    };

    /// <summary>One of every result shape a verb can produce.</summary>
    private static IEnumerable<VerbResult> EveryShape() =>
    [
        new VerbResult.Flights(new FlightList { Flights = [AFlight(42, "nightly audit"), AFlight(7, "spike")] }),
        new VerbResult.Flights(new FlightList { Flights = [] }),
        new VerbResult.Flight(AFlight(42, "nightly audit")),
        new VerbResult.Launched(new FlightLaunched
        {
            FlightId = "019fe815-6136-7518-bb57-b06d6d3f411a",
            FlightNumber = null,
        }),
        new VerbResult.Log(new FlightLog
        {
            FlightId = "019fe815-6136-7518-bb57-b06d6d3f411a",
            FlightNumber = FlightRef.Format(42),
            Entries =
            [
                new FlightLogEntry
                {
                    At = new DateTimeOffset(2026, 8, 9, 12, 0, 0, TimeSpan.Zero),
                    Kind = "lease-granted",
                    Detail = "{\"generation\":1}",
                },
            ],
        }),
        new VerbResult.Runners(new RunnerList
        {
            Runners =
            [
                new RunnerSummary
                {
                    RunnerId = "019fe8a2-0707-70c2-9ff8-be3adb54cef0",
                    Label = "laptop",
                    State = RunnerStates.Busy,
                    CurrentFlightId = "019fe815-6136-7518-bb57-b06d6d3f411a",
                    CurrentFlightNumber = FlightRef.Format(42),
                    LastHeartbeatAt = new DateTimeOffset(2026, 8, 9, 12, 0, 0, TimeSpan.Zero),
                },
                new RunnerSummary
                {
                    RunnerId = "019fe8a2-0707-70c2-9ff8-be3adb54cef1",
                    Label = "builder",
                    State = RunnerStates.Offline,
                },
            ],
        }),
        new VerbResult.Diagnosis(new DoctorReport
        {
            Checks =
            [
                new DoctorCheck
                {
                    Name = "control plane",
                    Passed = false,
                    Detail = "could not connect",
                    Blocking = true,
                    Fixable = false,
                },
                new DoctorCheck
                {
                    Name = "session",
                    Passed = true,
                    Detail = "valid until 2026-08-10",
                    Blocking = false,
                    Fixable = true,
                    Fix = "gg login",
                },
            ],
        }),
    ];

    [Test]
    public async Task Human_output_survives_a_round_trip_through_the_json()
    {
        // The property 4b depends on. If the text were produced from anything
        // the JSON does not carry, these two would differ.
        foreach (var result in EveryShape())
        {
            var direct = VerbOutput.ToText(result);
            var viaJson = VerbOutput.ToText(VerbOutput.Parse(result.Kind, VerbOutput.ToJson(result)));

            await Assert.That(viaJson).IsEqualTo(direct)
                .Because($"{result.Kind} must render from the JSON, not alongside it.");
        }
    }

    [Test]
    public async Task Every_shape_actually_renders_something()
    {
        // Guards the test above: two empty strings are equal, and a renderer
        // that returned "" for everything would pass it.
        foreach (var result in EveryShape().Where(r => r is not VerbResult.Flights { Value.Flights.Count: 0 }))
        {
            await Assert.That(string.IsNullOrWhiteSpace(VerbOutput.ToText(result))).IsFalse()
                .Because($"{result.Kind} renders nothing at all.");
        }
    }

    [Test]
    public async Task An_empty_list_says_so_rather_than_printing_nothing()
    {
        // Nothing printed and nothing found look identical in a terminal, and
        // one of them is a bug somebody should be chasing.
        var text = VerbOutput.ToText(new VerbResult.Flights(new FlightList { Flights = [] }));

        await Assert.That(string.IsNullOrWhiteSpace(text)).IsFalse();
    }

    [Test]
    public async Task The_json_is_the_wire_shape_and_not_a_wrapper_around_it()
    {
        // What --json prints must be the same document the control plane sent,
        // so a person can pipe it into anything that knows the contract. A
        // {"result": …} envelope invented here would break that quietly.
        var json = VerbOutput.ToJson(new VerbResult.Flight(AFlight(42, "nightly audit")));

        foreach (var member in ProtocolSurface.JsonMembers[typeof(FlightSummary)])
        {
            await Assert.That(json).Contains($"\"{member}\"")
                .Because($"--json must carry the declared member '{member}'.");
        }

        await Assert.That(json).DoesNotContain("\"result\"");
        await Assert.That(json).DoesNotContain("\"kind\":\"flight\"");
    }

    [Test]
    public async Task Rendering_strips_control_sequences_it_was_handed()
    {
        // Stored-clean is the guarantee, and this is the belt to that braces:
        // a control plane that was compromised, or an older one that stored a
        // name before stripping existed, must not be able to drive a terminal
        // through gg.
        var poisoned = AFlight(42, "\u001b[31mDROP\u001b[0m audit");

        var text = VerbOutput.ToText(new VerbResult.Flight(poisoned));

        await Assert.That(ControlText.ContainsControlSequence(text, allowLineBreaks: true)).IsFalse()
            .Because("gg renders into a terminal, and is the last place that can refuse to.");
        await Assert.That(text).Contains("DROP audit");
    }

    [Test]
    public async Task Every_result_kind_can_be_parsed_back()
    {
        // Parse is what lets gg re-render a --json payload somebody sent us,
        // which is the whole reason the two surfaces share one shape. A kind
        // it cannot read is a support bundle nobody can open.
        foreach (var result in EveryShape())
        {
            var reparsed = VerbOutput.Parse(result.Kind, VerbOutput.ToJson(result));

            await Assert.That(reparsed.Kind).IsEqualTo(result.Kind);
        }
    }

    [Test]
    public async Task An_unknown_result_kind_halts_rather_than_rendering_nothing()
    {
        // Article XI. Returning "" for a kind nothing understands would print
        // a blank screen and exit zero.
        await Assert.That(() => VerbOutput.Parse("telepathy", "{}"))
            .Throws<InvalidOperationException>();
    }
}
