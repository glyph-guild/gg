using System.Text.RegularExpressions;

namespace Gg.Cli.Tests;

/// <summary>
/// A refresh reads what boot read, because both go through one place.
/// </summary>
/// <remarks>
/// <para>
/// <b>The defect this is written against, and it shipped.</b> Boot built the
/// model and then applied a <c>with</c> block of local facts — the settings,
/// this machine's runner id, its name, and what the control plane offers. The
/// refresh called the same loader and applied none of them, so every one was
/// preserved from the state before it. Taking an offer and reopening help
/// showed the line about the offer that had just been taken.
/// </para>
/// <para>
/// <b>A field added to boot and forgotten in the refresh is the shape, and it
/// will happen again.</b> Two <c>with</c> blocks that have to agree cannot be
/// held to agreeing by anything but attention — the reason
/// <c>ConsoleLoop</c>'s own reload takes the whole model and answers with it
/// rather than naming six fields, which is written up in that method. This is
/// that argument applied one layer out.
/// </para>
/// <para>
/// <b>So it is one function, called twice.</b> The assertion is not that the
/// offer is re-read; it is that there is nowhere for the next field to be
/// added to only one of them.
/// </para>
/// </remarks>
public partial class BootAndRefreshReadTheSameThingsTests
{
    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Gg.sln")))
        {
            directory = directory.Parent;
        }

        var at = (directory ?? throw new InvalidOperationException("Gg.sln not found")).FullName;

        return File.ReadAllText(Path.Combine(at, "Gg.Cli", "Program.cs"));
    }

    [Test]
    public async Task Both_the_boot_and_the_reload_apply_the_local_facts()
    {
        var source = Root();

        var applications = LocalFacts().Count(source);

        await Assert.That(applications).IsGreaterThanOrEqualTo(3)
            .Because("one declaration and two callers: the boot and the reload. Fewer than "
                   + "three means one of the two paths is building the model its own way, "
                   + "which is how the offer came to be read at boot and never again.");
    }

    [Test]
    public async Task The_reload_does_not_rebuild_the_model_its_own_way()
    {
        // THE OTHER DIRECTION. Sharing a function is worth nothing if the
        // reload also carries a `with` block of its own - the two would drift
        // exactly as they did before, with an extra layer to read first.
        var source = Root();

        var at = source.IndexOf("reload: current =>", StringComparison.Ordinal);

        await Assert.That(at).IsGreaterThan(-1)
            .Because("the reload delegate was renamed, so this scan reads nothing.");

        var delegated = source[at..source.IndexOf("\n        signIn:", at, StringComparison.Ordinal)];

        await Assert.That(delegated).Contains("LocalFacts", StringComparison.Ordinal)
            .Because("a refresh that skips the local facts preserves them from the state "
                   + "before it, which is the whole defect.");

        await Assert.That(delegated).DoesNotContain("Settings =", StringComparison.Ordinal)
            .Because("a second place that assigns them is a second place to forget the "
                   + "next field.");
    }

    [Test]
    public async Task What_a_refresh_re_reads_includes_the_offer()
    {
        // NAMED, because it is the one whose staleness a person sees: they take
        // an offer, the file changes, and the page they took it from would go on
        // advertising it until the console was restarted.
        var source = Root();

        var at = source.IndexOf("static AppState LocalFacts", StringComparison.Ordinal);

        await Assert.That(at).IsGreaterThan(-1);

        var body = source[at..source.IndexOf("\nstatic ", at + 1, StringComparison.Ordinal)];

        await Assert.That(body).Contains("Offered =", StringComparison.Ordinal);
        await Assert.That(body).Contains("Settings =", StringComparison.Ordinal);
        await Assert.That(body).Contains("LocalRunnerId =", StringComparison.Ordinal)
            .Because("a runner registered since boot is a runner this console should see "
                   + "on a refresh, and it was in the boot block for that reason.");
    }

    [GeneratedRegex(@"\bLocalFacts\b")]
    private static partial Regex LocalFacts();
}
