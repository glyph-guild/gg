using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Gg.Contracts;

namespace Gg.Runner.Facts;

/// <summary>One thing the runner observed, before it has a digest.</summary>
public abstract record FactPayload
{
    public sealed record Environment(EnvironmentIdentity Value) : FactPayload;

    public sealed record Source(SourceProvenance Value) : FactPayload;

    public sealed record Change(ChangeManifest Value) : FactPayload;

    /// <summary>What a loop did. Short, decidable, read first.</summary>
    public sealed record Loop(LoopOutcome Value) : FactPayload;

    /// <summary>Where the loop's transcript is, without carrying it.</summary>
    public sealed record Transcript(ArtifactReference Value) : FactPayload;

    /// <summary>Where the work landed, once a destination admitted it.</summary>
    public sealed record Landing(DestinationLanded Value) : FactPayload;

    /// <summary>
    /// A branch reached the remote and nothing was proposed.
    /// </summary>
    /// <remarks>
    /// Beside <see cref="Landing"/> rather than folded into it: the push happens under
    /// the first gate and the proposal under the second, and one payload reporting
    /// both would be a name true of one of its uses.
    /// </remarks>
    public sealed record Push(DestinationPushed Value) : FactPayload;

    /// <summary>What the loop did, for a person who will never see the transcript.</summary>
    public sealed record Digest(LoopDigest Value) : FactPayload;

    /// <summary>
    /// The work kind this loop nominated, and why.
    /// </summary>
    /// <remarks>
    /// The one payload here that is a REQUEST rather than a record of
    /// something. Everything else says what happened; this asks that a flight
    /// exist, and admission answers.
    /// </remarks>
    public sealed record Nomination(FlightNomination Value) : FactPayload;

    /// <summary>A question an agent could not answer from the work itself.</summary>
    public sealed record Question(LoopQuestion Value) : FactPayload;

    /// <summary>
    /// A session a person flew, and what holding the terminal cost the
    /// measurement.
    /// </summary>
    /// <remarks>
    /// The only payload here whose subject is an absence. It travels INSTEAD of
    /// <see cref="Loop"/> rather than beside it: a session claiming both that it
    /// measured a loop and that it could not is the contradiction the kind
    /// exists to prevent.
    /// </remarks>
    public sealed record Attended(LoopAttended Value) : FactPayload;
}

/// <summary>Stage one's output: observed, undigested, unfiltered.</summary>
public sealed record GatheredFacts(IReadOnlyList<FactPayload> Items);

/// <summary>
/// Stage one-and-a-half's output. Only <see cref="FactHygiene.Clean"/> makes one.
/// </summary>
/// <remarks>
/// A wrapper for the same reason the others are wrappers: <see
/// cref="FactPipeline.Digest"/> takes one of these and nothing else, so
/// "stripped BEFORE the digest" is enforced by what the types allow rather than
/// remembered. The hash is computed over the fact as produced, so a digest of
/// unstripped facts would be a hash of bytes that are never stored.
/// </remarks>
public sealed record CleanFacts(IReadOnlyList<FactPayload> Items);

/// <summary>
/// Stage two's output. Nothing else can produce one.
/// </summary>
/// <remarks>
/// A wrapper rather than a bare list, and that is the entire mechanism: the
/// filter takes THIS type, so it cannot be handed something undigested, and
/// only <see cref="FactPipeline.Digest"/> makes one.
/// </remarks>
public sealed record DigestedFacts(IReadOnlyList<FactEnvelope> Items);

/// <summary>
/// Stage three's output, and the only thing egress accepts.
/// </summary>
/// <remarks>
/// Same mechanism from the other end: <see cref="IRunnerProtocol.ShipFactsAsync"/>
/// takes this, so nothing unfiltered can be shipped, and only
/// <see cref="FactPipeline.Filter"/> makes one.
/// </remarks>
public sealed record FilteredFacts(IReadOnlyList<FactEnvelope> Items);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(EnvironmentIdentity))]
[JsonSerializable(typeof(SourceProvenance))]
[JsonSerializable(typeof(ChangeManifest))]
[JsonSerializable(typeof(LoopOutcome))]
[JsonSerializable(typeof(ArtifactReference))]
[JsonSerializable(typeof(DestinationLanded))]
[JsonSerializable(typeof(LoopDigest))]
[JsonSerializable(typeof(FactEnvelope))]
internal sealed partial class FactJsonContext : JsonSerializerContext;

