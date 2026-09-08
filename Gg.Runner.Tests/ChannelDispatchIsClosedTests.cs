using Gg.Contracts;
using Gg.Runner;

namespace Gg.Runner.Tests;

/// <summary>
/// The channel answers two things and refuses everything else.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is where ADR-0013's read-only constraint stops being a sentence.</b>
/// The ADR says a general data channel to a component <c>CLAUDE.md</c> calls
/// hostile is a bad idea, and one that can only answer <c>tail-log</c> and
/// <c>status</c> is defensible. A dispatch that fell through to "do what it
/// says" would be the first kind wearing the second kind's name.
/// </para>
/// <para>
/// <b>The vocabulary IS the switch</b>, so widening the channel means adding a
/// value to a closed vocabulary — which moves a fingerprint and costs a contract
/// version. That is the cost the closure exists to impose.
/// </para>
/// </remarks>
public class ChannelDispatchIsClosedTests
{
    private static readonly DateTimeOffset T0 = new(2026, 9, 8, 7, 0, 0, TimeSpan.Zero);

    private sealed class ALog(params string[] lines) : IReadOnlyLog
    {
        public int Asked { get; private set; }

        public TailRead Tail(int howMany)
        {
            Asked = howMany;
            return new TailRead(lines.TakeLast(howMany).ToArray(), lines.Length > howMany);
        }
    }

    private static AskDispatch Dispatching(IReadOnlyLog log) =>
        new(new WhatThisRunnerSays(new SilentObserver(), log, () => T0));

    [Test]
    public async Task It_answers_a_tail()
    {
        var dispatch = Dispatching(new ALog("one", "two", "three"));

        var said = dispatch.Answer(new RunnerAsk
        {
            Kind = RunnerAskKinds.TailLog,
            TailLog = new TailLogAsk { Lines = 2 },
        });

        await Assert.That(said!.Kind).IsEqualTo(RunnerAskKinds.TailLog);
        await Assert.That(said.Tail!.Lines).IsEquivalentTo(new[] { "two", "three" });
        await Assert.That(said.Tail.Truncated).IsTrue()
            .Because("a tail that stopped at the bound and one genuinely that short read "
                   + "identically without this.");
    }

    [Test]
    public async Task It_answers_a_status()
    {
        var dispatch = Dispatching(new ALog());

        var said = dispatch.Answer(new RunnerAsk
        {
            Kind = RunnerAskKinds.Status,
            Status = new StatusAsk(),
        });

        await Assert.That(said!.Status!.Doing).IsNotEmpty();
        await Assert.That(said.Status.At).IsEqualTo(T0);
    }

    [Test]
    public async Task A_kind_it_does_not_know_gets_no_answer_at_all()
    {
        // NOT AN ERROR ANSWER, and not silence either. Null means "say nothing"
        // to a caller that must not turn it into an empty tail: an empty answer
        // and a refused ask are different facts, and this system's own
        // vocabulary calls collapsing two silences its worst failure.
        var dispatch = Dispatching(new ALog("one"));

        var said = dispatch.Answer(new RunnerAsk { Kind = "run-command" });

        await Assert.That(said).IsNull();
        await Assert.That(dispatch.Refused).IsEqualTo(1);
    }

    [Test]
    public async Task A_known_kind_with_no_payload_is_refused_rather_than_guessed()
    {
        // `tail-log` WITH NOTHING SAYING HOW MANY LINES IS NOT A TAIL-LOG.
        // Answering it with a default would be inventing what somebody asked for
        // - and the caller who sent it has a bug they will never find.
        var dispatch = Dispatching(new ALog("one"));

        await Assert.That(dispatch.Answer(new RunnerAsk { Kind = RunnerAskKinds.TailLog }))
            .IsNull();
        await Assert.That(dispatch.Answer(new RunnerAsk { Kind = RunnerAskKinds.Status }))
            .IsNull();
        await Assert.That(dispatch.Refused).IsEqualTo(2);
    }

    [Test]
    public async Task Refusals_are_counted_rather_than_logged()
    {
        // A HOSTILE PEER CAN SEND ANYTHING AS FAST AS IT LIKES. A runner that
        // wrote a line per refusal would be a runner whose disk somebody else
        // controls - and the log it writes is the very thing tail-log serves.
        var dispatch = Dispatching(new ALog());

        for (var i = 0; i < 50; i++)
        {
            dispatch.Answer(new RunnerAsk { Kind = $"probe-{i}" });
        }

        await Assert.That(dispatch.Refused).IsEqualTo(50);
    }

    [Test]
    public async Task The_runner_bounds_the_tail_rather_than_trusting_the_number_it_was_sent()
    {
        // A RUNNER THAT TRUSTED THE COUNT could be asked for its whole disk. The
        // bound is the contract's, and both ends hold it - the far end because
        // it should, this end because it must.
        var log = new ALog("one", "two");
        var dispatch = Dispatching(log);

        dispatch.Answer(new RunnerAsk
        {
            Kind = RunnerAskKinds.TailLog,
            TailLog = new TailLogAsk { Lines = int.MaxValue },
        });

        await Assert.That(log.Asked).IsEqualTo(RunnerAskBounds.MaxLines);

        dispatch.Answer(new RunnerAsk
        {
            Kind = RunnerAskKinds.TailLog,
            TailLog = new TailLogAsk { Lines = -5 },
        });

        await Assert.That(log.Asked).IsEqualTo(1)
            .Because("a negative count is not zero lines, it is a caller with a bug - and "
                   + "answering with nothing would look like an empty log.");
    }

    [Test]
    public async Task What_leaves_is_stripped()
    {
        // APPLIED HERE RATHER THAN AT WHATEVER RENDERS IT, because the
        // contract's rule is ONE rule for both sides - and a second transport
        // that forgot would carry escape sequences into somebody's terminal.
        var dispatch = Dispatching(new ALog("\u001b[2Jcleared your screen"));

        var said = dispatch.Answer(new RunnerAsk
        {
            Kind = RunnerAskKinds.TailLog,
            TailLog = new TailLogAsk { Lines = 1 },
        });

        await Assert.That(said!.Tail!.Lines[0]).DoesNotContain("\u001b");
        await Assert.That(said.Tail.Lines[0]).Contains("cleared your screen")
            .Because("stripping must remove sequences and not meaning.");
    }

    [Test]
    public async Task Every_kind_in_the_vocabulary_has_an_arm()
    {
        // THE CLOSURE, FROM THE OTHER SIDE. A value added to RunnerAskKinds with
        // no arm here would be a kind the contract says exists and the runner
        // silently refuses - which reads to a console exactly like a runner one
        // version behind.
        var dispatch = Dispatching(new ALog("a line"));

        var asks = new Dictionary<string, RunnerAsk>(StringComparer.Ordinal)
        {
            [RunnerAskKinds.TailLog] = new()
            {
                Kind = RunnerAskKinds.TailLog,
                TailLog = new TailLogAsk { Lines = 1 },
            },
            [RunnerAskKinds.Status] = new()
            {
                Kind = RunnerAskKinds.Status,
                Status = new StatusAsk(),
            },
        };

        foreach (var kind in RunnerAskKinds.All)
        {
            await Assert.That(asks.ContainsKey(kind)).IsTrue()
                .Because($"'{kind}' is in the vocabulary and this test does not know how to "
                       + "ask it, which means nobody checked the runner can answer it.");
            await Assert.That(dispatch.Answer(asks[kind])).IsNotNull()
                .Because($"'{kind}' is in the vocabulary and the dispatch refused it.");
        }
    }
}
