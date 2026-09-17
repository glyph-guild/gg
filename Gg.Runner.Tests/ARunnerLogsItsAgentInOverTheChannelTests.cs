using System.Text.Json;
using Gg.Contracts;
using Gg.Runner.Execution;

namespace Gg.Runner.Tests;

/// <summary>
/// A runner drives its agent's own login ceremony when asked to over the
/// channel: it starts the agent's <c>setup-token</c>, says the URL back, types
/// the code a person brings, and keeps what the agent mints under the agent's
/// locator. The token is in nothing it says.
/// </summary>
/// <remarks>
/// <para>
/// <b>One ceremony at a time, bounded, and never while flying.</b> A second
/// <c>setup-token</c> beside a first would be two children waiting for one
/// person; one beside a flight spends the allowance the flight is spending.
/// An abandoned ceremony is ended at <see cref="AgentLoginCeremony.Patience"/>
/// on the beat's clock, because a child waiting for a code nobody will type is
/// a process nobody asked for.
/// </para>
/// <para>
/// <b>The refusals are the dispatch's, in order.</b> No port is a sentence
/// naming <c>accept-agent-login</c> - the <c>Written=false</c> lesson: silence
/// is indistinguishable from a runner too old. A provider this adapter is not,
/// or a code the contract bounds out, is counted and dropped, as every other
/// malformed ask is.
/// </para>
/// </remarks>
public class ARunnerLogsItsAgentInOverTheChannelTests
{
    private const string TheUrl = "https://claude.com/cai/oauth/authorize?code_challenge=abc&state=xyz";
    private const string TheToken = "sk-ant-oat01-minted-by-the-agent-not-a-real-one";
    private static readonly DateTimeOffset T0 = new(2026, 9, 15, 12, 0, 0, TimeSpan.Zero);

    private sealed class FakeAgent : IAuthenticateAnAgent
    {
        public string Provider => "claude";

        public string Locator { get; } = CredentialLocator.ForAgent("claude");

        public string TokenVariable => "CLAUDE_CODE_OAUTH_TOKEN";

        public Task<AgentStanding> ProbeAsync(string? token, CancellationToken cancellationToken) =>
            Task.FromResult(new AgentStanding(true, AgentCredentialSources.Token, "", T0));

        public bool NeedsLogin(string? said) => false;
    }

    private sealed class FakeChild : IAgentLoginChild
    {
        public string? Url { get; init; } = TheUrl;

        public string? Token { get; init; } = TheToken;

        public string? Typed { get; private set; }

        public bool Disposed { get; private set; }

        public Task<string?> UrlAsync(CancellationToken cancellationToken) => Task.FromResult(Url);

        public Task<string?> TokenAsync(string code, CancellationToken cancellationToken)
        {
            Typed = code;
            return Task.FromResult(Token);
        }

        /// <summary>What the child had written when it was asked.</summary>
        public string Screen { get; init; } = "";

        public string LastWords(int characters) =>
            Screen.Length <= characters ? Screen : Screen[^characters..];

        public void Dispose() => Disposed = true;
    }

    [Test]
    public async Task A_login_that_minted_nothing_says_what_the_agent_said()
    {
        // THREE ROUNDS OF NOT KNOWING. A person pasted a code, the ceremony
        // waited ninety seconds, and the refusal said the agent "printed no
        // token ... the code may have been wrong, or the ceremony had expired".
        // The one thing that knew - the child's own screen, which says `Invalid
        // code` or `Expired` in its own words - was thrown away, so the next
        // attempt was as blind as the last.
        var rig = new Rig();
        rig.Login.Next = () => new FakeChild
        {
            Token = null,
            Screen = "Paste code here if prompted > \nInvalid code. Try again.",
        };

        _ = await rig.Ceremony.BeginAsync(flying: null, CancellationToken.None);
        var finished = await rig.Ceremony.FinishAsync("a-code", CancellationToken.None);

        await Assert.That(finished.Written).IsFalse();
        await Assert.That(finished.Diagnosis).Contains("Invalid code")
            .Because("the agent's own words are the only thing that says which of the two "
                   + "guesses this was.");
    }

