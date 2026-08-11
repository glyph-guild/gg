using System.Reflection;
using Gg.Contracts.Description;

namespace Gg.Contracts.Tests;

/// <summary>
/// A fact type that is not registered fails the build.
/// </summary>
/// <remarks>
/// <para>
/// The runner rule: <i>a fact the pinned vocabulary does not contain is
/// rejected loudly. Never accepted-and-ignored - silently absent is
/// indistinguishable from satisfied.</i> That is a claim about runtime, and it
/// is only reachable if the vocabulary is complete. This is the build-time half.
/// </para>
/// <para>
/// Four registrations have to move together for a fact to cross the boundary:
/// the pinned id, the entry in <see cref="FactKinds"/>, the JSON member
/// declaration, and the slot on <see cref="FactEnvelope"/> that carries it.
/// Three out of four is a fact that serializes to nothing, or one the other
/// side cannot name. So they are checked as a set.
/// </para>
/// </remarks>
public class FactManifestTests
{
    [Test]
    public async Task Every_fact_type_in_the_contract_is_fully_registered()
    {
        var offenders = FactManifest.Unregistered(FactManifest.FactTypesIn(typeof(FactKinds).Assembly));

        await Assert.That(offenders).IsEmpty()
            .Because("a fact type registered three ways out of four crosses the boundary as nothing. "
                   + "Found: " + string.Join("; ", offenders));
    }

    [Test]
    public async Task Every_declared_kind_has_exactly_one_type_behind_it()
    {
        // The other direction. A kind named in FactKinds with no type is a
        // string a runner could emit and nothing could ever populate.
        var types = FactManifest.FactTypesIn(typeof(FactKinds).Assembly);

        foreach (var kind in FactKinds.All)
        {
            var matching = types
                .Where(t => t.GetCustomAttribute<FactKindAttribute>()!.Kind == kind)
                .ToList();

            await Assert.That(matching.Count).IsEqualTo(1)
                .Because($"'{kind}' is named in FactKinds and {matching.Count} types claim it.");
        }
    }

    [Test]
    public async Task The_manifest_found_the_facts_that_exist()
    {
        // Every assertion here iterates a discovered set. An empty one would
        // make the whole file pass while checking nothing - and a scan that
        // finds no fact types is exactly what a broken attribute lookup returns.
        var types = FactManifest.FactTypesIn(typeof(FactKinds).Assembly);

        await Assert.That(types).IsNotEmpty();
        await Assert.That(types.Count).IsEqualTo(FactKinds.All.Count);
        await Assert.That(FactKinds.All).Contains(FactKinds.EnvironmentIdentity);
    }

    // ---- the poison twins ----
    //
    // The scan runs over the CONTRACT assembly, so a planted type in this one
    // is invisible to it. The rule is therefore a function over a set of types,
    // and the twins hand it types it would otherwise never see. Without them
    // "no offenders" is also what a scan of the wrong assembly returns.

    /// <summary>A fact nobody registered. Every one of the four is missing.</summary>
    [FactKind("planted.unregistered")]
    private sealed record PlantedUnregisteredFact
    {
        public required string Value { get; init; }
    }

    /// <summary>Registered in the manifest, with no slot on the envelope to arrive in.</summary>
    [FactKind(FactKinds.EnvironmentIdentity)]
    private sealed record PlantedFactWithNoSlot
    {
        public required string Value { get; init; }
    }

    [Test]
    public async Task The_poison_twin_an_unregistered_fact_type_is_caught()
    {
        var offenders = FactManifest.Unregistered([typeof(PlantedUnregisteredFact)]);

        await Assert.That(offenders).IsNotEmpty()
            .Because("if the rule cannot see this, the assertion above proves nothing.");
        await Assert.That(string.Join("; ", offenders)).Contains(nameof(PlantedUnregisteredFact));
    }

    [Test]
    public async Task The_poison_twin_a_fact_with_nowhere_to_arrive_is_caught()
    {
        // The subtler one, and the likelier mistake: the kind is registered, so
        // the manifest looks complete, and the envelope has no field the
        // payload could travel in. It would serialize to a digest and nothing.
        var offenders = FactManifest.Unregistered([typeof(PlantedFactWithNoSlot)]);

        await Assert.That(string.Join("; ", offenders)).Contains("envelope");
    }

