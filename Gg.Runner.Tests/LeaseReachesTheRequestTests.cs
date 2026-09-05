using System.Reflection;
using Gg.Contracts;
using Gg.Runner.Execution;

namespace Gg.Runner.Tests;

/// <summary>
/// A value the lease carries and the request has a slot for is actually copied.
/// </summary>
/// <remarks>
/// <para>
/// <b>THREE TIMES IN ONE DAY, a thing that exists and is never constructed.</b>
/// An executor held by no runner, a take session constructed by no console, and
/// <c>LeaseLoop.Instructions</c> rendered by the contract and read by the prompt
/// with nothing carrying it between them. The third had tests on both halves and
/// was green end to end while not one byte reached an agent.
/// </para>
/// <para>
/// <b>Why <c>required</c> would not have caught it, though it is the first thing
/// to reach for.</b> A required member forces every construction site to decide,
/// which is exactly right for a value whose absence is a bug. But absence is a
/// real state for all three of these - no rejection, no prior attempt, no
/// standing instructions - so the rule "required unless absence means something"
/// leaves precisely this family optional, and the compiler stays silent. What
/// was missing is not a decision at the construction site; it is the WIRE
/// between two types that both already have the member.
/// </para>
/// <para>
/// <b>So the ratchet is over the pairing.</b> A name on both <c>LeaseLoop</c>
/// and <c>ExecutorRequest</c> is a value the lease carries and the loop needs;
/// the runner is the only thing that can join them, and it must be seen doing
/// it. This fails the day somebody adds a member to both types and not to the
/// initializer, which is the day the defect is cheap.
/// </para>
/// <para>
/// <b>What it does not reach.</b> A pair that changed names in transit -
/// <c>WallClockSeconds</c> becoming <c>WallClock</c> - shares no name and is
/// invisible here. The guard is a name match, not a dataflow analysis, and it
/// buys the common case rather than the general one.
/// </para>
/// </remarks>
public class LeaseReachesTheRequestTests
{
    /// <summary>
    /// A shared name the runner deliberately does not copy, and why. Empty, so
    /// the first entry has to say what it means.
    /// </summary>
    private static readonly Dictionary<string, string> Unwired = [];

    private static IReadOnlyList<string> SharedNames()
    {
        var onTheRequest = typeof(ExecutorRequest)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .ToHashSet(StringComparer.Ordinal);

        return
        [
            .. typeof(LeaseLoop)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(p => p.Name)
                .Where(onTheRequest.Contains)
                .OrderBy(name => name, StringComparer.Ordinal),
        ];
    }

    /// <summary>The runner's <c>new ExecutorRequest { … }</c>, braces balanced.</summary>
    private static string TheInitializer()
    {
        var source = File.ReadAllText(
            Path.Combine(RepoRoot(), "Gg.Runner", "RunnerLoop.cs"));

        var at = source.IndexOf("new ExecutorRequest", StringComparison.Ordinal);
        if (at < 0)
        {
            throw new InvalidOperationException(
                "RunnerLoop no longer builds an ExecutorRequest by initializer; this guard "
              + "reads one and has to be rewritten rather than deleted.");
        }

        var open = source.IndexOf('{', at);
        var depth = 0;
        for (var i = open; i < source.Length; i++)
        {
            depth += source[i] switch { '{' => 1, '}' => -1, _ => 0 };
            if (depth == 0)
            {
                return source[open..(i + 1)];
            }
        }

        throw new InvalidOperationException("unbalanced braces in the ExecutorRequest initializer");
    }

    [Test]
    public async Task Every_value_the_lease_and_the_request_both_name_is_carried_across()
    {
        var initializer = TheInitializer();

        var dropped = SharedNames()
            .Where(name => !Unwired.ContainsKey(name))
            .Where(name => !initializer.Contains($"{name} = loop.{name}", StringComparison.Ordinal))
            .ToList();

        await Assert.That(dropped).IsEmpty()
            .Because("a member on both LeaseLoop and ExecutorRequest is a value the control "
                   + "plane sent for the loop to use, and the runner is the only thing that "
                   + "can join them. Instructions sat on both types for a whole slice with "
                   + "no line copying it, and every test on either side passed. Copy it, or "
                   + "add it to Unwired with what its absence means. Found: "
                   + string.Join(", ", dropped));
    }

    [Test]
    public async Task The_guard_is_looking_at_something()
    {
        // LIVENESS. An empty intersection, or an initializer this stopped being
        // able to find, would pass the assertion above forever.
        var shared = SharedNames();

        await Assert.That(shared).Contains("Instructions")
            .Because("the member whose absence this guard was written for.");
        await Assert.That(shared).Contains("ResumesFrom")
            .Because("the member whose absence ResumptionContextTests was written for.");
        await Assert.That(shared.Count).IsGreaterThanOrEqualTo(4);

        var initializer = TheInitializer();
        await Assert.That(initializer).Contains("LoopId = loop.LoopId");
        await Assert.That(initializer).EndsWith("}");
    }

    [Test]
    public async Task It_notices_a_name_that_is_not_carried()
    {
        // PLANTED. The check is a substring match, so it has to be shown failing
        // on a name the initializer does not assign.
        var initializer = TheInitializer();

        await Assert.That(initializer.Contains("Executor = loop.Executor", StringComparison.Ordinal))
            .IsFalse()
            .Because("Executor is on the lease and not on the request, so if this matched, "
                   + "the check would be matching something other than what it reads.");
    }

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null
            && !File.Exists(Path.Combine(directory.FullName, "Gg.Contracts", "fact-vocabulary.json")))
        {
            directory = directory.Parent;
        }

        return (directory ?? throw new InvalidOperationException("repository root not found")).FullName;
    }
}