    [Test]
    public async Task What_the_agent_said_carries_no_secret_and_no_escape()
    {
        // WHAT A DIAGNOSIS CROSSES INTO. It is rendered on somebody's console,
        // so a screen scraped whole would carry the token the child just
        // printed, the code a person typed, and whatever escape sequences the
        // child used to draw itself.
        var rig = new Rig();
        rig.Login.Next = () => new FakeChild
        {
            Token = null,
            Screen = "\u001b[2Jtoken: sk-ant-oat01-" + new string('x', 40)
                   + " for code a-very-secret-code\u0007",
        };

        _ = await rig.Ceremony.BeginAsync(flying: null, CancellationToken.None);
        var finished = await rig.Ceremony.FinishAsync(
            "a-very-secret-code", CancellationToken.None);

        await Assert.That(finished.Diagnosis).DoesNotContain("sk-ant-oat01-")
            .Because("a token in a diagnosis is a token on a screen and in a log.");
        await Assert.That(finished.Diagnosis).DoesNotContain("a-very-secret-code")
            .Because("the code a person typed is theirs, and this side has it only to type it.");
        await Assert.That(finished.Diagnosis!.Any(char.IsControl)).IsFalse()
            .Because("this lands on a terminal, and a child that drew itself must not draw on "
                   + "somebody else's screen.");
    }

    private sealed class FakeLogin : IRunAnAgentLogin
    {
        public List<FakeChild> Children { get; } = [];

        public Func<FakeChild> Next { get; set; } = () => new FakeChild();

        public Task<IAgentLoginChild> StartAsync(CancellationToken cancellationToken)
        {
            var child = Next();
            Children.Add(child);
            return Task.FromResult<IAgentLoginChild>(child);
        }
    }

    private sealed class AStore : IKeepACredential
    {
        public Dictionary<string, string> Kept { get; } = [];

        public bool Keep(string locator, string secret)
        {
            Kept[locator] = secret;
            return true;
        }
    }

    private sealed class Quiet : IAnswersAboutItself
    {
        public string? FlightNumber { get; set; }

        public LogTail Tail(int lines) => new() { Lines = [], Truncated = false };

        public RunnerStatusReport Status() => new()
        {
            Doing = FlightNumber is null ? "idle" : "flying",
            At = T0,
            FlightNumber = FlightNumber,
        };
    }

