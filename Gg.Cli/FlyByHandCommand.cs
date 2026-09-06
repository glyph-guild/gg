using Gg.Client;
using Gg.Contracts;

namespace Gg.Cli;

/// <summary>
/// `gg fly --hand`: open the flight, then hand this terminal to the person who
/// opened it.
/// </summary>
/// <remarks>
/// <para>
/// <b>A class rather than a switch arm, because a switch arm is what went
/// missing.</b> Every piece under this was built and tested — the refusal, the
/// executor, the directed claim, the one-flight runner — and none of it ran,
/// because <c>Program.cs</c> read <c>fly.Text</c> and never <c>fly.ByHand</c>.
/// The arm is now one line calling this, and this is a thing a test can reach.
/// </para>
/// <para>
/// <b>The order is the design.</b> The plan is read FIRST, because everything
/// after it creates something: read afterwards it answers the same question and
/// leaves a flight behind for somebody else's fleet to work. The terminal is
/// handed over LAST, because there has to be a flight to hand over — and only
/// when the open actually produced one, since a refused or diverted open leaves
/// nothing to claim and a runner started anyway would wait out its long poll
/// asking for a flight that does not exist.
/// </para>
/// <para>
/// <b>Seams rather than construction, and not for tidiness.</b> What broke here
/// was the join between two halves that were each fine, so what this file needs
/// most is to be assertable from outside. Every dependency arrives as a
/// delegate; <c>Program.cs</c> supplies the real ones.
/// </para>
/// </remarks>
public static class FlyByHandCommand
{
    /// <param name="plan">What a flight opened now would need, priced against the live fleet.</param>
    /// <param name="advertised">What THIS machine advertises — a label some other runner has is useless here.</param>
    /// <param name="open">Opens the flight, through the same ingress as an ordinary `gg fly`.</param>
    /// <param name="hold">Runs the attended runner for the flight just opened, and answers its exit code.</param>
    /// <param name="say">Where the person is told what happened.</param>
    public static async Task<int> RunAsync(
        CliAction.Fly fly,
        Func<CancellationToken, Task<Checklist>> plan,
        IReadOnlyList<string> advertised,
        Func<CancellationToken, Task<VerbResult>> open,
        Func<string, CancellationToken, Task<int>> hold,
        Action<string> say,
        Func<string, Task<IReadOnlyList<PendingGate>>> gates,
        IGateAnswer answer,
        Func<string, string, string, string?, Task<bool>> decide,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fly);
        ArgumentNullException.ThrowIfNull(hold);
        ArgumentNullException.ThrowIfNull(say);

        var flown = await FlyByHand.FlyAsync(plan, advertised, open, cancellationToken);

        if (flown.Refused is { } refused)
        {
            // THE REASON'S OWN SENTENCE, rendered by the one renderer. Every
            // other surface that shows a refusal calls Reason.Sentence, and a
            // second wording composed here would be a second answer to "why can
            // this machine not fly it" that nobody could compare with the first.
            say(Reason.Sentence(refused.Reason.Kind, refused.Reason.Params));
            return ExitCodes.Refused;
        }

        say(fly.Json ? VerbOutput.ToJson(flown.Opened!) : VerbOutput.ToText(flown.Opened!));

        // A LAUNCH OR NOTHING TO HOLD. The open goes through the ordinary
        // ingress, so it can answer something that is not a launch - and there
        // is then no flight to claim by name.
        if (flown.Opened is not VerbResult.Launched launched)
        {
            return ExitCodes.For(flown.Opened!);
        }

        var exit = await hold(launched.Value.FlightId, cancellationToken);

        // A NUMBER OR NOTHING TO OFFER. The flight number is minted
        // asynchronously, so a launch can answer before it exists - and a gate is
        // asked for by the reference a person types. Saying so beats a prompt
        // that silently never appears.
        if (launched.Value.FlightNumber is not { Length: > 0 } number)
        {
            say("This flight has no number yet, so anything it is waiting on is not offered "
              + "here. `gg gates` will have it once the number lands.");

            return exit;
        }

        await AnsweringAsync(number, gates, answer, decide, say, fly.Json);

        return exit;
    }

    /// <summary>
    /// Offers whatever this flight is now waiting on, while the person is still
    /// at the terminal.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>AFTER the work, because there is nothing to decide before it.</b> A
    /// gate is opened by what the flight produced, so asking first would be
    /// asking about a manifest that does not exist.
    /// </para>
    /// <para>
    /// <b>Rendered by the one renderer.</b> What a gate looks like is
    /// <c>gg gates</c>' answer and this shows the same thing - so a field added
    /// there appears here without anybody remembering, and a second layout
    /// cannot drift into showing less. <c>because</c> is the column that matters
    /// most: when the condition is <i>loop asked for a decision</i> the Engine
    /// composes that sentence from the fact, so the agent's own question is the
    /// tail of it.
    /// </para>
    /// <para>
    /// <b>Nothing waiting asks nothing.</b> The common case is a flight that
    /// opened no gate, and a prompt that appeared anyway would make every
    /// hand-flight cost a keystroke.
    /// </para>
    /// </remarks>
    private static async Task AnsweringAsync(
        string flightNumber,
        Func<string, Task<IReadOnlyList<PendingGate>>> gates,
        IGateAnswer answer,
        Func<string, string, string, string?, Task<bool>> decide,
        Action<string> say,
        bool json)
    {
        var waiting = await gates(flightNumber);

        if (waiting.Count == 0)
        {
            return;
        }

        // NOBODY TO ASK, OR AN ANSWER NOBODY WOULD READ. `--json` is a machine
        // reading this and a prompt would block it for ever; redirected stdin is
        // the same fact one layer down. Both say where the decision lives rather
        // than printing a question into a pipe.
        if (json || !answer.CanAsk)
        {
            say($"{waiting.Count} decision(s) are waiting on this flight. Run `gg gates` to "
              + "see them and `gg decide` to answer.");

            return;
        }

        foreach (var gate in waiting)
        {
            say(VerbOutput.ToText(new VerbResult.Gates(new GateList { Gates = [gate] })));

            // A REASON TRAVELS WITH A REJECTION, because the product requires
            // one: the loop runs again with it, and a rejection that says
            // nothing sends the work back against the same instructions it just
            // followed. Approving needs none.
            //
            // NULL IS NOBODY ANSWERING, which is not a decision. Somebody who
            // closed the terminal leaves the gate exactly as it was.
            if (answer.Ask(gate.ObligationId) is not var (outcome, reason))
            {
                say($"No answer, so {gate.ObligationId} is still waiting.");

                return;
            }

            say(await decide(flightNumber, gate.ObligationId, outcome, reason)
                ? $"Recorded: {gate.ObligationId} {outcome}."
                : $"That decision was not recorded, and {gate.ObligationId} is still waiting. "
                + "Nothing about the flight changed.");
        }
    }
}
