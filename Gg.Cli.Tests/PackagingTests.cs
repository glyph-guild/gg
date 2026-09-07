using System.Xml.Linq;

namespace Gg.Cli.Tests;

/// <summary>
/// The command a person types, and the package that installs it.
/// </summary>
/// <remarks>
/// <para>
/// <b>There was no way to obtain this binary except by building it.</b> Three
/// files said so in prose, and a pool host paid for it: the .NET SDK, a git
/// clone, and a platform linker on every machine that wanted to run a runner.
/// </para>
/// <para>
/// <b>The package id and the command are different names, and only one of them
/// is typed.</b> <c>gg</c> is taken on nuget.org, so the package is
/// <c>GlyphGuild.Gg.Cli</c> — matching <c>GlyphGuild.Gg.Contracts</c> — while
/// <c>ToolCommandName</c> keeps the command <c>gg</c>. Nothing else in the
/// repository would notice if that drifted: no test asserts the printed name,
/// and every script and doc would simply be wrong at once.
/// </para>
/// <para>
/// <b><c>PublishAot</c> stays, and is overridden only at pack time.</b> A tool
/// package is IL and a native binary is not, so the two cannot both apply to
/// one invocation. Leaving AOT as the project's default keeps
/// <c>dotnet publish</c> and CI's <c>aot</c> job behaving exactly as they did,
/// and confines the exception to the one command that needs it — asserted here
/// so that "just turn AOT off in the csproj" does not quietly become the fix.
/// </para>
/// </remarks>
public class PackagingTests
{
    private static XDocument CliProject() => XDocument.Load(RepoFile("Gg.Cli", "Gg.Cli.csproj"));

    private static string? Property(XDocument project, string name) => project
        .Descendants()
        .FirstOrDefault(e => e.Name.LocalName == name)?.Value;

    [Test]
    public async Task The_cli_is_packable_as_a_tool()
    {
        var project = CliProject();

        await Assert.That(Property(project, "PackAsTool")).IsEqualTo("true")
            .Because("without this the package is a library nobody can install, and the only "
                   + "way to get gg stays 'clone it and build it'.");
        await Assert.That(Property(project, "IsPackable")).IsEqualTo("true");
    }

    [Test]
    public async Task The_command_is_gg_whatever_the_package_is_called()
    {
        // THE PAIR THAT MUST NOT DRIFT. The package id cannot be `gg` - that id
        // belongs to somebody else on nuget.org - so the two names differ on
        // purpose, and the one people type is the one nothing else asserts.
        var project = CliProject();

        await Assert.That(Property(project, "ToolCommandName")).IsEqualTo("gg")
            .Because("every script, doc and systemd unit in both repositories invokes `gg`. "
                   + "Renaming the command breaks all of them at once and no other test looks.");
        await Assert.That(Property(project, "PackageId")).IsEqualTo("GlyphGuild.Gg.Cli")
            .Because("it matches GlyphGuild.Gg.Contracts, and fixing it now is cheaper than "
                   + "discovering it after something is published under another name.");
    }

    [Test]
    public async Task The_package_declares_a_licence_because_warnings_are_errors()
    {
        // TreatWarningsAsErrors is repo-wide, and a pack with no licence raises
        // NU5125. So this is not metadata hygiene - without it the package does
        // not build at all.
        await Assert.That(Property(CliProject(), "PackageLicenseExpression")).IsEqualTo("MIT");
    }

    [Test]
    public async Task Native_publishing_is_still_the_projects_default()
    {
        // The binaries are what a machine with no .NET runtime installs, which
        // is every pool host. If AOT were turned off here to make packing work,
        // that would stop being true and the only sign would be a much smaller
        // release asset.
        await Assert.That(Property(CliProject(), "PublishAot")).IsEqualTo("true")
            .Because("a tool package needs the runtime; the native binary needs nothing. Pack "
                   + "overrides this per-invocation - it must not be removed from the project.");
    }

