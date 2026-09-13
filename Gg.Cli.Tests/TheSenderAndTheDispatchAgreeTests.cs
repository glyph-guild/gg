using Gg.Client;
using Gg.Contracts;
using Gg.Runner;

namespace Gg.Cli.Tests;

/// <summary>
/// What the console actually sends is something the runner actually answers.
/// </summary>
/// <remarks>
/// <para>
/// <b>Written because the same shape failed on a real machine one verb
/// over.</b> A follow loop sent <c>new RunnerAsk { Kind = Status }</c> with no
/// <c>StatusAsk</c> beside it. <see cref="AskDispatch"/> matches the kind AND
/// the member — <c>when ask.Status is not null</c> — so every ask was refused
/// and counted, and the whole feature did nothing. Every test stayed green,
/// because the double on the far end answered on the kind and never looked at
/// the payload.
/// </para>
/// <para>
/// <b>The credential path did not have that bug, and had nothing that would
/// have caught it.</b> The runner's own tests build their asks; the console's
/// send is never exercised against a dispatch; and
/// <c>AConsoleReachesARunnerTests</c> writes its own literals rather than
/// taking what the sender constructs. Three test files over this path, and the
/// one question none of them asks is whether the two halves agree.
/// </para>
/// <para>
/// <b>Here because this is the only assembly that sees both</b>, which is
/// itself the architecture working: <c>Gg.Client</c> cannot reference
/// <c>Gg.Runner</c>, so the agreement between them can only be checked where
/// both are already on the shelf.
/// </para>
/// <para>
/// <b>And the REAL dispatch, never a double.</b> A double is precisely what
/// hid this the first time: it is free to be more forgiving than the thing it
/// stands in for, and the forgiveness is invisible.
/// </para>
/// </remarks>
public class TheSenderAndTheDispatchAgreeTests
{
    private const string TheSecret = "ghp-not-a-real-token-6e2f-81a3";

    private sealed class Quiet : IAnswersAboutItself
    {
        public LogTail Tail(int lines) => new() { Lines = [], Truncated = false };

        public RunnerStatusReport Status() => new()
        {
            Doing = "nothing",
            At = DateTimeOffset.UnixEpoch,
        };
    }

    private sealed class AStore : IKeepACredential
    {
        public string? Kept { get; private set; }

        public bool Keep(string locator, string secret)
        {
            Kept = secret;
            return true;
        }
    }

    [Test]
    public async Task The_ask_the_console_builds_is_one_the_runner_answers()
    {
        var store = new AStore();
        var dispatch = new AskDispatch(new Quiet(), store);

        // WHAT THE SENDER WOULD PUT ON THE WIRE, taken from the sender rather
        // than written again here. A literal in this file would assert that a
        // shape the dispatch likes exists, which nobody doubted - the question
        // is whether the console produces it.
        var said = dispatch.Answer(
            SendACredential.Asking("local:acme/widgets", TheSecret));

        await Assert.That(said).IsNotNull()
            .Because("a refused ask is silence on the channel: the console waits out its "
                   + "patience and reports a runner that did not answer, which sends "
                   + "somebody to look at a machine that is working perfectly.");

        await Assert.That(said!.Configured!.Written).IsTrue();
        await Assert.That(store.Kept).IsEqualTo(TheSecret);
    }

    [Test]
    public async Task A_kind_with_nothing_beside_it_is_refused_and_counted()
    {
        // THE POISON TWIN, and the exact mistake that shipped elsewhere. If the
        // dispatch answered this, the test above would pass on a sender that had
        // stopped filling the payload - which is how a feature comes to do
        // nothing while every test agrees it works.
        var dispatch = new AskDispatch(new Quiet(), new AStore());

        var said = dispatch.Answer(new RunnerAsk
        {
            Kind = RunnerAskKinds.ConfigureCredential,
        });

        await Assert.That(said).IsNull();
        await Assert.That(dispatch.Refused).IsEqualTo(1)
            .Because("counted rather than logged, so a peer sending nonsense cannot make a "
                   + "runner write to its own disk.");
    }

    [Test]
    public async Task Every_kind_the_console_can_send_is_matched_by_its_payload()
    {
        // THE RULE GENERALISED, over the whole vocabulary rather than the one
        // verb this file came from. Any arm in AskDispatch matches on a member
        // as well as a kind, so a kind sent bare is refused whatever it is -
        // and a sender that forgot one fails silently in exactly this way.
        var dispatch = new AskDispatch(new Quiet(), new AStore());

        foreach (var kind in RunnerAskKinds.All)
        {
            await Assert.That(dispatch.Answer(new RunnerAsk { Kind = kind })).IsNull()
                .Because($"'{kind}' with no payload is not an ask of that kind, and treating "
                       + "it as one with a default would be inventing what somebody asked "
                       + "for.");
        }
    }
}
