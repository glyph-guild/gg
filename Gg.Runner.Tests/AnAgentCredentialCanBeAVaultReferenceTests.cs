using Gg.Contracts;
using Gg.Runner.Execution;

namespace Gg.Runner.Tests;

/// <summary>
/// An agent's credential may be named as a reference the machine reads, not
/// only as a file somebody put there.
/// </summary>
/// <remarks>
/// <para>
/// <b>Because a pool member cannot be given one, and the read side already
/// worked.</b> <c>MachineCredentialStore.Read</c> has routed by scheme since the
/// slot credential shipped — <c>keyvault://</c> to the vault, everything else
/// off this machine's own disk — and a member already reads its agent token
/// through that exact call, with the token placed into the child as
/// <c>CLAUDE_CODE_OAUTH_TOKEN</c> either way. The one thing missing was a way to
/// SAY so: <c>CredentialLocator.ForAgent</c> hardcodes the local prefix, and
/// <c>ClaudeAgentAuthentication</c> fixed the locator at construction.
/// </para>
/// <para>
/// <b>What that cost.</b> A member's store starts empty, the two doors that fill
/// it both write into the container, and a member token is not renewable — so
/// every twelve hours the pool replaced the member and the ceremony had to be
/// repeated against a new runner id. Measured: three members in sequence, each
/// holding for want of a login, each ending on its credential having taken no
/// work.
/// </para>
/// <para>
/// <b>Absent keeps today's behaviour, deliberately.</b> Every machine with a
/// token already placed derives the same local locator it always did; this is a
/// way to name somewhere else, not a change to where the default is.
/// </para>
/// <para>
/// <b>And a bare value is refused without being echoed.</b> That ordering is the
/// slot credential's own rule: the reference check runs before anything prints
/// the value, because the refusal is exactly the moment somebody has pasted a
/// token where a name belongs, and a diagnosis quoting it would put the secret
/// in a console and a flight log.
/// </para>
/// </remarks>
public class AnAgentCredentialCanBeAVaultReferenceTests
{
    private const string Declared = "claude=/usr/local/bin/claude";

    private const string AVaultReference =
        "keyvault://kv-example.vault.azure.net/agent-claude";

    /// <summary>Token-shaped, and it must never appear in a diagnosis.</summary>
    private const string APastedToken =
        "sk-ant-oat01-ZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZ";

    [Test]
    public async Task A_declared_reference_is_what_the_agent_reads()
    {
        var agent = ExecutorConfiguration.AgentFromEnvironment(
            declaration: Declared, credential: AVaultReference);

        await Assert.That(agent!.Locator).IsEqualTo(AVaultReference)
            .Because("the store routes on the scheme, so naming a vault reference here is the "
                   + "whole of reading the token from a vault - nothing else has to change.");
    }

    [Test]
    public async Task Absent_keeps_the_local_locator_every_machine_already_uses()
    {
        var agent = ExecutorConfiguration.AgentFromEnvironment(
            declaration: Declared, credential: null);

        await Assert.That(agent!.Locator).IsEqualTo(CredentialLocator.ForAgent("claude"))
            .Because("a machine with a token already placed must keep deriving the same file. "
                   + "This names somewhere else; it does not move the default.");
    }

    [Test]
    public async Task A_local_agent_locator_may_be_named_outright()
    {
        var agent = ExecutorConfiguration.AgentFromEnvironment(
            declaration: Declared, credential: CredentialLocator.ForAgent("claude"));

        await Assert.That(agent!.Locator).IsEqualTo(CredentialLocator.ForAgent("claude"))
            .Because("the default spelled out loud is not a different setting, and refusing it "
                   + "would make the explicit form of today's behaviour an error.");
    }

    [Test]
    public async Task A_pasted_token_is_refused()
    {
        var refused = Assert.Throws<InvalidOperationException>(
            () => ExecutorConfiguration.AgentFromEnvironment(
                declaration: Declared, credential: APastedToken));

        await Assert.That(refused).IsNotNull()
            .Because("a value where a name belongs is the one mistake this setting invites, and "
                   + "accepting it would read the secret as a file path and then hold for ever "
                   + "because nothing is there.");
    }

    [Test]
    public async Task And_the_refusal_does_not_repeat_the_value()
    {
        var refused = Assert.Throws<InvalidOperationException>(
            () => ExecutorConfiguration.AgentFromEnvironment(
                declaration: Declared, credential: APastedToken));

        await Assert.That(refused!.Message).DoesNotContain(APastedToken)
            .Because("the slot credential's rule, for its reason: the refusal is the moment a "
                   + "token has been pasted, and a diagnosis quoting it puts the secret into a "
                   + "console and a flight log.");
        await Assert.That(refused.Message).Contains(ExecutorConfiguration.CredentialVariable)
            .Because("it has to say which setting to fix, and the variable's name is the one "
                   + "part of this that is safe to print.");
    }

    [Test]
    public async Task A_local_locator_outside_the_agent_namespace_is_refused()
    {
        // ForRepo reduces a slug through the same character set, and the two
        // derivations are disjoint only because one refuses the other's
        // namespace. A repository's locator named here would read a file the
        // tracker's credential owns.
        var refused = Assert.Throws<InvalidOperationException>(
            () => ExecutorConfiguration.AgentFromEnvironment(
                declaration: Declared, credential: "local:repo/jdx-jdnext"));

        await Assert.That(refused).IsNotNull();
    }

    [Test]
    public async Task A_malformed_vault_reference_is_refused_rather_than_read_later()
    {
        var refused = Assert.Throws<InvalidOperationException>(
            () => ExecutorConfiguration.AgentFromEnvironment(
                declaration: Declared, credential: "keyvault://not-a-vault"));

        await Assert.That(refused).IsNotNull()
            .Because("a reference that cannot be parsed fails at the first probe instead, which "
                   + "is a machine that holds for ever with a diagnosis about a vault rather "
                   + "than about the line somebody typed.");
    }
}
