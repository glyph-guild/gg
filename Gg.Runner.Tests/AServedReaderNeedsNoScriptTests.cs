using Gg.Contracts;
using Gg.Runner.Execution;

namespace Gg.Runner.Tests;

/// <summary>
/// A deployment declares a host, and this binary reads the tracker itself.
/// </summary>
/// <remarks>
/// <para>
/// <b>WHAT THIS RETIRES.</b> A runner in the fleet reads its tracker through a
/// script an operator installed and maintains by hand, declared as
/// <c>GG_INTENT_READERS='key=python3 /home/gg/tracker-mcp.py|VAR=locator'</c>.
/// It is 164 lines: three environment variables, one GET, six fields and a
/// markup strip. Everything in it is now in this binary, so what a deployment
/// has to state is the host - and the script, the interpreter and the file on
/// the runner all stop existing.
/// </para>
/// <para>
/// <b>One format, not two.</b> <c>IntentConfiguration</c>'s own remark says an
/// operator configuring a runner should learn one shape, and points at
/// <c>GG_VCS_HOSTS</c>. So this is the same list: comma between entries,
/// <c>key=value</c> inside one, and the same <c>|</c> before an optional
/// credential that <c>GG_INTENT_READERS</c> already uses. What is absent is the
/// variable name, because a server this binary IS does not need to be told
/// which environment variable to read its secret from.
/// </para>
/// <para>
/// <b>Both kinds coexist, and a key may not be both.</b> A tracker this binary
/// has no shape for is still somebody's command, and that path is untouched. A
/// key declared in both variables is ambiguous in a way no default can settle,
/// so it is refused where it is written.
/// </para>
/// </remarks>
public class AServedReaderNeedsNoScriptTests
{
    private const string Host = "https://tracker.example/acme";

    private static SelfInvocation Self() =>
        SelfInvocation.For("/usr/local/bin/gg", "/usr/local/bin/gg")!;

    private static ExecutorRequest ARequest(string? provider = "a-tracker") => new()
    {
        WorkingDirectory = "/tmp/gg-tree",
        LoopId = "implement",
        IntentProvider = provider,
        IntentId = "26",
        Moves = [LoopMoves.Read],
        WallClock = TimeSpan.FromMinutes(30),
        TranscriptPath = "/tmp/gg-transcript.ndjson",
    };

    [Test]
    public async Task A_host_declaration_becomes_a_reader_this_binary_serves()
    {
        var readers = IntentConfiguration.FromEnvironment(
            declaration: "", served: $"a-tracker={Host}|local:acme/board", self: Self());

        await Assert.That(readers).Count().IsEqualTo(1);
        await Assert.That(readers[0].Key).IsEqualTo("a-tracker");
        await Assert.That(readers[0].Command).IsEqualTo("/usr/local/bin/gg");
        await Assert.That(string.Join(" ", readers[0].Arguments))
            .IsEqualTo("runner read --provider a-tracker --host " + Host
                     + " --credential local:acme/board");
    }

    [Test]
    public async Task A_served_reader_carries_nothing_for_an_environment_block()
    {
        // THE PROPERTY THE WHOLE MOVE IS FOR. Nothing here asks the launch to
        // place a secret, so the launch places none - and the agent's argv,
        // which every ps on the host can read, holds a credential's NAME.
        var readers = IntentConfiguration.FromEnvironment(
            declaration: "", served: $"a-tracker={Host}|local:acme/board", self: Self());

        await Assert.That(readers[0].EnvironmentVariable).IsNull();
        await Assert.That(readers[0].Locator).IsNull();
    }

    [Test]
    public async Task A_tracker_reachable_without_a_secret_declares_only_a_host()
    {
        // The declaration half already refuses to make a credential-free
        // tracker invent one. This half must not undo that.
        var readers = IntentConfiguration.FromEnvironment(
            declaration: "", served: $"a-tracker={Host}", self: Self());

        await Assert.That(readers).Count().IsEqualTo(1);
        await Assert.That(string.Join(" ", readers[0].Arguments)).DoesNotContain("--credential");
    }

    [Test]
    public async Task Both_kinds_of_reader_can_be_configured_at_once()
    {
        // A TRACKER THIS BINARY HAS NO SHAPE FOR IS STILL SOMEBODY'S COMMAND.
        // The escape hatch does not close; it stops being the only path.
        var readers = IntentConfiguration.FromEnvironment(
            declaration: "odd-one=odd-mcp --stdio",
            served: $"a-tracker={Host}",
            self: Self());

        await Assert.That(readers.Select(r => r.Key)).Contains("odd-one");
        await Assert.That(readers.Select(r => r.Key)).Contains("a-tracker");
        await Assert.That(readers.Single(r => r.Key == "odd-one").Command).IsEqualTo("odd-mcp");
    }

    [Test]
    public async Task A_key_declared_as_both_is_refused_where_it_is_written()
    {
        // AMBIGUOUS IN A WAY NO DEFAULT SETTLES. Picking one silently would
        // mean an operator who edited the variable they were thinking of saw
        // no change at all, which is the worst way to learn about precedence.
        await Assert.That(() => IntentConfiguration.FromEnvironment(
                declaration: "a-tracker=some-mcp",
                served: $"a-tracker={Host}",
                self: Self()))
            .Throws<InvalidOperationException>();
    }

    [Test]
    public async Task An_entry_with_no_host_is_refused_rather_than_served_against_nothing()
    {
        await Assert.That(() => IntentConfiguration.FromEnvironment(
                declaration: "", served: "a-tracker=", self: Self()))
            .Throws<InvalidOperationException>();
    }

    [Test]
    public async Task Declaring_nothing_serves_nothing()
    {
        // THE ANCHOR, and it is the ordinary state: every runner in the fleet
        // declares neither variable, and a text flight names no tracker.
        await Assert.That(IntentConfiguration.FromEnvironment(
            declaration: "", served: "", self: Self())).IsEmpty();
        await Assert.That(IntentConfiguration.FromEnvironment(
            declaration: "", served: null, self: Self())).IsEmpty();
    }

    [Test]
    public async Task A_flight_whose_tracker_is_served_is_not_refused_before_it_starts()
    {
        // THE PRE-FLIGHT GATE READS THIS SAME LIST. A served reader missing
        // from it would ground every flight naming that tracker, with a refusal
        // saying no reader is configured while one plainly is.
        var readers = IntentConfiguration.FromEnvironment(
            declaration: "", served: $"a-tracker={Host}", self: Self());

        await Assert.That(IntentConfiguration.Unreadable("a-tracker", readers)).IsNull();
        await Assert.That(IntentConfiguration.Unreadable("another", readers)).IsNotNull()
            .Because("a tracker nothing declares is still refused, or the gate guards nothing.");
    }

    [Test]
    public async Task The_launch_for_a_served_tracker_names_this_binary_and_no_secret()
    {
        // END TO END, through the one path a flight actually takes: the same
        // reader list the runner builds, handed to the launch it builds it for.
        var arguments = ClaudeCodeExecutor.ArgumentsFor(
            ARequest(),
            IntentConfiguration.FromEnvironment(
                declaration: "", served: $"a-tracker={Host}|local:acme/board", self: Self()),
            secret: "the-secret");

        var config = arguments[arguments.ToList().IndexOf("--mcp-config") + 1];

        await Assert.That(config).Contains("runner");
        await Assert.That(config).Contains("local:acme/board");
        await Assert.That(config).DoesNotContain("env");
        await Assert.That(arguments.Any(a => a.Contains("the-secret", StringComparison.Ordinal)))
            .IsFalse()
            .Because("this is the whole reason the reader moved into the binary.");
    }
}
