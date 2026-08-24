using Gg.Contracts;

namespace Gg.Client.Tests;

/// <summary>
/// The narrowing's parser: a closed root of exactly one key, chosen by the
/// caller and never by the document.
/// </summary>
/// <remarks>
/// <para>
/// <b>The role comes from which door was knocked on.</b> There is no
/// <c>kind:</c> discriminator, deliberately: a document that could name its
/// own role is the governed thing describing its own authority - the same
/// rule as <c>layer:</c> and <c>provenance:</c>, one level up. Whoever calls
/// <c>ParseNarrowing</c> decided the role the way whoever applies a document
/// decides the layer.
/// </para>
/// <para>
/// <b>Refused, not ignored, naming the key.</b> A narrowing carrying
/// <c>loops:</c> that parsed anyway would be an author granting something
/// the composed envelope silently drops - the exact silent-no-op class this
/// slice deletes from composition.
/// </para>
/// </remarks>
public class NarrowingParseTests
{
    private const string Valid = """
        obligations:
          needs-a-person:
            check: human
            approver: lead
        """;

    [Test]
    public async Task The_narrowing_parser_accepts_what_its_emitter_writes()
    {
        var parsed = EnvelopeYaml.ParseNarrowing(Valid);

        await Assert.That(parsed.Diagnosis).IsNull();
        await Assert.That(parsed.Narrowing!.Obligations.Single().Id).IsEqualTo("needs-a-person");
    }

    [Test]
    public async Task A_narrowing_cannot_say_loops()
    {
        var parsed = EnvelopeYaml.ParseNarrowing(
            Valid + "\nloops:\n  implement:\n    executor: frontier\n");

        await Assert.That(parsed.Narrowing).IsNull();
        await Assert.That(parsed.Diagnosis).Contains("loops");
        await Assert.That(parsed.Diagnosis).Contains("obligations")
            .Because("naming what was expected is most of the value of naming what was found.");
    }

    [Test]
    public async Task A_narrowing_cannot_say_destinations()
    {
        var parsed = EnvelopeYaml.ParseNarrowing(
            Valid + "\ndestinations:\n  pull-request:\n    kind: pull-request\n");

        await Assert.That(parsed.Narrowing).IsNull();
        await Assert.That(parsed.Diagnosis).Contains("destinations");
    }

    [Test]
    public async Task A_narrowing_cannot_say_context()
    {
        var parsed = EnvelopeYaml.ParseNarrowing(
            Valid + "\ncontext:\n  scope: \"src/**\"\n  constitution: \"1.0.0\"\n");

        await Assert.That(parsed.Narrowing).IsNull();
        await Assert.That(parsed.Diagnosis).Contains("context");
    }

    [Test]
    public async Task A_narrowing_cannot_say_a_selection()
    {
        var parsed = EnvelopeYaml.ParseNarrowing(Valid + "\nenvironment: prod\n");

        await Assert.That(parsed.Narrowing).IsNull();
        await Assert.That(parsed.Diagnosis).Contains("environment");
    }

    [Test]
    public async Task A_narrowing_obligation_cannot_declare_provenance()
    {
        // The same rule as the envelope's parser, because it is the same
        // MapObligation - shared rather than copied, so the two documents
        // cannot drift about who assigns authority.
        var parsed = EnvelopeYaml.ParseNarrowing("""
            obligations:
              needs-a-person:
                check: human
                approver: lead
                provenance: org
            """);

        await Assert.That(parsed.Narrowing).IsNull();
        await Assert.That(parsed.Diagnosis).Contains("provenance");
        await Assert.That(parsed.Diagnosis).Contains("not yours to set");
    }
}
