using Gg.Contracts;
using Gg.Runner.Execution;
using Gg.Runner.Facts;

namespace Gg.Runner.Tests;

/// <summary>
/// Everything the runner produces is stripped where it is produced.
/// </summary>
/// <remarks>
/// <para>
/// <b>Slice one stripped at ingress, before storage.</b> Two things have made
/// that insufficient. The evidence hash is computed over the fact AS PRODUCED,
/// so cleaning it on the far side makes the stored bytes disagree with the hash
/// that proves what they were - which is why the control plane now refuses dirty
/// text instead, and why an escape sequence in a pull-request title could fail
/// an honest flight. And the live channel is never stored, so it inherits
/// nothing from a rule about storage.
/// </para>
/// <para>
/// So the runner normalises at production, before the digest. Then the fact as
/// produced is clean, the hash matches storage, the live stream is clean, and
/// <i>refuse dirty text at ingress</i> becomes the re-validation of a control
/// rather than the only control - the pattern this codebase already runs for
/// classification: filter before egress, re-derive at ingress.
/// </para>
/// </remarks>
public class FactHygieneTests
{
    private static readonly DateTimeOffset T0 = new(2026, 8, 13, 12, 0, 0, TimeSpan.Zero);

    /// <summary>An escape sequence, as a forge would hand one over.</summary>
    /// <remarks>
    /// Shaped like the real thing: a title somebody set to colour a terminal and
    /// rewrite its window title. <c>ESC ] 0 ; … BEL</c> is the one that matters -
    /// it sets the title of whatever terminal renders it.
    /// </remarks>
    private const string Esc = "\u001b";

    private const string Bel = "\u0007";

    private static readonly string Poison =
        $"{Esc}]0;pwned{Bel}fix: {Esc}[31mred{Esc}[0m title";

    private const string Clean = "fix: red title";

    [Test]
    public async Task A_hostile_pull_request_title_is_clean_before_the_digest_is_computed()
    {
        // THE POISON TWIN, through the real path: a fork slug is filled in by a
        // forge from somebody else's account, and it is the field the brief's
        // example travels in.
        var digested = FactPipeline.Digest(
            FactHygiene.Clean(new GatheredFacts([new FactPayload.Source(ASource() with
            {
                ForkSlug = Poison,
            })])),
            "flight-1", T0);

        var fact = digested.Items.Single();

        await Assert.That(fact.Source!.ForkSlug).IsEqualTo(Clean);
        await Assert.That(fact.Source!.ForkSlug).DoesNotContain(Esc);
    }

    [Test]
    public async Task The_hash_is_over_the_clean_bytes_so_storage_and_the_hash_agree()
    {
        // The correctness half. If the digest were computed over the dirty value
        // and the far side cleaned it, the stored bytes would not be the bytes
        // the hash proves - so the control plane could not tell a cleaned fact
        // from a tampered one.
        var dirty = FactPipeline.Digest(
            FactHygiene.Clean(new GatheredFacts([new FactPayload.Source(ASource() with
            {
                ForkSlug = Poison,
            })])),
            "flight-1", T0);

        var already = FactPipeline.Digest(
            FactHygiene.Clean(new GatheredFacts([new FactPayload.Source(ASource() with
            {
                ForkSlug = Clean,
            })])),
            "flight-1", T0);

        await Assert.That(dirty.Items.Single().Digest).IsEqualTo(already.Items.Single().Digest)
            .Because("the fact as produced is the clean one, so its hash is the clean one's hash. "
                   + "A flight whose forge handed it an escape sequence and one whose forge did not "
                   + "are the same flight.");
    }

    [Test]
    public async Task Every_field_the_runner_produces_is_stripped_and_not_just_the_risky_looking_ones()
    {
        // A path is a filename somebody chose; a ref is a branch name somebody
        // chose; a tool version is whatever a binary printed. Picking the fields
        // that look external is how the one nobody thought of stays dirty.
        var clean = FactHygiene.Clean(new GatheredFacts(
        [
            new FactPayload.Source(ASource() with { Slug = Poison, ResolvedRef = Poison }),
            new FactPayload.Change(AManifest(Poison)),
            new FactPayload.Loop(ALoop() with { Reason = Poison, Executor = Poison }),
            new FactPayload.Environment(AnEnvironment()),
            new FactPayload.Transcript(ATranscript() with { Locator = Poison }),
            new FactPayload.Landing(ALanding() with { Branch = Poison }),
            new FactPayload.Digest(ADigest() with { Searches = [Poison] }),
        ]));

        // Every string on every payload, gathered and checked together: the
        // point is that no field was picked over another.
        var strings = Strings(clean).ToList();

        await Assert.That(strings).IsNotEmpty()
            .Because("with nothing collected this passes without checking anything.");

        foreach (var value in strings)
        {
            await Assert.That(value).DoesNotContain(Esc)
                .Because("one escape anywhere is a terminal somebody else can drive.");
            await Assert.That(value).DoesNotContain(Bel);
        }

        await Assert.That(strings).Contains("fix: red title")
            .Because("stripped rather than dropped: the value still says what it said.");
    }

