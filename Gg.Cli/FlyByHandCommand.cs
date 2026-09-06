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

        return await hold(launched.Value.FlightId, cancellationToken);
    }
}
