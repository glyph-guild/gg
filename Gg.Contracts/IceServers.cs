namespace Gg.Contracts;

/// <summary>
/// Somewhere a peer connection asks for its route: a STUN server, or a TURN
/// relay and the credential to use it (ADR-0042).
/// </summary>
/// <remarks>
/// <para>
/// <b>The W3C dictionary's shape, because that is what every library and every
/// hosted relay already speaks.</b> A list of urls sharing one credential. A
/// STUN entry carries none.
/// </para>
/// <para>
/// <b>A credential here grants relay bandwidth and nothing else.</b> The relay
/// carries DTLS records it cannot open, because the fingerprints that key them
/// travel sealed. So this rides an introduction in the clear - it is minted for
/// the side that reads it and expires - and never rides an offer, which is
/// applied unattended on the grounds that it grants nothing.
/// </para>
/// </remarks>
[PinnedId("051013dc-efda-40a7-b413-a7a181b1e617")]
public sealed record IceServer
{
    /// <summary><c>stun:</c>, <c>stuns:</c>, <c>turn:</c> or <c>turns:</c> urls, one server.</summary>
    public required IReadOnlyList<string> Urls { get; init; }

    /// <summary>The TURN username, or null for a STUN entry.</summary>
    public string? Username { get; init; }

    /// <summary>The TURN credential, or null for a STUN entry.</summary>
    public string? Credential { get; init; }

    /// <summary>Null when the entry is usable, or why not.</summary>
    public static string? Validate(IceServer server) => null;

    /// <summary>Whether any url of this entry is a relay.</summary>
    public bool IsRelay => false;

    /// <summary>A local <c>stun-servers</c> value as entries, one per url, with no credential.</summary>
    public static IReadOnlyList<IceServer> FromStun(IReadOnlyList<string> stunServers) => [];

    /// <summary>
    /// The servers a channel uses: what its introduction carried, whole, or the
    /// local value when it carried none.
    /// </summary>
    public static IReadOnlyList<IceServer> Choose(
        IReadOnlyList<IceServer> offered, IReadOnlyList<string> localStun) => [];
}

/// <summary>Which way a channel's packets went, read off its nominated pair.</summary>
public enum ChannelPath
{
    /// <summary>Host to host: the two machines can see each other's own addresses.</summary>
    Direct,

    /// <summary>Through at least one NAT, by an address a STUN server reported.</summary>
    ThroughNat,

    /// <summary>Through the relay, because no direct path was found.</summary>
    Relayed,
}

/// <summary>Reads a channel's path off its candidate types.</summary>
public static class ChannelPaths
{
    /// <summary>
    /// The path a nominated pair took, from each end's candidate type
    /// (<c>host</c>, <c>srflx</c>, <c>prflx</c> or <c>relay</c>).
    /// </summary>
    public static ChannelPath Of(string? local, string? remote) => ChannelPath.Direct;

    /// <summary>The path in words a person reads in a status line.</summary>
    public static string Said(ChannelPath path) => "";
}
