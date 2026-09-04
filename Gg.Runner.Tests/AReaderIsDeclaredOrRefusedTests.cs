using Gg.Runner.Execution;

namespace Gg.Runner.Tests;

/// <summary>
/// Which trackers this runner can read, and what happens when it cannot read
/// the one a flight names.
/// </summary>
/// <remarks>
/// <para>
/// <b>The agent has never had a tool that could read a work item.</b>
/// <c>ClaudeCodeExecutor</c> passes <c>--strict-mcp-config</c> and no
/// <c>--mcp-config</c>, which is a deliberate and correct pair — it removes the
/// operator's own servers — but it leaves the agent with nothing that reaches a
/// tracker. The prompt's own remark says what should happen instead: <i>"the
/// agent resolves what it points at from inside the customer's environment, with
/// the customer's own credential."</i> That was the design; nothing implemented
/// it.
/// </para>
/// <para>
/// <b>Configured, never compiled in, because this binary is public and names no
/// forge.</b> Which tracker a tenant uses is the control plane's knowledge and
/// the deployment's business — the same rule <c>GG_VCS_HOSTS</c> already follows
/// one noun over, and the rule <c>NoSourceFileNamesAnIdentityProvider</c>
/// enforces on every file here.
/// </para>
/// <para>
/// <b>An unreadable tracker is refused, not attempted.</b> A runner that invoked
/// anyway would spend a loop's whole budget discovering it cannot read the one
/// thing the flight is about, and report a blocker somebody has to go and
/// interpret. <i>"A provider nobody configured is a declared capability gap"</i>
/// — the words already in this repository for exactly this.
/// </para>
/// </remarks>
public class AReaderIsDeclaredOrRefusedTests
{
    [Test]
    public async Task A_runner_declares_which_trackers_it_can_read()
    {
        // The shape mirrors GG_VCS_HOSTS deliberately: one variable answering
        // "which providers does this runner serve, and how", so an operator
        // configuring a host learns one format rather than two.
        var readers = IntentConfiguration.FromEnvironment(
            "a-tracker=tracker-mcp --stdio,another=other-mcp");

        await Assert.That(readers).Count().IsEqualTo(2);
        await Assert.That(readers[0].Key).IsEqualTo("a-tracker");
        await Assert.That(readers[0].Command).IsEqualTo("tracker-mcp");
        await Assert.That(readers[0].Arguments).IsEquivalentTo((string[])["--stdio"]);
    }

    [Test]
    public async Task A_runner_that_declares_nothing_reads_no_trackers()
    {
        // THE ANCHOR, and it is every runner in the fleet today. Absent is the
        // ordinary state and must stay one - the same disposition
        // DestinationConfiguration has for a runner configured to read and not
        // to write.
        await Assert.That(IntentConfiguration.FromEnvironment("")).IsEmpty();
        await Assert.That(IntentConfiguration.FromEnvironment(null)).IsEmpty();
    }

    [Test]
    public async Task A_declaration_missing_its_command_is_refused_naming_the_entry()
    {
        // Article XI at the point of configuration. An entry that parsed to a
        // provider with no command would leave a runner advertising a tracker it
        // cannot launch anything for, which is the capability gap wearing the
        // costume of a capability.
        await Assert.That(() => IntentConfiguration.FromEnvironment("a-tracker="))
            .Throws<InvalidOperationException>();
    }

    [Test]
    public async Task A_work_item_this_runner_cannot_read_is_refused_before_a_loop_is_spent()
    {
        // THE REFUSAL, and the reason it belongs here rather than in the agent's
        // lap: the agent would burn a whole wall-clock budget establishing that
        // it has no tool, and report it as prose somebody has to interpret.
        var why = IntentConfiguration.Unreadable(
            provider: "a-tracker", readers: []);

        await Assert.That(why).IsNotNull();
        await Assert.That(why!).Contains("a-tracker")
            .Because("a refusal has to name the provider, or an operator cannot tell which "
                   + "declaration is missing.");
    }

    [Test]
    public async Task A_work_item_this_runner_can_read_is_not_refused()
    {
        var readers = IntentConfiguration.FromEnvironment("a-tracker=tracker-mcp");

        await Assert.That(IntentConfiguration.Unreadable("a-tracker", readers)).IsNull();
    }

    [Test]
    public async Task A_flight_naming_no_tracker_is_never_refused_for_want_of_a_reader()
    {
        // A link flight and a text flight both name no tracker, and neither has
        // ever needed one. A refusal that fired on them would ground the whole
        // fleet on a runner that had simply not declared something it does not
        // need.
        await Assert.That(IntentConfiguration.Unreadable(provider: null, readers: [])).IsNull();
    }
}