/// <summary>
/// Digest, then filter, then egress. In that order, because the types say so.
/// </summary>
/// <remarks>
/// <para>
/// <b>The digest is computed before the filter.</b> Compute it after and every
/// later analysis derives from already-redacted material - conclusions about a
/// document nobody produced, with no way to tell from the digest that this
/// happened.
/// </para>
/// <para>
/// <b>The filter runs before egress.</b> The alternative - send everything and
/// redact at the far end - fails a security review for the obvious reason, and
/// no amount of care at the far end makes the transmission not have happened.
/// </para>
/// <para>
/// Establishing this shape now, with one harmless fact in it, means step 7 adds
/// a fact rather than a pipeline. Getting it backwards means step 7 reorders a
/// path that is already carrying real content.
/// </para>
/// </remarks>
public static class FactPipeline
{
    /// <summary>
    /// Turns observations into envelopes, each with its digest and its key.
    /// </summary>
    /// <remarks>
    /// The digest is over the payload as it was observed. The idempotency key
    /// is the flight, the kind and that digest: a replay of the same
    /// observation is a duplicate, a genuinely different observation is a new
    /// fact, and two flights in identical containers do not collide.
    /// </remarks>
    public static DigestedFacts Digest(CleanFacts gathered, string flightId, DateTimeOffset observedAt)
    {
        ArgumentNullException.ThrowIfNull(gathered);

        var envelopes = new List<FactEnvelope>(gathered.Items.Count);

        foreach (var payload in gathered.Items)
        {
            var (kind, json) = Canonical(payload);
            var digest = Sha256(json);

            envelopes.Add(payload switch
            {
                FactPayload.Environment environment => new FactEnvelope
                {
                    IdempotencyKey = Key(flightId, kind, digest),
                    Kind = kind,
                    Digest = digest,
                    ObservedAt = observedAt,
                    Environment = environment.Value,
                },
                FactPayload.Source source => new FactEnvelope
                {
                    IdempotencyKey = Key(flightId, kind, digest),
                    Kind = kind,
                    Digest = digest,
                    ObservedAt = observedAt,
                    Source = source.Value,
                },
                FactPayload.Loop loop => new FactEnvelope
                {
                    IdempotencyKey = Key(flightId, kind, digest),
                    Kind = kind,
                    Digest = digest,
                    ObservedAt = observedAt,
                    Loop = loop.Value,
                },

                FactPayload.Digest summary => new FactEnvelope
                {
                    IdempotencyKey = Key(flightId, kind, digest),
                    Kind = kind,
                    Digest = digest,
                    ObservedAt = observedAt,
                    LoopDigest = summary.Value,
                },

                FactPayload.Question question => new FactEnvelope
                {
                    IdempotencyKey = Key(flightId, kind, digest),
                    Kind = kind,
                    Digest = digest,
                    ObservedAt = observedAt,
                    Question = question.Value,
                },

                FactPayload.Attended attended => new FactEnvelope
                {
                    IdempotencyKey = Key(flightId, kind, digest),
                    Kind = kind,
                    Digest = digest,
                    ObservedAt = observedAt,
                    Attended = attended.Value,
                },

                FactPayload.Nomination nomination => new FactEnvelope
                {
                    IdempotencyKey = Key(flightId, kind, digest),
                    Kind = kind,
                    Digest = digest,
                    ObservedAt = observedAt,
                    Nomination = nomination.Value,
                },

                FactPayload.Landing landing => new FactEnvelope
                {
                    IdempotencyKey = Key(flightId, kind, digest),
                    Kind = kind,
                    Digest = digest,
                    ObservedAt = observedAt,
                    Landed = landing.Value,
                },

                FactPayload.Push push => new FactEnvelope
                {
                    IdempotencyKey = Key(flightId, kind, digest),
                    Kind = kind,
                    Digest = digest,
                    ObservedAt = observedAt,
                    Pushed = push.Value,
                },

                FactPayload.Transcript transcript => new FactEnvelope
                {
                    IdempotencyKey = Key(flightId, kind, digest),
                    Kind = kind,
                    Digest = digest,
                    ObservedAt = observedAt,
                    Transcript = transcript.Value,
                },

                FactPayload.Change change => new FactEnvelope
                {
                    IdempotencyKey = Key(flightId, kind, digest),
                    Kind = kind,
                    Digest = digest,
                    ObservedAt = observedAt,
                    Change = change.Value,
                },
                // Article XI. A payload with no envelope shape halts rather
                // than being dropped: silently absent is indistinguishable
                // from satisfied.
                _ => throw new InvalidOperationException(
                    $"'{payload.GetType().Name}' has no envelope shape. A fact nothing can carry must "
                  + "not be quietly left behind."),
            });
        }

        return new DigestedFacts(envelopes);
    }

    /// <summary>
    /// Withholds what must not leave, and passes the rest.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Today it enforces one rule: the digest budget. <b>Nothing is
    /// truncated</b> - a fact cut in half is a false fact rather than a small
    /// one - so an over-budget item is withheld whole, here, where the thing
    /// that made it can still name it.
    /// </para>
    /// <para>
    /// Classification joins in step 7, when there is content to classify. The
    /// ceiling is taken now and deliberately unused, because a stage that has
    /// to grow a parameter is a stage every caller has to be found and changed
    /// for.
    /// </para>
    /// </remarks>
    public static FilteredFacts Filter(DigestedFacts digested, string classificationCeiling)
    {
        ArgumentNullException.ThrowIfNull(digested);
        ArgumentException.ThrowIfNullOrWhiteSpace(classificationCeiling);

        // Article XI, at the control that matters most: a ceiling nobody
        // declared halts here rather than permitting everything. A typo in a
        // tenant's configuration must not be the same as switching the filter
        // off, and IsAtOrBelow throws on one.
        if (Classifications.RankOf(classificationCeiling) is null)
        {
            throw new ArgumentException(
                $"'{classificationCeiling}' is not a classification ceiling. Expected one of: "
              + string.Join(", ", Classifications.Ordered) + ".",
                nameof(classificationCeiling));
        }

        return new FilteredFacts(
            [.. digested.Items.Select(item => Withhold(item, classificationCeiling))
                              .Where(item => !OverBudget(item))]);
    }

