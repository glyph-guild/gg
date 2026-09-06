using System.Reflection;

namespace Gg.Console.Tests;

/// <summary>
/// Every port the loop takes is one the composition root passes, or one this
/// file says out loud is not passed yet and why.
/// </summary>
/// <remarks>
/// <para>
/// <b>`y' blinks.</b> The key is bound, it is on the hint line, it is in
/// <c>ShellCommands.Handled</c>, the loop has an arm for it, and
/// <c>ConsoleHandFlight</c> has the pieces it would use - and <c>Program.cs</c>
/// passes no <c>flyByHand</c>, so the arm takes its null branch, records a
/// sentence admitting the console is not configured, and rebuilds. From the
/// outside that is a flicker.
/// </para>
/// <para>
/// <b>This is the twelfth time.</b> The comments in this console name the
/// others: take and hand were optional arguments only tests ever supplied for
/// two whole slices; the browser was constructed nowhere; the credential list
/// and the notices were fetched by nothing; <c>QueueReason.AwaitingDecision</c>
/// was declared, rendered and produced by nothing. Every one was a thing that
/// existed, was tested, and was never reached from the composition root - and
/// every one was found by a person pressing the key.
/// </para>
/// <para>
/// <b>An optional port is what makes it possible.</b> The default is null so a
/// test can build a loop with three arguments, which is worth keeping; what it
/// costs is that forgetting one is silent. So the exemption list is the price:
/// a port that is not wired has to be named here, with the reason, and a
/// reason that stops being true fails the row below it.
/// </para>
/// </remarks>
public class EveryPortIsPassedTests
{
    /// <summary>Ports the composition root deliberately does not pass.</summary>
    private static readonly Dictionary<string, string> NotWiredYet = new()
    {
        ["hand"] =
            "HandSession needs two ports the product does not have: an `infer` that spawns "
          + "an agent to propose what appears to have been done, and an `ask` that reads a "
          + "confirmation from the terminal. Building the first means invoking an executor "
          + "from the console, which is a boundary the slice that added the key does not "
          + "touch. So the hand-back key answers `this console is not configured to hand "
          + "flights back', and ConsoleTakeWiringTests says so out loud rather than "
          + "asserting a wiring that would have to be faked to pass.",
    };

    /// <summary>
    /// The arguments of the one <c>new ConsoleLoop(...)</c> in the composition
    /// root, and nothing else in the file.
    /// </summary>
    /// <remarks>
    /// <b>Sliced rather than searched, because the names are not unique.</b>
    /// <c>Bundle.Build</c> in the same file takes a <c>flightLog:</c> of its own
    /// and it is a different thing entirely - a guard that read the whole file
    /// would have called this port wired, and later called it null, on the
    /// strength of a diagnostics call twenty lines away.
    /// </remarks>
    private static string TheCall()
    {
        var program = Sources.Read("Gg.Cli", "Program.cs");
        var opened = program.IndexOf("new ConsoleLoop(", StringComparison.Ordinal);
        var closed = program.IndexOf(".Run(initial)", StringComparison.Ordinal);

        return opened >= 0 && closed > opened ? program[opened..closed] : "";
    }

    private static IReadOnlyList<ParameterInfo> Ports() =>
        [.. typeof(ConsoleLoop)
            .GetConstructors()
            .Single()
            .GetParameters()
            .Where(p => p.IsOptional)];

    [Test]
    public async Task Every_optional_port_is_passed_by_the_composition_root()
    {
        var program = TheCall();

        await Assert.That(program).IsNotEmpty()
            .Because("the composition root builds one ConsoleLoop and this guard reads it. "
                   + "An empty slice passes every row below by finding nothing.");

        var missing = Ports()
            .Select(p => p.Name!)
            .Where(name => !NotWiredYet.ContainsKey(name))
            .Where(name => !program.Contains(name + ":", StringComparison.Ordinal))
            .ToList();

        await Assert.That(missing).IsEmpty()
            .Because("a port nobody passes is a key that reaches its arm, takes the null "
                   + "branch and redraws - which reads as a flicker rather than as a "
                   + "feature that was never wired. Found: " + string.Join(", ", missing));
    }

    [Test]
    public async Task And_none_of_them_is_passed_as_null()
    {
        // THE OTHER HALF, because `hand: null` satisfies the row above while
        // being exactly the thing it is looking for. Naming it and passing
        // nothing is how a gap gets to look like a wiring.
        var program = TheCall();

        var nulled = Ports()
            .Select(p => p.Name!)
            .Where(name => !NotWiredYet.ContainsKey(name))
            .Where(name => program.Contains(name + ": null", StringComparison.Ordinal))
            .ToList();

        await Assert.That(nulled).IsEmpty()
            .Because("passed as null is not passed. Found: " + string.Join(", ", nulled));
    }

    [Test]
    public async Task The_exemption_list_names_nothing_that_is_wired()
    {
        // THE ROW THAT KEEPS THE LIST HONEST. An exemption that stopped being
        // needed is a hole nobody is looking at - the shape ModalEscapeTests and
        // ConsoleDataReachTests already use for the same hazard.
        var program = TheCall();

        var stale = NotWiredYet.Keys
            .Where(name => program.Contains(name + ":", StringComparison.Ordinal)
                        && !program.Contains(name + ": null", StringComparison.Ordinal))
            .ToList();

        await Assert.That(stale).IsEmpty()
            .Because("wired, so the reason beside it is no longer true. Found: "
                   + string.Join(", ", stale));
    }

    [Test]
    public async Task The_exemption_list_names_only_ports_that_exist()
    {
        var unknown = NotWiredYet.Keys
            .Except(Ports().Select(p => p.Name!))
            .ToList();

        await Assert.That(unknown).IsEmpty()
            .Because("a renamed port leaves its excuse behind, still passing. Found: "
                   + string.Join(", ", unknown));
    }
}
