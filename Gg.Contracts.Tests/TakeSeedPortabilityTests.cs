namespace Gg.Contracts.Tests;

/// <summary>
/// What the seed says when the thing a person wants is on another machine.
/// </summary>
/// <remarks>
/// <b>An absent section and a thing that does not exist must not read the same
/// way.</b> The seed already refuses to let that happen for the agent's account -
/// <c>NO ACCOUNT:</c> is a line rather than a gap - and the transcript needs the
/// same treatment for a harder reason: it is the one artifact that <i>cannot</i>
/// cross. <c>ArtifactScopes</c> has exactly one value, <c>runner-local</c>, and a
/// transcript contains customer code, so a person on the other side of a handoff
/// gets a hash and a locator they cannot follow. Saying so is the honest version;
/// omitting the section reads as a flight that produced nothing.
/// </remarks>
public class TakeSeedPortabilityTests
{
    private static LoopDigest Digest() => new()
    {
        LoopId = "implement",
        FilesReadNotEdited = ["config/settings.yaml"],
        FilesEdited = ["src/orders.py"],
        Searches = ["rounding"],
        Errors = [],
        RefusedMoves = [],
        Attempts = 4,
        StopReason = LoopOutcomes.Exhausted,
    };

    private static ArtifactReference Transcript() => new()
    {
        Locator = "/home/runner/.local/state/gg/transcripts/019ff8aa/implement.ndjson",
        Sha256 = new string('a', 64),
        Bytes = 91_240,
        MediaType = "application/x-ndjson",
        Scope = ArtifactScopes.RunnerLocal,
    };

    [Test]
    public async Task A_transcript_on_another_machine_is_stated_rather_than_omitted()
    {
        var seed = TakeSeedComposer.Compose(
            "GG-42", "019ff8aa-1111-7000-8000-000000000001", Digest(),
            account: null, transcript: Transcript());

        await Assert.That(seed.TranscriptState).IsEqualTo(TranscriptStates.Elsewhere);

        var rendered = TakeSeedComposer.Render(seed);

        await Assert.That(rendered).Contains(ArtifactScopes.RunnerLocal)
            .Because("a person who needs the transcript has to learn that it exists, where it is, "
                   + "and that this is a capability the platform does not have yet - rather than "
                   + "concluding the flight produced none.");
        await Assert.That(rendered).Contains("cannot be fetched");
    }

    [Test]
    public async Task A_flight_that_produced_no_transcript_reads_differently_from_one_held_elsewhere()
    {
        // THE POISON TWIN, and without it the assertion above passes on a seed
        // that says the same thing in both cases. These two send a person to
        // different places: one to another machine, one to nowhere.
        var elsewhere = TakeSeedComposer.Render(TakeSeedComposer.Compose(
            "GG-42", "019ff8aa-1111-7000-8000-000000000001", Digest(),
            account: null, transcript: Transcript()));

        var none = TakeSeedComposer.Render(TakeSeedComposer.Compose(
            "GG-42", "019ff8aa-1111-7000-8000-000000000001", Digest(),
            account: null, transcript: null));

        await Assert.That(none).IsNotEqualTo(elsewhere);
        await Assert.That(none).Contains("NO TRANSCRIPT");
        await Assert.That(none).DoesNotContain("cannot be fetched")
            .Because("there is nothing to fetch, so saying it cannot be fetched would invent an "
                   + "artifact.");
    }

    [Test]
    public async Task A_rendered_seed_names_no_absolute_path_and_no_machine()
    {
        // The locator IS an absolute path on somebody's runner, and it is the one
        // thing in the seed that has to be quoted without becoming a path this
        // document depends on - so it is quoted as a LOCATOR under a scope that
        // says it does not resolve here, and nothing else in the rendering is a
        // path outside the tree.
        var seed = TakeSeedComposer.Compose(
            "GG-42", "019ff8aa-1111-7000-8000-000000000001", Digest(),
            account: "I stopped at the rounding boundary.", transcript: null);

        var rendered = TakeSeedComposer.Render(seed);

        var absolute = rendered
            .Split('\n', ' ')
            .Where(t => t.StartsWith('/') || t.StartsWith("~/", StringComparison.Ordinal)
                     || t.Contains(":\\", StringComparison.Ordinal))
            .ToList();

        await Assert.That(absolute).IsEmpty()
            .Because("a seed carrying a path on one machine is a seed that only works on it. "
                   + "Found: " + string.Join(", ", absolute));
    }

    [Test]
    public async Task The_seed_stamps_the_revision_it_was_composed_at()
    {
        var seed = TakeSeedComposer.Compose(
            "GG-42", "019ff8aa-1111-7000-8000-000000000001", Digest(),
            account: null, transcript: null);

        await Assert.That(seed.Revision).IsEqualTo(TakeSeed.CurrentRevision)
            .Because("a reader has to be able to tell what shape it was handed without asking "
                   + "which build composed it.");
    }

    [Test]
    public async Task Two_seeds_composed_from_one_digest_measure_the_same_thing()
    {
        // FOUND, not predicted. TakeMeasurements is a record, and a record
        // compares IReadOnlyList<T> members by REFERENCE - so two measurements
        // built from one digest were unequal, and the parity check that a
        // control-plane seed measures what a local one would have could not pass
        // however correct both composers were. LoopDigest documents this exact
        // trap for this exact reason; the type made OF digests inherited it.
        var digest = Digest();

        var one = TakeSeedComposer.Compose(
            "GG-42", "019ff8aa-1111-7000-8000-000000000001", digest, account: null);
        var other = TakeSeedComposer.Compose(
            "GG-42", "019ff8aa-1111-7000-8000-000000000001", digest, account: null);

        await Assert.That(one.Measurements).IsEqualTo(other.Measurements);
        await Assert.That(one.Measurements.GetHashCode())
            .IsEqualTo(other.Measurements.GetHashCode())
            .Because("equal values with different hashes are worse than unequal values: they work "
                   + "until something puts them in a dictionary.");
    }

    [Test]
    public async Task Measurements_that_differ_are_still_unequal()
    {
        // The poison twin. Value equality that returned true for everything
        // would satisfy the assertion above and destroy the comparison it exists
        // to enable.
        var mine = TakeSeedComposer.Compose(
            "GG-42", "019ff8aa-1111-7000-8000-000000000001", Digest(), account: null);
        var thinner = TakeSeedComposer.Compose(
            "GG-42", "019ff8aa-1111-7000-8000-000000000001",
            Digest() with { FilesReadNotEdited = [] }, account: null);

        await Assert.That(mine.Measurements).IsNotEqualTo(thinner.Measurements)
            .Because("filesReadNotEdited is the closest thing the stream holds to 'considered and "
                   + "ruled out', so losing it must be detectable.");
    }

    [Test]
    public async Task A_flight_killed_before_it_measured_anything_still_gets_a_seed()
    {
        // Carried forward from the local composer, and it matters more now: the
        // flight most likely to need taking over is the one that ended badly, and
        // a takeover that refused to start because a list was empty would fail
        // exactly that flight. Empty measurements are measurements.
        var seed = TakeSeedComposer.Compose(
            "GG-42", "019ff8aa-1111-7000-8000-000000000001", digest: null,
            account: null, transcript: null);

        await Assert.That(seed.Measurements.FilesEdited).IsEmpty();
        await Assert.That(seed.Measurements.StopReason).IsEqualTo("unknown");
        await Assert.That(seed.AccountState).IsEqualTo(AccountStates.Missing);
        await Assert.That(seed.TranscriptState).IsEqualTo(TranscriptStates.None);
    }
}
