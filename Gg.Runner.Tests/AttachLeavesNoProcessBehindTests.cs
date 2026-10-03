using System.Diagnostics;
using Gg.Contracts;
using Gg.Runner.Environments;

namespace Gg.Runner.Tests;

/// <summary>
/// Attach brings the stack up and returns; what it started outlives it and it
/// does not.
/// </summary>
/// <remarks>
/// <para>
/// <b>S58.2-03, and it is the point where the five-point shape was strained.</b>
/// Four points do a thing and exit; <c>attach</c> has to leave a stack UP. The
/// owner resolved it by making the hook's job <i>reaching</i> health rather than
/// holding it: <i>"The hook brings the stack up in the background, polls until
/// it answers, returns. The daemon owns what's running; no process survives the
/// hook."</i>
/// </para>
/// <para>
/// <b>Both halves, because either alone is the wrong thing.</b> A hook whose own
/// process survives is a second owner of the same containers, and reclaim stops
/// being the single way an instance empties. A hook that takes its work down
/// with it has not brought a stack up at all.
/// </para>
/// <para>
/// <b>Proven without Docker, on purpose.</b> The semantic is about process
/// lifetime, not about containers: a backgrounded child standing for the stack
/// says exactly the same thing and runs in the CI this repository has.
/// </para>
/// </remarks>
public class AttachLeavesNoProcessBehindTests
{
    private static (string Tree, string Script) AScript(string body)
    {
        var tree = Directory.CreateDirectory(Path.Combine(
            Path.GetTempPath(), "gg-attach-" + Guid.NewGuid().ToString("n"))).FullName;

        var script = Path.Combine(tree, "hooks.sh");
        File.WriteAllText(script, "#!/bin/sh\n" + body + "\n");

        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(script, UnixFileMode.UserRead | UnixFileMode.UserWrite
                                       | UnixFileMode.UserExecute);
        }

        return (tree, script);
    }

    [Test]
    [Category("RealStack")]
    public async Task What_attach_started_is_still_running_and_the_hook_is_not()
    {
        var marker = Path.Combine(Path.GetTempPath(), "gg-attach-" + Guid.NewGuid().ToString("n"));
        var (tree, script) = AScript(
            $"( sleep 30; rm -f {marker} ) >/dev/null 2>&1 &\n"
          + $"echo $! > {marker}\n"
          + "exit 0");

        try
        {
            var performance = await StackScript.PerformAsync(
                script, StackScript.Attach, tree, StackScript.Patience);

            await Assert.That(performance.Outcome).IsEqualTo(StackOutcomes.Exited);
            await Assert.That(performance.Exit).IsEqualTo(0);

            await Assert.That(performance.Survived).IsFalse()
                .Because("the hook's own process must be gone when it returns - one that "
                       + "stayed up would be a second owner of what it started, and reclaim "
                       + "would stop being the single way an instance empties.");

            // AND THE WORK PERSISTS, which is the other half. A hook that took
            // its stack down with it has not brought one up.
            var child = int.Parse(File.ReadAllText(marker).Trim(), null);

            await Assert.That(IsAlive(child)).IsTrue()
                .Because("attach's job is REACHING health rather than holding it, so what it "
                       + "started outlives the hook - the daemon owns it now.");

            Kill(child);
        }
        finally
        {
            File.Delete(marker);
            Directory.Delete(tree, recursive: true);
        }
    }

    private static bool IsAlive(int pid)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            return !process.HasExited;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static void Kill(int pid)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            process.Kill(entireProcessTree: true);
        }
        catch (ArgumentException)
        {
            // Already gone, which is the outcome this is reaching for.
        }
    }
}
