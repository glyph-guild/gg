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
/// <b>This is where ADR-0013's closed-vocabulary constraint stops being a
/// sentence.</b>
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
    IKeepACredential? credentials = null,
    // THE CEREMONY, OR NOTHING - the same shape one door over. A runner
    // nobody wired with it refuses to begin a login, and says so, because
    // the port is decided by a key of its own (accept-agent-login) and a
    // person who set the other key would otherwise go looking for a version.
    AgentLoginCeremony? login = null,
    // WHO TO TELL WHEN A CREDENTIAL LANDS. The held loop looks again the
    // moment it hears, rather than on its next cadence; null tells nobody.
    Action<string>? kept = null)
{
    private readonly IAnswersAboutItself _runner = runner;
    private readonly IKeepACredential? _credentials = credentials;
    private readonly AgentLoginCeremony? _login = login;
    private readonly Action<string>? _kept = kept;
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
                    // VALIDATED FIRST, AND BEFORE IT BECOMES A PATH OR AN ECHO.
                    // By the contract's own rule, and refused rather than
                    // sanitised. CredentialStore holds this too; a bound only the
                    // far end enforces disappears the moment the far end is wrong,
                    // and this is the machine whose disk it would be.
                    //
                    // AHEAD OF THE KEEPER CHECK so a steered locator gets the same
                    // silence whatever this machine is wired for: the arm below
                    // hands the locator back, and handing back `../../etc/passwd`
                    // would be answering a question nobody legitimate asked.
                    if (CredentialLocator.Validate(given.Locator) is not null)
                    {
                        Interlocked.Increment(ref _refused);
                        return null;
                    }

                    // NOWHERE TO KEEP IT IS A REFUSAL, AND IT SAYS SO.
                    //
                    // This returned null, reasoning that `written: false` would
                    // tell a console the runner COULD be configured and something
                    // went wrong. The sender reads it the other way round - its
                    // written-false arm names `accept-configured` and says whose
                    // decision it is - so the two halves disagreed about what the
                    // value meant, and that sentence could never be reached.
                    //
                    // WHAT SILENCE COST. It is indistinguishable from a runner too
                    // old to have this arm at all, so a live, beating, idle machine
                    // reported "either it is running a gg that predates this, or the
                    // ask did not reach it" - sending somebody to compare versions
                    // when the answer was one line in a file on that machine.
                    //
                    // STILL A REFUSAL: nothing is written, the counter moves, and
                    // the boolean is the whole of the answer. Saying "I heard you
                    // and I will not" discloses nothing that asking did not already
                    // establish.
                    if (_credentials is null)
                    {
                        Interlocked.Increment(ref _refused);

                        return new RunnerSaid
                        {
                            Kind = RunnerAskKinds.ConfigureCredential,
                            Configured = new ConfiguredCredential
                            {
                                // THE LOCATOR THE SENDER SENT, echoed so it can
                                // name which credential in what it prints. Already
                                // validated above, and handed straight back to the
                                // peer that supplied it rather than used.
                                Locator = given.Locator,
                                Written = false,
                            },
                        }.Stripped();
                    }

                    var written = _credentials.Keep(given.Locator, given.Secret);
                    if (written)
                    {
                        _kept?.Invoke(given.Locator);
                    }

                    return new RunnerSaid
                    {
                        Kind = RunnerAskKinds.ConfigureCredential,
                        Configured = new ConfiguredCredential
                        {
                            Locator = given.Locator,
                            Written = written,
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
    /// <summary>
    /// Answers the whole vocabulary: the two ceremony kinds here, everything
    /// else by <see cref="Answer"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Async because the ceremony waits on a child</b> - eight seconds for
    /// a URL, a person's browser visit for a token - and the three older kinds
    /// answer from memory. The channel serves through this one path and
    /// serialises per conversation, so an ask is never answered out of order.
    /// </para>
    /// <para>
    /// <b>The refusals, in order.</b> A provider that is not a locator segment
    /// or a code the contract bounds out is malformed: counted and dropped,
    /// as every malformed ask is. No port is a SENTENCE naming
    /// <c>accept-agent-login</c> - the <c>Written=false</c> lesson, learned
    /// once: silence is indistinguishable from a runner too old to have the
    /// arm. A provider this runner's adapter is not is counted and dropped:
    /// the console named an agent this machine does not run.
    /// </para>
    /// </remarks>
    public async Task<RunnerSaid?> AnswerAsync(RunnerAsk ask, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ask);

        switch (ask.Kind)
        {
            case RunnerAskKinds.BeginAgentLogin when ask.BeginAgentLogin is { } begin:
                {
                    if (LocatorFor(begin.Provider) is null)
                    {
                        Interlocked.Increment(ref _refused);
                        return null;
                    }

                    if (_login is null)
                    {
                        Interlocked.Increment(ref _refused);
                        return new RunnerSaid
                        {
                            Kind = RunnerAskKinds.BeginAgentLogin,
                            LoginBegun = new AgentLoginBegun
                            {
                                Provider = begin.Provider,
                                Started = false,
                                Diagnosis = NoPort,
                            },
                        }.Stripped();
                    }

                    if (!string.Equals(begin.Provider, _login.Provider, StringComparison.Ordinal))
                    {
                        Interlocked.Increment(ref _refused);
                        return null;
                    }

                    // NEVER WHILE FLYING, and the flight is named so the person
                    // knows what to wait for. The status report is the one
                    // place the dispatch already reads that from.
                    var flying = _runner.Status().FlightNumber;
                    return new RunnerSaid
                    {
                        Kind = RunnerAskKinds.BeginAgentLogin,
                        LoginBegun = await _login.BeginAsync(flying, cancellationToken),
                    }.Stripped();
                }

            case RunnerAskKinds.FinishAgentLogin when ask.FinishAgentLogin is { } finish:
                {
                    // THE CODE IS TYPED INTO A TERMINAL. Empty is not a code,
                    // over the bound is not a code, and a control character in
                    // one is a keystroke somebody else chose.
                    if (LocatorFor(finish.Provider) is not { } locator
                        || finish.Code is not { Length: > 0 and <= RunnerAskBounds.MaxLoginCode }
                        || finish.Code.Any(char.IsControl))
                    {
                        Interlocked.Increment(ref _refused);
                        return null;
                    }

                    if (_login is null)
                    {
                        Interlocked.Increment(ref _refused);
                        return new RunnerSaid
                        {
                            Kind = RunnerAskKinds.FinishAgentLogin,
                            LoginFinished = new AgentLoginFinished
                            {
                                Provider = finish.Provider,
                                Locator = locator,
                                Written = false,
                                Diagnosis = NoPort,
                            },
                        }.Stripped();
                    }

                    if (!string.Equals(finish.Provider, _login.Provider, StringComparison.Ordinal))
                    {
                        Interlocked.Increment(ref _refused);
                        return null;
                    }

                    return new RunnerSaid
                    {
                        Kind = RunnerAskKinds.FinishAgentLogin,
                        LoginFinished = await _login.FinishAsync(finish.Code, cancellationToken),
                    }.Stripped();
                }

            default:
                return Answer(ask);
        }
    }

    /// <summary>What a runner says when nothing wired it to run the ceremony.</summary>
    private const string NoPort =
        "this machine's configuration does not say `accept-agent-login`, so it will not start its "
      + "agent's login ceremony. A person opens that file on the machine; a pool member opens it "
      + "itself when it first starts, so one saying this runs a gg from before that, and takes its "
      + "token by `gg credential send --agent`.";

    /// <summary>The agent's locator for a provider, or null when the name is not a segment.</summary>
    private static string? LocatorFor(string? provider)
    {
        if (provider is not { Length: > 0 })
        {
            return null;
        }

        try
        {
            return CredentialLocator.ForAgent(provider);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

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
