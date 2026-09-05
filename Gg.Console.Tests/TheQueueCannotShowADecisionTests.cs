using System.Reflection;

namespace Gg.Console.Tests;

/// <summary>
/// The pane called "flights needing me" cannot contain a flight that needs me.
/// </summary>
/// <remarks>
/// <para>
/// <b>The queue's own vocabulary names the case it cannot produce.</b>
/// <c>QueueReason.AwaitingDecision</c> exists, is rendered by <c>PaneText</c> as
/// <i>"awaiting a decision"</i>, and carries a doc comment calling it <i>"the
/// reason this pane is a queue of DECISIONS rather than a list of flights"</i>.
/// Nothing in the product ever produces one.
/// </para>
/// <para>
/// <b>Because the projection is not given gates.</b>
/// <c>ConsoleProjection.Queue(flights, logs, runners)</c> takes three arguments and a
/// gate is in none of them, so the function could not answer the question even
/// if somebody asked it to. The two reasons it can produce are a lease that
/// expired twice and a runner that went offline holding the flight - both
/// trouble, neither a decision.
/// </para>
/// <para>
/// <b>What that costs a tenant.</b> Gates ARE loaded at boot, into
/// <c>AppState.Gates</c>, and they feed the modal - which opens on the SELECTED
/// row. So a tenant whose flights are all waiting on a person, and none of whose
/// runners has died, opens the console to an empty queue, cannot select
/// anything, and therefore cannot open the modal that holds the decisions. The
/// console's whole purpose is unreachable by the ordinary path.
/// </para>
/// <para>
/// <b>Asserted as an absence, on purpose.</b> These are the twelfth instance of
/// registered-and-never-invoked in this estate and the point is that the
/// SYMBOLS exist. A test that asserted the two reasons it does produce would
/// pass today and pass for ever without noticing the third.
/// </para>
/// </remarks>
public class TheQueueCannotShowADecisionTests
{
    private static IReadOnlyList<string> ProductionSources()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "Gg.Console")))
        {
            dir = dir.Parent;
        }

        return dir is null
            ? []
            : [.. Directory.EnumerateFiles(
                    Path.Combine(dir.FullName, "Gg.Console"), "*.cs", SearchOption.AllDirectories)
                .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
                                        StringComparison.Ordinal)
                         && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}",
                                        StringComparison.Ordinal))];
    }

    [Test]
    public async Task Nothing_produces_the_reason_the_pane_exists_for()
    {
        var sources = ProductionSources();

        await Assert.That(sources).IsNotEmpty()
            .Because("the scan found no console source, so it asserted nothing.");

        // WHERE IT IS ALLOWED TO APPEAR: the enum that declares it, and the
        // renderer that turns it into words. Anywhere else would be a producer,
        // and a producer is exactly what is missing.
        var mentions = sources
            .Where(f => File.ReadAllText(f).Contains("AwaitingDecision", StringComparison.Ordinal))
            .Select(f => Path.GetFileName(f)!)
            .Order(StringComparer.Ordinal)
            .ToList();

        await Assert.That(mentions).IsEquivalentTo((string[])["AppState.cs", "PaneText.cs"])
            .Because("declared and rendered and made by nothing. If this list grew, somebody "
                   + "gave the queue its decisions and this test is the one to delete. "
                   + "Found: " + string.Join(", ", mentions));
    }

    [Test]
    public async Task The_projection_is_never_told_about_gates()
    {
        // THE MECHANISM UNDER THE ABSENCE. It is not that the function forgets
        // to look at gates - it is that it is not given any, so no amount of
        // reading its body would find the case. The signature is the finding.
        var queue = typeof(ConsoleProjection).GetMethod(
            "Queue", BindingFlags.Public | BindingFlags.Static);

        await Assert.That(queue).IsNotNull()
            .Because("the projection this slice is about has moved or been renamed, and this "
                   + "test is asserting about a function that no longer exists.");

        var parameters = queue!.GetParameters().Select(p => p.ParameterType.Name).ToList();

        await Assert.That(parameters.Any(n => n.Contains("Gate", StringComparison.Ordinal)))
            .IsFalse()
            .Because("a gate is not among what the queue is given, so 'flights needing me' is "
                   + "computed from things that are not people needing anything. Takes: "
                   + string.Join(", ", parameters));
    }
}
