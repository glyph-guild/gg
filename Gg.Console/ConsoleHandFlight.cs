using System.Diagnostics;
using Gg.Client;
using Gg.Local;

namespace Gg.Console;

/// <summary>
/// Hands the selected flight to the person sitting at the console.
/// </summary>
/// <remarks>
/// <para>
/// <b>It spawns <c>gg</c>; it does not become a runner.</b> The runner is
/// treated as hostile and the OS is what keeps it apart from the console — and
/// the reference graph already enforces it, since nothing here can name a
/// runner type. Local means "on this machine", never "in this process".
/// </para>
/// <para>
/// <b>The refusal is computed HERE rather than read off the child.</b> The child
/// inherits the terminal, so a person does see what it printed — and then the
/// console redraws over it, and the pane says nothing about a flight that was
/// never opened. Asking first gives the answer somewhere to live, and satisfies
/// rule 5 with the same call.
/// </para>
/// </remarks>
public static class ConsoleHandFlight
{
    /// <summary>The sentence a refused hand-flight leaves in the model.</summary>
    /// <remarks>
    /// <b>The label first, because it is the actionable half.</b> A person
    /// reading this is deciding whether to bring an environment up, and the
    /// remedy is only useful once they know which one.
    /// </remarks>
    public static string Refused(string requirement, string remedy) =>
        $"Nothing was created: this flight requires {requirement}. {remedy}";

    /// <summary>What a hand-flight that actually ran leaves in the model.</summary>
    public static string Flew(string what) =>
        $"{what} was flown by hand on this machine.";

    /// <summary>
    /// Starts <c>gg fly --hand</c> with the terminal, and waits for the person.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Nothing is redirected.</b> This is the takeover's disposition and the
    /// opposite of the executor's: a person is at the keyboard and the child
    /// owns the screen until it exits. A redirect here would leave somebody
    /// typing into a pipe nobody reads.
    /// </para>
    /// <para>
    /// <b>Through <see cref="SelfInvocation"/>, so the child is THIS gg.</b> A
    /// bare <c>gg</c> resolved from PATH is whichever one is installed, which on
    /// a developer's machine is routinely not the one they are running.
    /// </para>
    /// </remarks>
    public static ProcessStartInfo StartInfoFor(SelfInvocation self, string intent)
    {
        ArgumentNullException.ThrowIfNull(self);

        var info = new ProcessStartInfo(self.Command)
        {
            RedirectStandardInput = false,
            RedirectStandardOutput = false,
            RedirectStandardError = false,
            UseShellExecute = false,
        };

        // THROUGH `Under`, WHICH ALREADY ANSWERS THE HARD HALF. Whether this
        // process needs its own assembly handed back to it - `dotnet Gg.Cli.dll`
        // rather than `gg` - is a question SelfInvocation settled, and its
        // remark says a second verb must ask rather than re-derive it. The
        // failure it records is a server that never started and nothing that
        // said so.
        foreach (var argument in self.Under("fly"))
        {
            info.ArgumentList.Add(argument);
        }

        info.ArgumentList.Add(intent);

        // LAST, and it does not matter where. `--hand` is stripped before the
        // verb is matched, exactly as `--json` is - which is what lets a person
        // type it wherever they reach for it.
        info.ArgumentList.Add("--hand");

        return info;
    }
}
