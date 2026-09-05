using Gg.Local;

namespace Gg.Console.Tests;

/// <summary>
/// The console can learn which trackers this machine can read.
/// </summary>
/// <remarks>
/// <para>
/// <b>S29.3-06, and it is a structural claim rather than a behavioural one.</b>
/// A browse pane needs two facts before it can show anything: which readers
/// exist, and how to start one. Both live in <c>IntentConfiguration</c>, which
/// was in <c>Gg.Runner</c> - a project <c>Gg.Console</c> deliberately does not
/// reference, because a console must be structurally unable to act as a runner.
/// </para>
/// <para>
/// <b>So the question is not "can it call this" but "where does this live".</b>
/// <c>Gg.Local</c>'s charter is local paths and local configuration, readable by
/// a runner and a console alike, with no transport, no credential and no wire
/// type. Which trackers a machine can read is exactly that, and this file
/// failing to compile is what says it is not there yet.
/// </para>
/// <para>
/// <b>A locator is not a credential.</b> The one thing here that comes near the
/// charter's edge is <c>IntentReader.Locator</c>, and it is a NAME - the string
/// <c>gg credential add</c> files a secret under. Nothing in this project can
/// resolve it, which is the property that keeps the charter honest: the console
/// carries the name and the child does the reading.
/// </para>
/// </remarks>
public class TheConsoleCanSeeItsReadersTests
{
    [Test]
    public async Task It_can_read_which_trackers_this_machine_declares()
    {
        var readers = IntentConfiguration.FromEnvironment(
            declaration: "a-tracker=some-mcp --stdio",
            served: "",
            self: SelfInvocation.For("/usr/local/bin/gg", "/usr/local/bin/gg"));

        await Assert.That(readers).Count().IsEqualTo(1);
        await Assert.That(readers[0].Key).IsEqualTo("a-tracker");
    }

    [Test]
    public async Task It_can_learn_how_to_start_a_reader_this_binary_serves()
    {
        // WHAT THE PANE ACTUALLY NEEDS. Not the reader's identity - the command
        // line that starts it, so a browse can spawn one and speak to it.
        var readers = IntentConfiguration.FromEnvironment(
            declaration: "",
            served: "a-tracker=https://tracker.example/acme|local:acme/board",
            self: SelfInvocation.For("/usr/local/bin/gg", "/usr/local/bin/gg"));

        await Assert.That(readers[0].Command).IsEqualTo("/usr/local/bin/gg");
        await Assert.That(string.Join(" ", readers[0].Arguments)).Contains("runner read");
    }

    [Test]
    public async Task It_can_say_a_tracker_is_not_readable_here()
    {
        // The refusal a pane shows when a person asks to browse something no
        // reader declares - the same sentence the runner's pre-flight gate uses,
        // because two wordings for one state is two states as far as a reader
        // of the screen is concerned.
        var readers = IntentConfiguration.FromEnvironment(
            declaration: "", served: "", self: null);

        await Assert.That(IntentConfiguration.Unreadable("a-tracker", readers)).IsNotNull();
    }

    [Test]
    public async Task The_browse_contract_is_the_one_the_reader_answers()
    {
        // THE ANCHOR. BrowseTool already lives in Gg.Local and the console
        // already sees it; this asserts the two halves are in the same place, so
        // a pane asking "is this reader browsable" and a reader declaring that
        // it is are reading one declaration.
        await Assert.That(BrowseTool.IsBrowsable(["get_work_item", BrowseTool.Name])).IsTrue();
        await Assert.That(typeof(IntentConfiguration).Assembly)
            .IsEqualTo(typeof(BrowseTool).Assembly)
            .Because("the reader declaration and the browse contract are one project's job.");
    }
}
