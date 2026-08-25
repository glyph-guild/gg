using System.Reflection;
using Gg.Contracts;

namespace Gg.Contracts.Tests;

/// <summary>
/// The attestation carries digests, hashes and stamps only — and its own
/// Validate refuses what the ledger must never hold.
/// </summary>
/// <remarks>
/// <para>
/// <b>The strategy containment's rule, on the wire that crosses hourly.</b>
/// An attestation describes what a maintenance action did to a container,
/// never what is inside one — no member's type can carry code, a credential
/// or a host, and no member is named for the thing it must not hold.
/// </para>
/// <para>
/// <b>A failure that cannot say why escalates nothing.</b> The diagnosis is
/// required exactly when the outcome is failed, because the failed
/// attestation is the discriminator between the two maintenance tiers and an
/// empty one would escalate a mystery.
/// </para>
/// </remarks>
public class PoolAttestationTests
{
    private static IReadOnlyList<Type> PoolTypes { get; } =
    [
        typeof(PoolAttestation),
        typeof(PoolAction),
        typeof(PoolActionList),
        typeof(PoolStatus),
        typeof(PoolLedger),
    ];

    private static readonly string[] ForbiddenWords =
        ["host", "socket", "daemon", "endpoint", "address", "token", "secret",
         "password", "credential", "apikey", "privatekey", "accesskey"];

    private static bool IsAllowedShape(Type type) =>
        type == typeof(string)
        || type == typeof(int)
        || type == typeof(bool)
        || type == typeof(Guid)
        || type == typeof(DateTimeOffset)
        || Nullable.GetUnderlyingType(type) is { } inner && IsAllowedShape(inner)
        || PoolTypes.Contains(type)
        || type == typeof(LockHash)
        || (type.IsGenericType
            && type.GetGenericTypeDefinition() == typeof(IReadOnlyList<>)
            && IsAllowedShape(type.GetGenericArguments()[0]));

    private static IEnumerable<PropertyInfo> MembersOf(Type type) =>
        type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.Name != "EqualityContract");

    [Test]
    public async Task No_pool_member_has_a_shape_that_could_carry_code_or_a_secret()
    {
        var offending = PoolTypes
            .SelectMany(t => MembersOf(t).Select(p => (Type: t, Property: p)))
            .Where(m => !IsAllowedShape(m.Property.PropertyType))
            .Select(m => $"{m.Type.Name}.{m.Property.Name}: {m.Property.PropertyType.Name}")
            .ToList();

        await Assert.That(offending).IsEmpty();
    }

    [Test]
    public async Task No_pool_member_is_named_for_the_thing_it_must_not_hold()
    {
        var offending = PoolTypes
            .SelectMany(t => MembersOf(t).Select(p => (Type: t, Property: p)))
            .Where(m => ForbiddenWords.Any(w =>
                m.Property.Name.Contains(w, StringComparison.OrdinalIgnoreCase)))
            .Select(m => $"{m.Type.Name}.{m.Property.Name}")
            .ToList();

        await Assert.That(offending).IsEmpty()
            .Because("the daemon's endpoint lives in the resident runner's environment "
                   + "and nowhere on the wire.");
    }

    [Test]
    public async Task An_unknown_action_is_refused_naming_it()
    {
        var diagnosis = PoolAttestation.Validate(WellFormed() with { Action = "power-on" });

        await Assert.That(diagnosis!).Contains("power-on");
    }

    [Test]
    public async Task An_unknown_outcome_is_refused_naming_it()
    {
        var diagnosis = PoolAttestation.Validate(WellFormed() with { Outcome = "mostly-fine" });

        await Assert.That(diagnosis!).Contains("mostly-fine");
    }

    [Test]
    public async Task A_failure_without_a_diagnosis_is_refused()
    {
        var diagnosis = PoolAttestation.Validate(WellFormed() with
        {
            Outcome = PoolOutcomes.Failed,
            Diagnosis = null,
        });

        await Assert.That(diagnosis!).Contains("diagnosis")
            .Because("the failed attestation is the discriminator between the two tiers, "
                   + "and a failure that cannot say why escalates nothing.");
    }

    [Test]
    public async Task A_non_v7_attestation_id_is_refused()
    {
        var diagnosis = PoolAttestation.Validate(WellFormed() with
        {
            AttestationId = Guid.Parse("11111111-1111-4111-8111-111111111111"),
        });

        await Assert.That(diagnosis!).Contains("UUIDv7");
    }

    [Test]
    public async Task A_well_formed_attestation_validates_clean()
    {
        await Assert.That(PoolAttestation.Validate(WellFormed())).IsNull();
    }

    private static PoolAttestation WellFormed() => new()
    {
        AttestationId = Guid.Parse("01890a5d-ac96-774b-bcce-b302099a8057"),
        Pool = "payments-pool",
        Action = PoolActions.Verify,
        Outcome = PoolOutcomes.Verified,
        ImageDigest = "sha256:6c3c624b58dbbcd3c0dd82b4c53f04194d1247c6eebdaab7c610cf7d66709b3b",
        Provenance = EnvironmentProvenance.Reused,
        ScopeProbedAt = DateTimeOffset.Parse("2026-08-25T10:00:00Z"),
        MeasuredAt = DateTimeOffset.Parse("2026-08-25T10:00:03Z"),
    };
}
