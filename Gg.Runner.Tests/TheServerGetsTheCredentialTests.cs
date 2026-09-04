using Gg.Contracts;
using Gg.Runner.Execution;

namespace Gg.Runner.Tests;

/// <summary>
/// The tool server is given its credential; the agent is not.
/// </summary>
/// <remarks>
/// <para>
/// <b>This was claimed before it was true.</b> The first version of the reader
/// work shipped a comment saying <i>"the credential is the server's, never the
/// agent's — the server process holds it"</i>, and nothing resolved a credential
/// at all: the config carried a command and arguments and no environment.
/// </para>
/// <para>
/// <b>And the gap was not neutral.</b> A runner does not clear the child
/// environment, so the agent inherits the runner's and a tool server inherits
/// the agent's. The only way a secret reached a server was AMBIENT — exported
/// beside the runner — which puts it exactly where an agent holding
/// <c>run-tests</c> can read it. The comment asserted the property that route
/// breaks.
/// </para>
/// <para>
/// <b>So the declaration names both halves</b>: the variable the server expects,
/// and the credential to resolve from the store <c>gg credential add</c> already
/// writes. The secret goes into the server's own environment block and nowhere
/// else.
/// </para>
/// </remarks>
public class TheServerGetsTheCredentialTests
{
    private static ExecutorRequest ARequest() => new()
    {
        WorkingDirectory = "/tmp/gg-tree",
        LoopId = "implement",
        IntentProvider = "a-tracker",
        IntentId = "26",
        Moves = [LoopMoves.Read],
        WallClock = TimeSpan.FromMinutes(30),
        TranscriptPath = "/tmp/gg-transcript.ndjson",
    };

    [Test]
    public async Task A_declaration_names_the_variable_and_the_credential()
    {
        var reader = IntentConfiguration.FromEnvironment(
            "a-tracker=tracker-mcp --stdio|TRACKER_TOKEN=local:acme/board")[0];

        await Assert.That(reader.Command).IsEqualTo("tracker-mcp");
        await Assert.That(reader.Arguments).IsEquivalentTo((string[])["--stdio"]);
        await Assert.That(reader.EnvironmentVariable).IsEqualTo("TRACKER_TOKEN");
        await Assert.That(reader.Locator).IsEqualTo("local:acme/board");
    }

    [Test]
    public async Task A_reader_needing_no_credential_still_declares_cleanly()
    {
        // THE ANCHOR. A tracker reachable without a secret - an internal one, a
        // mock in a walk - must not be made to invent a credential to satisfy a
        // parser.
        var reader = IntentConfiguration.FromEnvironment("a-tracker=tracker-mcp")[0];

        await Assert.That(reader.EnvironmentVariable).IsNull();
        await Assert.That(reader.Locator).IsNull();
    }

    [Test]
    public async Task The_secret_reaches_the_servers_environment_and_no_argument()
    {
        // THE DEFECT. An env block is the only place it may appear: an argument
        // is visible in `ps` to everything on the host, which is the mistake
        // that already cost this program one rotated credential.
        var arguments = ClaudeCodeExecutor.ArgumentsFor(
            ARequest(),
            IntentConfiguration.FromEnvironment(
                "a-tracker=tracker-mcp|TRACKER_TOKEN=local:acme/board"),
            secret: "the-secret");

        var config = arguments[arguments.ToList().IndexOf("--mcp-config") + 1];

        await Assert.That(config).Contains("TRACKER_TOKEN");
        await Assert.That(config).Contains("the-secret");
        await Assert.That(arguments.Count(a => a.Contains("the-secret", StringComparison.Ordinal)))
            .IsEqualTo(1)
            .Because("the config is the one argument that may carry it, and even that is a "
                   + "process argument - so it must be the only one.");
    }

    [Test]
    public async Task A_reader_needing_no_credential_gets_no_environment_block()
    {
        var arguments = ClaudeCodeExecutor.ArgumentsFor(
            ARequest(), IntentConfiguration.FromEnvironment("a-tracker=tracker-mcp"));

        var config = arguments[arguments.ToList().IndexOf("--mcp-config") + 1];

        await Assert.That(config).DoesNotContain("env");
    }

    [Test]
    public async Task A_declaration_naming_a_credential_it_cannot_resolve_is_refused()
    {
        // ARTICLE XI, and the same disposition NoCredentialResolver already has:
        // refuse loudly rather than launch a server with an empty secret, which
        // fails at the tracker with an authentication error nobody can trace
        // back to a missing file on this host.
        var why = IntentConfiguration.Unresolvable(
            IntentConfiguration.FromEnvironment(
                "a-tracker=tracker-mcp|TRACKER_TOKEN=local:acme/board")[0],
            secret: null);

        await Assert.That(why).IsNotNull();
        await Assert.That(why!).Contains("local:acme/board");
        await Assert.That(why).Contains("a-tracker");
    }

    [Test]
    public async Task A_resolved_credential_is_not_refused()
    {
        var reader = IntentConfiguration.FromEnvironment(
            "a-tracker=tracker-mcp|TRACKER_TOKEN=local:acme/board")[0];

        await Assert.That(IntentConfiguration.Unresolvable(reader, "the-secret")).IsNull();
    }

    [Test]
    public async Task A_declaration_with_half_a_credential_is_refused_where_it_is_written()
    {
        // A variable with no credential, or a credential with no variable, both
        // describe a server that will start and fail to authenticate. Refused at
        // parse, where an operator can still fix it.
        await Assert.That(() => IntentConfiguration.FromEnvironment("a-tracker=mcp|TRACKER_TOKEN"))
            .Throws<InvalidOperationException>();
        await Assert.That(() => IntentConfiguration.FromEnvironment("a-tracker=mcp|=local:acme/x"))
            .Throws<InvalidOperationException>();
    }
}