    [Test]
    public async Task Every_envelope_payload_slot_belongs_to_a_declared_fact()
    {
        // And the reverse of the slot check: a nullable payload on the envelope
        // that no [FactKind] type claims is a field a runner could populate
        // with something the vocabulary does not contain.
        var declared = FactManifest.FactTypesIn(typeof(FactKinds).Assembly).ToHashSet();

        var orphans = FactManifest.PayloadSlots(typeof(FactEnvelope))
            .Where(slot => !declared.Contains(slot.PropertyType))
            .Select(slot => slot.Name)
            .ToList();

        await Assert.That(orphans).IsEmpty()
            .Because("Found: " + string.Join(", ", orphans));
        await Assert.That(FactManifest.PayloadSlots(typeof(FactEnvelope))).IsNotEmpty();
    }
}

/// <summary>
/// An envelope names one kind and carries exactly that payload.
/// </summary>
/// <remarks>
/// The same rule <see cref="FlightIntent.Validate"/> enforces, for the same
/// reason: a kind and a payload that disagree is a document whose meaning
/// depends on which reader saw it first.
/// </remarks>
public class FactEnvelopeTests
{
    private static EnvironmentIdentity AnEnvironment() => new()
    {
        HostFingerprint = new string('a', 64),
        ImageDigest = null,
        Locks = [new LockHash { Path = "package-lock.json", Sha256 = new string('b', 64) }],
        Tools = [new ToolVersion { Name = "git", Version = "2.50.1" }],
        Provenance = EnvironmentProvenance.Fresh,
    };

    private static FactEnvelope AnEnvelope() => new()
    {
        IdempotencyKey = "flight-1:environment.identity:1",
        Kind = FactKinds.EnvironmentIdentity,
        Digest = new string('c', 64),
        ObservedAt = new DateTimeOffset(2026, 8, 11, 12, 0, 0, TimeSpan.Zero),
        Environment = AnEnvironment(),
    };

    [Test]
    public async Task A_well_formed_envelope_validates()
    {
        await Assert.That(FactEnvelope.Validate(AnEnvelope())).IsNull();
    }

    [Test]
    public async Task A_kind_the_vocabulary_does_not_contain_is_refused_loudly()
    {
        // The runner rule, at the contract. Accepted-and-ignored is the failure
        // mode: silently absent is indistinguishable from satisfied.
        var diagnosis = FactEnvelope.Validate(AnEnvelope() with { Kind = "environment.identitee" });

        await Assert.That(diagnosis).IsNotNull();
        await Assert.That(diagnosis!).Contains("environment.identitee")
            .Because("a refusal that does not name what arrived makes somebody read their own code.");
    }

    [Test]
    public async Task An_envelope_naming_a_kind_it_does_not_carry_is_refused()
    {
        await Assert.That(FactEnvelope.Validate(AnEnvelope() with { Environment = null })).IsNotNull();
    }

    [Test]
    public async Task An_envelope_carrying_a_payload_it_did_not_name_is_refused()
    {
        // Two payloads, or the wrong one. Either way the envelope says one
        // thing and holds another, and which wins would be decided by whichever
        // reader looked first.
        var confused = AnEnvelope() with
        {
            Source = new SourceProvenance
            {
                Provider = "local",
                Slug = "acme/widgets",
                RequestedRef = "refs/heads/main",
                ResolvedRef = "refs/heads/main",
                HeadCommit = new string('d', 40),
                HeadIsFork = false,
                ForkSlug = null,
                FileCount = 3,
                Bytes = 120,
            },
        };

        await Assert.That(FactEnvelope.Validate(confused)).IsNotNull();
    }

    [Test]
    public async Task An_envelope_with_no_idempotency_key_is_refused()
    {
        // Dedupe is the control plane's, and it keys on this. An envelope
        // without one replays as a new fact every time it is retried.
        await Assert.That(FactEnvelope.Validate(AnEnvelope() with { IdempotencyKey = "  " })).IsNotNull();
    }

    [Test]
    public async Task A_digest_that_is_not_a_sha256_is_refused()
    {
        // The digest is the evidence budget's unit and the thing later analysis
        // derives from. A truncated or hand-typed one compares unequal forever.
        foreach (var malformed in (string[])["", "abc", new string('c', 63), new string('C', 64), new string('z', 64)])
        {
            await Assert.That(FactEnvelope.Validate(AnEnvelope() with { Digest = malformed })).IsNotNull()
                .Because($"'{malformed}' is not a sha256.");
        }
    }

