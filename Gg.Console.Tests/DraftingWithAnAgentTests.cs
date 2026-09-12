using System.Text.Json;
using Gg.Console;
using Gg.Local;

namespace Gg.Console.Tests;

/// <summary>
/// `m` on the Envelope tab hands the terminal to an agent, in the working copy.
/// </summary>
/// <remarks>
/// <para>
/// <b>The same shape as composing a flight's intent, one document over.</b>
/// A pty gg owns, a bar gg keeps, and a value that comes back only by tool
/// call — the argument being the one <c>submit_intent</c> already makes: a
/// tool call is a thing the agent chose to make, in a shape read
/// mechanically, and it is a narrower thing to trust than prose.
/// </para>
/// <para>
/// <b>Only the agent half, and that is a scope rather than an oversight.</b>
/// Composing a flight offers a choice — editor or agent — because both can do
/// the job from nothing. Editing a document in <c>$EDITOR</c> needs to know
/// WHICH document, and this pane has no selection yet; an agent needs none,
/// because it reads the estate and names what it changed. A key that offered
/// the choice and did nothing on one arm would be worse than one that was
/// never offered.
/// </para>
/// <para>
/// <b>`m` because the console already chose that letter for this act.</b>
/// <c>ComposeChoice</c>'s own comment settles it: of what was free, <c>w</c> is
/// "write it myself" and <c>m</c> is the model. Using a different letter for
/// the same thing one pane over would be the drift a single keymap exists to
/// prevent.
/// </para>
/// </remarks>
public class DraftingWithAnAgentTests
{
    private static DirectoryInfo Somewhere() => Directory.CreateDirectory(Path.Combine(
        Path.GetTempPath(), "gg-draft-test-" + Guid.NewGuid().ToString("N")[..8]));

    /// <summary>A stand-in for the agent: a script, hosted for real.</summary>
    private static string FakeAgent(string script)
    {
        var path = Path.Combine(Path.GetTempPath(), $"gg-fake-drafter-{Guid.NewGuid():N}.sh");
        File.WriteAllText(path, "#!/bin/sh\n" + script);
        return path;
    }

    private static SelfInvocation Ourselves() => new("/usr/local/bin/gg", ["runner", "tools"]);

    /// <summary>
    /// Inside the airspace actions, which is where this key lives now.
    /// </summary>
    /// <remarks>
    /// <b>It was on the tab and the tab ran out of room.</b> The status line
    /// carried ten keys and truncated mid-sentence, so the four acts on the
    /// airspace as a whole went behind <c>a</c> - and inside a modal the
    /// letters are free, which is what let this one keep its own.
    /// </remarks>
    private static KeymapContext Behind() =>
        new(UiMode.AirspaceActions, TabId.Envelope);

    [Test]
    public async Task The_key_drafts_from_the_airspace_actions()
    {
        await Assert.That(Keymap.Resolve(KeyStroke.Char('m'), Behind()))
            .IsEqualTo(Command.DraftEstate);
    }

    [Test]
    public async Task It_does_nothing_on_any_other_tab()
    {
        foreach (var tab in Tabs.All.Where(t => t != TabId.Envelope))
        {
            await Assert.That(Keymap.Resolve(
                    KeyStroke.Char('m'), new KeymapContext(UiMode.Normal, tab)))
                .IsNull()
                .Because($"`m' is the envelope tab's, and it resolved on {Tabs.Name(tab)}.");
        }
    }

    [Test]
    public async Task Drafting_is_the_shell_s_work_and_never_a_session_s()
    {
        await Assert.That(ShellCommands.Handled).Contains(Command.DraftEstate)
            .Because("it hands the terminal to a child process, which is the one thing a UI "
                   + "session may never do - and the read exception does not stretch to a "
                   + "spawn.");

        await Assert.That(ShellCommands.Reads).DoesNotContain(Command.DraftEstate);

        var before = new AppState { ActiveTab = TabId.Envelope };
        await Assert.That(Reducer.Reduce(before, Command.DraftEstate)).IsEqualTo(before)
            .Because("the shell handles it, so the reducer answers with the state it was "
                   + "given.");
    }

