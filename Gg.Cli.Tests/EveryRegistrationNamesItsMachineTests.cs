using System.Text.RegularExpressions;

namespace Gg.Cli.Tests;

/// <summary>
/// Every place the CLI registers a runner says which machine it is on.
/// </summary>
/// <remarks>
/// <para>
/// <b>Three call sites, one fact.</b> The resident (<c>gg runner up</c>), the
/// attended runner and the pool maintainer each register separately and each
/// builds its label from <c>Environment.MachineName</c>. A site that forgot
/// the machine would put that runner on no machine at all, and the fleet
/// would show it outside the host it plainly runs on.
/// </para>
/// <para>
/// <b>A scan, because the omission compiles.</b> The parameter is optional so
/// an older caller keeps working, which is exactly what lets a new call site
/// leave it out without a single error. This is the only thing that notices.
/// </para>
/// </remarks>
public partial class EveryRegistrationNamesItsMachineTests
{
    private static string ProgramText()
    {
        var here = new DirectoryInfo(AppContext.BaseDirectory);
        while (here is not null && !Directory.Exists(Path.Combine(here.FullName, "Gg.Cli")))
        {
            here = here.Parent;
        }

        return File.ReadAllText(Path.Combine(here!.FullName, "Gg.Cli", "Program.cs"));
    }

    // A call, up to the `);` that closes it. NOT up to the first `;`: the
    // maintainer's call carries a comment with a semicolon in it, and a match
    // that stopped there never reached the argument it was looking for.
    [GeneratedRegex(@"RegisterRunnerAsync\((?<args>.*?)\);", RegexOptions.Singleline)]
    private static partial Regex Call();

    [Test]
    public async Task Every_call_site_passes_the_machine()
    {
        var calls = Call().Matches(ProgramText()).Select(m => m.Groups["args"].Value).ToList();

        await Assert.That(calls.Count).IsGreaterThanOrEqualTo(3)
            .Because("the resident, the attended runner and the maintainer each register; "
                   + "fewer found means this scan stopped looking at them.");

        var silent = calls.Where(a => !a.Contains("machine:", StringComparison.Ordinal)).ToList();

        await Assert.That(silent).IsEmpty()
            .Because("a registration that names no machine puts its runner on no host, and "
                   + "the fleet then shows it apart from the machine it runs on. Unnamed: "
                   + string.Join(" | ", silent.Select(a => a.Trim()[..Math.Min(60, a.Trim().Length)])));
    }
}
