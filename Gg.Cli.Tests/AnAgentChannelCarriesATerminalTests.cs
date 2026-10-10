using System.Collections.Concurrent;
using System.Security.Cryptography;
using Gg.Client;
using Gg.Contracts;
using Gg.Runner;
using SIPSorcery.Net;

namespace Gg.Cli.Tests;

/// <summary>
/// Over a real data channel, an ad hoc session's terminal crosses both ways: output
/// reaches the console, input and resizes reach the child, and a machine that did
/// not opt in says so (slice seventy, S70.2-02; ADR-0039 Decisions 2 and 6).
/// </summary>
/// <remarks>
/// <b>The whole handshake, in one process</b> - <c>AConsoleReachesARunnerTests</c>'
/// rig: the console reaches with an `agent` channel instead of a `tail` one, and the
/// runner answers with an agent server beside its ask dispatch. The child is a fake;
/// its pty is the CLI's, proven where that is built.
/// </remarks>
[NotInParallel("a-real-webrtc-handshake")]
public class AnAgentChannelCarriesATerminalTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 10, 12, 0, 0, TimeSpan.Zero);

    private sealed class Child : IAgentSessionChild
    {
        private readonly BlockingCollection<byte[]> _out = [];
        private byte[]? _left;
        private readonly TaskCompletionSource<int> _exited = new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal ConcurrentQueue<byte> Typed { get; } = new();

        internal ConcurrentQueue<(int, int)> Resized { get; } = new();

        internal void Say(string text) => _out.Add(System.Text.Encoding.UTF8.GetBytes(text));

        public int Read(byte[] buffer)
        {
            try
            {
                // A REAL TERMINAL HANDS OVER WHAT FITS, and the rest on the next read.
                var next = _left ?? _out.Take();
                var taken = Math.Min(next.Length, buffer.Length);
                next.AsSpan(0, taken).CopyTo(buffer);
                _left = taken < next.Length ? next[taken..] : null;
                return taken;
            }
            catch (InvalidOperationException)
            {
                return 0;
            }
        }

        public void Write(ReadOnlySpan<byte> bytes)
        {
            foreach (var b in bytes)
            {
                Typed.Enqueue(b);
            }
        }

        public void Resize(int columns, int rows) => Resized.Enqueue((columns, rows));

        public void Kill()
        {
            _out.CompleteAdding();
            _exited.TrySetResult(137);
        }

        public Task<int> Exited => _exited.Task;

        public void Dispose()
        {
        }
    }

    private sealed class Host : IHostAgentSessions
    {
        internal Child? Last { get; private set; }

        public Task<IAgentSessionChild> StartAsync(AgentSessionStart start, CancellationToken cancellationToken)
        {
            Last = new Child();
            return Task.FromResult<IAgentSessionChild>(Last);
        }
    }

    private sealed record Wire(RTCDataChannel Channel, ConcurrentQueue<AgentFrame> Heard) : IDisposable
    {
        public void Send(AgentFrame frame) => Channel.send(AgentFrameCodec.Encode(frame));

        public async Task<T?> NextAsync<T>(Func<T, bool>? when = null) where T : AgentFrame
        {
            var deadline = DateTime.UtcNow.AddSeconds(10);

            while (DateTime.UtcNow < deadline)
            {
                if (Heard.OfType<T>().FirstOrDefault(f => when?.Invoke(f) ?? true) is { } found)
                {
                    return found;
                }

                await Task.Yield();
            }

            return null;
        }

        public string Output => System.Text.Encoding.UTF8.GetString(
            [.. Heard.OfType<AgentOutput>().SelectMany(o => o.Bytes)]);

        public void Dispose() => Channel.close();
    }

    private static async Task<Wire> ReachAsync(AgentChannelServer? agents)
    {
        using var runnerKey = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        using var ephemeral = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);

        var introduction = new RunnerIntroduction
        {
            IntroductionId = "intro-70",
            RunnerId = "01a06385-322f-7371-93a2-ce35db5c4fbe",
            RunnerPublicKey = Convert.ToBase64String(runnerKey.ExportSubjectPublicKeyInfo()),
            Capability = "a-capability",
            ExpiresAt = T0.AddMinutes(1),
        };

        HandshakeResult answered = new(null, HandshakeFailure.None, "not run", []);
        var runner = new RunnerChannel([], TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(20));
        var says = new WhatThisRunnerSays(new SilentObserver(), _ => new NoLog(), () => T0);

        var reached = await new ConsoleChannel([], TimeSpan.FromSeconds(20)).ReachAsync(
            introduction,
            ephemeral,
            new PinnedRunnerKeys(Path.Combine(
                Directory.CreateTempSubdirectory("gg-agent-reach-").FullName, "pins.json")),
            T0,
            async (offer, ct) => answered = await runner.AnswerAsync(
                new PendingIntroduction { IntroductionId = "intro-70", Offer = offer },
                runnerKey, new AskDispatch(says), ct, agents),
            _ => Task.FromResult(answered.Answer is { } a ? new Collected(a, AnswerState.Arrived) : Collected.NotYet),
            channelLabel: AgentChannel.Label,
            cancellationToken: CancellationToken.None);

        await Assert.That(reached.Failure).IsEqualTo(ReachFailure.None).Because(reached.Said);

        var channel = reached.Conversation!.Channel;
        var heard = new ConcurrentQueue<AgentFrame>();
        channel.onmessage += (_, _, data) =>
        {
            if (AgentFrameCodec.Decode(data) is { } frame)
            {
                heard.Enqueue(frame);
            }
        };

        return new Wire(channel, heard);
    }

    private sealed class NoLog : IReadOnlyLog
    {
        public TailRead Tail(int lines) => new([], false);
    }

    [Test]
    public async Task The_terminal_crosses_both_ways()
    {
        var host = new Host();
        var sessions = new AgentSessions(
            host, [Directory.CreateTempSubdirectory("gg-agent-root-").FullName],
            flying: () => false, now: () => T0);

        using var wire = await ReachAsync(new AgentChannelServer(sessions));

        wire.Send(new StartAgentSession { Columns = 100, Rows = 30, SessionId = "a1b2" });

        var started = await wire.NextAsync<AgentSessionStarted>();
        await Assert.That(started?.SessionId).IsEqualTo("a1b2");

        host.Last!.Say("Welcome to Claude Code");
        await Assert.That(await Eventually(() => wire.Output.Contains("Welcome to Claude Code"))).IsTrue()
            .Because("what the agent's terminal writes reaches the console.");

        wire.Send(new AgentInput { Bytes = "hi\r"u8.ToArray() });
        await Assert.That(await Eventually(() => host.Last.Typed.Count == 3)).IsTrue()
            .Because("what the console types reaches the agent.");

        wire.Send(new AgentResize { Columns = 132, Rows = 50 });
        await Assert.That(await Eventually(() => host.Last.Resized.Contains((132, 50)))).IsTrue();

        wire.Send(new KillAgentSession());
        var exited = await wire.NextAsync<AgentSessionExited>();
        await Assert.That(exited?.Code).IsEqualTo(137);
    }

    [Test]
    public async Task A_machine_that_did_not_opt_in_says_so()
    {
        using var wire = await ReachAsync(agents: null);

        wire.Send(new StartAgentSession { Columns = 80, Rows = 24 });

        var refused = await wire.NextAsync<AgentSessionRefused>();
        await Assert.That(refused?.Because).Contains("accept-agent-sessions")
            .Because("a person told nothing concludes the network failed; the machine's own "
                   + "configuration is the thing to name.");
    }

    [Test]
    public async Task Asking_lists_what_the_machine_holds()
    {
        var sessions = new AgentSessions(
            new Host(), [Directory.CreateTempSubdirectory("gg-agent-root-").FullName],
            flying: () => false, now: () => T0);
        _ = await sessions.StartAsync(new StartAgentSession { Columns = 80, Rows = 24, SessionId = "held" }, CancellationToken.None);

        using var wire = await ReachAsync(new AgentChannelServer(sessions));
        wire.Send(new ListAgentSessions());

        var list = await wire.NextAsync<AgentSessionList>();
        await Assert.That(list!.Sessions.Select(s => s.SessionId)).Contains("held");
    }

    private static async Task<bool> Eventually(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);

        while (DateTime.UtcNow < deadline)
        {
            if (condition())
            {
                return true;
            }

            await Task.Yield();
        }

        return condition();
    }
}
