namespace Gg.Cli.Tests;

/// <summary>
/// <c>gg agent login --runner &lt;id&gt; [--agent claude]</c> — log a runner's
/// agent in from here, over the channel.
/// </summary>
/// <remarks>
/// <para>
/// <b>A verb of its own rather than a flag on <c>credential send</c></b>,
/// because it is a different act: a send carries a value this machine holds
/// or a person types; this makes a RUNNER run its agent's own ceremony, shows
/// the person a URL, and carries back a code. Nothing this machine holds is
/// involved, and the token never passes through here at all.
/// </para>
/// <para>
/// <b>Nothing on the line could carry the code.</b> It is read with the echo
/// off after the URL is shown, for the same reason a credential is: an
/// argument is in shell history before any code of ours ran.
/// </para>
/// </remarks>
public class AgentLoginArgsTests
{
    private static CliAction.AgentLogin Parsed(params string[] args)
    {
        var action = CliArgs.Parse(args);
        return action as CliAction.AgentLogin ?? throw new InvalidOperationException(
            $"'{string.Join(" ", args)}' did not parse as an agent login but as {action.GetType().Name}"
          + (action is CliAction.Unknown unknown ? $": {unknown.Message}" : "."));
    }

    [Test]
    public async Task It_names_a_runner_and_defaults_to_claude()
    {
        var login = Parsed("agent", "login", "--runner", "runner-1");

        await Assert.That(login.RunnerId).IsEqualTo("runner-1");
        await Assert.That(login.Agent).IsEqualTo("claude")
            .Because("the one adapter there is, and the one GG_EXECUTOR_BINARY declares bare.");
        await Assert.That(login.Json).IsFalse();
    }

    [Test]
    public async Task The_agent_may_be_named_and_json_is_honoured()
    {
        var login = Parsed("agent", "login", "--runner", "runner-1", "--agent", "claude", "--json");

        await Assert.That(login.Agent).IsEqualTo("claude");
        await Assert.That(login.Json).IsTrue();
    }

    [Test]
    public async Task An_agent_name_that_is_not_a_locator_segment_is_refused_naming_it()
    {
        foreach (var name in new[] { "Claude", "co dex", "../etc" })
        {
            var refused = CliArgs.Parse(["agent", "login", "--runner", "runner-1", "--agent", name]);

            var unknown = await Assert.That(refused).IsTypeOf<CliAction.Unknown>();
            await Assert.That(unknown!.Message).Contains(name, StringComparison.Ordinal);
        }
    }

    [Test]
    public async Task Without_a_runner_it_is_refused_naming_the_flag()
    {
        var refused = CliArgs.Parse(["agent", "login"]);

        var unknown = await Assert.That(refused).IsTypeOf<CliAction.Unknown>();
        await Assert.That(unknown!.Message).Contains("--runner", StringComparison.Ordinal)
            .Because("which machine is the one thing this verb cannot guess.");
    }

    [Test]
    public async Task An_option_it_does_not_take_is_refused_by_name()
    {
        // THE ONE SOMEBODY ADDS "FOR SCRIPTING" is the one that puts the code
        // in shell history, and an option silently dropped would then ask for
        // the code anyway.
        foreach (var flag in (string[])["--code", "--token", "--secret", "--url"])
        {
            var refused = CliArgs.Parse(["agent", "login", "--runner", "runner-1", flag, "value"]);

            var unknown = await Assert.That(refused).IsTypeOf<CliAction.Unknown>();
            await Assert.That(unknown!.Message).Contains(flag, StringComparison.Ordinal)
                .Because($"{flag} is refused by name rather than ignored.");
        }
    }

    [Test]
    public async Task No_member_of_the_action_is_named_for_secret_material()
    {
        foreach (var member in typeof(CliAction.AgentLogin).GetProperties())
        {
            foreach (var word in (string[])
                ["secret", "token", "password", "passphrase", "bearer", "apikey", "code", "url"])
            {
                await Assert.That(member.Name.Contains(word, StringComparison.OrdinalIgnoreCase))
                    .IsFalse()
                    .Because($"'{member.Name}' would carry on the command line what this verb "
                           + "exists to carry over a sealed channel.");
            }
        }

        await Assert.That(typeof(CliAction.AgentLogin).GetProperties().Length).IsLessThanOrEqualTo(3)
            .Because("a runner, an agent and --json. A fourth member is a decision made here.");
    }

    [Test]
    public async Task The_usage_says_it_exists()
    {
        var usage = ((CliAction.Unknown)CliArgs.Parse(["nonsense"])).Message;

        await Assert.That(usage).Contains("agent login", StringComparison.Ordinal);
    }
}