    /// <summary>
    /// Removes what may not cross from one fact, and records that it did.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Only the manifest has classified items today. The environment fact is
    /// about the machine rather than about a customer's files, and a filter
    /// that started dropping it would be one applying a rule to something the
    /// rule is not about.
    /// </para>
    /// <para>
    /// <b>Never silently.</b> The withheld count goes up by exactly what came
    /// out, so the manifest still accounts for every file it observed - which
    /// is the invariant that makes a filtered manifest a smaller TRUE statement
    /// rather than the false one a truncation would be.
    /// </para>
    /// <para>
    /// The digest is not recomputed, and that is the point of computing it
    /// first: it describes what was observed, so a digest that no longer
    /// matches its payload is evidence that something was withheld.
    /// </para>
    /// </remarks>
    private static FactEnvelope Withhold(FactEnvelope item, string ceiling)
    {
        if (item.Change is not { } manifest || manifest.Resolution != ChangeResolution.Files)
        {
            return item;
        }

        var permitted = manifest.Paths
            .Where(p => Classifications.IsAtOrBelow(p.Classification, ceiling))
            .ToList();

        return permitted.Count == manifest.Paths.Count
            ? item
            : item with
            {
                Change = manifest with
                {
                    Paths = permitted,
                    PathsWithheld = manifest.PathsWithheld + (manifest.Paths.Count - permitted.Count),
                },
            };
    }

    /// <summary>Whether one envelope is larger than the evidence budget allows.</summary>
    /// <remarks>
    /// Measured over the serialized envelope, because that is what ingress will
    /// measure. Two different measurements of "how big is this" is how a runner
    /// ships something it believed was within budget.
    /// </remarks>
    public static bool OverBudget(FactEnvelope envelope) =>
        Encoding.UTF8.GetByteCount(
            JsonSerializer.Serialize(envelope, FactJsonContext.Default.FactEnvelope)) > FactBudget.MaxItemBytes;

    private static (string Kind, string Json) Canonical(FactPayload payload) => payload switch
    {
        FactPayload.Environment environment => (
            FactKinds.EnvironmentIdentity,
            JsonSerializer.Serialize(environment.Value, FactJsonContext.Default.EnvironmentIdentity)),
        FactPayload.Source source => (
            FactKinds.SourceProvenance,
            JsonSerializer.Serialize(source.Value, FactJsonContext.Default.SourceProvenance)),
        FactPayload.Change change => (
            FactKinds.ChangeManifest,
            JsonSerializer.Serialize(change.Value, FactJsonContext.Default.ChangeManifest)),
        FactPayload.Loop loop => (
            FactKinds.LoopOutcome,
            JsonSerializer.Serialize(loop.Value, FactJsonContext.Default.LoopOutcome)),
        FactPayload.Transcript transcript => (
            FactKinds.LoopTranscript,
            JsonSerializer.Serialize(transcript.Value, FactJsonContext.Default.ArtifactReference)),
        FactPayload.Digest summary => (
            FactKinds.LoopDigest,
            JsonSerializer.Serialize(summary.Value, FactJsonContext.Default.LoopDigest)),
        FactPayload.Nomination nomination => (
            FactKinds.FlightNomination,
            JsonSerializer.Serialize(nomination.Value, FactJsonContext.Default.FlightNomination)),

        FactPayload.Question question => (
            FactKinds.LoopQuestion,
            JsonSerializer.Serialize(question.Value, FactJsonContext.Default.LoopQuestion)),
        FactPayload.Attended attended => (
            FactKinds.LoopAttended,
            JsonSerializer.Serialize(attended.Value, FactJsonContext.Default.LoopAttended)),
        FactPayload.Landing landing => (
            FactKinds.DestinationLanded,
            JsonSerializer.Serialize(landing.Value, FactJsonContext.Default.DestinationLanded)),
        FactPayload.Push push => (
            FactKinds.DestinationPushed,
            JsonSerializer.Serialize(push.Value, FactJsonContext.Default.DestinationPushed)),
        _ => throw new InvalidOperationException(
            $"'{payload.GetType().Name}' has no canonical form, so it has no digest."),
    };

    /// <summary>
    /// The flight, the kind, and the digest.
    /// </summary>
    /// <remarks>
    /// The flight is in it because two runners in identical containers observe
    /// the same environment: without it the second flight's fact would dedupe
    /// away and that flight would have no environment recorded at all.
    /// </remarks>
    private static string Key(string flightId, string kind, string digest) =>
        $"{flightId}:{kind}:{digest}";

    private static string Sha256(string value) =>
        Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}
