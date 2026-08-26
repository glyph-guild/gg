using System.Reflection;
using Gg.Client;
using Gg.Contracts;

namespace Gg.Client.Tests;

/// <summary>
/// A strategy renders, and what it renders parses back to the same model.
/// </summary>
/// <remarks>
/// <para>
/// <b>There was no renderer at all.</b> <c>EnvelopeText</c> covered envelopes and
/// narrowings; a strategy could be written and applied and never read back as
/// text, so <c>strategies/</c> — one of the four directories ADR-0016 draws —
/// could not be emitted by anything. A working copy that silently omits a
/// document class is worse than one that fails.
/// </para>
/// <para>
/// <b>And an unproven round trip is what ADR-0013 says this obligation exists to
/// prevent.</b> <c>EnvelopeText.Render</c> omitted <c>Obligation.Evidence</c>
/// once, so <c>show</c> → <c>apply</c> stripped a governance declaration and
/// nobody noticed. Pull, apply, and every declaration the renderer forgets is
/// silently gone — at estate scale, across every document a tenant has.
/// </para>
/// <para>
/// <b>The poison twins are the point.</b> A round trip that passes because both
/// sides lose the same key proves nothing, so each key is dropped in turn and the
/// comparison must notice.
/// </para>
/// </remarks>
public class StrategyRoundTripTests
{
    private static EnvironmentStrategy Full() => new()
    {
        Kind = StrategyKinds.DockerHost,
        Environment = "aspire-payments",
        Inventory = new StrategyInventory { Pool = "payments-pool", Size = 3 },
        PullPoint = PullPoints.ResidentRunner,
        Image = "ghcr.io/example/env@sha256:"
              + "6c3c624b58dbbcd3c0dd82b4c53f04194d1247c6eebdaab7c610cf7d66709b3b",
        Bounds = new StrategyBounds { PoolMax = 2, ActiveHours = "08:00-18:00Z" },
    };

    private static EnvironmentStrategy Minimal() => Full() with
    {
        Bounds = new StrategyBounds { PoolMax = 2, ActiveHours = null },
    };

    [Test]
    public async Task What_renders_parses_back_to_the_same_model()
    {
        var parsed = EnvelopeYaml.ParseStrategy(EnvelopeText.Render(Full()));

        await Assert.That(parsed.Diagnosis).IsNull()
            .Because($"the emitter's own output must parse: {parsed.Diagnosis}");
        await Assert.That(parsed.Strategy).IsEqualTo(Full());
    }

    [Test]
    public async Task An_absent_bound_stays_absent()
    {
        // Absent is a state, not a default. active-hours: null rendered as an
        // empty key would parse back as a bound nobody declared, and a bound
        // nobody declared is a wait nobody can clear.
        var text = EnvelopeText.Render(Minimal());

        await Assert.That(text).DoesNotContain("active-hours");
        await Assert.That(EnvelopeYaml.ParseStrategy(text).Strategy).IsEqualTo(Minimal());
    }

    [Test]
    [Arguments("kind")]
    [Arguments("environment")]
    [Arguments("pull-point")]
    [Arguments("image")]
    [Arguments("pool")]
    [Arguments("size")]
    [Arguments("pool-max")]
    [Arguments("active-hours")]
    public async Task A_rendering_that_dropped_this_key_would_be_caught(string key)
    {
        // THE POISON TWIN. Removing the key from the rendered text must break
        // the round trip - either the parse refuses it or the model differs.
        // A key that can go missing without either happening is a key this
        // suite is not actually testing.
        var text = EnvelopeText.Render(Full());
        var poisoned = string.Join(
            '\n', text.Split('\n').Where(line => !line.TrimStart().StartsWith(key + ":",
                StringComparison.Ordinal)));

        await Assert.That(poisoned).IsNotEqualTo(text)
            .Because($"'{key}' is not in the rendering at all, so this twin proves nothing");

        var parsed = EnvelopeYaml.ParseStrategy(poisoned);

        await Assert.That(parsed.Diagnosis is not null || parsed.Strategy != Full()).IsTrue()
            .Because($"dropping '{key}' round-tripped clean, which is how a declaration "
                   + "goes missing between a pull and the apply that follows it");
    }

    [Test]
    public async Task Every_member_of_the_strategy_schema_is_accounted_for_by_this_suite()
    {
        // THE RATCHET. A member added to the strategy contract and not rendered
        // is a field an architect writes that pull silently drops - which is the
        // evidence: defect, one document class over. The list is the claim; a
        // new member fails the build until somebody decides it is covered.
        string[] covered =
        [
            nameof(EnvironmentStrategy.Kind),
            nameof(EnvironmentStrategy.Environment),
            nameof(EnvironmentStrategy.Inventory),
            nameof(EnvironmentStrategy.PullPoint),
            nameof(EnvironmentStrategy.Image),
            nameof(EnvironmentStrategy.Bounds),
            nameof(StrategyInventory.Pool),
            nameof(StrategyInventory.Size),
            nameof(StrategyBounds.PoolMax),
            nameof(StrategyBounds.ActiveHours),
        ];

        var members = new[]
            {
                typeof(EnvironmentStrategy), typeof(StrategyInventory), typeof(StrategyBounds),
            }
            .SelectMany(t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            .Where(p => !string.Equals(p.Name, "EqualityContract", StringComparison.Ordinal))
            .Select(p => p.Name)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var unaccounted = members.Except(covered, StringComparer.Ordinal).ToList();

        await Assert.That(unaccounted).IsEmpty()
            .Because("these cross the text form with nothing proving they survive it: "
                   + string.Join(", ", unaccounted));

        var stale = covered.Except(members, StringComparer.Ordinal).ToList();
        await Assert.That(stale).IsEmpty()
            .Because("these are covered by name and no longer exist, so the list is "
                   + "reassuring about nothing: " + string.Join(", ", stale));
    }
}
