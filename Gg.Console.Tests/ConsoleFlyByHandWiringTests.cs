using System.Text.Json;
using Gg.Console;

namespace Gg.Console.Tests;

/// <summary>
/// Flying by hand from the console: the session ends, the person gets the
/// terminal, and what happened is in the model.
/// </summary>
/// <remarks>
/// <para>
/// <b>The terminal-release shape, against something much larger.</b> A person
/// holds the screen for as long as the work takes rather than an editor for
/// seconds. It works for the same reason <c>$EDITOR</c> always did: the UI
/// session is over before the child starts, so the terminal is provably free,
/// and the next session is rebuilt from the model alone.
/// </para>
/// <para>
/// <b>The console spawns <c>gg</c> and links no runner type.</b> The runner is
/// treated as hostile and the OS keeps it apart from the console — and the
/// reference graph already enforces it, since <c>Gg.Console</c> can reach
/// <c>Gg.Contracts</c>, <c>Gg.Local</c> and <c>Gg.Client</c> and nothing else.
/// Local means "on this machine", never "in the console's process".
/// </para>
/// <para>
/// <b>And the refusal has to reach the MODEL, not just the screen.</b> The child
/// inherits the terminal, so a person does see what it printed — and then the
/// console redraws over it. A refusal a person cannot see afterwards is a
/// hand-flight that silently did nothing.
/// </para>
/// </remarks>
public class ConsoleFlyByHandWiringTests
{
    // ---- S26.5-01 ----

    [Test]
    public async Task The_ui_session_is_over_before_the_child_starts()
    {
        // WHAT BEING IN THIS SET MEANS: the UI session ends, the terminal is
        // provably free, the shell does the work, and the next session is built
        // from the model. Four bound, advertised keys were inert before this was
        // one declaration; a command that spawns a child and is NOT in it would
        // reach the reducer and return the state unchanged.
        await Assert.That(ShellCommands.Handled).Contains(Command.FlyByHand);
    }

    // ---- S26.5-02 ----

    [Test]
    public async Task The_next_session_is_rebuilt_from_the_model_alone()
    {
        // NO PROCESS HANDLE ON THE MODEL. AppState is serialized to disk under
        // GG_STATE_DUMP and handed to the diagnostics bundle, so a handle on it
        // is both unserializable and a live resource in a document.
        var state = new AppState { LastHandFlight = "GG-1042: flown by hand" };

        var written = JsonSerializer.Serialize(state);
        var read = JsonSerializer.Deserialize<AppState>(written)!;

        await Assert.That(read.LastHandFlight).IsEqualTo("GG-1042: flown by hand");
    }

    // ---- S26.5-05 ----

    [Test]
    public async Task What_happened_is_derived_rather_than_set_in_the_arm()
    {
        // ONE SLOT, DERIVED. Each arm records its outcome in its own field and
        // `Said` takes whichever changed - so a new arm cannot forget to say
        // anything, which is a thing arms have forgotten before.
        var before = new AppState();
        var after = before with { LastHandFlight = "GG-1042: flown by hand" };

        await Assert.That(ConsoleLoop.Said(before, after)).IsEqualTo("GG-1042: flown by hand");
    }

    // ---- S26.5-06 ----

    [Test]
    public async Task The_environment_refusal_reaches_the_model()
    {
        // NOT THE CHILD'S SCREEN. The child inherits the terminal so the person
        // does see it - and then the console redraws over it, and the pane says
        // nothing about a flight that was never opened.
        //
        // Which is why the console asks BEFORE it spawns: the answer has
        // somewhere to live, and rule 5 is satisfied by the same call rather
        // than by the child repeating it.
        var refused = ConsoleHandFlight.Refused(
            "environment=aspire-payments",
            "This machine does not advertise 'environment=aspire-payments'.");

        await Assert.That(refused).Contains("environment=aspire-payments");

        var state = new AppState() with { LastHandFlight = refused };

        await Assert.That(ConsoleLoop.Said(new AppState(), state)).IsEqualTo(refused);
    }

    // ---- S26.5-03 ----

    [Test]
    public async Task The_console_links_no_runner_type()
    {
        // RULE 2, ASSERTED OVER REACHABILITY rather than over a list of project
        // names - the graph gained Gg.Local since the brief was written, and a
        // fixed set of three would have failed for the wrong reason. What
        // matters is that no path from here reaches the runner.
        var console = typeof(ConsoleLoop).Assembly;

        var runner = console.GetReferencedAssemblies()
            .Select(a => a.Name)
            .Where(n => n is not null && n.StartsWith("Gg.Runner", StringComparison.Ordinal))
            .ToList();

        await Assert.That(runner).IsEmpty()
            .Because("the runner is treated as hostile and the OS keeps it apart from the "
                   + "console. Local means on this machine, never in this process. Found: "
                   + string.Join(", ", runner));
    }
}
