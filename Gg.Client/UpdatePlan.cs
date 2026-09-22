using Gg.Contracts;
using Gg.Local;

namespace Gg.Client;

/// <summary>One thing to run, and what it is for.</summary>
/// <remarks>
/// <b>Every step carries its reason.</b> This runs commands on somebody's
/// machine; a step nobody can explain is a step nobody should approve, and the
/// reason is what a console shows beside it rather than a bare command line.
/// </remarks>
public sealed record UpdateStep(string Command, string Because);

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
    public static UpdatePlan For(
        InstallShape shape,
        string? installed,
        string? target,
        string? installer,
        bool toolPathWritable)
    {
        ArgumentNullException.ThrowIfNull(shape);

        // AN UNKNOWN TARGET IS A REFUSAL, NEVER A CLEAN BILL. A machine told it
        // is current because nobody could say otherwise does not update and
        // never learns it was asked to.
        if (target is not { Length: > 0 })
        {
            return Cannot(
                shape, installed, target,
                "What version is current could not be established, so nothing was moved. "
              + "This may already be the newest.");
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
                    $"This gg is {here}, which is ahead of the {target} the control plane "
                  + "calls current. Nothing was moved.");
            }
        }

        return shape.Kind switch
        {
            InstallKind.GlobalTool => ByDotnet(shape, installed, target, global: true, writable: true),
            InstallKind.ToolPath => ByDotnet(shape, installed, target, global: false, writable: toolPathWritable),
            InstallKind.Native => ByInstaller(shape, installed, target, installer),

            InstallKind.Container => Cannot(
                shape, installed, target,
                $"This gg is in a container, where the image is the unit of change: {target} "
              + "arrives by rebuilding the member image at that release and repinning the "
              + "pool to the new digest. A container cannot replace its own gg, and a member "
              + "that could would be a member that can replace what the host starts."),

            _ => Cannot(
                shape, installed, target,
                "How this gg was installed could not be established, so nothing was moved - "
              + "guessing which installer owns these bytes is how an update writes over a "
              + "layout it does not understand."),
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
        var where = global ? "-g" : $"--tool-path {shape.ToolPath}";

        return new UpdatePlan(
            shape,
            installed,
            target,
            global
                ? $"This gg is a .NET tool; dotnet moves it to {target}."
                : $"This gg is a .NET tool at {shape.ToolPath}; dotnet moves it to {target}.",
            [
                new UpdateStep(
                    "dotnet nuget locals http-cache --clear",
                    "a stale index reports a published version as missing, and the two read "
                  + "the same"),
                new UpdateStep(
                    $"dotnet tool update {UpdateAdvice.PackageId} --version {target} {where}",
                    "dotnet owns this directory and writes the new bytes beside the old"),
            ],
            Refusal: null,
            NeedsRoot: !writable,
            Restarts: false);
    }

    /// <summary>
    /// The native shape, which the installer owns.
    /// </summary>
    /// <remarks>
    /// The installer puts the release in its own versioned directory, checks
    /// the bytes against an attestation before writing any, renames the symlink
    /// rather than deleting and recreating it - so the path is never absent -
    /// and restarts the unit if there is one. None of which this binary should
    /// reimplement.
    /// </remarks>
    private static UpdatePlan ByInstaller(
        InstallShape shape, string? installed, string target, string? installer)
    {
        if (installer is not { Length: > 0 } from)
        {
            return Cannot(
                shape, installed, target,
                "This gg is a self-contained binary and no installer is configured for this "
              + "machine, so there is nowhere to get one from that this machine chose. "
              + "Configure it and run this again.");
        }

        return new UpdatePlan(
            shape,
            installed,
            target,
            $"This gg is a self-contained binary; its installer moves it to {target}.",
            [
                new UpdateStep(
                    $"{from} --version {target}",
                    "the installer verifies the bytes against an attestation before writing "
                  + "any, installs beside what is there, and swaps the link by rename"),
            ],
            Refusal: null,
            NeedsRoot: false,
            Restarts: true);
    }

    private static UpdatePlan Nothing(
        InstallShape shape, string? installed, string? target, string summary) =>
        new(shape, installed, target, summary, [], Refusal: null, NeedsRoot: false, Restarts: false);

    private static UpdatePlan Cannot(
        InstallShape shape, string? installed, string? target, string why) =>
        new(shape, installed, target, why, [], why, NeedsRoot: false, Restarts: false);
}
