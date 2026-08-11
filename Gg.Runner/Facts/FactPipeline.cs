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
}

/// <summary>Stage one's output: observed, undigested, unfiltered.</summary>
public sealed record GatheredFacts(IReadOnlyList<FactPayload> Items);

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
    public static DigestedFacts Digest(GatheredFacts gathered, string flightId, DateTimeOffset observedAt)
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

        return new FilteredFacts([.. digested.Items.Where(item => !OverBudget(item))]);
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
