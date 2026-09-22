using Gg.Client;
using Gg.Local;

namespace Gg.Cli.Tests;

/// <summary>
/// What moving this machine to a newer gg actually takes, per install shape.
/// </summary>
/// <remarks>
/// <para>
/// <b>gg still never rewrites the file it is executing.</b> What changed on
/// 2026-09-22 is that it now hands off to whoever owns that shape's bytes -
/// <c>dotnet</c> owns a tool directory, <c>install.sh</c> owns the versioned
/// native layout, an image builder owns a container - instead of printing a
/// line and leaving. The distinction is not a technicality: a process that
/// overwrites its own inode is the macOS SIGKILL this project has already
/// paid for once.
/// </para>
/// <para>
/// <b>The control plane supplies a VERSION and never a location.</b> The
/// machine's own configuration says where its installer comes from, and the
/// installer verifies the bytes against an attestation naming the repo it was
/// built from. So a control plane that has been taken over can move a fleet
/// to a different VERSION - which is visible, and bounded by what was ever
/// published - and can never move it to different BYTES. No forge name is
/// compiled in either, which is the rule ProviderNeutralityTests already
/// holds.
/// </para>
/// <para>
/// <b>Silence never reads as currency.</b> An unknown target is a refusal
/// with its reason, never "you are up to date" - the failure this project
/// keeps finding is silence reading as agreement.
/// </para>
/// </remarks>
public class WhatUpdatingThisMachineTakesTests
{
    private const string Installer = "https://example.invalid/install.sh";

    private static UpdatePlan For(
        InstallKind kind,
        string? installed = "0.48.0",
        string? target = "0.49.0",
        string? toolPath = null,
        bool writable = true,
        string? installer = Installer) =>
        UpdatePlans.For(
            new InstallShape(kind, toolPath), installed, target, installer, writable);

    // ---- what it refuses, and why ----

    [Test]
    public async Task An_unknown_target_is_refused_rather_than_called_current()
    {
        var plan = For(InstallKind.ToolPath, target: null, toolPath: "/usr/local/lib/gg");

        await Assert.That(plan.CanApply).IsFalse();
        await Assert.That(plan.Refusal).IsNotNull();
        await Assert.That(plan.Refusal!.ToLowerInvariant()).DoesNotContain("up to date")
            .Because("a machine told it is current because the oracle could not be reached "
                   + "never updates and never learns it was asked to - which is the failure "
                   + "this whole verb exists around.");
    }

    [Test]
    public async Task A_container_is_refused_with_the_two_steps_that_would_work()
    {
        var plan = For(InstallKind.Container);

        await Assert.That(plan.CanApply).IsFalse();
        await Assert.That(plan.Refusal).Contains("image")
            .Because("a member cannot replace its own gg: the image is the unit of change, "
                   + "so the honest answer names rebuilding and repinning rather than "
                   + "pretending there is a command.");
    }

    [Test]
    public async Task An_install_nobody_can_place_is_refused()
    {
        var plan = For(InstallKind.Unknown);

        await Assert.That(plan.CanApply).IsFalse();
        await Assert.That(plan.Refusal).IsNotNull()
            .Because("guessing which installer owns these bytes is how an update writes over "
                   + "a layout it does not understand.");
    }

    [Test]
    public async Task A_native_install_with_no_installer_configured_is_refused()
    {
        var plan = For(InstallKind.Native, installer: null);

        await Assert.That(plan.CanApply).IsFalse();
        await Assert.That(plan.Refusal).IsNotNull()
            .Because("where the installer comes from is this machine's own configuration - "
                   + "never the control plane's - so an unconfigured machine is asked rather "
                   + "than sent somewhere chosen for it.");
    }

    // ---- nothing to do is not a refusal ----

    [Test]
    public async Task A_machine_already_there_has_nothing_to_run()
    {
        var plan = For(InstallKind.ToolPath, installed: "0.49.0", target: "0.49.0",
            toolPath: "/usr/local/lib/gg");

        await Assert.That(plan.Steps).IsEmpty();
        await Assert.That(plan.Refusal).IsNull()
            .Because("being current is a good outcome, not a failure, and a refusal would "
                   + "read as one.");
        await Assert.That(plan.AlreadyThere).IsTrue();
    }

    [Test]
    public async Task A_machine_ahead_of_current_is_left_alone_and_said_so()
    {
        var plan = For(InstallKind.ToolPath, installed: "0.50.0", target: "0.49.0",
            toolPath: "/usr/local/lib/gg");

        await Assert.That(plan.Steps).IsEmpty();
        await Assert.That(plan.Summary.ToLowerInvariant()).Contains("ahead")
            .Because("a machine newer than what the control plane calls current is a real "
                   + "state - a release in flight, or a key somewhere it should not be - and "
                   + "quietly downgrading it would destroy the evidence.");
    }

    // ---- what it does ----

    [Test]
    public async Task A_tool_path_install_is_dotnets_to_move()
    {
        var plan = For(InstallKind.ToolPath, toolPath: "/usr/local/lib/gg");

        await Assert.That(plan.CanApply).IsTrue();
        await Assert.That(string.Join(" ", plan.Steps.Select(s => s.Command)))
            .Contains("dotnet tool update")
            .And.Contains("--tool-path /usr/local/lib/gg")
            .And.Contains("--version 0.49.0");
    }

