using Gg.Client;
using Gg.Contracts;

namespace Gg.Client.Tests;

/// <summary>
/// <c>gg envelope validate</c> reads a document as the role its location says
/// it is, and names the role it read.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is where the copied <c>root.yaml</c> gets caught.</b> ADR-0018 § 7's
/// fourth refusal is the one that will get skipped: a <c>README.md</c> is
/// obviously not an envelope, but a complete envelope is not obviously
/// anything. It parses, it validates, it is a legal document of the wrong type
/// — and it is exactly what somebody gets by copying <c>root.yaml</c> as a
/// starting point, which is the obvious way to begin.
/// </para>
/// <para>
/// <b>The role comes from the directory, using the table that already maps
/// them.</b> <c>AirspaceNames</c> has said since slice thirteen that
/// <c>narrowings/</c> holds narrowings and <c>work-kinds/</c> holds work kinds;
/// a working copy is written that way and a service repository's
/// <c>.goodgrief/narrowings/</c> is the same shape one repository over. Reading
/// the location rather than guessing from the content means a file that is
/// nearly a narrowing is refused as a narrowing rather than accepted as
/// something else.
/// </para>
/// <para>
/// <b>And the answer names the role</b>, because "valid" against the wrong rules
/// is the failure this is trying to prevent, and a person who meant to write a
/// narrowing needs to see that it was read as one.
/// </para>
/// </remarks>
public class NarrowingRoleInferenceTests
{
    private const string ANarrowing = """
        obligations:
          - id: pci-review
            check: human
            approver: an-auditor
        """;

    private const string ACompleteEnvelope = """
        context:
          scope: "src/**"
          constitution: "1.0.0"
        obligations:
          - id: in-scope
            check: machine
            rule: no-file-outside-scope
        loops:
          - id: implement
            executor: frontier
            discharges: [in-scope]
            moves: [read, edit]
            budget:
              wall-clock: 30m
            on-exhaustion: handoff-to-human
        destinations:
          - id: pull-request
            kind: pull-request
            requires: [in-scope]
        """;

    private static EnvelopeValidation Validated(string text, string? path) =>
        (EnvelopeCommands.Validate(text, path) as VerbResult.EnvelopeValidated)!.Value;

    [Test]
    public async Task A_file_in_the_narrowings_directory_is_read_as_a_narrowing()
    {
        var result = Validated(ANarrowing, ".goodgrief/narrowings/pci.yaml");

        await Assert.That(result.Valid).IsTrue();
        await Assert.That(result.Role).IsEqualTo(Roles.Narrowing);
    }

    [Test]
    public async Task A_complete_envelope_in_the_narrowings_directory_is_refused()
    {
        // THE ONE THAT WOULD GET SKIPPED. Accepting it hands a team `scope:`
        // and `constitution:` - the two fields ADR-0018 § 1 exists to keep away
        // from them - through a merge nobody gated.
        var result = Validated(ACompleteEnvelope, ".goodgrief/narrowings/pci.yaml");

        await Assert.That(result.Valid).IsFalse();
        await Assert.That(result.Role).IsEqualTo(Roles.Narrowing)
            .Because("it says what it TRIED to read it as, or the diagnosis is unreadable.");
        await Assert.That(result.Diagnosis!).Contains("context")
            .Because("the refusal names the key it could not accept, which is the one the "
                   + "author has to delete.");
    }

    [Test]
    public async Task The_same_complete_envelope_is_valid_where_a_complete_envelope_belongs()
    {
        // The liveness half. The document above is not malformed - it is a
        // legal document of the wrong type - and a check that refused it
        // everywhere would be refusing something else.
        var result = Validated(ACompleteEnvelope, "airspace/root.yaml");

        await Assert.That(result.Valid).IsTrue();
        await Assert.That(result.Role).IsEqualTo(Roles.Root);
    }

    [Test]
    public async Task A_narrowing_outside_a_narrowings_directory_is_refused_as_an_envelope()
    {
        // The mirror, and it is the reason inference beats guessing: a
        // narrowing put in the work-kinds directory is a document in the wrong
        // place, and reading it as what its location says catches that instead
        // of silently accepting a partial document as a whole one.
        var result = Validated(ANarrowing, "airspace/work-kinds/implement.yaml");

        await Assert.That(result.Valid).IsFalse();
        await Assert.That(result.Role).IsEqualTo(Roles.WorkKind);
    }

    [Test]
    public async Task With_no_path_the_shape_decides_and_the_role_is_still_named()
    {
        // Reading from stdin has no location to infer from, and refusing that
        // would break `gg envelope validate -`, which is what CI pipes into.
        await Assert.That(Validated(ANarrowing, path: null).Role).IsEqualTo(Roles.Narrowing);
        await Assert.That(Validated(ACompleteEnvelope, path: null).Role).IsEqualTo(Roles.Root);
    }

    [Test]
    public async Task An_unrecognised_directory_falls_back_to_the_shape()
    {
        // A file somebody keeps somewhere else entirely. Refusing on location
        // alone would make the verb useless for anyone not using our layout.
        await Assert.That(Validated(ANarrowing, "policy/whatever.yaml").Role)
            .IsEqualTo(Roles.Narrowing);
    }

    [Test]
    public async Task A_strategy_directory_reads_as_a_strategy()
    {
        // The third document class, so the inference reads the whole table
        // rather than the two entries this slice happens to need.
        var result = Validated(ANarrowing, "airspace/strategies/warm-pool.yaml");

        await Assert.That(result.Role).IsEqualTo(Roles.Strategy);
        await Assert.That(result.Valid).IsFalse();
    }
}
