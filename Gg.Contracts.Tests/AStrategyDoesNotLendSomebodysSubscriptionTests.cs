using Gg.Contracts.Authoring;

namespace Gg.Contracts.Tests;

/// <summary>
/// A strategy does not name where an agent credential is, because the credential
/// is a person's and the strategy is the tenant's.
/// </summary>
/// <remarks>
/// <para>
/// <b>Withdrawn, having shipped in 0.244.0 and been declared by nobody.</b> The
/// owner named the collision: an agent token is attached to a person's account,
/// and a member is <i>"THE TENANT'S, by construction: a pool warms it, a strategy
/// bounds it and twelve hours end it, so nobody claims one"</i>. A tenant-scoped
/// document naming a person's credential lends that subscription on their behalf
/// — and this product already says why that cannot be: <c>GG_ALLOWANCE</c> is
/// <i>"a name its owner chose, never an account. Unset means this machine reports
/// none, because an allowance nobody named is one nobody agreed to lend."</i>
/// </para>
/// <para>
/// <b>It was worse than what it replaced, in one exact way.</b>
/// <c>gg credential send --runner … --agent claude</c> is a person, once, for one
/// container, and it is spent when that container dies. A field on the strategy
/// made the same lending standing, pool-wide, and performable by anyone who can
/// apply a document.
/// </para>
/// <para>
/// <b>And it broke attribution.</b> <c>AllowanceSummary</c> carries the name its
/// owner chose and the runners spending it; members reading a strategy-declared
/// token would spend an allowance nobody attached them to, so a floor kept back
/// for somebody else protects nothing.
/// </para>
/// <para>
/// <b>REFUSED, NOT IGNORED, and that is the whole of this test.</b> The strategy
/// reader admits a closed set of keys, so withdrawing the member is what makes a
/// document that names it fail at the door. Silently dropping the line would be
/// the worse outcome by far: a tenant would write it, apply it, read it back in
/// their own working copy, and believe their pool had a credential it never got.
/// </para>
/// <para>
/// <b>What stays, because only the SOURCE was wrong.</b>
/// <c>MemberCredentialIssued.AgentLocator</c> — a member still has to be told
/// where to read, whatever decides it — and <c>GG_AGENT_LOCATOR</c>, by which a
/// machine names its own. What replaces this is an allowance a person lends, which
/// is an act of theirs rather than a line in somebody else's document.
/// </para>
/// </remarks>
public class AStrategyDoesNotLendSomebodysSubscriptionTests
{
    private const string AStrategyNamingOne = """
        kind: docker-host
        environment: ui
        inventory:
          pool: gg-pool-ui
          size: 2
          warm: 2
        pull-point: resident-runner
        image: "127.0.0.1:5000/gg-member-browser@sha256:7249a4ca005782263b53b7d560c1178bd7127ee0a03307dd3d03b3c3e21c6e2c"
        agent-locator: "keyvault://a-vault.example.invalid/agent-claude"
        bounds:
          pool-max: 2
        """;

    [Test]
    public async Task A_strategy_that_names_an_agent_locator_is_refused()
    {
        var parsed = EnvelopeYaml.ParseStrategy(AStrategyNamingOne);

        await Assert.That(parsed.Diagnosis).IsNotNull()
            .Because("the reader admits a closed set of keys, so the withdrawal is what makes a "
                   + "document naming this fail at the door - and failing is the point. A line "
                   + "silently dropped would let a tenant write it, apply it, read it back and "
                   + "believe their pool had a credential it never got.");
    }

    [Test]
    public async Task And_the_refusal_names_the_key_so_somebody_can_find_it()
    {
        var parsed = EnvelopeYaml.ParseStrategy(AStrategyNamingOne);

        await Assert.That(parsed.Diagnosis!).Contains("agent-locator")
            .Because("whoever wrote the line has to be told which line, because the remedy is "
                   + "not a correction to it - it is somewhere else entirely.");
    }

    [Test]
    public async Task A_strategy_that_names_none_is_untouched()
    {
        var parsed = EnvelopeYaml.ParseStrategy(
            AStrategyNamingOne.Replace(
                "agent-locator: \"keyvault://a-vault.example.invalid/agent-claude\"\n",
                string.Empty,
                StringComparison.Ordinal));

        await Assert.That(parsed.Diagnosis).IsNull()
            .Because("every strategy in force names none, and the withdrawal must not touch a "
                   + "single one of them.");
    }
}