    [Test]
    public async Task A_tool_path_it_cannot_write_says_so_before_it_tries()
    {
        var plan = For(InstallKind.ToolPath, toolPath: "/usr/local/lib/gg", writable: false);

        await Assert.That(plan.NeedsRoot).IsTrue()
            .Because("/usr/local/lib/gg is root's on every pool host, and `dotnet tool "
                   + "update` failing on a permission is a worse answer than saying which "
                   + "privilege it wants.");
    }

    [Test]
    public async Task The_stale_cache_is_cleared_before_the_feed_is_asked()
    {
        var plan = For(InstallKind.ToolPath, toolPath: "/usr/local/lib/gg", writable: false);

        await Assert.That(plan.Steps.First().Command).Contains("nuget locals http-cache")
            .Because("measured twice on this fleet: root's own http-cache holds the "
                   + "pre-publish index, and a version that IS on the feed then reads as "
                   + "`not found in NuGet feeds` - indistinguishable from real indexing lag "
                   + "unless the cache is cleared first.");
    }

    [Test]
    public async Task A_native_install_is_the_installers_to_move()
    {
        var plan = For(InstallKind.Native);

        await Assert.That(plan.CanApply).IsTrue();
        await Assert.That(plan.Steps.Any(s => s.Command.Contains("0.49.0", StringComparison.Ordinal)))
            .IsTrue();
        await Assert.That(plan.Restarts).IsTrue()
            .Because("the installer swaps the symlink and restarts the unit, so a caller has "
                   + "to know a service is going to move under it.");
    }

    [Test]
    public async Task A_native_install_always_wants_root()
    {
        await Assert.That(For(InstallKind.Native).NeedsRoot).IsTrue()
            .Because("read off the installer rather than assumed: it writes "
                   + "/usr/local/lib/gg and swaps /usr/local/bin/gg, both root's on every "
                   + "platform gg ships to. Its --root is a staging prefix for building an "
                   + "image - the symlink still points at the absolute /usr/local path - so "
                   + "there is no user-prefix install to fall back to, and a laptop is no "
                   + "different from a host. A caller that learns this from a permission "
                   + "error has already started.");
    }

    [Test]
    public async Task No_forge_is_named_by_the_planner_itself()
    {
        var plan = For(InstallKind.Native);
        var everything = plan.Summary + string.Join(" ", plan.Steps.Select(s => s.Command));

        await Assert.That(everything.ToLowerInvariant()).DoesNotContain("github")
            .Because("ProviderNeutralityTests keeps a forge's name out of this binary so a "
                   + "second one ships without changing it - the location comes in as "
                   + "configuration and the planner only passes it on.");
    }

    // ---- what a version may be ----

    [Test]
    public async Task A_target_that_is_not_a_version_is_refused()
    {
        foreach (var pretender in (string[])
                 ["0.49.0; rm -rf /", "../../etc/passwd", "0.49.0 --tool-path /", "latest",
                  "v0.49.0", "0.49.0\n--version"])
        {
            var plan = For(InstallKind.ToolPath, target: pretender, toolPath: "/usr/local/lib/gg");

            await Assert.That(plan.CanApply).IsFalse()
                .Because($"'{pretender}' arrived from a control plane and is not a version. "
                       + "Nothing downstream is shell-interpreted, so this is not the last "
                       + "line of defence - it is the one that makes the others easy to "
                       + "reason about.");
        }
    }

    [Test]
    public async Task A_prerelease_is_still_a_version()
    {
        var plan = For(InstallKind.ToolPath, target: "0.50.0-rc.1", toolPath: "/usr/local/lib/gg");

        await Assert.That(plan.CanApply).IsTrue()
            .Because("refusing a legitimate prerelease would make this rule the thing that "
                   + "stops a release going out.");
    }

    [Test]
    public async Task A_step_is_a_program_and_its_arguments_rather_than_a_line()
    {
        var plan = For(InstallKind.ToolPath, toolPath: "/usr/local/lib/gg");
        var update = plan.Steps.Last();

        await Assert.That(update.Program).IsEqualTo("dotnet");
        await Assert.That(update.Arguments).Contains("0.49.0")
            .Because("the version is its own argument, so no quoting rule stands between "
                   + "what was planned and what runs.");
        await Assert.That(update.Arguments.Any(a => a.Contains(' ', StringComparison.Ordinal)))
            .IsFalse()
            .Because("an argument carrying a space is the shape that has to be quoted, and "
                   + "quoting is what this structure exists to avoid.");
    }

    [Test]
    public async Task Every_step_says_what_it_is_for()
    {
        var plan = For(InstallKind.ToolPath, toolPath: "/usr/local/lib/gg", writable: false);

        await Assert.That(plan.Steps.All(s => s.Because.Length > 0)).IsTrue()
            .Because("this runs commands on somebody's machine, and a step nobody can "
                   + "explain is a step nobody should approve.");
    }
}
