using System.Text.Json;
using Gg.Client;
using Gg.Contracts;
using Gg.Runner;

namespace Gg.Cli.Tests;

/// <summary>
/// An introduction tells each side where to ask for its route (S78.2-01,
/// ADR-0042 Decision 3).
/// </summary>
/// <remarks>
/// <para>
/// <b>Both halves, because each side is minted its own.</b> The console reads
/// the introduction it asked for; the runner reads the one its heartbeat
/// delivers. A credential minted for one and read by the other would name the
/// wrong subject in the relay's log.
/// </para>
/// <para>
/// <b>In <c>Gg.Cli.Tests</c></b> because it is the one assembly that sees both
/// the console's wire context and the runner's.
/// </para>
/// </remarks>
public class AnIntroductionCarriesItsIceServersTests
{
    private static readonly IceServer Relay = new()
    {
        Urls = ["turn:relay.invalid:3478?transport=udp", "turn:relay.invalid:3478?transport=tcp"],
        Username = "1760500000:principal-7",
        Credential = "aGVsbG8gcmVsYXk=",
    };

    private static RunnerIntroduction Introduced(params IceServer[] servers) => new()
    {
        IntroductionId = "intro-1",
        RunnerId = "runner-1",
        RunnerPublicKey = "a-key",
        Capability = "a-capability",
        ExpiresAt = new DateTimeOffset(2026, 10, 14, 2, 0, 0, TimeSpan.Zero),
        IceServers = servers,
    };

    [Test]
    public async Task The_consoles_servers_cross_the_wire_with_their_credential()
    {
        var json = JsonSerializer.Serialize(
            Introduced(Relay), ProtocolJsonContext.Default.RunnerIntroduction);
        var read = JsonSerializer.Deserialize(json, ProtocolJsonContext.Default.RunnerIntroduction)!;

        await Assert.That(json).Contains("\"iceServers\"");
        await Assert.That(read.IceServers.Single().Urls).IsEquivalentTo(Relay.Urls);
        await Assert.That(read.IceServers.Single().Username).IsEqualTo(Relay.Username);
        await Assert.That(read.IceServers.Single().Credential).IsEqualTo(Relay.Credential);
    }

    [Test]
    public async Task The_runners_servers_cross_on_its_heartbeat()
    {
        var pending = new PendingIntroduction
        {
            IntroductionId = "intro-1",
            Offer = new RunnerSealedOffer { Sealed = [1, 2, 3] },
            IceServers = [Relay],
        };

        var json = JsonSerializer.Serialize(pending, RunnerJsonContext.Default.PendingIntroduction);
        var read = JsonSerializer.Deserialize(json, RunnerJsonContext.Default.PendingIntroduction)!;

        await Assert.That(read.IceServers.Single().Credential).IsEqualTo(Relay.Credential);
    }

    [Test]
    public async Task A_control_plane_that_predates_them_sends_none_and_nothing_breaks()
    {
        // THE KEY IS ABSENT, not empty: every deployed control plane today.
        const string Old = """
            {"introductionId":"intro-1","runnerId":"runner-1","runnerPublicKey":"a-key",
             "capability":"a-capability","expiresAt":"2026-10-14T02:00:00+00:00"}
            """;

        var read = JsonSerializer.Deserialize(Old, ProtocolJsonContext.Default.RunnerIntroduction)!;

        await Assert.That(read.IceServers).IsEmpty();
    }

    [Test]
    public async Task What_the_introduction_carried_is_used_whole()
    {
        // WHOLE, NOT MERGED WITH THE LOCAL LIST. The library uses one server
        // and prefers a relay; a local STUN entry beside the minted one would
        // be ignored at best.
        var chosen = IceServer.Choose([Relay], ["stun:stun.invalid:19302"]);

        await Assert.That(chosen).IsEquivalentTo(new[] { Relay });
    }

    [Test]
    public async Task Without_one_the_local_stun_servers_are_used_as_today()
    {
        var chosen = IceServer.Choose([], ["stun:a.invalid:19302", "stun:b.invalid:3478"]);

        await Assert.That(chosen.SelectMany(s => s.Urls))
            .IsEquivalentTo(new[] { "stun:a.invalid:19302", "stun:b.invalid:3478" });
        await Assert.That(chosen.All(s => s.Username is null && s.Credential is null)).IsTrue();
        await Assert.That(IceServer.Choose([], [])).IsEmpty();
    }

    [Test]
    public async Task An_entry_says_whether_it_is_a_relay()
    {
        await Assert.That(Relay.IsRelay()).IsTrue();
        await Assert.That(new IceServer { Urls = ["stun:a.invalid:19302"] }.IsRelay()).IsFalse();
        await Assert.That(new IceServer { Urls = ["turns:a.invalid:443"], Username = "u", Credential = "c" }.IsRelay())
            .IsTrue();
    }

    [Test]
    public async Task A_relay_without_a_credential_or_an_unknown_scheme_is_refused()
    {
        await Assert.That(IceServer.Validate(Relay)).IsNull();
        await Assert.That(IceServer.Validate(new IceServer { Urls = ["stun:a.invalid:19302"] }))
            .IsNull();

        foreach (var (why, server) in ((string, IceServer)[])
            [("a relay with no credential", new IceServer { Urls = ["turn:a.invalid:3478"] }),
             ("no urls", new IceServer { Urls = [] }),
             ("an unknown scheme", new IceServer { Urls = ["http://a.invalid"] }),
             ("a url with a space", new IceServer { Urls = ["turn:a.invalid :3478"], Username = "u", Credential = "c" })])
        {
            await Assert.That(IceServer.Validate(server)).IsNotNull().Because(why);
        }
    }

    [Test]
    public async Task No_route_with_a_relay_offered_is_a_firewall_a_person_can_name()
    {
        // THE SENTENCE CHANGES MEANING. With only STUN, no route is hole
        // punching that failed - the case the relay exists for. With a relay
        // offered and still no route, both ends could not reach even the relay:
        // UDP and TCP to it are blocked, which is a rule somebody wrote.
        var said = ConsoleChannel.NoRoute([Relay]);

        await Assert.That(said).Contains("relay");
        await Assert.That(said).Contains("3478");
        await Assert.That(said.Contains("hole punching", StringComparison.Ordinal)).IsFalse()
            .Because("hole punching is what the relay replaced. Said: " + said);

        await Assert.That(ConsoleChannel.NoRoute([new IceServer { Urls = ["stun:a.invalid:19302"] }]))
            .IsEqualTo(ConsoleChannel.NoRoute(askedAnywhere: true));
        await Assert.That(ConsoleChannel.NoRoute([]))
            .IsEqualTo(ConsoleChannel.NoRoute(askedAnywhere: false));
    }
}
