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

/// <summary>Where a credential this runner is given is kept.</summary>
/// <remarks>
/// <para>
/// <b>A port for <see cref="IAnswersAboutItself"/>'s reason and one more.</b>
/// The transport must not decide what happens to a secret - but more than that,
/// <c>Gg.Runner</c> cannot see the project the credential store lives in, and
/// that separation is the architecture rather than an accident. The composition
/// root, which sees both, hands one in. Or does not.
/// </para>
/// <para>
/// <b>Which is the lock.</b> A runner whose composition root wired nothing here
/// cannot be given a credential at all, exactly as a runner handed no private
/// key cannot be reached. "This runner may be configured" is a decision somebody
/// made about a machine, not a capability every runner has because the contract
/// grew a value.
/// </para>
/// <para>
/// <b>It answers whether the secret landed rather than throwing.</b> What calls
/// it is a channel a hostile peer is on the other end of, and an exception out
/// of a dispatch arm is a peer that can end a runner's conversation at will.
/// </para>
/// </remarks>
public interface IKeepACredential
{
    /// <summary>Keeps the secret under the locator. Whether it landed.</summary>
    bool Keep(string locator, string secret);
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
public sealed class AskDispatch(
    IAnswersAboutItself runner,
    // NULL IS THE DEFAULT AND THE DEFAULT IS CLOSED. A runner nobody wired to
    // keep a credential refuses to be given one, which is the same shape as a
    // runner handed no private key being unreachable - a wiring decision
    // somebody made, rather than a capability the contract handed out.
    IKeepACredential? credentials = null)
{
    private readonly IAnswersAboutItself _runner = runner;
    private readonly IKeepACredential? _credentials = credentials;
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
                    Tail = WithinBytes(_runner.Tail(lines)),
                }.Stripped();
            }

            case RunnerAskKinds.Status when ask.Status is not null:
                return new RunnerSaid
                {
                    Kind = RunnerAskKinds.Status,
                    Status = _runner.Status(),
                }.Stripped();

            // THE ONE ARM THAT WRITES, and every narrowing on it is here rather
            // than at whatever wired it. It performs nothing, returns no data,
            // and touches one file the runner already writes for itself - which
            // is what makes it not the `RunCommand` ADR-0013 names and
            // RunnerAskClosureTests plants.
            case RunnerAskKinds.ConfigureCredential when ask.ConfigureCredential is { } given:
            {
                // NOWHERE TO KEEP IT IS A REFUSAL, not a failure reported
                // politely. An answer saying `written: false` would tell a
                // console this runner COULD be configured and something went
                // wrong; it cannot be, and those are different facts.
                if (_credentials is null)
                {
                    Interlocked.Increment(ref _refused);
                    return null;
                }

                // VALIDATED BEFORE IT BECOMES A PATH, by the contract's own
                // rule, and refused rather than sanitised. CredentialStore holds
                // this too; a bound only the far end enforces disappears the
                // moment the far end is wrong, and this is the machine whose
                // disk it would be.
                if (CredentialLocator.Validate(given.Locator) is not null)
                {
                    Interlocked.Increment(ref _refused);
                    return null;
                }

                return new RunnerSaid
                {
                    Kind = RunnerAskKinds.ConfigureCredential,
                    Configured = new ConfiguredCredential
                    {
                        Locator = given.Locator,
                        Written = _credentials.Keep(given.Locator, given.Secret),
                    },
                }.Stripped();
            }

            default:
                // A KIND WITH NO PAYLOAD LANDS HERE TOO, deliberately. `tail-log`
                // with nothing to say how many lines is not a tail-log; treating
                // it as one with a default would be inventing what somebody
                // asked for.
                Interlocked.Increment(ref _refused);
                return null;
        }
    }
    /// <summary>
    /// The same tail, cut to the byte bound the contract declares.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b><c>RunnerAskBounds.MaxBytes</c> was declared, asserted as a constant,
    /// and enforced NOWHERE.</b> It did not matter while no production log was
    /// wired; it matters the moment the tail is a flight's live view, where a
    /// single <c>text</c> entry is a paragraph of an agent's prose and two
    /// hundred of them are megabytes.
    /// </para>
    /// <para>
    /// <b>The bound is on the CONTRACT so this side can hold it</b>, which is
    /// <c>HeartbeatCadence</c>'s argument: a bound only the far end enforces
    /// disappears the moment the far end is wrong, and this is the machine whose
    /// egress it spends.
    /// </para>
    /// <para>
    /// <b>The NEWEST lines survive.</b> Cutting from the front would answer a
    /// question about what a flight is doing NOW with what it was doing when it
    /// started. <c>Truncated</c> already means "there is more than this", so it
    /// stays true whichever bound did the cutting.
    /// </para>
    /// </remarks>
    private static LogTail WithinBytes(LogTail tail)
    {
        var budget = RunnerAskBounds.MaxBytes;
        var kept = new List<string>(tail.Lines.Count);

        // BACKWARDS, so what is dropped is the oldest.
        for (var i = tail.Lines.Count - 1; i >= 0; i--)
        {
            var cost = System.Text.Encoding.UTF8.GetByteCount(tail.Lines[i]) + 1;

            if (cost > budget)
            {
                break;
            }

            budget -= cost;
            kept.Insert(0, tail.Lines[i]);
        }

        return kept.Count == tail.Lines.Count
            ? tail
            : new LogTail { Lines = kept, Truncated = true };
    }
}
