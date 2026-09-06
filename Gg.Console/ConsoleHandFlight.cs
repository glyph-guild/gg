using System.Diagnostics;
using Gg.Client;
using Gg.Contracts;
using Gg.Local;

namespace Gg.Console;

/// <summary>
/// Opens a flight and hands the terminal to the person sitting at the console.
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

    /// <summary>
    /// Starts a runner on this machine, and does not wait for it.
    /// </summary>
    /// <param name="log">
    /// Where the child's output goes, because nobody is watching it.
    /// </param>
    /// <remarks>
    /// <para>
    /// <b>NOT the terminal, unlike flying by hand.</b> <c>gg runner up</c> is a
    /// server: it runs until it is stopped, and handing it the screen would mean
    /// a person could either watch a runner or use this console and not both.
    /// So it is spawned with its output redirected to a file and this returns
    /// immediately.
    /// </para>
    /// <para>
    /// <b>Which means what comes back is "starting", not "started".</b> The
    /// runner registers and then heartbeats, and the tab reads the control
    /// plane's derived state - so it appears a beat later, under a refresh. A
    /// sentence claiming it is up would be this console reporting its own
    /// optimism.
    /// </para>
    /// <para>
    /// <b>And it lives as long as this terminal does.</b> The child is not
    /// detached from the session, so closing the terminal takes the runner with
    /// it. That is the honest behaviour for a runner somebody started from a
    /// console they are sitting at, and it is said here rather than discovered.
    /// </para>
    /// </remarks>
    public static AppState Start(
        AppState state,
        SelfInvocation? self,
        string log,
        Func<ProcessStartInfo, bool> start)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(start);

        if (self is null)
        {
            return state with
            {
                LastRunner =
                    "Nothing was started: this gg cannot work out how to re-run itself, so the "
                  + "runner would be whichever gg is on PATH.",
            };
        }

        var info = new ProcessStartInfo(self.Command)
        {
            RedirectStandardInput = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        foreach (var argument in self.Under("runner"))
        {
            info.ArgumentList.Add(argument);
        }

        info.ArgumentList.Add("up");

        return state with
        {
            LastRunner = start(info)
                ? $"A runner is starting on this machine. Its output is in {log}, and it "
                + "appears in this tab a beat after it registers - press g."
                : "Nothing was started: the runner process would not start.",
        };
    }

    /// <summary>
    /// Ask, refuse or hand over, and say which.
    /// </summary>
    /// <param name="plan">
    /// What a flight would need of a machine, off the control plane.
    /// </param>
    /// <param name="advertised">
    /// What THIS machine advertises. The plan prices against the fleet, and a
    /// label some other runner has is useless to a person at this keyboard.
    /// </param>
    /// <param name="ask">
    /// The intent, from the same editor <c>n new flight</c> opens. Supplied by
    /// the loop, which owns the terminal.
    /// </param>
    /// <param name="start">
    /// Runs the child and waits for it. Injected so this whole order is
    /// testable without spawning anything.
    /// </param>
    /// <remarks>
    /// <para>
    /// <b>THE ORDER IS THE FEATURE, and it is the CLI verb's order with one
    /// addition.</b> <c>FlyByHand.FlyAsync</c> reads the plan before it opens
    /// anything, because a flight created and then abandoned because this laptop
    /// was wrong is litter with a number on it. Here the prompt goes after the
    /// refusal too: asking somebody to write a paragraph and then telling them
    /// the machine was never eligible is the same refusal and an insult.
    /// </para>
    /// <para>
    /// <b>The check is <see cref="HandRefusal.For"/>, which the verb also
    /// uses.</b> A containment run written again here would be the second
    /// evaluator this design forbids, one process further out, and the day the
    /// two disagreed a person would be refused for a label no runner was ever
    /// asked about.
    /// </para>
    /// <para>
    /// <b>And every outcome lands in the model.</b> The child inherits the
    /// terminal, so a person does see what it printed - and then this console
    /// redraws over it. A line claiming a flight over a child that failed would
    /// be the only record left, so the exit code is read rather than assumed.
    /// </para>
    /// </remarks>
    public static AppState Fly(
        AppState state,
        Func<Checklist> plan,
        IReadOnlyList<string> advertised,
        Func<string> ask,
        SelfInvocation? self,
        Func<ProcessStartInfo, int> start)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(advertised);
        ArgumentNullException.ThrowIfNull(ask);
        ArgumentNullException.ThrowIfNull(start);

        if (self is null)
        {
            // WHICH gg THE CHILD WOULD BE. SelfInvocation answers "this one" and
            // returns null when it cannot - a bare `gg` off PATH is whichever is
            // installed, which on a developer's machine is routinely not the one
            // they are running.
            return state with
            {
                LastHandFlight =
                    "Nothing was created: this gg cannot work out how to re-run itself, so "
                  + "the flight would be handed to whichever gg is on PATH.",
                HandFlightProblem =
                    "Nothing was created: this gg cannot work out how to re-run itself, so "
                  + "the flight would be handed to whichever gg is on PATH.",
            };
        }

        Checklist required;

        try
        {
            required = plan();
        }
        catch (Exception failure) when (failure is NotSignedInException
                                            or ProtocolTooOldException
                                            or NoEnvelopeException
                                            or HttpRequestException)
        {
            // ITS OWN FAILURE, said in the model. Rule 5's third sentence: what
            // one read loses is one read's worth, and the rest of the console is
            // still true.
            return state with
            {
                LastHandFlight = "Nothing was created: " + failure.Message,
                HandFlightProblem = "Nothing was created: " + failure.Message,
            };
        }

        if (HandRefusal.For(required, advertised) is { } refused)
        {
            return state with
            {
                LastHandFlight = Refused(refused.Requirement, refused.Remedy),
                HandFlightProblem = Refused(refused.Requirement, refused.Remedy),
            };
        }

        var intent = ask().Trim();

        if (intent.Length == 0)
        {
            // A flight opened by accident is a record somebody has to explain
            // and a number that is now taken.
            return state with
            {
                LastHandFlight = "Nothing was created: no intent was written.",
                HandFlightProblem = "Nothing was created: no intent was written.",
            };
        }

        var exit = start(StartInfoFor(self, intent));

        var trouble = exit == 0
            ? null
            : $"'{intent}' was not flown: gg exited {exit}. Whatever it printed is above this "
            + "screen, and this console has drawn over it.";

        return state with
        {
            LastHandFlight = trouble ?? Flew(intent),

            // NULL ON THE WAY THROUGH, deliberately. A person who is refused,
            // fixes it and flies must not meet the old refusal again, and this
            // field is what the loop reads to decide whether to open the modal.
            HandFlightProblem = trouble,
        };
    }
}
