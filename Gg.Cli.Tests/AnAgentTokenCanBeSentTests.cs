using Gg.Contracts;

namespace Gg.Cli.Tests;

/// <summary>
/// <c>gg credential send --runner &lt;id&gt; --agent claude</c> — the agent's
/// token goes to a runner the way a forge credential does.
/// </summary>
/// <remarks>
/// <para>
/// <b>The same verb, because it is the same act.</b> A credential this machine
/// holds - or a person types, with the echo off - travels over the sealed
/// channel to a runner's own store, under a locator both ends derive. What
/// differs is WHICH locator: a repository's is derived from a slug, an
/// agent's from the adapter key <c>GG_EXECUTOR_BINARY</c> declares. So
/// <c>--agent</c> sits beside <c>--repo</c>, and exactly one of them is given.
/// </para>
/// <para>
/// <b>Its own action rather than a nullable member on <c>CredentialSend</c>.</b>
/// That record's three members are asserted by name against secret-shaped
/// words, and a second record gets the same guard here rather than the first
/// growing an optional half that every reader of it has to null-check.
/// </para>
/// <para>
/// <b>The value is a key, validated at the parse.</b> <c>CredentialLocator.ForAgent</c>
/// refuses rather than reduces - an agent's name is not prose - and refusing
/// at the parse means the person is told before an introduction is spent on a
/// send that could not derive a locator.
/// </para>
/// </remarks>
public class AnAgentTokenCanBeSentTests
{
    private static CliAction.AgentCredentialSend Sent(params string[] args)
    {
        var action = CliArgs.Parse(args);
        return action as CliAction.AgentCredentialSend ?? throw new InvalidOperationException(
            $"'{string.Join(" ", args)}' did not parse as an agent send but as {action.GetType().Name}"
          + (action is CliAction.Unknown unknown ? $": {unknown.Message}" : "."));
    }

    [Test]
    public async Task It_names_a_runner_and_an_agent()
    {
        var sent = Sent("credential", "send", "--runner", "runner-1", "--agent", "claude");

        await Assert.That(sent.RunnerId).IsEqualTo("runner-1");
        await Assert.That(sent.Agent).IsEqualTo("claude");
    }

    [Test]
    public async Task The_runner_flag_may_come_first_as_it_does_for_a_repository()
    {
        var sent = Sent("credential", "send", "--agent", "claude", "--runner", "runner-1");

        await Assert.That(sent.RunnerId).IsEqualTo("runner-1");
    }

    [Test]
    public async Task A_repository_send_still_parses_as_it_always_did()
    {
        // THE ANCHOR. Adding a second kind of send must not take the first
        // away from every script that uses it.
        var action = CliArgs.Parse(["credential", "send", "--runner", "runner-1", "--repo", "acme/widgets"]);

        var sent = await Assert.That(action).IsTypeOf<CliAction.CredentialSend>();
        await Assert.That(sent!.Repo).IsEqualTo("acme/widgets");
    }

    [Test]
    public async Task Both_a_repository_and_an_agent_is_refused_naming_both()
    {
        // One send, one credential. Two locators would be two files and one
        // secret, and the refusal has to say which flag to drop.
        var refused = CliArgs.Parse(
            ["credential", "send", "--runner", "runner-1", "--repo", "acme/widgets", "--agent", "claude"]);

        var unknown = await Assert.That(refused).IsTypeOf<CliAction.Unknown>();
        await Assert.That(unknown!.Message).Contains("--repo", StringComparison.Ordinal);
        await Assert.That(unknown.Message).Contains("--agent", StringComparison.Ordinal);
    }

    [Test]
    public async Task Neither_is_refused_naming_both_options()
    {
        var refused = CliArgs.Parse(["credential", "send", "--runner", "runner-1"]);

        var unknown = await Assert.That(refused).IsTypeOf<CliAction.Unknown>();
        await Assert.That(unknown!.Message).Contains("--repo", StringComparison.Ordinal);
        await Assert.That(unknown.Message).Contains("--agent", StringComparison.Ordinal)
            .Because("a person who only knew about --repo learns here that an agent's token "
                   + "travels the same way.");
    }

    [Test]
    public async Task An_agent_name_that_is_not_a_locator_segment_is_refused_at_the_parse()
    {
        // ForAgent's rule, applied before any network: refused, not tidied.
        foreach (var name in new[] { "Claude", "co dex", "../etc" })
        {
            var refused = CliArgs.Parse(["credential", "send", "--runner", "runner-1", "--agent", name]);

            var unknown = await Assert.That(refused).IsTypeOf<CliAction.Unknown>();
            await Assert.That(unknown!.Message).Contains(name, StringComparison.Ordinal)
                .Because("the refusal names the value, so somebody knows which word to change.");
        }
    }

    [Test]
    public async Task No_flag_could_carry_the_token()
    {
        // SendingACredentialTakesNoSecretTests' assertion, for the second
        // kind of send: the flag somebody adds later "for scripting" is the
        // one that puts a token in shell history.
        foreach (var flag in (string[])["--secret", "--token", "--password", "--value", "--oauth"])
        {
            var refused = CliArgs.Parse(
                ["credential", "send", "--runner", "runner-1", "--agent", "claude",
                 flag, "sk-ant-oat01-not-a-real-token"]);

            await Assert.That(refused).IsTypeOf<CliAction.Unknown>()
                .Because($"{flag} would put a token in shell history before any code of ours ran.");
        }
    }

    [Test]
    public async Task No_member_of_the_action_is_named_for_secret_material()
    {
        foreach (var member in typeof(CliAction.AgentCredentialSend).GetProperties())
        {
            foreach (var word in (string[])
                ["secret", "token", "password", "passphrase", "bearer", "apikey"])
            {
                await Assert.That(member.Name.Contains(word, StringComparison.OrdinalIgnoreCase))
                    .IsFalse()
                    .Because($"'{member.Name}' is named for secret material on a verb whose "
                           + "whole job is to move one without it being typed.");
            }
        }
    }

    [Test]
    public async Task The_usage_says_an_agent_can_be_sent()
    {
        // The verb walk finds `credential`; nothing finds a flag. The usage
        // line is where somebody learns --agent exists.
        var usage = ((CliAction.Unknown)CliArgs.Parse(["nonsense"])).Message;

        await Assert.That(usage).Contains("--agent", StringComparison.Ordinal);
    }
}
