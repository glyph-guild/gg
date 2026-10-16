using System.Text.RegularExpressions;

namespace Gg.Runner.Tests;

/// <summary>
/// Every command a host runbook tells somebody to type installs something that
/// exists, at a version somebody named, where the runner cannot rewrite it.
/// </summary>
/// <remarks>
/// <para>
/// <b>Slice forty-three, S43.1-01.</b> Two runbooks bring a machine into the
/// fleet, and a person following either on a fresh VM hit a package that does
/// not exist (<c>GlyphGuild.Gg</c>), a pin thirty-three releases old, and an
/// install link that 404s. None of it failed anything: prose is read by people,
/// and people find out on the machine.
/// </para>
/// <para>
/// <b>The package id is read, never written here.</b> A test that hard-coded
/// <c>GlyphGuild.Gg.Cli</c> would pass on the day the csproj renamed it, which
/// is the drift this exists to catch - so it asks <c>Gg.Cli.csproj</c>.
/// </para>
/// <para>
/// <b>The call on pins: a pin in a runbook is the version being released, and a
/// release moves it.</b> cloud-init sat at 0.4.0 while the fleet ran 0.37 -
/// before the maintainer renewed its own credential, before the proxy allowed a
/// build, before a roll - and nothing noticed. Holding every runbook pin to
/// <c>VersionPrefix</c> means the release pull request that moves the version
/// moves the pins with it, in the same diff, where somebody reads them.
/// </para>
/// <para>
/// <b>The pins are the fleet's, and the README no longer has one</b> (owner,
/// 2026-10-15). A machine in the fleet runs a release somebody chose, so
/// cloud-init and both member images still pin and are still held here. A
/// person installing gg on a laptop takes the newest release - the installers
/// resolve it, and verify its attestation as they verify a named one - so the
/// README's commands name no version and <c>releases/latest</c> is a link it
/// may give: contract releases are never marked latest
/// (<c>ContractsAreNeverLatestTests</c>), so it always resolves to a gg release.
/// </para>
/// </remarks>
public class TheHostRunbooksNameWhatExistsTests
{
    private static readonly Regex Package = new(@"GlyphGuild\.[A-Za-z][A-Za-z.]*[A-Za-z]", RegexOptions.Compiled);

    private static readonly Regex Semver = new(@"\d+\.\d+\.\d+", RegexOptions.Compiled);

    /// <summary>What the CLI is actually published as.</summary>
    private static string PackageId()
    {
        var csproj = File.ReadAllText(Path.Combine(RepoRoot(), "Gg.Cli", "Gg.Cli.csproj"));
        return Regex.Match(csproj, "<PackageId>([^<]+)</PackageId>").Groups[1].Value;
    }

    /// <summary>The version this repository is releasing.</summary>
    private static string Releasing()
    {
        var props = File.ReadAllText(Path.Combine(RepoRoot(), "Directory.Build.props"));
        return Regex.Match(props, "<VersionPrefix>([^<]+)</VersionPrefix>").Groups[1].Value;
    }

    /// <summary>Everything a person follows to put gg on a machine.</summary>
    private static IReadOnlyList<(string File, string Text)> Runbooks()
    {
        var root = RepoRoot();

        return
        [
            .. Directory.EnumerateFiles(Path.Combine(root, "deploy"), "*", SearchOption.AllDirectories)
                .Concat(Directory.EnumerateFiles(Path.Combine(root, "packaging"), "*", SearchOption.AllDirectories))
                .Append(Path.Combine(root, "README.md"))
                .Order(StringComparer.Ordinal)
                .Select(f => (Path.GetRelativePath(root, f), File.ReadAllText(f))),
        ];
    }

    /// <summary>What a machine in the fleet is built from: everything under deploy and packaging.</summary>
    /// <remarks>
    /// The installer scripts live in deploy too, and they are not a fleet
    /// artifact: install.sh asks the forge for the newest release when it is
    /// given no version, which is what a person on a laptop wants.
    /// </remarks>
    private static bool IsFleet(string file) =>
        !file.Equals("README.md", StringComparison.Ordinal)
        && !Path.GetFileName(file).StartsWith("install.", StringComparison.Ordinal);

    /// <summary>
    /// Every line that tells somebody to install or update gg as a tool.
    /// </summary>
    /// <remarks>
    /// <b>A command, not a mention.</b> "`dotnet tool install --add-source` takes a
    /// directory" is prose about the tool and names no package; a line that names
    /// one is a line somebody will paste.
    /// </remarks>
    private static IReadOnlyList<(string File, string Line)> ToolLines() =>
    [
        .. from runbook in Runbooks()
           from line in runbook.Text.Split('\n')
           where Regex.IsMatch(line, @"dotnet tool (install|update)\b") && Package.IsMatch(line)
           select (runbook.File, $"{runbook.File}: {line.Trim()}"),
    ];

    [Test]
    public async Task There_are_tool_lines_to_check()
    {
        // THE ANCHOR. Every assertion below passes over nothing if the scan
        // stops finding the lines it is about.
        await Assert.That(ToolLines().Count).IsGreaterThanOrEqualTo(3)
            .Because("cloud-init, the pool README and the resident README each install the tool.");
    }

    [Test]
    public async Task Every_install_line_names_the_package_that_exists()
    {
        var id = PackageId();

        var wrong = ToolLines()
            .Select(t => t.Line)
            .Where(line => Package.Matches(line).Any(m => m.Value != id))
            .ToList();

        await Assert.That(wrong).IsEmpty()
            .Because($"the CLI is published as {id}, and a runbook naming anything else fails on the "
                   + "machine, not here. Found: " + string.Join(" | ", wrong));
    }

