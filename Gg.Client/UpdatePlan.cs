using Gg.Contracts;
using Gg.Local;

namespace Gg.Client;

/// <summary>One thing to run, and what it is for.</summary>
/// <remarks>
/// <para>
/// <b>A program and its arguments, never a command line.</b> Nothing here is
/// shell-interpreted, and the arguments are built one at a time - the rule
/// <c>Ran</c> already states for every child this binary starts, and the
/// reason it matters more here than anywhere else: the version in these
/// arguments arrives from a control plane, and a joined string would have to
/// be split again before it could run. Splitting is where a version becomes a
/// second command.
/// </para>
/// <para>
/// <b>Every step carries its reason.</b> This runs commands on somebody's
/// machine; a step nobody can explain is a step nobody should approve, and
/// what a console shows beside it is the reason rather than a bare line.
/// </para>
/// <para>
/// <b>And whether it is the step that wants root.</b> Only that one goes under
/// <c>sudo</c>: fetching an installer needs no privilege, and a download run as
/// root is a download whose file root owns.
/// </para>
/// </remarks>
public sealed record UpdateStep(
    string Program, IReadOnlyList<string> Arguments, string Because, bool Elevated = false)
{
    /// <summary>
    /// The same step, run through <c>sudo</c> - or itself, when it is not the
    /// one that wants root.
    /// </summary>
    /// <remarks>
    /// <b>Still a program and its arguments.</b> <c>sudo</c> takes the program
    /// as its first argument and passes the rest through untouched, so wrapping
    /// a step adds no shell and no quoting rule.
    /// </remarks>
    public UpdateStep UnderSudo() =>
        Elevated ? this with { Program = "sudo", Arguments = [Program, .. Arguments] } : this;

    /// <summary>
    /// What to show a person, and never what is executed.
    /// </summary>
    /// <remarks>
    /// Joined here for reading only. The executor takes
    /// <see cref="Program"/> and <see cref="Arguments"/>, so what runs cannot
    /// differ from what was planned by a quoting rule.
    /// </remarks>
    public string Command => string.Join(' ', new[] { Program }.Concat(Arguments));
}

/// <summary>
/// What moving this machine to a newer <c>gg</c> takes, or why it cannot.
/// </summary>
/// <param name="Shape">Which install this is.</param>
/// <param name="Installed">What is here now.</param>
/// <param name="Target">What to move to, or null when nobody could say.</param>
/// <param name="Summary">One line for a person.</param>
/// <param name="Steps">In order. Empty when there is nothing to do.</param>
/// <param name="Refusal">Why this machine cannot be moved, or null.</param>
/// <param name="NeedsRoot">Whether the steps want a privilege this process lacks.</param>
/// <param name="Restarts">Whether performing them restarts a service.</param>
public sealed record UpdatePlan(
    InstallShape Shape,
    string? Installed,
    string? Target,
    string Summary,
    IReadOnlyList<UpdateStep> Steps,
    string? Refusal,
    bool NeedsRoot,
    bool Restarts)
{
    /// <summary>Whether there is something to run and a reason to run it.</summary>
    public bool CanApply => Refusal is null && Steps.Count > 0;

    /// <summary>
    /// The steps as one line a person can paste, each run only if the last
    /// succeeded, with <c>sudo</c> on exactly the steps that want it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>For reading and pasting, never for executing</b> - the same rule as
    /// <see cref="UpdateStep.Command"/>, which this joins.
    /// </para>
    /// <para>
    /// <b>Joined by <c>&amp;&amp;</c> because the obvious join is wrong.</b>
    /// Shown as two lines, the steps were being pasted as
    /// <c>curl -o FILE URL | sudo sh FILE</c>: the pipe carries nothing, both
    /// sides start at once, and <c>sh</c> reads whatever part of the file has
    /// arrived - it worked only because the password prompt held it back.
    /// </para>
    /// </remarks>
    public string Pasteable(bool privileged) =>
        string.Join(" && ", Steps.Select(step => (privileged ? step : step.UnderSudo()).Command));

    /// <summary>
    /// Whether this machine is already on the target.
    /// </summary>
    /// <remarks>
    /// Told apart from a refusal on purpose: being current is a good outcome,
    /// and a refusal would read as a failure.
    /// </remarks>
    public bool AlreadyThere =>
        Refusal is null
        && Steps.Count == 0
        && Installed is { Length: > 0 }
        && string.Equals(Installed, Target, StringComparison.Ordinal);
}