    private sealed class FakeClock : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = T0;
    }

    private sealed class Rig
    {
        public FakeLogin Login { get; } = new();

        public AStore Store { get; } = new();

        public Quiet Runner { get; } = new();

        public FakeClock Clock { get; } = new();

        public List<string> Kept { get; } = [];

        public List<string> Said { get; } = [];

        public AgentLoginCeremony Ceremony { get; }

        public AskDispatch Dispatch { get; }

        public Rig()
        {
            Ceremony = new AgentLoginCeremony(
                new FakeAgent(),
                new AgentLoginPorts(Login, Store),
                Clock,
                kept: Kept.Add,
                saying: Said.Add);
            Dispatch = new AskDispatch(Runner, Store, Ceremony, kept: Kept.Add);
        }
    }

    private static RunnerAsk Beginning(string provider = "claude") => new()
    {
        Kind = RunnerAskKinds.BeginAgentLogin,
        BeginAgentLogin = new BeginAgentLoginAsk { Provider = provider },
    };

    private static RunnerAsk Finishing(string code, string provider = "claude") => new()
    {
        Kind = RunnerAskKinds.FinishAgentLogin,
        FinishAgentLogin = new FinishAgentLoginAsk { Provider = provider, Code = code },
    };

    private static string Json(RunnerSaid? said) =>
        JsonSerializer.Serialize(said, ChannelJson.Default.RunnerSaid);

    [Test]
    public async Task Begin_starts_the_agents_own_ceremony_and_says_the_url()
    {
        var rig = new Rig();

        var said = await rig.Dispatch.AnswerAsync(Beginning(), CancellationToken.None);

        var begun = said?.LoginBegun;
        await Assert.That(begun).IsNotNull();
        await Assert.That(begun!.Started).IsTrue();
        await Assert.That(begun.Provider).IsEqualTo("claude");
        await Assert.That(begun.Url).IsEqualTo(TheUrl)
            .Because("the URL is the one thing a person needs from this half, and it is safe "
                   + "to show: the code it leads to is useless without the verifier the child "
                   + "holds.");
        await Assert.That(begun.ExpiresAt).IsEqualTo(T0 + AgentLoginCeremony.Patience)
            .Because("a person told when the ceremony ends can decide whether to hurry.");
        await Assert.That(rig.Login.Children).Count().IsEqualTo(1);
        await Assert.That(rig.Ceremony.InProgress).IsTrue();
    }

    [Test]
    public async Task A_second_begin_while_one_is_open_is_refused_and_says_until_when()
    {
        var rig = new Rig();
        _ = await rig.Dispatch.AnswerAsync(Beginning(), CancellationToken.None);

        var again = await rig.Dispatch.AnswerAsync(Beginning(), CancellationToken.None);

        var begun = again?.LoginBegun;
        await Assert.That(begun).IsNotNull();
        await Assert.That(begun!.Started).IsFalse();
        await Assert.That(begun.Diagnosis).Contains("already")
            .Because("two ceremonies would be two children waiting for one person.");
        await Assert.That(begun.ExpiresAt).IsEqualTo(T0 + AgentLoginCeremony.Patience);
        await Assert.That(rig.Login.Children).Count().IsEqualTo(1)
            .Because("refused means nothing was started.");
    }

    [Test]
    public async Task Finish_keeps_the_token_under_the_agents_locator_and_says_only_the_locator()
    {
        var rig = new Rig();
        var begun = await rig.Dispatch.AnswerAsync(Beginning(), CancellationToken.None);

        var said = await rig.Dispatch.AnswerAsync(Finishing("the-code-from-the-browser"), CancellationToken.None);

        var child = rig.Login.Children.Single();
        await Assert.That(child.Typed).IsEqualTo("the-code-from-the-browser");
        await Assert.That(rig.Store.Kept).ContainsKey("local:agent/claude");
        await Assert.That(rig.Store.Kept["local:agent/claude"]).IsEqualTo(TheToken)
            .Because("the token lands where the executor's launch and the held loop's probe "
                   + "both look for it.");
        await Assert.That(rig.Kept).Contains("local:agent/claude")
            .Because("the loop is told, so a held runner looks again now rather than on the "
                   + "next cadence.");

        var finished = said?.LoginFinished;
        await Assert.That(finished).IsNotNull();
        await Assert.That(finished!.Written).IsTrue();
        await Assert.That(finished.Locator).IsEqualTo("local:agent/claude");

        // THE TOKEN IS IN NOTHING SAID BACK, asserted over the bytes that would
        // cross rather than over member names. And the poison twin: the URL IS
        // in what was said, so the scan can see the kind of thing it hunts.
        await Assert.That(Json(said)).DoesNotContain(TheToken);
        await Assert.That(Json(begun)).DoesNotContain(TheToken);
        // The serializer escapes '&' and '?', so the twin looks for the part
        // of the URL that survives encoding rather than the whole string.
        await Assert.That(Json(begun)).Contains("claude.com/cai/oauth/authorize");
        await Assert.That(begun!.LoginBegun!.Url).IsEqualTo(TheUrl);

        await Assert.That(child.Disposed).IsTrue()
            .Because("the ceremony is over, and the child with it.");
        await Assert.That(rig.Ceremony.InProgress).IsFalse();
    }

    [Test]
    public async Task Finish_with_no_ceremony_open_says_nothing_was_written()
    {
        var rig = new Rig();

        var said = await rig.Dispatch.AnswerAsync(Finishing("late"), CancellationToken.None);

        var finished = said?.LoginFinished;
        await Assert.That(finished).IsNotNull();
        await Assert.That(finished!.Written).IsFalse();
        await Assert.That(finished.Locator).IsEqualTo("local:agent/claude");
        await Assert.That(finished.Diagnosis).Contains("no login is in progress");
        await Assert.That(rig.Store.Kept).IsEmpty();
    }

    [Test]
    public async Task A_child_that_prints_no_token_is_said_as_not_written_and_nothing_is_kept()
    {
        var rig = new Rig();
        rig.Login.Next = () => new FakeChild { Token = null };
        _ = await rig.Dispatch.AnswerAsync(Beginning(), CancellationToken.None);

        var said = await rig.Dispatch.AnswerAsync(Finishing("wrong-code"), CancellationToken.None);

        var finished = said?.LoginFinished;
        await Assert.That(finished!.Written).IsFalse();
        await Assert.That(finished.Diagnosis).IsNotNull();
        await Assert.That(rig.Store.Kept).IsEmpty();
        await Assert.That(rig.Kept).IsEmpty();
        await Assert.That(rig.Login.Children.Single().Disposed).IsTrue();
        await Assert.That(rig.Ceremony.InProgress).IsFalse()
            .Because("a wrong code ends the ceremony; the person begins again.");
    }

    [Test]
    public async Task A_child_that_prints_no_url_is_said_as_not_started_and_is_ended()
    {
        var rig = new Rig();
        rig.Login.Next = () => new FakeChild { Url = null };

        var said = await rig.Dispatch.AnswerAsync(Beginning(), CancellationToken.None);

        var begun = said?.LoginBegun;
        await Assert.That(begun!.Started).IsFalse();
        await Assert.That(begun.Url).IsNull();
        await Assert.That(begun.Diagnosis).IsNotNull();
        await Assert.That(rig.Login.Children.Single().Disposed).IsTrue();
        await Assert.That(rig.Ceremony.InProgress).IsFalse();
    }

    [Test]
    public async Task An_unknown_provider_is_refused_and_counted()
    {
        var rig = new Rig();

        var begun = await rig.Dispatch.AnswerAsync(Beginning("codex"), CancellationToken.None);
        var finished = await rig.Dispatch.AnswerAsync(Finishing("x", "codex"), CancellationToken.None);

        await Assert.That(begun).IsNull();
        await Assert.That(finished).IsNull();
        await Assert.That(rig.Dispatch.Refused).IsEqualTo(2)
            .Because("counted rather than logged, so a peer sending nonsense cannot make a "
                   + "runner write.");
        await Assert.That(rig.Login.Children).IsEmpty();
    }

    [Test]
    public async Task An_oversize_or_empty_code_is_refused_and_counted()
    {
        var rig = new Rig();
        _ = await rig.Dispatch.AnswerAsync(Beginning(), CancellationToken.None);

        var oversize = await rig.Dispatch.AnswerAsync(
            Finishing(new string('a', RunnerAskBounds.MaxLoginCode + 1)), CancellationToken.None);
        var empty = await rig.Dispatch.AnswerAsync(Finishing(""), CancellationToken.None);

        await Assert.That(oversize).IsNull();
        await Assert.That(empty).IsNull();
        await Assert.That(rig.Dispatch.Refused).IsEqualTo(2);
        await Assert.That(rig.Login.Children.Single().Typed).IsNull()
            .Because("a refused code never reaches the child.");
        await Assert.That(rig.Ceremony.InProgress).IsTrue()
            .Because("a refused ask is not the person's answer; they may still bring one.");
    }

    [Test]
    public async Task No_port_is_a_sentence_naming_accept_agent_login()
    {
        // THE WRITTEN-FALSE LESSON, applied before it is learned again: silence
        // here is indistinguishable from a runner too old to have the arm.
        var store = new AStore();
        var dispatch = new AskDispatch(new Quiet(), store);

        var begun = await dispatch.AnswerAsync(Beginning(), CancellationToken.None);
        var finished = await dispatch.AnswerAsync(Finishing("x"), CancellationToken.None);

        await Assert.That(begun?.LoginBegun).IsNotNull();
        await Assert.That(begun!.LoginBegun!.Started).IsFalse();
        await Assert.That(begun.LoginBegun.Diagnosis).Contains("accept-agent-login");
        await Assert.That(finished?.LoginFinished).IsNotNull();
        await Assert.That(finished!.LoginFinished!.Written).IsFalse();
        await Assert.That(finished.LoginFinished.Diagnosis).Contains("accept-agent-login");
        await Assert.That(store.Kept).IsEmpty();
    }

    [Test]
    public async Task A_begin_while_flying_is_refused()
    {
        var rig = new Rig();
        rig.Runner.FlightNumber = "GG-7";

        var said = await rig.Dispatch.AnswerAsync(Beginning(), CancellationToken.None);

        var begun = said?.LoginBegun;
        await Assert.That(begun!.Started).IsFalse();
        await Assert.That(begun.Diagnosis).Contains("GG-7")
            .Because("a second agent beside a flight spends the allowance the flight is "
                   + "spending, and the person is told which flight to wait for.");
        await Assert.That(rig.Login.Children).IsEmpty();
    }

    [Test]
    public async Task An_abandoned_ceremony_is_over_at_patience_and_its_child_is_ended()
    {
        var rig = new Rig();
        _ = await rig.Dispatch.AnswerAsync(Beginning(), CancellationToken.None);

        await Assert.That(AgentLoginCeremony.IsOver(T0, T0 + AgentLoginCeremony.Patience - TimeSpan.FromSeconds(1)))
            .IsFalse();
        await Assert.That(AgentLoginCeremony.IsOver(T0, T0 + AgentLoginCeremony.Patience)).IsTrue();

        rig.Ceremony.Expire(T0 + AgentLoginCeremony.Patience - TimeSpan.FromSeconds(1));
        await Assert.That(rig.Ceremony.InProgress).IsTrue();

        rig.Ceremony.Expire(T0 + AgentLoginCeremony.Patience);
        await Assert.That(rig.Ceremony.InProgress).IsFalse();
        await Assert.That(rig.Login.Children.Single().Disposed).IsTrue()
            .Because("a child waiting for a code nobody will type is a process nobody asked for.");

        // AND A NEW ONE MAY BEGIN, which is what makes expiry recovery rather
        // than a dead end.
        rig.Clock.UtcNow = T0 + AgentLoginCeremony.Patience;
        var again = await rig.Dispatch.AnswerAsync(Beginning(), CancellationToken.None);
        await Assert.That(again!.LoginBegun!.Started).IsTrue();
        await Assert.That(rig.Login.Children).Count().IsEqualTo(2);
    }

    [Test]
    public async Task Disposing_the_ceremony_ends_its_child()
    {
        var rig = new Rig();
        _ = await rig.Dispatch.AnswerAsync(Beginning(), CancellationToken.None);

        rig.Ceremony.Dispose();

        await Assert.That(rig.Login.Children.Single().Disposed).IsTrue()
            .Because("a runner that stops takes its ceremony with it, the way it takes its "
                   + "conversations.");
    }

    [Test]
    public async Task The_kinds_with_no_payload_are_refused_on_the_async_path_too()
    {
        var rig = new Rig();

        foreach (var kind in RunnerAskKinds.All)
        {
            await Assert.That(await rig.Dispatch.AnswerAsync(new RunnerAsk { Kind = kind }, CancellationToken.None))
                .IsNull()
                .Because($"'{kind}' with no payload is not an ask of that kind.");
        }

        await Assert.That(rig.Login.Children).IsEmpty();
    }

    [Test]
    public async Task The_async_path_still_answers_the_synchronous_kinds()
    {
        var rig = new Rig();

        var said = await rig.Dispatch.AnswerAsync(
            new RunnerAsk { Kind = RunnerAskKinds.Status, Status = new StatusAsk() },
            CancellationToken.None);

        await Assert.That(said?.Status).IsNotNull()
            .Because("one path serves the whole vocabulary; the channel does not choose.");
    }

    [Test]
    public async Task A_credential_kept_over_configure_tells_the_loop_too()
    {
        var rig = new Rig();

        _ = await rig.Dispatch.AnswerAsync(
            new RunnerAsk
            {
                Kind = RunnerAskKinds.ConfigureCredential,
                ConfigureCredential = new ConfigureCredentialAsk
                {
                    Locator = "local:agent/claude",
                    Secret = "sk-ant-oat01-sent-from-a-laptop",
                },
            },
            CancellationToken.None);

        await Assert.That(rig.Kept).Contains("local:agent/claude")
            .Because("`gg credential send --agent claude` is the other way a token arrives, "
                   + "and the held loop should look again for that one too.");
    }

    [Test]
    public async Task The_ceremony_says_one_fixed_line_each_way_and_never_the_url_code_or_token()
    {
        var rig = new Rig();
        _ = await rig.Dispatch.AnswerAsync(Beginning(), CancellationToken.None);
        _ = await rig.Dispatch.AnswerAsync(Finishing("the-code"), CancellationToken.None);

        await Assert.That(rig.Said).Count().IsEqualTo(2)
            .Because("started, and ended: a journal line per event, not per byte.");
        foreach (var line in rig.Said)
        {
            await Assert.That(line).DoesNotContain(TheUrl);
            await Assert.That(line).DoesNotContain("the-code");
            await Assert.That(line).DoesNotContain(TheToken);
        }

        await Assert.That(rig.Said[1]).Contains("written: true");
    }
}