    [Test]
    public async Task The_launch_grants_the_document_tool_and_configures_the_server()
    {
        using var terminal = new HostedTerminal { Columns = 80, Rows = 24 };
        var tree = Somewhere();

        // The fake agent copies its own arguments out, which is the only way to
        // assert a command line without asserting about a string gg built for
        // itself.
        var agent = FakeAgent($"printf '%s\\n' \"$@\" > '{tree.FullName}/argv'\n");

        try
        {
            new PtyDraftSession($"/bin/sh {agent}", () => terminal, self: Ourselves())
                .Draft(tree.FullName);

            var argv = await File.ReadAllLinesAsync(Path.Combine(tree.FullName, "argv"));

            await Assert.That(argv).Contains(DocumentTool.Qualified)
                .Because("granted by the qualified name, from the one declaration that owns "
                       + "all three spellings - so a launch cannot grant a tool the server "
                       + "declares under another name.");

            var flag = Array.IndexOf(argv, "--mcp-config");
            await Assert.That(flag).IsGreaterThanOrEqualTo(0)
                .Because("a grant whose server was never configured tells the agent a tool "
                       + "exists and it spends turns calling nothing. Passed: "
                       + string.Join(" ", argv));

            using var config = JsonDocument.Parse(argv[flag + 1]);
            var server = config.RootElement
                .GetProperty("mcpServers").GetProperty(DocumentTool.Server);

            await Assert.That(server.GetProperty("command").GetString())
                .IsEqualTo("/usr/local/bin/gg")
                .Because("the server is this binary re-execed, and a path that is not this "
                       + "binary is a child that fails at startup after the agent has been "
                       + "told the tool exists.");

            await Assert.That(server.GetProperty("env")
                    .GetProperty(DocumentTool.RootVariable).GetString())
                .IsEqualTo(tree.FullName)
                .Because("the tool server is the only thing here permitted to know where a "
                       + "document goes, and it learns it from this rather than by looking.");

            await Assert.That(argv).DoesNotContain("--strict-mcp-config")
                .Because("a person is driving this session and keeps their own servers and "
                       + "settings, which is the same choice the takeover makes - and the "
                       + "cost is stated in the type rather than hidden.");
        }
        finally
        {
            tree.Delete(recursive: true);
        }
    }

    [Test]
    public async Task The_agent_is_never_told_where_the_working_copy_is()
    {
        using var terminal = new HostedTerminal { Columns = 80, Rows = 24 };
        var tree = Somewhere();

        // It writes its own environment out, so what is asserted is what the
        // child could actually read rather than what gg meant to pass.
        var agent = FakeAgent($"env > '{tree.FullName}/env'\n");

        try
        {
            new PtyDraftSession($"/bin/sh {agent}", () => terminal, self: Ourselves())
                .Draft(tree.FullName);

            var environment = await File.ReadAllTextAsync(Path.Combine(tree.FullName, "env"));

            await Assert.That(environment)
                .DoesNotContain(DocumentTool.RootVariable, StringComparison.Ordinal)
                .Because("an agent that knew the variable could set it for something else it "
                       + "started, and the one thing this arrangement rests on is that only "
                       + "the tool server knows where a document goes.");
        }
        finally
        {
            tree.Delete(recursive: true);
        }
    }

    [Test]
    public async Task The_agent_works_in_the_working_copy()
    {
        using var terminal = new HostedTerminal { Columns = 80, Rows = 24 };
        var tree = Somewhere();
        var agent = FakeAgent($"pwd > '{tree.FullName}/cwd'\n");

        try
        {
            new PtyDraftSession($"/bin/sh {agent}", () => terminal, self: Ourselves())
                .Draft(tree.FullName);

            var cwd = (await File.ReadAllTextAsync(Path.Combine(tree.FullName, "cwd"))).Trim();

            await Assert.That(cwd).EndsWith(tree.Name)
                .Because("a narrowing only means anything against the root and work kind it "
                       + "constrains, so an agent drafting one has to be able to read them - "
                       + "and reading is not the pen.");
        }
        finally
        {
            tree.Delete(recursive: true);
        }
    }

    [Test]
    public async Task With_no_working_copy_it_says_so_and_starts_nothing()
    {
        var started = false;

        var said = new PtyDraftSession(
            "/bin/false",
            () => new HostedTerminal { Columns = 80, Rows = 24 },
            self: Ourselves(),
            host: (_, _, _, _, _, _, _) =>
            {
                started = true;
                return Task.FromResult(0);
            }).Draft(null);

        await Assert.That(started).IsFalse()
            .Because("an agent hosted with nowhere to put a document would spend a person's "
                   + "attention on a session whose only possible answer is a refusal.");
        await Assert.That(said).Contains("airspace", StringComparison.Ordinal)
            .Because("the setting is the fix, and naming it is the difference between a "
                   + "refusal and a shrug.");
    }

    [Test]
    public async Task A_working_copy_that_is_not_there_is_not_created()
    {
        var started = false;

        var said = new PtyDraftSession(
            "/bin/false",
            () => new HostedTerminal { Columns = 80, Rows = 24 },
            self: Ourselves(),
            host: (_, _, _, _, _, _, _) =>
            {
                started = true;
                return Task.FromResult(0);
            }).Draft(Path.Combine(Path.GetTempPath(), "gg-not-there-" + Guid.NewGuid()));

        await Assert.That(started).IsFalse();
        await Assert.That(said).Contains("pull", StringComparison.OrdinalIgnoreCase)
            .Because("a directory gg made because a draft wanted one is an estate nobody "
                   + "pulled, and the first apply out of it would submit documents against "
                   + "no precondition at all.");
    }
}
