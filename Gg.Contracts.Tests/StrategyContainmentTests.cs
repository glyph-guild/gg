using System.Reflection;
using Gg.Contracts;

namespace Gg.Contracts.Tests;

/// <summary>
/// No member of the strategy document can hold a host, a socket path or a
/// credential — the boundary by shape, not by string.
/// </summary>
/// <remarks>
/// <para>
/// <b>The repository registration's rule, one document over.</b> A strategy
/// governs a pool on a customer's host, and which host the resident runner's
/// credential goes to must never be a policy edit in the control plane. The
/// precedent is <see cref="CredentialContainmentTests"/>: the hole is not a
/// badly named string, it is <c>object</c>, <c>JsonElement</c> or a
/// string-to-string map, any of which carries a socket path while passing a
/// name check — so the property SHAPES are the closed set.
/// </para>
/// <para>
/// The name check is here too, narrower than the shape check and aimed at
/// the words that would name the thing itself: a member called
/// <c>host</c>, <c>socket</c>, <c>daemon</c>, <c>endpoint</c> or a
/// secret-shaped word is an offence even as a string, because the endpoint
/// lives in the resident runner's environment (<c>GG_POOL_ENDPOINT</c>) and
/// nowhere on the wire.
/// </para>
/// </remarks>
public class StrategyContainmentTests
{
    private static IReadOnlyList<Type> StrategyTypes { get; } =
    [
        typeof(EnvironmentStrategy),
        typeof(StrategyInventory),
        typeof(StrategyBounds),
    ];

    private static readonly string[] ForbiddenWords =
        ["host", "socket", "daemon", "endpoint", "address", "token", "secret",
         "password", "credential", "apikey", "privatekey", "accesskey"];

    private static bool IsAllowedShape(Type type) =>
        type == typeof(string)
        || type == typeof(int)
        || type == typeof(bool)
        || type == typeof(DateTimeOffset)
        || Nullable.GetUnderlyingType(type) is { } inner && IsAllowedShape(inner)
        || StrategyTypes.Contains(type)
        || (type.IsGenericType
            && type.GetGenericTypeDefinition() == typeof(IReadOnlyList<>)
            && IsAllowedShape(type.GetGenericArguments()[0]));

    private static IEnumerable<PropertyInfo> MembersOf(Type type) =>
        type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.Name != "EqualityContract");

    [Test]
    public async Task No_strategy_member_has_a_shape_that_could_carry_a_host_or_secret()
    {
        var offending = StrategyTypes
            .SelectMany(t => MembersOf(t).Select(p => (Type: t, Property: p)))
            .Where(m => !IsAllowedShape(m.Property.PropertyType))
            .Select(m => $"{m.Type.Name}.{m.Property.Name}: {m.Property.PropertyType.Name}")
            .ToList();

        await Assert.That(offending).IsEmpty()
            .Because("a member whose type is a free-form container carries a socket path "
                   + "while passing every name check");
    }

    [Test]
    public async Task No_strategy_member_is_named_for_the_thing_it_must_not_hold()
    {
        var offending = StrategyTypes
            .SelectMany(t => MembersOf(t).Select(p => (Type: t, Property: p)))
            .Where(m => ForbiddenWords.Any(w =>
                m.Property.Name.Contains(w, StringComparison.OrdinalIgnoreCase)))
            .Select(m => $"{m.Type.Name}.{m.Property.Name}")
            .ToList();

        await Assert.That(offending).IsEmpty()
            .Because("the endpoint lives in the resident runner's environment and nowhere "
                   + "on the wire - a member named for it is the design leaking");
    }

    [Test]
    public async Task The_strategy_role_exists_and_the_vocabulary_still_closes()
    {
        await Assert.That(Roles.All).Contains(Roles.Strategy);
        await Assert.That(Roles.All.Count).IsEqualTo(4)
            .Because("root, work-kind, narrowing, strategy - a fifth role is a design event");
    }

    [Test]
    public async Task Validate_refuses_an_unknown_kind_naming_the_closed_set()
    {
        var diagnosis = EnvironmentStrategy.Validate(WellFormed() with { Kind = "vm-fleet" });

        await Assert.That(diagnosis!).Contains("vm-fleet");
        await Assert.That(diagnosis!).Contains(StrategyKinds.DockerHost)
            .Because("the other seven rows are eight infrastructures that do not exist yet; "
                   + "the refusal names the one that does");
    }

    [Test]
    public async Task Validate_refuses_a_pool_maximum_outside_the_inventory()
    {
        var overSize = WellFormed() with
        {
            Bounds = new StrategyBounds { PoolMax = 4, ActiveHours = null },
        };

        var diagnosis = EnvironmentStrategy.Validate(overSize);

        await Assert.That(diagnosis!).Contains("pool-max")
            .Because("a bound above the inventory is a promise the inventory cannot keep");
    }

    [Test]
    public async Task Validate_refuses_malformed_active_hours_naming_the_shape()
    {
        var malformed = WellFormed() with
        {
            Bounds = new StrategyBounds { PoolMax = 2, ActiveHours = "9ish to 5ish" },
        };

        var diagnosis = EnvironmentStrategy.Validate(malformed);

        await Assert.That(diagnosis!).Contains("HH:MM-HH:MMZ")
            .Because("a schedule bound nobody can parse binds nothing");
    }

    [Test]
    public async Task A_well_formed_strategy_validates_clean()
    {
        await Assert.That(EnvironmentStrategy.Validate(WellFormed())).IsNull();
    }

    private static EnvironmentStrategy WellFormed() => new()
    {
        Kind = StrategyKinds.DockerHost,
        Environment = "aspire-payments",
        Inventory = new StrategyInventory { Pool = "payments-pool", Size = 3 },
        PullPoint = PullPoints.ResidentRunner,
        Image = "ghcr.io/example/env@sha256:6c3c624b58dbbcd3c0dd82b4c53f04194d1247c6eebdaab7c610cf7d66709b3b",
        Bounds = new StrategyBounds { PoolMax = 2, ActiveHours = "08:00-20:00Z" },
    };
}