    [Test]
    public async Task Every_install_line_names_a_path_the_runner_cannot_write_and_the_fleet_pins_a_version()
    {
        // --tool-path rather than -g, everywhere, because -g installs into the
        // home of whoever typed it: on a host that is writable by the runner's
        // own user, and on this owner's Mac it lands on a PATH entry with a
        // literal `~` that resolves nowhere.
        //
        // --version on the FLEET's lines, because a fleet runs a release
        // somebody chose, and without one `dotnet tool` takes whatever reached
        // nuget.org last. The README's line names none since 2026-10-15: a
        // person installing the tool on a laptop takes the newest, by the
        // owner's decision.
        var loose = ToolLines()
            .Where(t => (IsFleet(t.File) && !t.Line.Contains("--version", StringComparison.Ordinal))
                     || !t.Line.Contains("--tool-path", StringComparison.Ordinal)
                     || Regex.IsMatch(t.Line, @"\s(-g|--global)(\s|$)"))
            .Select(t => t.Line)
            .ToList();

        await Assert.That(loose).IsEmpty()
            .Because("every install names a tool path and none is global, and every fleet install "
                   + "names its version. Found: " + string.Join(" | ", loose));
    }

    [Test]
    public async Task Nothing_the_fleet_is_built_from_resolves_latest()
    {
        // `releases/latest` WAS refused everywhere, when the contracts workflow
        // took it and the README's link 404'd. That is fixed where releases are
        // made (ContractsAreNeverLatestTests), and the README now links it on
        // purpose. A machine in the fleet still runs the release it was given,
        // so nothing cloud-init or a member image follows may float.
        //
        // LINKS, not mentions. A first formulation matched the bare words and
        // fired on a sentence explaining why there was no such link. A URL is
        // what somebody pastes.
        var floating = (from runbook in Runbooks()
                        where IsFleet(runbook.File)
                        from line in runbook.Text.Split('\n')
                        where Regex.IsMatch(line, @"https?://\S*/releases/latest\b")
                        select $"{runbook.File}: {line.Trim()}").ToList();

        await Assert.That(floating).IsEmpty()
            .Because("a fleet runs the release somebody named, and 'latest' is not one. Found: "
                   + string.Join(" | ", floating));
    }

    [Test]
    public async Task The_readme_installs_the_newest_release()
    {
        // THE OTHER HALF of the owner's decision: a person's one-line install
        // names no version, and a version pinned in the README would go stale
        // between releases for no gain.
        var readme = File.ReadAllText(Path.Combine(RepoRoot(), "README.md"));

        await Assert.That(readme).Contains("/releases/latest/download/install.sh");
        await Assert.That(readme).Contains("/releases/latest/download/install.ps1");
        await Assert.That(Regex.IsMatch(readme, @"(?m)^\$?v='?\d+\.\d+\.\d+")).IsFalse()
            .Because("the README's commands take the newest release; a pin belongs to the fleet.");
    }

    [Test]
    public async Task A_pin_in_a_runbook_is_the_version_being_released()
    {
        var releasing = Releasing();
        var root = RepoRoot();

        var cloudInit = File.ReadAllText(Path.Combine(root, "deploy", "pool-host", "cloud-init.yaml"));
        var memberImage = File.ReadAllText(Path.Combine(root, "deploy", "member-browser", "Dockerfile"));

        // AND THE DEV POOL'S, which joined this list because living outside it
        // is what let it rot. It was in gg-airspace, where nothing held its
        // version to anything: it reached gg 0.37.0 while every host ran
        // 0.67.0 - thirty releases - and three releases after a hand bump it
        // was stale again. Both member images are held to the release now.
        var devImage = File.ReadAllText(Path.Combine(root, "deploy", "member", "Dockerfile"));

        var pins = new List<(string Where, string Version)>();

        pins.AddRange(Regex.Matches(cloudInit, @"--version\s+(\d+\.\d+\.\d+)")
            .Select(m => ("cloud-init --version", m.Groups[1].Value)));
        pins.AddRange(Regex.Matches(cloudInit, @"releases/download/v(\d+\.\d+\.\d+)/")
            .Select(m => ("cloud-init bundle", m.Groups[1].Value)));
        pins.AddRange(Regex.Matches(cloudInit, @"runs gg (\d+\.\d+\.\d+)")
            .Select(m => ("cloud-init final message", m.Groups[1].Value)));
        pins.AddRange(Regex.Matches(memberImage, @"ARG GG_VERSION=(\d+\.\d+\.\d+)")
            .Select(m => ("member-browser", m.Groups[1].Value)));
        pins.AddRange(Regex.Matches(devImage, @"ARG GG_VERSION=(\d+\.\d+\.\d+)")
            .Select(m => ("member", m.Groups[1].Value)));

        foreach (var where in (string[])
            ["cloud-init --version", "cloud-init bundle", "member-browser", "member"])
        {
            await Assert.That(pins.Any(p => p.Where == where)).IsTrue()
                .Because($"no pin was found in {where}, so there is nothing to hold to the release.");
        }

        var stale = pins.Where(p => p.Version != releasing).Select(p => $"{p.Where} {p.Version}").ToList();

        await Assert.That(stale).IsEmpty()
            .Because($"this repository is releasing {releasing}, and a runbook pinned to another "
                   + "version installs something the fleet no longer runs. Found: "
                   + string.Join(", ", stale));
        await Assert.That(Semver.IsMatch(releasing)).IsTrue();
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