/// <summary>
/// What updating takes, decided without touching anything.
/// </summary>
/// <remarks>
/// <para>
/// <b>gg still never rewrites the file it is executing.</b> What it does now
/// is hand off to whoever owns that shape's bytes - <c>dotnet</c> owns a tool
/// directory, the installer owns the versioned native layout, an image builder
/// owns a container. A process that overwrites its own inode is the macOS
/// SIGKILL this project has already paid for once, and none of these do that:
/// the new bytes land beside the old and are renamed into place.
/// </para>
/// <para>
/// <b>The control plane supplies a VERSION and never a location.</b> Where the
/// installer comes from is this machine's own configuration. The installer
/// then verifies the bytes against an attestation naming the repository they
/// were built from - so a control plane that has been taken over can move a
/// fleet to a different VERSION, which is visible and bounded by what was ever
/// published, and can never move it to different BYTES.
/// </para>
/// <para>
/// <b>And no forge is named here.</b> <c>ProviderNeutralityTests</c> keeps a
/// forge's name out of this binary so a second one ships without changing it;
/// the location arrives as a string and this only passes it on.
/// </para>
/// <para>
/// <b>In Gg.Client because it needs both sides.</b> The shape of an install is
/// <c>Gg.Local</c>'s and the ordering of versions is the contract's, and
/// <c>Gg.Local</c> may not reference the contract - that boundary keeps wire
/// types out of the artifact a customer audits. This assembly already
/// references both.
/// </para>
/// <para>
/// <b>Pure.</b> Nothing here reads a disk, asks a feed or starts a process -
/// which is what lets every rule below be a test rather than a walk.
/// </para>
/// </remarks>
public static class UpdatePlans
{
    /// <summary>
    /// What this machine would have to run, and whether it can.
    /// </summary>
    /// <param name="shape">Which install this is.</param>
    /// <param name="installed">What is here now.</param>
    /// <param name="target">What the oracle says is current, or null.</param>
    /// <param name="installer">
    /// Where this machine's installer comes from, from its own configuration.
    /// Null is a machine that has not been told, and is asked rather than sent
    /// somewhere chosen for it.
    /// </param>
    /// <param name="toolPathWritable">
    /// Whether this process could write the tool directory. Passed in rather
    /// than probed, so the decision stays pure.
    /// </param>
    /// <param name="scratch">
    /// Where a fetched installer lands. Handed in for the same reason as the
    /// line above: choosing a temporary path is a question about a filesystem,
    /// and nothing here touches one.
    /// </param>
    public static UpdatePlan For(
        InstallShape shape,
        string? installed,
        string? target,
        string? installer,
        bool toolPathWritable,
        string scratch)
    {
        ArgumentNullException.ThrowIfNull(shape);

        // AN UNKNOWN TARGET IS A REFUSAL, NEVER A CLEAN BILL. A machine told it
        // is current because nobody could say otherwise does not update and
        // never learns it was asked to.
        if (target is not { Length: > 0 })
        {
            return Cannot(
                shape, installed, target,
                "gg couldn't check which version is the latest, so it has changed nothing. "
              + "You may already have the newest version.");
        }

        // A VERSION, PROVEN, BEFORE IT REACHES AN ARGUMENT LIST. This string
        // arrives from a control plane, and rule 4 of slice forty-eight is that
        // a request names a version and nothing else. Nothing downstream is
        // shell-interpreted, so this is not the last line of defence - it is
        // the one that makes the others easy to reason about, because after it
        // the value cannot contain a path, a flag or a space.
        if (!Numbered(target))
        {
            return Cannot(
                shape, installed, target,
                $"'{target}' is not a version number, so gg has changed nothing. A version "
              + "number looks like 0.88.20: digits and dots. gg will not act on anything else.");
        }

        if (installed is { Length: > 0 } here)
        {
            if (string.Equals(here, target, StringComparison.Ordinal))
            {
                return Nothing(shape, installed, target, $"This gg is {here}, which is current.");
            }

            if (VersionOrder.IsBehind(target, here))
            {
                return Nothing(
                    shape, installed, target,
                    $"This gg is {here}, which is ahead of {target}, the latest version gg "
                  + "knows about. gg has left it alone.");
            }
        }

        return shape.Kind switch
        {
            InstallKind.GlobalTool => ByDotnet(shape, installed, target, global: true, writable: true),
            InstallKind.ToolPath => ByDotnet(shape, installed, target, global: false, writable: toolPathWritable),
            InstallKind.Native => ByInstaller(shape, installed, target, installer, scratch),

            InstallKind.Container => Cannot(
                shape, installed, target,
                $"This gg runs in a container, and a container can't update its own gg. To get "
              + $"{target}, rebuild the container image at that release and point the pool at "
              + "the new image. Allowing it would let a container replace the program its host "
              + "starts, so gg has changed nothing."),

            _ => Cannot(
                shape, installed, target,
                "gg can't tell how it was installed, so it won't update itself. Guessing could "
              + "overwrite files it doesn't understand. To update, reinstall gg with the install "
              + "command from the releases page."),
        };
    }

