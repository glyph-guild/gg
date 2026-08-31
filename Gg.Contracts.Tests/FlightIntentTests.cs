namespace Gg.Contracts.Tests;

/// <summary>
/// Intent is three fields from the start, not a bare string.
/// </summary>
/// <remarks>
/// <para>
/// <c>gg fly "fix the login bug"</c> wants a string. <c>gg fly</c> pointed at
/// an issue wants a typed reference. A contract that ships the string first and
/// grows the reference later is a migration of stored data and of every
/// consumer; three fields with one populated costs nothing today.
/// </para>
/// <para>
/// Article XI decides the validation: an intent that names no kind, or names
/// one nothing understands, or carries two payloads, HALTS. None of those
/// quietly becomes an empty flight - a flight created from an intent nobody
/// could read is worse than a flight that was refused.
/// </para>
/// </remarks>
public class FlightIntentTests
{
    [Test]
    public async Task Free_text_is_an_intent()
    {
        var intent = new FlightIntent { Kind = FlightIntentKinds.Text, Text = "fix the login bug" };

        await Assert.That(FlightIntent.Validate(intent)).IsNull()
            .Because("validation returns the diagnosis, and there is nothing wrong with this one.");
    }

    [Test]
    public async Task A_typed_reference_is_an_intent()
    {
        var intent = new FlightIntent { Kind = FlightIntentKinds.Uri, Uri = "https://example.invalid/issues/7" };

        await Assert.That(FlightIntent.Validate(intent)).IsNull();
    }

    [Test]
    public async Task Carrying_both_payloads_is_refused()
    {
        // Which one wins would be a coin toss made by whoever wrote the reader,
        // and the two readers are in different repositories.
        var intent = new FlightIntent
        {
            Kind = FlightIntentKinds.Text,
            Text = "fix the login bug",
            Uri = "https://example.invalid/issues/7",
        };

        await Assert.That(FlightIntent.Validate(intent)).IsNotNull()
            .Because("an intent that says two things says nothing.");
    }

    [Test]
    public async Task Carrying_neither_payload_is_refused()
    {
        await Assert.That(FlightIntent.Validate(new FlightIntent { Kind = FlightIntentKinds.Text }))
            .IsNotNull();
        await Assert.That(FlightIntent.Validate(new FlightIntent { Kind = FlightIntentKinds.Uri }))
            .IsNotNull();

        // Whitespace is not a payload. A flight whose entire intent is a space
        // is the empty case wearing a disguise.
        await Assert.That(FlightIntent.Validate(
                new FlightIntent { Kind = FlightIntentKinds.Text, Text = "   " }))
            .IsNotNull();
    }

    [Test]
    public async Task The_payload_must_match_the_kind_it_declares()
    {
        // A kind that disagrees with the field carrying the payload is how a
        // consumer ends up rendering a URI as prose, or fetching free text.
        var mismatched = new FlightIntent
        {
            Kind = FlightIntentKinds.Uri,
            Text = "fix the login bug",
        };

        await Assert.That(FlightIntent.Validate(mismatched)).IsNotNull();
    }

    [Test]
    public async Task An_unknown_kind_halts_rather_than_being_ignored()
    {
        // Article XI. The tempting alternative - treat anything unrecognised as
        // text - creates a flight whose intent nobody agreed on, and it looks
        // exactly like a successful one.
        var diagnosis = FlightIntent.Validate(
            new FlightIntent { Kind = "telepathy", Text = "you know what I mean" });

        await Assert.That(diagnosis).IsNotNull();
        await Assert.That(diagnosis!).Contains("telepathy")
            .Because("a diagnosis that does not name the offending value is not a diagnosis.");
    }

    [Test]
    public async Task A_missing_kind_is_refused_too()
    {
        await Assert.That(FlightIntent.Validate(new FlightIntent { Kind = "", Text = "something" }))
            .IsNotNull();
    }

    [Test]
    public async Task The_known_kinds_are_the_ones_validation_accepts()
    {
        // Guards the list against drifting away from the validator: a kind
        // declared but not accepted would be advertised and then refused.
        foreach (var kind in FlightIntentKinds.All)
        {
            // A THIRD ARM AT SLICE NINETEEN, and the shape of this loop is why
            // it had to be written rather than noticed: `ticket` carries its
            // payload in TWO fields, so the two-way ternary that served while
            // every kind had one string could not populate it - and the kind
            // was advertised-and-refused until this changed. That is exactly
            // what this test exists to catch, caught on the first new kind
            // since it was written.
            var populated = kind switch
            {
                FlightIntentKinds.Uri =>
                    new FlightIntent { Kind = kind, Uri = "https://example.invalid/x" },
                FlightIntentKinds.Ticket =>
                    new FlightIntent { Kind = kind, Provider = "tracker", Id = "x" },
                _ => new FlightIntent { Kind = kind, Text = "x" },
            };

            await Assert.That(FlightIntent.Validate(populated)).IsNull()
                .Because($"'{kind}' is advertised as a kind, so it must validate.");
        }

        await Assert.That(FlightIntentKinds.All).IsNotEmpty();
    }
}