    [Test]
    public async Task A_fact_type_nobody_added_to_the_strip_fails_loudly()
    {
        // Article XI. The failure mode this class exists to prevent is a NEW
        // fact type quietly skipping it, so the fall-through throws and names
        // the type rather than passing the payload along unexamined.
        var thrown = Assert.Throws<InvalidOperationException>(() =>
            FactHygiene.Clean(new GatheredFacts([new UnknownPayload()])));

        await Assert.That(thrown!.Message).Contains("UnknownPayload");
        await Assert.That(thrown.Message).Contains("FactHygiene");
    }

    [Test]
    public async Task A_loops_reason_keeps_its_line_breaks_because_a_person_reads_it()
    {
        // The one field that is prose. Everywhere else a newline is a value
        // pretending to be two values; here it is formatting somebody wrote.
        var clean = FactHygiene.Clean(new GatheredFacts(
            [new FactPayload.Loop(ALoop() with { Reason = $"line one\nline two{Esc}[31m" })]));

        var reason = ((FactPayload.Loop)clean.Items.Single()).Value.Reason;

        await Assert.That(reason).IsEqualTo("line one\nline two");
    }

    [Test]
    public async Task What_the_runner_strips_is_exactly_what_the_control_plane_checks()
    {
        // TWO LISTS THAT MUST NOT DRIFT. The runner strips fields; the control
        // plane re-validates them through the contract's own rule. If hygiene
        // misses a field the detector checks, an honest flight is refused; if
        // the detector misses one hygiene cleans, a modified runner gets it
        // through. Neither side can be trusted to remember the other.
        var poisoned = new GatheredFacts(
        [
            new FactPayload.Source(ASource() with
            {
                Slug = Poison, RequestedRef = Poison, ResolvedRef = Poison,
                HeadCommit = Poison, Provider = Poison, ForkSlug = Poison,
            }),
            new FactPayload.Change(AManifest(Poison)),
            new FactPayload.Loop(ALoop() with { Reason = Poison, Executor = Poison, LoopId = Poison }),
            new FactPayload.Environment(AnEnvironment() with { HostFingerprint = Poison }),
            new FactPayload.Transcript(ATranscript() with { Locator = Poison, MediaType = Poison }),
            new FactPayload.Landing(ALanding() with { Branch = Poison, PullRequestUri = Poison }),
            new FactPayload.Digest(ADigest() with { Searches = [Poison], StopReason = LoopOutcomes.Completed }),
        ]);

        // Dirty, and the detector says so - which is the liveness half: without
        // it, an inert detector would make the assertion below meaningless.
        var dirty = FactPipeline.Digest(new CleanFacts(poisoned.Items), "flight-1", T0);

        await Assert.That(dirty.Items.Select(FactCleanliness.Unclean).Any(d => d is not null))
            .IsTrue()
            .Because("the poison has to be visible to the detector, or this proves nothing.");

        // Cleaned by the runner, and the detector finds nothing.
        var cleaned = FactPipeline.Digest(FactHygiene.Clean(poisoned), "flight-1", T0);

        foreach (var fact in cleaned.Items)
        {
            await Assert.That(FactCleanliness.Unclean(fact)).IsNull()
                .Because($"the runner stripped {fact.Kind} and the control plane still objects, so "
                       + "one of the two field lists has drifted from the other.");
        }
    }

    // ---- the live channel, which inherits nothing ----

    [Test]
    public async Task The_live_stream_is_clean_and_it_is_a_different_path_from_the_facts()
    {
        // Asserted SEPARATELY because it is stored nowhere: a rule about what is
        // stored gives it nothing, and it is the path that carries agent output
        // straight to a terminal.
        var path = Path.Combine(
            Path.GetTempPath(), "gg-hygiene-" + Guid.NewGuid().ToString("N")[..8], "live.ndjson");

        new LiveStream(path).Append(LiveLineKinds.Tool, Poison);

        var written = File.ReadAllText(path);

        await Assert.That(written).DoesNotContain(Esc);
        await Assert.That(written).DoesNotContain(Bel);
        await Assert.That(written).Contains("red title");

        Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
    }

