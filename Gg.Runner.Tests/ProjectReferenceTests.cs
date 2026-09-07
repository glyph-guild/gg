using System.Xml.Linq;

namespace Gg.Runner.Tests;

/// <summary>
/// The shared project is shared, carries no packages, and stays small.
/// </summary>
/// <remarks>
/// <para>
/// <b>A project created so two halves can agree becomes a junk drawer unless
/// something says what belongs.</b> <c>Gg.Local</c> exists because
/// <c>Gg.Console</c> does not reference <c>Gg.Runner</c>, <c>Gg.Runner</c> does
/// not reference <c>Gg.Client</c>, and <c>Gg.Contracts</c> is the wire contract
/// - the artifact a customer audits and the package good-grief consumes from a
/// release. A filesystem path is not part of a contract between two machines.
/// </para>
/// <para>
/// Its charter is local paths and local configuration. <b>No transport, no
/// credential, no wire type</b> - and the way that stays true is a package list
/// that is empty, asserted here rather than hoped for. A second slice
/// (browsing-work-in-the-console) is its next tenant and will want a JSON-RPC
/// client in it; that client may be hand-written, and this test is what makes
/// somebody notice they are adding a dependency instead.
/// </para>
/// </remarks>
public class ProjectReferenceTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Gg.sln")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("no Gg.sln above the test binary");
    }

    private static XDocument Project(string name) =>
        XDocument.Load(Path.Combine(RepoRoot(), name, name + ".csproj"));

    private static IReadOnlyList<string> Refs(XDocument p, string kind) =>
        [.. p.Descendants(kind).Select(e => (string?)e.Attribute("Include") ?? "").Order(StringComparer.Ordinal)];

    [Test]
    public async Task Both_halves_reference_the_shared_project()
    {
        foreach (var half in new[] { "Gg.Runner", "Gg.Console" })
        {
            await Assert.That(Refs(Project(half), "ProjectReference"))
                .Contains(r => r.Contains("Gg.Local", StringComparison.Ordinal))
                .Because($"{half} computes the live view's path, and it computes it from the "
                       + "one implementation or the two halves name different files.");
        }
    }

    [Test]
    public async Task The_shared_project_adds_a_package_to_nobody()
    {
        await Assert.That(Refs(Project("Gg.Local"), "PackageReference")).IsEmpty()
            .Because("everything that references this inherits whatever it carries. It holds "
                   + "paths and configuration; the day it needs a package is the day to ask "
                   + "whether what is being added belongs here at all.");
        await Assert.That(Refs(Project("Gg.Local"), "ProjectReference")).IsEmpty()
            .Because("it sits below everything and depends on nothing - including "
                   + "Gg.Contracts, because a local path is not a wire type.");
    }

    [Test]
    public async Task The_console_carries_three_named_packages_and_the_runner_none()
    {
        // The stated shape of these two, which the shared project must not
        // change. If a dependency arrives it arrives visibly, here - and this
        // list moving is the whole mechanism working, not a nuisance to be
        // widened. It went from one to three when the console learned to host a
        // child in a pseudo-terminal instead of handing the terminal away, and
        // each of the two is here for a reason worth writing down:
        //
        //   Porta.Pty   spawns the child on a pty gg owns, so every byte in and
        //               out passes through this process and the bar survives.
        //   XTerm.NET   interprets what the child writes, because gg has to know
        //               what is on the child's screen to repaint it under a bar.
        //
        // Both are net10.0 and currently published. The better-known Pty.Net and
        // XtermSharp are unlisted on nuget.org and netstandard2.0, and XtermSharp
        // additionally depends on NStack.Core - Terminal.Gui v1's string library,
        // beside the v2 this console runs on.
        await Assert.That(Refs(Project("Gg.Console"), "PackageReference"))
            .IsEquivalentTo((string[])["Porta.Pty", "Terminal.Gui", "XTerm.NET"]);

        // AND THE RUNNER STILL CARRIES NOTHING, which is the half of this that
        // is load-bearing. The runner is treated as hostile and kept in its own
        // process; what it does not depend on is what it cannot be reached
        // through. Hosting a child is a console concern and must not leak here.
        await Assert.That(Refs(Project("Gg.Runner"), "PackageReference")).IsEmpty();
    }

    [Test]
    public async Task The_shared_project_is_in_the_solution()
    {
        // A project nothing builds is a project CI cannot fail on, and this one
        // is referenced by two halves that would still compile without it being
        // listed - so the omission would surface as a packaging surprise later.
        var solution = await File.ReadAllTextAsync(Path.Combine(RepoRoot(), "Gg.sln"));

        await Assert.That(solution).Contains("Gg.Local")
            .Because("dotnet build -c Release at the root is what CI runs, and it builds the "
                   + "solution.");
    }
}
