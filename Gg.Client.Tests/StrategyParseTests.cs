using Gg.Contracts.Authoring;
using Gg.Contracts;

namespace Gg.Client.Tests;

/// <summary>
/// The strategy's parser: a closed root, a required pull point, and no key
/// that could hold a host.
/// </summary>
/// <remarks>
/// <para>
/// <b>The role comes from which door was knocked on</b>, the narrowing's rule
/// again: <c>kind:</c> here names the infrastructure row (docker-host), never
/// the document's role — whoever calls <c>ParseStrategy</c> decided that.
/// </para>
/// <para>
/// <b>A strategy naming no pull point is refused for THAT reason.</b> A
/// powered-off pool cannot pull, so the refusal happens at authoring, not at
/// 2 a.m. — and the poison twin below proves the refusal is the named one
/// rather than an incidental required-key error.
/// </para>
/// <para>
/// <b>No key can hold a host, a socket or a credential.</b> The closed key
/// set is the refusal: <c>host:</c>, <c>socket:</c>, <c>daemon:</c> and
/// <c>credential:</c> are refused by not being admitted, because which host a
/// runner's credential goes to must never be a policy edit here — the
/// repository registration's rule, one document over.
/// </para>
/// </remarks>
public class StrategyParseTests
{
    private const string Valid = """
        kind: docker-host
        environment: aspire-payments
        inventory:
          pool: payments-pool
          size: 3
        pull-point: resident-runner
        image: ghcr.io/example/env@sha256:6c3c624b58dbbcd3c0dd82b4c53f04194d1247c6eebdaab7c610cf7d66709b3b
        bounds:
          pool-max: 2
          active-hours: "08:00-20:00Z"
        """;

    [Test]
    public async Task The_strategy_parser_accepts_what_its_author_writes()
    {
        var parsed = EnvelopeYaml.ParseStrategy(Valid);

        await Assert.That(parsed.Diagnosis).IsNull();
        var strategy = parsed.Strategy!;
        await Assert.That(strategy.Kind).IsEqualTo(StrategyKinds.DockerHost);
        await Assert.That(strategy.Environment).IsEqualTo("aspire-payments");
        await Assert.That(strategy.Inventory.Pool).IsEqualTo("payments-pool");
        await Assert.That(strategy.Inventory.Size).IsEqualTo(3);
        await Assert.That(strategy.PullPoint).IsEqualTo(PullPoints.ResidentRunner);
        await Assert.That(strategy.Image).Contains("@sha256:");
        await Assert.That(strategy.Bounds.PoolMax).IsEqualTo(2);
        await Assert.That(strategy.Bounds.ActiveHours).IsEqualTo("08:00-20:00Z");
    }

    [Test]
    public async Task Bounds_are_optional_in_text_and_default_to_the_inventory()
    {
        var parsed = EnvelopeYaml.ParseStrategy("""
            kind: docker-host
            environment: aspire-payments
            inventory:
              pool: payments-pool
              size: 3
            pull-point: resident-runner
            image: ghcr.io/example/env@sha256:6c3c624b58dbbcd3c0dd82b4c53f04194d1247c6eebdaab7c610cf7d66709b3b
            """);

        await Assert.That(parsed.Diagnosis).IsNull();
        await Assert.That(parsed.Strategy!.Bounds.PoolMax).IsEqualTo(3)
            .Because("an undeclared pool-max is the inventory itself - the size is the "
                   + "outermost bound a pool can have, never an unbounded default");
        await Assert.That(parsed.Strategy!.Bounds.ActiveHours).IsNull();
    }

    [Test]
    public async Task A_strategy_naming_no_pull_point_is_refused_for_that_reason()
    {
        var parsed = EnvelopeYaml.ParseStrategy("""
            kind: docker-host
            environment: aspire-payments
            inventory:
              pool: payments-pool
              size: 3
            image: ghcr.io/example/env@sha256:6c3c624b58dbbcd3c0dd82b4c53f04194d1247c6eebdaab7c610cf7d66709b3b
            """);

        await Assert.That(parsed.Strategy).IsNull();
        await Assert.That(parsed.Diagnosis!).Contains("pull point")
            .Because("a powered-off pool cannot pull, and the author finds that out here");
        await Assert.That(parsed.Diagnosis!).Contains("pull-point")
            .Because("the refusal names the key an author must add");
    }

    /// <summary>
    /// The poison twin: a document missing a DIFFERENT required key is not
    /// refused with the pull-point's wording, so the named refusal is the
    /// pull point's own rather than a generic required-key error wearing it.
    /// </summary>
    [Test]
    public async Task The_pull_point_refusal_is_its_own_and_not_a_generic_missing_key()
    {
        var missingImage = EnvelopeYaml.ParseStrategy("""
            kind: docker-host
            environment: aspire-payments
            inventory:
              pool: payments-pool
              size: 3
            pull-point: resident-runner
            """);

        await Assert.That(missingImage.Strategy).IsNull();
        await Assert.That(missingImage.Diagnosis!).DoesNotContain("pull point")
            .Because("a missing image is a missing image; if this wording leaked, the "
                   + "pull-point refusal would be a generic error wearing a name");
    }

    [Test]
    [Arguments("host: tcp://10.0.0.4:2376")]
    [Arguments("socket: /var/run/docker.sock")]
    [Arguments("daemon: unix:///var/run/docker.sock")]
    [Arguments("credential: registry-pull-token")]
    public async Task A_key_that_could_hold_a_host_or_secret_is_refused_by_the_closed_set(
        string plantedLine)
    {
        var parsed = EnvelopeYaml.ParseStrategy(Valid + "\n" + plantedLine);

        await Assert.That(parsed.Strategy).IsNull();
        var plantedKey = plantedLine.Split(':')[0];
        await Assert.That(parsed.Diagnosis!).Contains(plantedKey)
            .Because("refused naming the key, not silently dropped - a line that looks "
                   + "load-bearing and does nothing is how a field becomes folklore");
    }

    [Test]
    public async Task An_unknown_pull_point_is_refused_naming_the_closed_set()
    {
        var parsed = EnvelopeYaml.ParseStrategy(Valid.Replace(
            "pull-point: resident-runner", "pull-point: carrier-pigeon"));

        await Assert.That(parsed.Strategy).IsNull();
        await Assert.That(parsed.Diagnosis!).Contains("carrier-pigeon");
        await Assert.That(parsed.Diagnosis!).Contains(PullPoints.ResidentRunner)
            .Because("the refusal offers what an author may write instead");
    }

    [Test]
    public async Task An_unpinned_image_is_refused_because_reset_needs_a_fixed_point()
    {
        var parsed = EnvelopeYaml.ParseStrategy(Valid.Replace(
            "image: ghcr.io/example/env@sha256:6c3c624b58dbbcd3c0dd82b4c53f04194d1247c6eebdaab7c610cf7d66709b3b",
            "image: ghcr.io/example/env:latest"));

        await Assert.That(parsed.Strategy).IsNull();
        await Assert.That(parsed.Diagnosis!).Contains("@sha256:")
            .Because("what reset resets TO must be a digest, or the reset converges on "
                   + "whatever the tag means today");
    }
}
