using System.Reflection;
using Gg.Contracts.Description;

namespace Gg.Contracts.Tests;

/// <summary>
/// The `agent` channel's frames: what an ad hoc Claude session on another
/// machine is carried in (slice seventy, ADR-0039).
/// </summary>
/// <remarks>
/// <para>
/// <b>Binary, and not a RunnerAsk.</b> The `tail` channel is one question and one
/// bounded answer, and <c>RunnerAskIsNotStreamingTests</c> keeps it that way. A
/// terminal is a stream of bytes both ways, so it gets a channel of its own with
/// frames of its own: a kind byte and a payload, nothing parsed that does not
/// have to be.
/// </para>
/// <para>
/// <b>Chunked under the message cap.</b> SIPSorcery fixes a data-channel message
/// at 256 KiB and throws on a larger send; a burst of terminal output is not
/// bounded by anything, so it is cut into frames the far side simply
/// concatenates - a terminal stream has no boundaries to keep.
/// </para>
/// </remarks>
public class AgentFramesRoundTripTests
{
    private static readonly DateTimeOffset Noon = new(2026, 10, 10, 12, 0, 0, TimeSpan.Zero);

    private static IEnumerable<AgentFrame> EveryFrame() =>
    [
        new StartAgentSession { Columns = 120, Rows = 40, Directory = "/work/jdnext", SessionId = "a1b2" },
        new StartAgentSession { Columns = 80, Rows = 24 },
        new AttachAgentSession { SessionId = "a1b2", Columns = 100, Rows = 30 },
        new AgentInput { Bytes = "hello\r"u8.ToArray() },
        new AgentResize { Columns = 132, Rows = 50 },
        new KillAgentSession(),
        new ListAgentSessions(),
        new AgentSessionStarted { SessionId = "a1b2" },
        new AgentOutput { Bytes = "\u001b[2J\u001b[Hwelcome"u8.ToArray() },
        new AgentSessionExited { Code = 130 },
        new AgentSessionList
        {
            Sessions =
            [
                new AgentSessionStanding
                {
                    SessionId = "a1b2",
                    Directory = "/work/jdnext",
                    StartedAt = Noon,
                    AttachedBy = "Kevin",
                    Alive = true,
                },
                new AgentSessionStanding { SessionId = "c3d4", StartedAt = Noon, Alive = false },
            ],
        },
        new AgentSessionRefused { Because = "this machine is flying GG-1058, and takes no session while it does" },
        new AgentSessionReadOnly(),
    ];

    [Test]
    public async Task Every_frame_round_trips()
    {
        foreach (var frame in EveryFrame())
        {
            var decoded = AgentFrameCodec.Decode(AgentFrameCodec.Encode(frame));

            await Assert.That(AgentFrameCodec.Encode(decoded!))
                .IsEquivalentTo(AgentFrameCodec.Encode(frame))
                .Because($"{frame.GetType().Name} must come back as it went.");
            await Assert.That(decoded!.GetType()).IsEqualTo(frame.GetType());
        }
    }

    [Test]
    public async Task A_megabyte_of_output_is_cut_under_the_cap_and_reassembled_exactly()
    {
        var burst = new byte[1024 * 1024];
        new Random(70).NextBytes(burst);

        var frames = AgentFrameCodec.Output(burst).Select(AgentFrameCodec.Encode).ToList();

        await Assert.That(frames.All(f => f.Length <= AgentChannel.MaxMessage)).IsTrue()
            .Because("SIPSorcery throws on a send over its cap, synchronously.");

        var reassembled = frames
            .Select(f => (AgentOutput)AgentFrameCodec.Decode(f)!)
            .SelectMany(o => o.Bytes)
            .ToArray();

        await Assert.That(reassembled).IsEquivalentTo(burst)
            .Because("a terminal stream has no boundaries to keep, so its chunks just concatenate.");
    }

    [Test]
    public async Task Garbage_is_refused_rather_than_thrown()
    {
        // A FRAME FROM A NEWER PEER, OR A TRUNCATED ONE, is nothing - never an
        // exception on the thread pumping somebody's terminal.
        await Assert.That(AgentFrameCodec.Decode([])).IsNull();
        await Assert.That(AgentFrameCodec.Decode([0xEE, 1, 2, 3])).IsNull();

        var started = AgentFrameCodec.Encode(new AgentSessionStarted { SessionId = "a1b2" });

        await Assert.That(AgentFrameCodec.Decode(started.AsSpan(0, started.Length - 1))).IsNull();
    }

    [Test]
    public async Task The_purpose_and_the_channel_are_declared()
    {
        await Assert.That(RunnerCapabilityPurposes.All).Contains(RunnerCapabilityPurposes.DriveAnAgent);
        await Assert.That(RunnerCapabilityPurposes.DriveAnAgent).IsEqualTo("drive-an-agent");
        await Assert.That(AgentChannel.Label).IsEqualTo("agent")
            .Because("the console creates the channel and the runner serves it by its label.");
    }

    [Test]
    public async Task Every_frame_is_pinned_and_in_the_vocabulary()
    {
        foreach (var type in EveryFrame().Select(f => f.GetType()).Distinct()
                     .Append(typeof(AgentSessionStanding)))
        {
            await Assert.That(type.GetCustomAttribute<PinnedIdAttribute>()).IsNotNull()
                .Because($"{type.Name} crosses the wire.");
            await Assert.That(Vocabulary.Types).Contains(type);
        }
    }

    [Test]
    public async Task No_endpoint_carries_a_frame()
    {
        // THE CHANNEL'S, NEVER THE CONTROL PLANE'S. A frame carries a person's
        // terminal, which is the customer's code on the screen; the control
        // plane introduces and holds nothing (ADR-0013's rule, kept by 0039).
        var frames = EveryFrame().Select(f => f.GetType()).Distinct().ToHashSet();

        foreach (var endpoint in ProtocolSurface.Endpoints)
        {
            await Assert.That(endpoint.Request is null || !frames.Contains(endpoint.Request)).IsTrue();
            await Assert.That(endpoint.Response is null || !frames.Contains(endpoint.Response)).IsTrue();
        }
    }
}
