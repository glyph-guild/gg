using System.Text.RegularExpressions;

namespace Gg.Cli.Tests;

/// <summary>
/// A refusal one verb path reports is one every verb path reports.
/// </summary>
/// <remarks>
/// <para>
/// <b>Measured against the live control plane, 2026-10-05.</b> `gg airspace apply` printed
/// a stack trace instead of a reason:
/// </para>
/// <code>
/// Unhandled exception. Gg.Client.StrategyRefusedException
///    at Gg.Client.ControlPlaneClient.ApplyExposureAsync...
///    at Gg.Client.FlightCommands.AirspaceApplyAsync...
///    at Program.g__EmitAsync...
/// </code>
/// <para>
/// <b>And the handler existed.</b> <c>StrategyAsync</c> catches
/// <c>StrategyRefusedException</c> and calls <c>Fail</c> with its message. <c>EmitAsync</c>
/// does not — so the same refusal is a sentence on one path and a crash on another. The
/// exception is NAMED for strategies and is thrown on the airspace path too, which is how a
/// per-path handler list goes stale without anybody noticing.
/// </para>
/// <para>
/// <b>Why a superset rather than equality.</b> Catching a refusal a path cannot raise costs
/// nothing; failing to catch one it can raise is a person losing the reason they were given.
/// So the question asked here is whether the broadest path — <c>EmitAsync</c>, which runs
/// every flight verb including the airspace ones — handles everything its siblings handle.
/// </para>
/// <para>
/// <b>A refusal is not an error.</b> The control plane saying no is the system working; the
/// caller has to be told WHAT it said. A stack trace discards exactly that, and in this case
/// it discarded the only record of why the apply was refused — the container log tail no
/// longer reached back far enough to recover it.
/// </para>
/// </remarks>
public class ARefusalIsReportedOnEveryPathTests
{
    private static string Program() =>
        File.ReadAllText(Path.Combine(RepoRoot(), "Gg.Cli", "Program.cs"));

    /// <summary>The exception type names a helper's own catch clauses name.</summary>
    private static HashSet<string> CaughtBy(string helper)
    {
        var text = Program();

        var start = text.IndexOf($"static async Task<int> {helper}(", StringComparison.Ordinal);

        if (start < 0)
        {
            throw new InvalidOperationException(
                $"{helper} is not in Program.cs - if it was renamed, this test has to follow it "
              + "rather than silently pass over a path nobody is checking.");
        }

        // TO THE NEXT TOP-LEVEL HELPER, because a catch list read past the end of its own
        // method would borrow its neighbour's and report agreement that is not there.
        var rest = text[start..];
        var next = rest.IndexOf("\nstatic ", StringComparison.Ordinal);
        var body = next > 0 ? rest[..next] : rest;

        return Regex.Matches(body, @"catch \((?<type>[A-Za-z]+Exception)")
            .Select(m => m.Groups["type"].Value)
            .Where(t => !t.StartsWith("System", StringComparison.Ordinal))
            .ToHashSet(StringComparer.Ordinal);
    }

    [Test]
    public async Task The_flight_path_reports_every_refusal_its_siblings_report()
    {
        var emit = CaughtBy("EmitAsync");
        var siblings = CaughtBy("EnvelopeAsync");
        siblings.UnionWith(CaughtBy("StrategyAsync"));

        // FRAMEWORK TYPES ARE NOT REFUSALS. A refusal is the control plane answering no; an
        // IOException is a machine failing, and the paths may reasonably differ there.
        siblings.RemoveWhere(t => t is "IOException" or "HttpRequestException"
                                    or "ArgumentException" or "InvalidOperationException"
                                    or "UnauthorizedAccessException");

        var missing = siblings.Except(emit).OrderBy(t => t, StringComparer.Ordinal).ToList();

        await Assert.That(missing).IsEmpty()
            .Because("every one of these is a refusal a sibling path turns into a sentence and "
                   + "this path lets escape as a stack trace. `gg airspace apply` runs through "
                   + "EmitAsync and throws StrategyRefusedException, which is how a live apply "
                   + "crashed instead of saying why it was refused. Missing: "
                   + string.Join(", ", missing));
    }

    [Test]
    public async Task Each_helper_really_catches_something_so_the_scan_cannot_pass_by_finding_nothing()
    {
        // THE LIVENESS ANCHOR. A regex that matched nothing would make the test above pass
        // for the worst possible reason, and this repository has shipped that mistake before.
        foreach (var helper in (string[]) ["EmitAsync", "EnvelopeAsync", "StrategyAsync"])
        {
            await Assert.That(CaughtBy(helper).Count).IsGreaterThan(2)
                .Because($"{helper} is known to catch several refusals, so a scan finding none "
                       + "has stopped reading the thing it claims to read.");
        }
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
