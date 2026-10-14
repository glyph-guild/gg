using Gg.Contracts;

namespace Gg.Contracts.Tests;

/// <summary>
/// A channel says which way its packets went (S78.2, ADR-0042 Decision 5).
/// </summary>
/// <remarks>
/// <b>Because a relayed channel is slower and costs the platform bandwidth.</b>
/// Somebody wondering why a drive feels sluggish should be able to read the
/// answer rather than guess it. And the relay is only a fallback: a fleet whose
/// channels are all relayed has a STUN problem the relay is hiding.
/// </remarks>
public class AChannelSaysWhichPathItTookTests
{
    [Test]
    [Arguments("host", "host", ChannelPath.Direct)]
    [Arguments("srflx", "host", ChannelPath.ThroughNat)]
    [Arguments("host", "prflx", ChannelPath.ThroughNat)]
    [Arguments("srflx", "srflx", ChannelPath.ThroughNat)]
    [Arguments("relay", "srflx", ChannelPath.Relayed)]
    [Arguments("host", "relay", ChannelPath.Relayed)]
    [Arguments("relay", "relay", ChannelPath.Relayed)]
    public async Task The_path_is_the_furthest_either_end_had_to_go(
        string local, string remote, ChannelPath expected)
    {
        // EITHER END'S RELAY MAKES IT RELAYED. Only one side may need the relay
        // - the side behind the symmetric router - and the packets still cross
        // it both ways.
        await Assert.That(ChannelPaths.Of(local, remote)).IsEqualTo(expected);
    }

    [Test]
    public async Task An_unknown_type_is_not_reported_as_direct()
    {
        // A NEWER LIBRARY'S CANDIDATE TYPE, or none at all because nothing was
        // nominated. "Direct" would be the comfortable wrong answer.
        await Assert.That(ChannelPaths.Of(null, "host")).IsEqualTo(ChannelPath.ThroughNat);
        await Assert.That(ChannelPaths.Of("something-new", "host"))
            .IsEqualTo(ChannelPath.ThroughNat);
    }

    [Test]
    public async Task Each_path_has_words_a_person_reads()
    {
        var said = Enum.GetValues<ChannelPath>().Select(ChannelPaths.Said).ToList();

        await Assert.That(said.All(s => !string.IsNullOrWhiteSpace(s))).IsTrue();
        await Assert.That(said.Distinct().Count()).IsEqualTo(said.Count)
            .Because("three paths that read the same are one path to the person reading.");
        await Assert.That(ChannelPaths.Said(ChannelPath.Relayed)).Contains("relay");
    }
}
