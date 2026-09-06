using Gg.Local;

namespace Gg.Cli.Tests;

/// <summary>
/// What <c>gg update</c> says, and what it must never do.
/// </summary>
/// <remarks>
/// <para>
/// <b><c>gg</c> does not replace its own binary.</b> Not by download, not by
/// rename, not by shelling out to something that would. `dotnet` moves the
/// bytes for a tool, a person moves them for a native install, and an image is
/// repinned. Every line of an updater we do not write is a line that cannot be
/// wrong on one of three platforms — so this verb reports, and names the
/// command.
/// </para>
/// <para>
/// <b>Naming the RIGHT command is the whole difficulty.</b> There is no single
/// one. <c>dotnet tool update -g</c> on a pool host installs into the invoking
/// user's home and leaves <c>/usr/local/lib/gg</c> untouched: it succeeds, says
/// so, and updates nothing. Advice that is confidently wrong on the machine
/// that matters most is worse than no advice.
/// </para>
/// <para>
/// <b><c>SelfInvocation</c> cannot answer this, and says so itself:</b>
/// <i>"An apphost - AOT or framework-dependent - takes the verb directly."</i>
/// It splits the <c>dotnet</c> host from an apphost, which is the question a
/// re-exec asks. A tool shim and a native binary are BOTH apphosts, and telling
/// those apart is the question this asks. The tell is the install layout: a
/// tool sits beside a <c>.store</c> directory and a native binary does not.
/// </para>
/// </remarks>
public class UpdateTests
{
    /// <summary>A layout where the given directories exist and no others.</summary>
    private static Func<string, bool> Directories(params string[] present) =>
        path => present.Contains(path, StringComparer.Ordinal);

    private const string GlobalTools = "/home/kate/.dotnet/tools";

    [Test]
    public async Task A_global_tool_is_told_to_update_globally()
    {
        var shape = InstallShape.For(
            processPath: $"{GlobalTools}/gg",
            directoryExists: Directories($"{GlobalTools}/.store"),
            inContainer: false,
            globalToolsDirectory: GlobalTools);

        await Assert.That(shape.Kind).IsEqualTo(InstallKind.GlobalTool);

        var advice = UpdateAdvice.For(shape, "0.5.0");

        await Assert.That(advice.Commands.Single())
            .IsEqualTo("dotnet tool update -g GlyphGuild.Gg.Cli --version 0.5.0");
    }

    [Test]
    public async Task A_tool_path_install_is_told_the_path_it_actually_lives_at()
    {
        // THE ONE THAT WOULD HAVE BEEN WRONG. A pool host installs with
        // --tool-path so the runner cannot write its own executable; the shim
        // is at /usr/local/lib/gg and the runner runs as another user
        // entirely. `-g` there resolves against whoever typed it.
        var shape = InstallShape.For(
            processPath: "/usr/local/lib/gg/gg",
            directoryExists: Directories("/usr/local/lib/gg/.store"),
            inContainer: false,
            globalToolsDirectory: GlobalTools);

        await Assert.That(shape.Kind).IsEqualTo(InstallKind.ToolPath);
        await Assert.That(shape.ToolPath).IsEqualTo("/usr/local/lib/gg");

        var advice = UpdateAdvice.For(shape, "0.5.0");

        await Assert.That(advice.Commands.Single())
            .IsEqualTo("dotnet tool update GlyphGuild.Gg.Cli --version 0.5.0 --tool-path /usr/local/lib/gg")
            .Because("-g would install into the home of whoever ran it and leave this shim alone, "
                   + "reporting success the whole way.");
    }

    [Test]
    public async Task A_native_binary_is_told_to_fetch_it_again_and_never_offered_an_update()
    {
        // A self-contained AOT binary is an apphost with nothing beside it. It
        // has no update path at all, by design, so the honest answer is "get
        // the release asset again" rather than a command that cannot work.
        var shape = InstallShape.For(
            processPath: "/usr/local/bin/gg",
            directoryExists: Directories(),
            inContainer: false,
            globalToolsDirectory: GlobalTools);

        await Assert.That(shape.Kind).IsEqualTo(InstallKind.Native);

        var advice = UpdateAdvice.For(shape, "0.5.0");

        await Assert.That(advice.Commands.Any(c => c.Contains("dotnet tool", StringComparison.Ordinal)))
            .IsFalse()
            .Because("there is no tool here to update, and a command that cannot work reads as a "
                   + "broken machine rather than as the wrong install shape.");
        await Assert.That(advice.Summary).Contains("0.5.0");
    }

    [Test]
    public async Task A_container_is_told_the_image_is_the_unit_of_change()
    {
        // Rule 3: a pool member never self-updates, because what reset resets
        // TO must be a fixed point. Updating gg inside a running member would
        // survive exactly until the next reset.
        var shape = InstallShape.For(
            processPath: "/usr/local/bin/gg",
            directoryExists: Directories(),
            inContainer: true,
            globalToolsDirectory: GlobalTools);

        await Assert.That(shape.Kind).IsEqualTo(InstallKind.Container);

        var advice = UpdateAdvice.For(shape, "0.5.0");

        await Assert.That(advice.Commands).IsEmpty()
            .Because("there is no command a person runs INSIDE the container that outlives a "
                   + "reset. The image is rebuilt and repinned somewhere else entirely.");
        await Assert.That(advice.Summary).Contains("image");
    }

    [Test]
    public async Task Not_knowing_what_is_current_is_said_rather_than_read_as_being_current()
    {
        // THE DEFECT THIS PROJECT KEEPS FINDING, on the field where it means
        // nobody updates. A null current version is an ABSENCE of knowledge,
        // and the one thing it must not render as is "you are up to date".
        var shape = InstallShape.For(
            processPath: $"{GlobalTools}/gg",
            directoryExists: Directories($"{GlobalTools}/.store"),
            inContainer: false,
            globalToolsDirectory: GlobalTools);

        var advice = UpdateAdvice.For(shape, current: null);

        await Assert.That(advice.CurrentIsKnown).IsFalse();
        await Assert.That(advice.Summary.Contains("up to date", StringComparison.OrdinalIgnoreCase))
            .IsFalse()
            .Because("silence from the oracle is not agreement with it.");
        await Assert.That(advice.Commands.Any(c => c.Contains("--version", StringComparison.Ordinal)))
            .IsFalse()
            .Because("a pinned command needs a version to pin to, and inventing one is worse than "
                   + "printing the unpinned form with the caveat.");
    }
}
