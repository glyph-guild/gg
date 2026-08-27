namespace Gg.Contracts.Tests;

/// <summary>
/// The fragment round trip, asserted at the MODEL and against files formatted
/// the way a person writes them.
/// </summary>
/// <remarks>
/// <para>
/// <b>The asymmetry that decides the shape of this file.</b> In the estate, our
/// canonical rendering IS the record — pull writes it, apply reads it back, and
/// a byte comparison is meaningful. Here the file is the record and our
/// rendering is a view: a team's formatting is theirs, and comparing bytes
/// against our canonical form would fail on every file anybody actually wrote.
/// So the round trip is <c>parse(render(parse(bytes))) == parse(bytes)</c>.
/// </para>
/// <para>
/// <b>And it runs on the half of the fork with the worse history.</b>
/// <c>evidence:</c> was authorable, load-bearing at the gate, and emitted by
/// neither render path for three contract versions — caught only when slice
/// nine wrote a round trip that compared MODELS rather than renderings. The
/// earlier test compared <c>render(parse(render(x)))</c> to <c>render(x)</c>,
/// which passes when both sides drop the same member.
/// </para>
/// <para>
/// <b>Poison twins are the other half.</b> A round trip proves what survives; a
/// twin proves the test would notice if something stopped surviving. Each one
/// deletes a key from the rendering and asserts the result no longer round
/// trips — and asserts first that the key was in the rendering at all, because
/// a twin that deletes nothing passes forever.
/// </para>
/// </remarks>
public class NarrowingPoisonTwinTests
{
    /// <summary>Every member of a narrowing that a person can write, set.</summary>
    private static EnvelopeNarrowing Everything() => new()
    {
        Obligations =
        [
            new Obligation
            {
                Id = "pci-review",
                Check = ObligationChecks.Human,
                Approver = "an-auditor",
                When = "change.manifest touches payments/**",
                Evidence = [EvidenceItems.ChangeManifest],
            },
            new Obligation
            {
                Id = "in-scope",
                Check = ObligationChecks.Machine,
                Rule = ObligationPredicates.NoFileOutsideScope,
            },
        ],
    };

    /// <summary>Written the way a person writes it, not the way we render it.</summary>
    private const string HandWritten = """
        # Our PCI narrowing. Touch payments and an auditor signs off.
        obligations:

          - id:        pci-review
            check:     human
            approver:  an-auditor
            when:      "change.manifest touches payments/**"
            evidence:  [ change.manifest ]

          - id:    in-scope
            check: machine
            rule:  no-file-outside-scope
        """;

    [Test]
    public async Task A_hand_formatted_narrowing_round_trips_at_the_model()
    {
        // THE SHAPE THIS FILE EXISTS FOR. Our rendering is a view, so the
        // comparison is between models and never between bytes.
        var once = EnvelopeYaml.ParseNarrowing(HandWritten);
        await Assert.That(once.Diagnosis).IsNull();

        var twice = EnvelopeYaml.ParseNarrowing(EnvelopeText.Render(once.Narrowing!));

        await Assert.That(twice.Diagnosis).IsNull();
        await Assert.That(twice.Narrowing).IsEqualTo(once.Narrowing)
            .Because("what a team wrote and what we read back must be the same document, "
                   + "however they chose to lay it out.");
    }

    [Test]
    public async Task Comments_and_spacing_are_ours_to_lose_and_the_model_is_not()
    {
        // Said out loud, because it is the one thing a team WILL notice: the
        // comment above is gone from our rendering and the obligations are not.
        var parsed = EnvelopeYaml.ParseNarrowing(HandWritten).Narrowing!;
        var rendered = EnvelopeText.Render(parsed);

        await Assert.That(rendered).DoesNotContain("Touch payments");
        await Assert.That(EnvelopeYaml.ParseNarrowing(rendered).Narrowing).IsEqualTo(parsed);
    }

    [Test]
    public async Task Every_expressible_member_survives_the_round_trip()
    {
        var rendered = EnvelopeText.Render(Everything());
        var parsed = EnvelopeYaml.ParseNarrowing(rendered);

        await Assert.That(parsed.Diagnosis).IsNull();
        await Assert.That(parsed.Narrowing).IsEqualTo(Everything());
    }

    [Test]
    [Arguments("id")]
    [Arguments("check")]
    [Arguments("approver")]
    [Arguments("when")]
    [Arguments("evidence")]
    [Arguments("rule")]
    public async Task A_rendering_that_dropped_this_key_would_be_caught(string key)
    {
        var text = EnvelopeText.Render(Everything());
        var poisoned = string.Join('\n', text.Split('\n')
            .Where(line => !line.TrimStart().StartsWith(key + ":", StringComparison.Ordinal)));

        await Assert.That(poisoned).IsNotEqualTo(text)
            .Because($"'{key}' is not in the rendering at all, so this twin proves nothing - "
                   + "which is exactly how `evidence:` went missing for three versions.");

        var parsed = EnvelopeYaml.ParseNarrowing(poisoned);

        await Assert.That(parsed.Diagnosis is not null || parsed.Narrowing != Everything()).IsTrue()
            .Because($"dropping '{key}' round-tripped clean, which is how a declaration goes "
                   + "missing without anything failing.");
    }

    [Test]
    public async Task Every_member_of_the_narrowing_schema_is_accounted_for_by_this_suite()
    {
        // The ratchet. A member added to the schema is a member this suite
        // stops covering silently, unless adding one fails here.
        string[] covered = ["Obligations"];

        var members = typeof(EnvelopeNarrowing).GetProperties().Select(p => p.Name).ToList();

        await Assert.That(members.Except(covered, StringComparer.Ordinal)).IsEmpty()
            .Because("a member nothing here sets is a member the round trip does not prove.");
        await Assert.That(covered.Except(members, StringComparer.Ordinal)).IsEmpty()
            .Because("a covered name that is not a member is a stale entry holding a hole "
                   + "open - the staleness check the envelope suite's ratchet does not have.");
    }
}
