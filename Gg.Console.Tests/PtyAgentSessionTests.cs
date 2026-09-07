using System.Text.Json;
using Gg.Local;

namespace Gg.Console.Tests;

/// <summary>
/// Composing an intent with an agent, in a pseudo-terminal gg keeps a bar on.
/// </summary>
/// <remarks>
/// <para>
/// <b>The same <see cref="IEditorSession"/> as the editor, and that is the whole
/// reason the choice is cheap.</b> Both take initial text, run a real child, and
/// answer with what came back — so the modal that offers "editor or agent" picks
/// between two implementations of one port rather than branching every launch
/// path twice. It also makes S33.1-01 true: both callers go through
/// <see cref="PtyHost"/>.
/// </para>
/// <para>
/// <b>What comes back arrives by tool call, and only by tool call.</b> The agent
/// never learns where the intent file is — only the tool server does, through
/// its own entry in the MCP config gg writes. An agent told the path could write
/// it directly, which would make the tool decorative and put a governance
/// decision back into whatever the agent felt like doing.
/// </para>
/// </remarks>
public class PtyAgentSessionTests
{
    /// <summary>A directory this test owns and can look inside.</summary>
    private static DirectoryInfo Somewhere() => Directory.CreateDirectory(Path.Combine(
        Path.GetTempPath(), "gg-compose-test-" + Guid.NewGuid().ToString("N")[..8]));

    /// <summary>A stand-in for the agent: a script, hosted for real.</summary>
    private static string FakeAgent(string script)
    {
        var path = Path.Combine(Path.GetTempPath(), $"gg-fake-agent-{Guid.NewGuid():N}.sh");
        File.WriteAllText(path, "#!/bin/sh\n" + script);
        return path;
    }

    private static SelfInvocation Ourselves() => new("/usr/local/bin/gg", ["runner", "tools"]);

    [Test]
    public async Task What_the_agent_submitted_is_what_comes_back()
    {
        // THE PLANT GOES THROUGH THE REAL PATH as far as this slice reaches: a
        // real child on a real pseudo-terminal, and the file read the way gg
        // reads it. What the fake agent does NOT do is call the tool - that arm
        // is asserted against the real server in PlatformToolServerTests, and
        // joining the two needs a real agent, which is step 0's job.
        using var terminal = new HostedTerminal { Columns = 80, Rows = 24 };
        await Assert.That(terminal.Opened).IsTrue();

        var compose = Somewhere();
        var agent = FakeAgent(
            $"printf 'Make the pool decider stop scanning' > '{compose.FullName}/intent.txt'\n");

        try
        {
            var composed = new PtyAgentSession(
                $"/bin/sh {agent}", () => terminal, self: Ourselves(),
                composeIn: compose.FullName).Edit("");

            await Assert.That(composed).IsEqualTo("Make the pool decider stop scanning");
        }
        finally
        {
            File.Delete(agent);
            compose.Delete(recursive: true);
        }
    }

    [Test]
    public async Task A_session_that_submitted_nothing_composes_nothing()
    {
        // AND IT MUST NOT INVENT ANYTHING. An agent that talked for twenty
        // minutes and never called the tool has produced no intent, and the
        // honest answer is empty - whatever it said on the way. Guessing from
        // the screen is what rule 7 forbids, and there is nothing else to guess
        // from.
        using var terminal = new HostedTerminal { Columns = 80, Rows = 24 };
        await Assert.That(terminal.Opened).IsTrue();

        var compose = Somewhere();
        var agent = FakeAgent("printf 'I thought about it and said a lot of words'\n");

        try
        {
            var composed = new PtyAgentSession(
                $"/bin/sh {agent}", () => terminal, self: Ourselves(),
                composeIn: compose.FullName).Edit("");

            await Assert.That(composed).IsEmpty()
                .Because("nothing was submitted, and the words on the screen are not an "
                       + "intent just because they are the only thing there.");
        }
        finally
        {
            File.Delete(agent);
            compose.Delete(recursive: true);
        }
    }

    [Test]
    public async Task The_tool_server_is_configured_and_the_intent_tool_is_granted()
    {
        // The two halves of making the tool usable, and they are separate on
        // purpose: a grant whose server was never configured tells the agent a
        // tool exists and it spends turns calling nothing.
        using var terminal = new HostedTerminal { Columns = 80, Rows = 24 };
        await Assert.That(terminal.Opened).IsTrue();

        var compose = Somewhere();

        // The fake agent copies its own arguments out so the test can read what
        // the launch actually passed - which is the only way to assert a
        // command line without asserting about a string gg built for itself.
        var agent = FakeAgent($"printf '%s\\n' \"$@\" > '{compose.FullName}/argv'\n");

        try
        {
            new PtyAgentSession(
                $"/bin/sh {agent}", () => terminal, self: Ourselves(),
                composeIn: compose.FullName).Edit("");

            var argv = await File.ReadAllLinesAsync(Path.Combine(compose.FullName, "argv"));

            await Assert.That(argv).Contains("--allowedTools");
            await Assert.That(argv).Contains(IntentTool.Qualified)
                .Because("granted by the qualified name, which is the spelling a launch uses - "
                       + "and it comes from the one declaration rather than being spelled here.");

            var configFlag = Array.IndexOf(argv, "--mcp-config");
            await Assert.That(configFlag).IsGreaterThanOrEqualTo(0)
                .Because("a grant whose server was never configured tells the agent a tool "
                       + "exists and it spends turns calling nothing. Passed: "
                       + string.Join(" ", argv));

            using var config = JsonDocument.Parse(argv[configFlag + 1]);
            var server = config.RootElement
                .GetProperty("mcpServers").GetProperty(IntentTool.Server);

            await Assert.That(server.GetProperty("command").GetString())
                .IsEqualTo("/usr/local/bin/gg")
                .Because("gg serves its own tools by re-invoking itself, and the path is the "
                       + "one this process was started from rather than a guess at where gg "
                       + "is installed.");

            var path = server.GetProperty("env").GetProperty(IntentTool.PathVariable).GetString();
            await Assert.That(path).IsEqualTo(Path.Combine(compose.FullName, "intent.txt"))
                .Because("the tool server is told where to write, because it is the only thing "
                       + "in this arrangement that may.");
        }
        finally
        {
            File.Delete(agent);
            compose.Delete(recursive: true);
        }
    }