    [Test]
    public async Task The_budget_is_declared_once_and_both_sides_read_it()
    {
        // Ingress rejects an over-budget item rather than truncating, and gg
        // must know the same number or it ships things it could have refused
        // locally.
        // Read from the constant rather than restated, so the assertion is
        // that both sides share ONE number rather than that this file agrees
        // with itself.
        var declared = FactBudget.MaxItemBytes;

        await Assert.That(declared).IsGreaterThan(0);
        await Assert.That(declared).IsLessThanOrEqualTo(64 * 1024)
            .Because("a budget nobody would ever hit is not a budget.");
    }

    [Test]
    public async Task Provenance_is_fresh_or_reused_and_nothing_else()
    {
        await Assert.That(EnvironmentProvenance.All)
            .IsEquivalentTo((string[])[EnvironmentProvenance.Fresh, EnvironmentProvenance.Reused]);

        await Assert.That(FactEnvelope.Validate(
            AnEnvelope() with { Environment = AnEnvironment() with { Provenance = "warm" } })).IsNotNull();
    }
}

/// <summary>The fact surface, as the protocol declares it.</summary>
public class FactSurfaceDeclarationTests
{
    [Test]
    public async Task Facts_are_shipped_against_a_lease_rather_than_a_flight()
    {
        // The lease is the authorisation. A runner naming a flight would be a
        // runner asserting facts about work it does not hold, and the flight
        // read surface stays developer-only for the reason it always was.
        var endpoint = ProtocolSurface.Endpoints.Single(
            e => e.Method == "POST" && e.Path == "/v1/leases/{id}/facts");

        await Assert.That(endpoint.Audience).IsEqualTo(Audience.Runner);
        await Assert.That(endpoint.Request).IsEqualTo(typeof(FactBatch));
        await Assert.That(endpoint.Response).IsEqualTo(typeof(FactBatchAccepted));
        await Assert.That(endpoint.Statuses).Contains(409)
            .Because("the generation fence refuses a runner that no longer holds this flight.");
    }

    [Test]
    public async Task A_flight_summary_carries_its_facts()
    {
        // Through the existing verb path: gg show renders them, and there is no
        // second fetch route for the console to have used.
        var members = ProtocolSurface.JsonMembers[typeof(FlightSummary)];

        await Assert.That(members).Contains("facts");
    }

    [Test]
    public async Task Every_fact_type_is_in_the_vocabulary_and_declared()
    {
        foreach (var type in (Type[])
                 [typeof(EnvironmentIdentity), typeof(SourceProvenance), typeof(LockHash),
                  typeof(ToolVersion), typeof(FactEnvelope), typeof(FactBatch),
                  typeof(FactBatchAccepted), typeof(FactRejection)])
        {
            await Assert.That(Vocabulary.Types).Contains(type);
            await Assert.That(ProtocolSurface.JsonMembers.ContainsKey(type)).IsTrue()
                .Because($"{type.Name} crosses the boundary, so its member names are declared.");
        }
    }

    [Test]
    public async Task No_fact_type_can_carry_a_file_body()
    {
        // "No source file content crosses. Only paths, counts, and hashes." The
        // control plane's own scan is the real proof; this is the cheap
        // build-time half - a member named for content is one that will hold it.
        // Words that name a file's CONTENTS. Not "source", which here names
        // the repository a fact is about - SourceProvenance carries a commit
        // and a count, and a rule that flagged it would be turned off.
        string[] contentWords = ["content", "body", "blob", "text", "diff", "patch", "data", "payload"];

        var offenders = FactManifest.FactTypesIn(typeof(FactKinds).Assembly)
            .Concat([typeof(FactEnvelope), typeof(LockHash), typeof(ToolVersion)])
            .SelectMany(t => t.GetProperties().Select(p => (Type: t, Property: p)))
            .Where(m => contentWords.Any(w => m.Property.Name.Contains(w, StringComparison.OrdinalIgnoreCase)))
            .Select(m => $"{m.Type.Name}.{m.Property.Name}")
            .ToList();

        await Assert.That(offenders).IsEmpty()
            .Because("Found: " + string.Join(", ", offenders));
    }
}
