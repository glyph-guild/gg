using Gg.Client;

namespace Gg.Cli.Tests;

/// <summary>
/// A console with nowhere to ask what it looks like from outside says so.
/// </summary>
/// <remarks>
/// <para>
/// <b>"This is the network rather than either machine" was wrong about the
/// machine.</b> A console with no STUN server configured offers only HOST
/// candidates — a laptop's address on its own LAN — and nothing on another
/// network can reach one. That is not something in between refusing traffic;
/// it is this end never having asked what its public address is.
/// </para>
/// <para>
/// <b>Measured, by varying the one thing every walk had set.</b> With
/// <c>GG_STUN_SERVERS</c> unset the handshake completes, the runner answers,
/// and ICE fails with exactly the sentence a person reported. Every earlier
/// walk exported it, which is how an environment hides a variable by never
/// varying it.
/// </para>
/// <para>
/// <b>The remedy is named rather than defaulted.</b> <c>StunConfiguration</c>
/// says why there is no built-in server: "the well-known ones belong to
/// companies this binary may not name", so which one a deployment uses is a
/// deployment's choice. A sentence that names the variable turns that decision
/// into something a person can act on instead of a silence they have to guess
/// at.
/// </para>
/// </remarks>
public class ARouteNeedsSomewhereToAskTests
{
    [Test]
    public async Task With_nowhere_to_ask_it_says_that_rather_than_blaming_the_network()
    {
        var said = ConsoleChannel.NoRoute(askedAnywhere: false);

        await Assert.That(said).Contains("GG_STUN_SERVERS")
            .Because("the remedy is a variable this console reads and nothing else here can "
                   + "guess for somebody. Said: " + said);

        await Assert.That(said.Contains("rather than either machine", StringComparison.Ordinal))
            .IsFalse()
            .Because("this end is exactly what went wrong: it offered only its own LAN "
                   + "address. Said: " + said);
    }

    [Test]
    public async Task With_somewhere_to_ask_it_is_the_network_between_them()
    {
        // THE ORIGINAL SENTENCE, AND STILL THE RIGHT ONE. Both ends offered
        // reflexive candidates and neither could reach the other, which is the
        // case TURN exists for - S34.Q-04, still open.
        var said = ConsoleChannel.NoRoute(askedAnywhere: true);

        await Assert.That(said).Contains("rather than either machine");
        await Assert.That(said.Contains("GG_STUN_SERVERS", StringComparison.Ordinal)).IsFalse()
            .Because("naming a variable that is already set sends somebody to check something "
                   + "that is fine. Said: " + said);
    }
}
