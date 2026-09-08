using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using Gg.Contracts;
using Gg.Runner;

namespace Gg.Runner.Tests;

/// <summary>
/// A runner answering a handshake still never listens.
/// </summary>
/// <remarks>
/// <para>
/// <b>The posture the whole transport choice rests on.</b> ADR-0013 chose WebRTC
/// partly because "ICE means neither peer runs a listening service — both dial
/// out and hole-punch — so the runner keeps its outbound-only posture". That is
/// a claim about a process, and it is the one claim in the ADR that a reader
/// cannot check by reading: a UDP socket bound for ICE looks, in code, exactly
/// like a UDP socket bound to serve.
/// </para>
/// <para>
/// <b>A note on what this file may say.</b> gg talks only to the control plane,
/// so no source file here names a cloud provider — including in a comment about
/// where something was measured. The step 0 measurement was between a laptop
/// behind a home NAT and a hosted machine behind a stateful firewall; which
/// hosting that was is the control plane's business and not this binary's.
/// </para>
/// <para>
/// <b>So it is asserted against the machine.</b> The distinction that matters is
/// TCP: a listening TCP socket is something anything on the network can connect
/// to, which is what "runs a listening service" means. ICE binds UDP and
/// initiates from it, and a UDP bind is not an accepting socket.
/// </para>
/// </remarks>
public class RunnerStillDialsOutTests
{
    private static ECDiffieHellman AKey() => ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);

    private sealed class NoLog : IReadOnlyLog
    {
        public TailRead Tail(int lines) => new([], false);
    }

    private static IReadOnlySet<int> ListeningTcpPorts() =>
        IPGlobalProperties.GetIPGlobalProperties()
            .GetActiveTcpListeners()
            .Select(e => e.Port)
            .ToHashSet();

    [Test]
    public async Task Answering_a_handshake_opens_no_listening_socket()
    {
        // MEASURED AS A DIFFERENCE, because this machine is already listening on
        // whatever it was listening on - a test that asserted "no TCP listeners"
        // would fail on any developer's laptop and prove nothing about us.
        var before = ListeningTcpPorts();

        var channel = new RunnerChannel(
            ["stun:stun.l.google.com:19302"], TimeSpan.FromSeconds(3));

        using var runner = AKey();
        using var ephemeral = AKey();

        // An offer this runner cannot answer is still an offer it GATHERS for:
        // the peer connection is built and ICE binds before the description is
        // rejected, which is exactly the window a listening socket would appear
        // in.
        var offer = RunnerSeal.SealOffer(
            Convert.ToBase64String(runner.ExportSubjectPublicKeyInfo()),
            ephemeral,
            Encoding.UTF8.GetBytes("not sdp"));

        await channel.AnswerAsync(
            new PendingIntroduction
            {
                IntroductionId = "intro-1",
                Offer = new RunnerSealedOffer { Sealed = offer },
            },
            runner,
            new AskDispatch(new WhatThisRunnerSays(
                new SilentObserver(), new NoLog(), () => DateTimeOffset.UnixEpoch)));

        var after = ListeningTcpPorts();

        await Assert.That(after.Except(before)).IsEmpty()
            .Because("a runner that accepted connections would be reachable by anything on "
                   + "its network, which is the posture ICE was chosen to preserve. New "
                   + "listeners: " + string.Join(", ", after.Except(before)));
    }

    [Test]
    public async Task The_scan_can_see_a_listener_that_is_really_there()
    {
        // THE POISON TWIN, and it is not decoration: the assertion above passes
        // on a scan that returns the same set every time, which is what a broken
        // enumeration looks like.
        //
        // A RAW SOCKET RATHER THAN THE CONVENIENCE LISTENER, because this
        // assembly forbids that type by name and the rule is right: a port
        // probed and released is a port somebody else can take in between.
        // Nothing here probes - it binds one and holds it for the length of an
        // assertion, which is the property that rule is protecting.
        var before = ListeningTcpPorts();

        using var planted = new Socket(
            AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

        planted.Bind(new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, 0));
        planted.Listen(1);

        var after = ListeningTcpPorts();

        await Assert.That(after.Except(before)).IsNotEmpty()
            .Because("if this cannot find a listener somebody just started, it cannot find "
                   + "one the runner started either.");
    }
}
