using Gg.Contracts;

namespace Gg.Contracts.Tests;

/// <summary>
/// An agent's own credential has a locator, derived one way on every machine.
/// </summary>
/// <remarks>
/// <para>
/// <b>Derived, for <c>ForRepo</c>'s reason:</b> <i>"two derivations that agree
/// today is how a runner ends up looking for a file the CLI never wrote."</i>
/// The console that sends an agent's token and the executor that reads it back
/// must spell the locator identically, so neither spells it — both ask.
/// </para>
/// <para>
/// <b>Refused rather than reduced.</b> <c>ForRepo</c> lowercases and collapses
/// a slug, because a slug is prose somebody typed. An agent's name is a key —
/// the same key <c>GG_EXECUTOR_BINARY</c> is declared with — and a key that
/// does not validate as a locator segment is a mistake to name, not a value to
/// tidy.
/// </para>
/// <para>
/// <b>Not a wire type, and this file is the proof.</b> <c>CredentialLocator</c>
/// is a static helper; the contract fingerprint hashes the vocabulary's
/// properties, so this moves no version. What crosses is the locator STRING,
/// inside <c>ConfigureCredentialAsk</c>, which already exists.
/// </para>
/// </remarks>
public class AnAgentHasALocatorTests
{
    [Test]
    public async Task The_claude_agents_locator_is_under_agent()
    {
        await Assert.That(CredentialLocator.ForAgent("claude")).IsEqualTo("local:agent/claude");
    }

    [Test]
    public async Task It_validates_as_a_locator()
    {
        // The whole point: a derived locator the store would refuse is a
        // credential nobody can send.
        await Assert.That(CredentialLocator.Validate(CredentialLocator.ForAgent("claude")))
            .IsNull();
    }

    [Test]
    public async Task A_name_that_is_not_a_locator_segment_is_refused_naming_it()
    {
        foreach (var name in new[] { "Claude", "co dex", "../etc", "" })
        {
            var refused = Assert.Throws<ArgumentException>(() => CredentialLocator.ForAgent(name));

            await Assert.That(refused.Message).Contains("agent")
                .Because($"'{name}' is a key rather than a slug: tidying it would let the "
                       + "console and the executor derive two different locators from "
                       + "two spellings of one agent.");
        }
    }

    [Test]
    public async Task The_agent_segment_is_reserved_so_a_repository_cannot_derive_the_same_file()
    {
        // FOUND WHILE WRITING THE TEST ABOVE. ForRepo reduces a slug through
        // the same character set a locator validates, so 'agent/claude' as a
        // repository slug derived the identical string - and then a forge
        // credential and the agent's token were one file, whichever was
        // written last. The two derivations are disjoint only if one of them
        // refuses the other's namespace, and the repository side is the one
        // that takes prose.
        var refused = Assert.Throws<ArgumentException>(() =>
            CredentialLocator.ForRepo("agent/claude"));

        await Assert.That(refused.Message).Contains(CredentialLocator.AgentSegment)
            .Because("the refusal names the reserved word, so somebody with a repository "
                   + "owner really called that learns why rather than getting a file "
                   + "an agent's token could overwrite.");
    }

    [Test]
    public async Task Any_other_owner_still_derives_as_it_always_did()
    {
        // The anchor: the reservation is one word, not a change to slugs.
        await Assert.That(CredentialLocator.ForRepo("agents/claude")).IsEqualTo("local:agents/claude");
        await Assert.That(CredentialLocator.ForRepo("acme/agent")).IsEqualTo("local:acme/agent");
    }
}