    [Test]
    public async Task The_agent_is_never_told_where_the_intent_goes()
    {
        // S33.3-06, AND THE REASON THE CONFIG CARRIES AN ENVIRONMENT AT ALL. The
        // path belongs to the tool server, which is a separate process gg also
        // wrote. An agent that knew it could write the file directly, and the
        // tool would become decorative - a governance decision back in the hands
        // of whatever the agent felt like doing, which is exactly what a tool
        // call is here to prevent.
        using var terminal = new HostedTerminal { Columns = 80, Rows = 24 };
        await Assert.That(terminal.Opened).IsTrue();

        var compose = Somewhere();
        var agent = FakeAgent(
            $"env > '{compose.FullName}/env'; printf '%s\\n' \"$@\" > '{compose.FullName}/argv'\n");

        try
        {
            new PtyAgentSession(
                $"/bin/sh {agent}", () => terminal, self: Ourselves(),
                composeIn: compose.FullName).Edit("");

            var environment = await File.ReadAllTextAsync(Path.Combine(compose.FullName, "env"));

            await Assert.That(environment)
                .DoesNotContain(IntentTool.PathVariable, StringComparison.Ordinal)
                .Because("the agent's own environment must not carry it. Only the tool server's "
                       + "does, and that is a different process with a different config.");

            // AND NOT ON THE COMMAND LINE EITHER, which is the other place a
            // path leaks - and the more likely one, since the config is passed
            // as an argument. The variable's NAME appears there, inside the
            // config JSON; what must not is a second, plainer copy the agent
            // could read without parsing anything.
            var argv = await File.ReadAllLinesAsync(Path.Combine(compose.FullName, "argv"));
            var plain = argv.Where(a => !a.Contains("mcpServers", StringComparison.Ordinal));

            await Assert.That(plain.Any(a => a.Contains("intent.txt", StringComparison.Ordinal)))
                .IsFalse()
                .Because("a path on the command line is one an agent reads without parsing "
                       + "anything. Passed: " + string.Join(" ", plain));
        }
        finally
        {
            File.Delete(agent);
            compose.Delete(recursive: true);
        }
    }

    [Test]
    public async Task The_bar_says_what_ends_the_session()
    {
        // A person handed an agent cannot ask it what gg is waiting for, because
        // it does not know either. The top row is the only surface gg still owns
        // while the child has the screen, so it is where the contract with the
        // person lives - and what they cannot work out for themselves is what
        // ends this and what happens if they just close it.
        using var terminal = new HostedTerminal { Columns = 100, Rows = 24 };
        await Assert.That(terminal.Opened).IsTrue();

        var compose = Somewhere();
        var agent = FakeAgent("exit 0\n");

        try
        {
            new PtyAgentSession(
                $"/bin/sh {agent}", () => terminal, self: Ourselves(),
                composeIn: compose.FullName).Edit("");

            await Assert.That(terminal.Painted).Contains("submit", StringComparison.OrdinalIgnoreCase)
                .Because("the bar has to name the thing that ends the session, and the thing "
                       + "that ends this one is the agent submitting.");
        }
        finally
        {
            File.Delete(agent);
            compose.Delete(recursive: true);
        }
    }

    [Test]
    public async Task The_intent_file_is_deleted_however_the_session_ended()
    {
        // It holds what somebody was proposing, in a directory everybody on this
        // machine can read, and it has already been handed back by the time this
        // matters.
        foreach (var (ending, script) in ((string, string)[])
                 [("submitted", "printf 'something' > '{DIR}/intent.txt'\n"),
                  ("abandoned", "exit 1\n"),
                  ("killed outright", "printf 'half a thought' > '{DIR}/intent.txt'; kill -9 $$\n")])
        {
            using var terminal = new HostedTerminal { Columns = 80, Rows = 24 };
            await Assert.That(terminal.Opened).IsTrue();

            var compose = Somewhere();
            var agent = FakeAgent(script.Replace("{DIR}", compose.FullName, StringComparison.Ordinal));

            try
            {
                new PtyAgentSession(
                    $"/bin/sh {agent}", () => terminal, self: Ourselves(),
                    composeIn: compose.FullName).Edit("");

                await Assert.That(File.Exists(Path.Combine(compose.FullName, "intent.txt")))
                    .IsFalse()
                    .Because($"a session that was {ending} still leaves gg holding the file.");
            }
            finally
            {
                File.Delete(agent);
                compose.Delete(recursive: true);
            }
        }
    }
}