    /// <summary>
    /// The shapes dotnet owns.
    /// </summary>
    /// <remarks>
    /// <b>The cache is cleared first, and it is not belt and braces.</b>
    /// Measured twice on this fleet: root's own http-cache holds the
    /// pre-publish index, so a version that IS on the feed comes back as "not
    /// found in NuGet feeds" - indistinguishable from real indexing lag, which
    /// has run to half an hour. Clearing first means the message that survives
    /// means what it says.
    /// </remarks>
    private static UpdatePlan ByDotnet(
        InstallShape shape, string? installed, string target, bool global, bool writable)
    {
        string[] where = global ? ["-g"] : ["--tool-path", shape.ToolPath ?? ""];

        return new UpdatePlan(
            shape,
            installed,
            target,
            global
                ? $"This gg is a .NET tool; dotnet moves it to {target}."
                : $"This gg is a .NET tool at {shape.ToolPath}; dotnet moves it to {target}.",
            [
                // BOTH under root when either is: the stale cache that matters
                // is the one belonging to whoever runs the update.
                new UpdateStep(
                    "dotnet",
                    ["nuget", "locals", "http-cache", "--clear"],
                    "a stale index reports a published version as missing, and the two read "
                  + "the same",
                    Elevated: !writable),
                new UpdateStep(
                    "dotnet",
                    ["tool", "update", UpdateAdvice.PackageId, "--version", target, .. where],
                    "dotnet owns this directory and writes the new bytes beside the old",
                    Elevated: !writable),
            ],
            Refusal: null,
            NeedsRoot: !writable,
            Restarts: false);
    }

    /// <summary>
    /// The native shape, which the installer owns.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The installer puts the release in its own versioned directory, checks
    /// the bytes against an attestation before writing any, renames the symlink
    /// rather than deleting and recreating it - so the path is never absent -
    /// and restarts the unit if there is one. None of which this binary should
    /// reimplement.
    /// </para>
    /// <para>
    /// <b>It always wants root, and that is not a property of the machine.</b>
    /// Read off the script rather than assumed: its destination is
    /// <c>/usr/local/lib/gg</c> and the link it swaps is
    /// <c>/usr/local/bin/gg</c>, both root's on every platform this ships to.
    /// Its <c>--root</c> is a staging prefix for building an image - the
    /// symlink it writes still points at the absolute <c>/usr/local</c> path -
    /// so there is no user-prefix install to fall back to and a laptop is no
    /// different from a host here.
    /// </para>
    /// </remarks>
    private static UpdatePlan ByInstaller(
        InstallShape shape, string? installed, string target, string? installer, string scratch)
    {
        if (installer is not { Length: > 0 } from)
        {
            return Cannot(
                shape, installed, target,
                "This gg is a single file, and this machine has no installer set up, so gg doesn't "
              + "know where to download the new version from. Set the installer location for this "
              + "machine and run this again, or reinstall gg with the install command from the "
              + "releases page.");
        }

        // A PATH IS RUN; ANYTHING ELSE IS FETCHED FIRST, AS ITS OWN STEP. A
        // URL cannot be executed, and the obvious way round that - one step
        // reading `curl … | sh` - is a shell line, which is the shape this
        // whole structure exists to avoid. Two steps instead: the fetch is
        // visible, has its own reason, and fails on its own.
        var local = !from.Contains("://", StringComparison.Ordinal);

        IReadOnlyList<UpdateStep> steps = local
            ?
            [
                new UpdateStep(
                    from,
                    ["--version", target],
                    "the installer verifies the bytes against an attestation before writing "
                  + "any, installs beside what is there, and swaps the link by rename",
                    Elevated: true),
            ]
            :
            [
                new UpdateStep(
                    "curl",
                    ["-fsSL", "-o", scratch, from],
                    "the installer comes from where this machine's own configuration says, "
                  + "which is the same choice its operator made installing it"),
                new UpdateStep(
                    "sh",
                    [scratch, "--version", target],
                    "the installer verifies the bytes against an attestation before writing "
                  + "any, installs beside what is there, and swaps the link by rename",
                    Elevated: true),
            ];

        return new UpdatePlan(
            shape,
            installed,
            target,
            $"This gg is a self-contained binary; its installer moves it to {target}.",
            steps,
            Refusal: null,

            // ALWAYS, rather than when a probe says so. The installer's
            // destination is root's on every platform gg ships to, so a plan
            // that said otherwise would be wrong everywhere rather than
            // sometimes - and a caller that learns it needs root from a
            // permission error has already started.
            NeedsRoot: true,
            Restarts: true);
    }

    /// <summary>
    /// Whether this is a version and not something wearing one's clothes.
    /// </summary>
    /// <remarks>
    /// Deliberately narrower than what a parser would accept: digits, dots and
    /// an optional prerelease. No build metadata, no leading `v`, no path
    /// separator, no space. A value that passes this cannot become a second
    /// argument however it is later handled.
    /// </remarks>
    private static bool Numbered(string version) =>
        System.Text.RegularExpressions.Regex.IsMatch(
            version,
            @"^[0-9]+\.[0-9]+\.[0-9]+(-[0-9A-Za-z.]+)?$",
            System.Text.RegularExpressions.RegexOptions.None,
            TimeSpan.FromSeconds(1));

    private static UpdatePlan Nothing(
        InstallShape shape, string? installed, string? target, string summary) =>
        new(shape, installed, target, summary, [], Refusal: null, NeedsRoot: false, Restarts: false);

    private static UpdatePlan Cannot(
        InstallShape shape, string? installed, string? target, string why) =>
        new(shape, installed, target, why, [], why, NeedsRoot: false, Restarts: false);
}