    [Test]
    public async Task This_repository_publishes_exactly_two_things()
    {
        // The five test projects already opt out. The three internal libraries
        // do NOT, so `dotnet pack` at the root produces Gg.Client, Gg.Console
        // and Gg.Runner packages beside the two intended ones.
        //
        // Nothing packs the root today, so this is latent rather than broken -
        // but a published Gg.Client is a public API surface nobody designed,
        // and the moment it exists somebody can depend on it. Named rather than
        // counted, for the reason ContractsDependencyTests gives: "at most
        // three" would let the next one in by swapping which.
        var packable = Directory
            .EnumerateFiles(RepoRoot(), "*.csproj", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(f => !string.Equals(
                Property(XDocument.Load(f), "IsPackable"), "false", StringComparison.OrdinalIgnoreCase))
            .Select(f => Path.GetFileNameWithoutExtension(f))
            .Order(StringComparer.Ordinal)
            .ToList();

        await Assert.That(packable).IsEquivalentTo((string[])["Gg.Cli", "Gg.Contracts"])
            .Because("this repository publishes the CLI people install and the contract "
                   + "consumers audit. Everything else is bundled inside the first and is not "
                   + "an API anybody should be able to reference. Found: "
                   + string.Join(", ", packable));
    }

    /// <summary>The release workflow, as text, because it is what ships.</summary>
    private static string ReleaseWorkflow() =>
        File.ReadAllText(RepoFile(".github", "workflows", "publish-cli.yml"));

    [Test]
    public async Task The_release_carries_the_whole_publish_directory_not_a_named_file()
    {
        // MEASURED, NOT ASSUMED, on 2026-09-07. An AOT publish of Gg.Cli emits
        // `gg` AND native libraries beside it - libonigwrap through Terminal.Gui,
        // libporta_pty through Porta.Pty - and .NET resolves a P/Invoke on FIRST
        // CALL rather than at startup. A release that carries only `gg` therefore
        // builds clean, starts clean, prints its version clean, and dies on the
        // keypress that first reaches that code.
        //
        // Verified both directions with an AOT build: with the library beside it
        // the child spawns; with the binary alone it is a DllNotFoundException,
        // and the loader's own probe list shows it looking in the executable's
        // directory first. So "beside the binary" is the whole requirement.
        //
        // THE DIRECTORY RATHER THAN A LIST OF NAMES, and that is the durable
        // half. A list is right until the next package brings an asset, and then
        // it is silently wrong - libonigwrap has been missing this whole time
        // without anybody noticing, presumably because nothing gg does calls
        // into it.
        var workflow = ReleaseWorkflow();

        await Assert.That(workflow).DoesNotContain("-C out gg", StringComparison.Ordinal)
            .Because("naming the binary is what left its native libraries behind.");

        await Assert.That(workflow).Contains("-C out .", StringComparison.Ordinal)
            .Because("packaging the directory picks up whatever the publish produced, "
                   + "including the asset the next dependency brings.");
    }

    [Test]
    public async Task The_release_does_not_ship_debug_symbols()
    {
        // The consequence of packaging the directory rather than a name: the
        // publish output also holds .pdb files and, on macOS, a .dSYM bundle.
        // They are larger than the binary and nobody downloading a release
        // asset wants them.
        var workflow = ReleaseWorkflow();

        foreach (var symbols in (string[])[".pdb", ".dSYM"])
        {
            await Assert.That(workflow).Contains(symbols, StringComparison.Ordinal)
                .Because($"packaging a directory means {symbols} has to be removed by name, "
                       + "or the release grows by more than the binary it contains.");
        }
    }

    [Test]
    public async Task Both_places_that_document_installing_put_the_library_beside_the_binary()
    {
        // TWO COPIES OF ONE INSTRUCTION. The README and the release notes each
        // tell somebody how to install gg, and they are written out separately -
        // so a fix applied to one leaves the other telling people to do the thing
        // that does not work.
        //
        // The destination is what matters rather than the wording: the loader
        // probes the executable's own directory, so both files have to land in
        // the same place. A shared library in a bin directory is unconventional
        // and it is exactly where dlopen looks.
        foreach (var (what, text) in ((string, string)[])
                 [("the release notes", ReleaseWorkflow()),
                  ("the README", File.ReadAllText(RepoFile("README.md")))])
        {
            await Assert.That(text).Contains("libporta_pty", StringComparison.Ordinal)
                .Because($"{what} tells somebody how to install gg, and an install that "
                       + "copies one file gives them a console whose editor never has a bar "
                       + "and no way to find out why.");
        }
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Gg.sln")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName
            ?? throw new InvalidOperationException("Gg.sln not found above " + AppContext.BaseDirectory);
    }

    private static string RepoFile(params string[] parts)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Gg.sln")))
        {
            dir = dir.Parent;
        }

        return dir is null
            ? throw new InvalidOperationException("Gg.sln not found above " + AppContext.BaseDirectory)
            : Path.Combine([dir.FullName, .. parts]);
    }
}
