using Gg.Client;
using Gg.Contracts;

namespace Gg.Client.Tests;

/// <summary>
/// <c>environment:</c> and <c>repository:</c> in the authored form.
/// </summary>
/// <remarks>
/// Root keys, beside <c>context:</c> rather than inside it: the context block
/// is what a flight is bound to, and a selection is what a flight is ABOUT -
/// declared once, validated for membership at apply, never merged. What this
/// file holds is the text form: the keys parse, absence stays absent, and the
/// round trip through the canonical render loses neither.
/// </remarks>
public class EnvelopeSelectionParseTests
{
    private const string Valid = """
        context:
          scope: "src/**"
          constitution: "1.0.0"
        environment: aspire-payments
        repository: acme/payments
        obligations:
          in-scope:
            check: machine
            rule: no-file-outside-scope
        loops:
          implement:
            executor: frontier
            discharges:
              - in-scope
            moves:
              - read
              - edit
            budget:
              wall-clock: "30m"
            on-exhaustion: handoff-to-human
        destinations:
          pull-request:
            kind: pull-request
            requires:
              - in-scope
        """;

    [Test]
    public async Task The_selections_parse()
    {
        var parsed = EnvelopeYaml.Parse(Valid);

        await Assert.That(parsed.Diagnosis).IsNull();
        await Assert.That(parsed.Envelope!.Environment).IsEqualTo("aspire-payments");
        await Assert.That(parsed.Envelope!.Repository).IsEqualTo("acme/payments");
    }

    [Test]
    public async Task Absent_selections_stay_absent()
    {
        // Reading a missing key back as "" would be a different document on
        // disk and the same value to the engine - the preserve-unadmitted
        // round-trip rule, applied to the selections.
        var parsed = EnvelopeYaml.Parse(Valid
            .Replace("environment: aspire-payments\n", "", StringComparison.Ordinal)
            .Replace("repository: acme/payments\n", "", StringComparison.Ordinal));

        await Assert.That(parsed.Diagnosis).IsNull();
        await Assert.That(parsed.Envelope!.Environment).IsNull();
        await Assert.That(parsed.Envelope!.Repository).IsNull();
    }

    [Test]
    public async Task A_selecting_envelope_round_trips_without_loss()
    {
        var parsed = EnvelopeYaml.Parse(Valid);

        await Assert.That(EnvelopeText.Render(parsed.Envelope!))
            .IsEqualTo(EnvelopeText.Render(
                EnvelopeYaml.Parse(EnvelopeText.Render(parsed.Envelope!)).Envelope!));
    }

    [Test]
    public async Task A_selection_inside_context_is_refused_with_the_path()
    {
        // The closed-schema rule catches the natural mistake: these read like
        // context, and the diagnosis has to say where they actually go.
        var parsed = EnvelopeYaml.Parse(Valid
            .Replace("environment: aspire-payments\n", "", StringComparison.Ordinal)
            .Replace("  constitution: \"1.0.0\"\n",
                     "  constitution: \"1.0.0\"\n  environment: aspire-payments\n",
                     StringComparison.Ordinal));

        await Assert.That(parsed.Envelope).IsNull();
        await Assert.That(parsed.Diagnosis!).Contains("environment");
        await Assert.That(parsed.Diagnosis!).Contains("context");
    }
}
