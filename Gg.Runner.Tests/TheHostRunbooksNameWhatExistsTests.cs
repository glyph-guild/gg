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

    /// <summary>
    /// Every line that tells somebody to install or update gg as a tool.
    /// </summary>
    /// <remarks>
    /// <b>A command, not a mention.</b> "`dotnet tool install --add-source` takes a
    /// directory" is prose about the tool and names no package; a line that names
    /// one is a line somebody will paste.
    /// </remarks>
    private static IReadOnlyList<string> ToolLines() =>
    [
        .. from runbook in Runbooks()
           from line in runbook.Text.Split('\n')
           where Regex.IsMatch(line, @"dotnet tool (install|update)\b") && Package.IsMatch(line)
           select $"{runbook.File}: {line.Trim()}",
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
            .Where(line => Package.Matches(line).Any(m => m.Value != id))
            .ToList();

        await Assert.That(wrong).IsEmpty()
            .Because($"the CLI is published as {id}, and a runbook naming anything else fails on the "
                   + "machine, not here. Found: " + string.Join(" | ", wrong));
    }

    [Test]
    public async Task Every_install_line_pins_a_version_and_a_path_the_runner_cannot_write()
    {
        // --version, because without one `dotnet tool` takes whatever reached
        // nuget.org last, and a stolen API key is what reaches it. --tool-path
        // rather than -g, because -g installs into the home of whoever typed it:
        // on a host that is writable by the runner's own user, and on this
        // owner's Mac it lands on a PATH entry with a literal `~` that resolves
        // nowhere.
        var loose = ToolLines()
            .Where(line => !line.Contains("--version", StringComparison.Ordinal)
                        || !line.Contains("--tool-path", StringComparison.Ordinal)
                        || Regex.IsMatch(line, @"\s(-g|--global)(\s|$)"))
            .ToList();

        await Assert.That(loose).IsEmpty()
            .Because("every install names its version and a tool path, and none is global. Found: "
                   + string.Join(" | ", loose));
    }

    [Test]
    public async Task Nothing_a_person_follows_resolves_latest()
    {
        // `releases/latest` is whichever release the forge marks latest, and the
        // contracts workflow publishes one on most pushes - so the README's
        // install link resolved to a contract release with no gg assets, and
        // 404'd.
        //
        // LINKS, not mentions. A first formulation matched the bare words and
        // fired on the README sentence explaining why there is no such link -
        // which is the opposite of the defect. A URL is what somebody pastes.
        var floating = (from runbook in Runbooks()
                        from line in runbook.Text.Split('\n')
                        where Regex.IsMatch(line, @"https?://\S*/releases/latest\b")
                        select $"{runbook.File}: {line.Trim()}").ToList();

        await Assert.That(floating).IsEmpty()
            .Because("'latest' is not a version anybody named. Found: " + string.Join(" | ", floating));
    }

    [Test]
    public async Task A_pin_in_a_runbook_is_the_version_being_released()
    {
        var releasing = Releasing();
        var root = RepoRoot();

        var cloudInit = File.ReadAllText(Path.Combine(root, "deploy", "pool-host", "cloud-init.yaml"));
        var memberImage = File.ReadAllText(Path.Combine(root, "deploy", "member-browser", "Dockerfile"));
        var readme = File.ReadAllText(Path.Combine(root, "README.md"));

        var pins = new List<(string Where, string Version)>();

        pins.AddRange(Regex.Matches(cloudInit, @"--version\s+(\d+\.\d+\.\d+)")
            .Select(m => ("cloud-init --version", m.Groups[1].Value)));
        pins.AddRange(Regex.Matches(cloudInit, @"releases/download/v(\d+\.\d+\.\d+)/")
            .Select(m => ("cloud-init bundle", m.Groups[1].Value)));
        pins.AddRange(Regex.Matches(cloudInit, @"runs gg (\d+\.\d+\.\d+)")
            .Select(m => ("cloud-init final message", m.Groups[1].Value)));
        pins.AddRange(Regex.Matches(memberImage, @"ARG GG_VERSION=(\d+\.\d+\.\d+)")
            .Select(m => ("member-browser", m.Groups[1].Value)));
        pins.AddRange(Regex.Matches(readme, @"(?m)^v=(\d+\.\d+\.\d+)")
            .Select(m => ("README", m.Groups[1].Value)));

        foreach (var where in (string[])["cloud-init --version", "cloud-init bundle", "member-browser", "README"])
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