    [Test]
    public async Task The_digest_extractor_strips_what_it_pulls_out_of_the_stream()
    {
        // The third producer. It reads the agent's own output, which is the
        // least trustworthy text on the machine.
        var stream =
            "{\"type\":\"assistant\",\"message\":{\"content\":[{\"type\":\"tool_use\","
          + "\"name\":\"Grep\",\"input\":{\"pattern\":\"\\u001b]0;pwned\\u0007slug\"}}]}}";

        var digest = TranscriptDigest.Extract(
            stream, "implement", ["/work/tree"], LoopOutcomes.Completed, refused: []);

        await Assert.That(digest.Searches.Single()).DoesNotContain(Esc);
        await Assert.That(digest.Searches.Single()).Contains("slug");
    }

    /// <summary>
    /// Every string reachable from these payloads.
    /// </summary>
    /// <remarks>
    /// Walked rather than listed, so a field added later is covered without
    /// anybody remembering to add it here. This is a test, so reflection is
    /// free; the shipped path has none.
    /// </remarks>
    private static IEnumerable<string> Strings(CleanFacts clean)
    {
        var seen = new HashSet<object>(ReferenceEqualityComparer.Instance);

        foreach (var payload in clean.Items)
        {
            foreach (var value in Walk(payload, seen, depth: 0))
            {
                yield return value;
            }
        }
    }

    private static IEnumerable<string> Walk(object? node, HashSet<object> seen, int depth)
    {
        if (node is null || depth > 6)
        {
            yield break;
        }

        if (node is string text)
        {
            yield return text;
            yield break;
        }

        if (node is System.Collections.IEnumerable list)
        {
            foreach (var item in list)
            {
                foreach (var value in Walk(item, seen, depth + 1))
                {
                    yield return value;
                }
            }

            yield break;
        }

        if (!node.GetType().Namespace!.StartsWith("Gg.", StringComparison.Ordinal)
            || !seen.Add(node))
        {
            yield break;
        }

        foreach (var property in node.GetType().GetProperties())
        {
            object? value;
            try
            {
                value = property.GetValue(node);
            }
            catch (Exception)
            {
                continue;
            }

            foreach (var found in Walk(value, seen, depth + 1))
            {
                yield return found;
            }
        }
    }

    // ---- fixtures ----

    private sealed record UnknownPayload : FactPayload;

    private static SourceProvenance ASource() => new()
    {
        Provider = "forge",
        Slug = "acme/widgets",
        RequestedRef = "refs/heads/main",
        ResolvedRef = "refs/heads/main",
        HeadCommit = new string('a', 40),
        HeadIsFork = false,
        FileCount = 3,
        Bytes = 100,
    };

    private static ChangeManifest AManifest(string path) => new()
    {
        BaseCommit = new string('e', 40),
        HeadCommit = new string('f', 40),
        Resolution = ChangeResolution.Files,
        DiffBasis = DiffBasis.TwoPoint,
        Paths =
        [
            new ChangedPath
            {
                Path = path,
                Change = ChangeKinds.Modified,
                LinesAdded = 1,
                LinesRemoved = 0,
                Classification = Classifications.Public,
            },
        ],
        Directories = [],
        Languages = [],
        FilesChanged = 1,
        LinesAdded = 1,
        LinesRemoved = 0,
        PathsWithheld = 0,
    };

    private static LoopOutcome ALoop() => new()
    {
        LoopId = "implement",
        Outcome = LoopOutcomes.Completed,
        Reason = "done",
        Executor = ExecutorRungs.Frontier,
        Attempts = 1,
        DurationMs = 10,
        MovesUsed = [LoopMoves.Read],
    };

    private static EnvironmentIdentity AnEnvironment() => new()
    {
        HostFingerprint = new string('b', 64),
        Locks = [],
        Tools = [new ToolVersion { Name = "dotnet", Version = "10.0.0" }],
        Provenance = EnvironmentProvenance.Fresh,
    };

    private static ArtifactReference ATranscript() => new()
    {
        Locator = "/state/transcript.ndjson",
        Sha256 = new string('c', 64),
        Bytes = 10,
        MediaType = "application/x-ndjson",
        Scope = ArtifactScopes.RunnerLocal,
    };

    private static DestinationLanded ALanding() => new()
    {
        DestinationId = "pull-request",
        Branch = "gg/GG-1",
        PullRequestUri = "https://forge.example/acme/widgets/pull/1",
        PullRequestNumber = 1,
    };

    private static LoopDigest ADigest() => new()
    {
        LoopId = "implement",
        FilesReadNotEdited = [],
        FilesEdited = [],
        Searches = [],
        Errors = [],
        RefusedMoves = [],
        Attempts = 1,
        StopReason = LoopOutcomes.Completed,
    };
}
