using Gg.Contracts;

namespace Gg.Runner;

/// <summary>What a runner will say about itself, and nothing else.</summary>
/// <remarks>
/// <b>An interface so the transport does not decide the answers.</b> ADR-0013
/// requires the protocol to exist before anything carries it; this is the same
/// rule one layer down, so the WebRTC channel and the dead-drop hand a
/// <see cref="RunnerAsk"/> to the same object and neither gets to interpret one.
/// </remarks>
public interface IAnswersAboutItself
{
    /// <summary>The last lines this runner wrote, bounded.</summary>
    LogTail Tail(int lines);

    /// <summary>What it is doing, and what it last failed at.</summary>
    RunnerStatusReport Status();
}

/// <summary>
/// The channel's whole vocabulary, in one switch that cannot grow by accident.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is where ADR-0013's read-only constraint stops being a sentence.</b>
/// The ADR says a general data channel to a component <c>CLAUDE.md</c> calls
/// hostile is a bad idea and one that can only answer <c>tail-log</c> and
/// <c>status</c> is defensible. A dispatch that fell through to "do what it
/// says" would be the first kind wearing the second kind's name.
/// </para>
/// <para>
/// <b>Refused rather than ignored, and counted rather than logged.</b> An ask
/// this does not know is either a newer console talking to an older runner or
/// somebody probing; both are worth a number, and neither is worth a log line
/// that a hostile peer can make the runner write as often as it likes. Silence
/// would be worse still: <see cref="RunnerAskKinds"/>' own argument is that
/// silently absent is indistinguishable from satisfied.
/// </para>
/// <para>
/// <b>Everything that leaves is stripped.</b> <see cref="RunnerSaid.Stripped"/>
/// is applied here rather than at whatever renders it, because the contract's
/// rule is one rule for both sides - and a second transport that forgot would be
/// a second transport carrying escape sequences into somebody's terminal.
/// </para>
/// </remarks>
public sealed class AskDispatch(IAnswersAboutItself runner)
{
    private readonly IAnswersAboutItself _runner = runner;
    private int _refused;

    /// <summary>How many asks this runner did not recognise.</summary>
    /// <remarks>
    /// <b>A number rather than a log.</b> A hostile peer can send anything as
    /// fast as it likes, and a runner that wrote a line per refusal would be a
    /// runner whose disk somebody else controls.
    /// </remarks>
    public int Refused => _refused;

    /// <summary>
    /// Answers one ask, or nothing when it is not one this runner knows.
    /// </summary>
    /// <returns>
    /// The answer, stripped — or null, which the caller must treat as "say
    /// nothing" rather than as an empty answer.
    /// </returns>
    public RunnerSaid? Answer(RunnerAsk ask)
    {
        ArgumentNullException.ThrowIfNull(ask);

        // THE VOCABULARY IS THE SWITCH. A kind not named here has no arm, and
        // adding one means adding a value to a closed vocabulary - which moves a
        // fingerprint and costs a contract version, which is the whole point.
        switch (ask.Kind)
        {
            case RunnerAskKinds.TailLog when ask.TailLog is { } asked:
            {
                // BOUNDED HERE TOO, not only at the far end. The bound is the
                // contract's, and a runner that trusted the number it was sent
                // would be a runner a console could ask for its whole disk.
                var lines = Math.Clamp(asked.Lines, 1, RunnerAskBounds.MaxLines);

                return new RunnerSaid
                {
                    Kind = RunnerAskKinds.TailLog,
                    Tail = _runner.Tail(lines),
                }.Stripped();
            }

            case RunnerAskKinds.Status when ask.Status is not null:
                return new RunnerSaid
                {
                    Kind = RunnerAskKinds.Status,
                    Status = _runner.Status(),
                }.Stripped();

            default:
                // A KIND WITH NO PAYLOAD LANDS HERE TOO, deliberately. `tail-log`
                // with nothing to say how many lines is not a tail-log; treating
                // it as one with a default would be inventing what somebody
                // asked for.
                Interlocked.Increment(ref _refused);
                return null;
        }
    }
}
