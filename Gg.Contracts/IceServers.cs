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

    /// <summary>The schemes an entry may use: two that ask, two that relay.</summary>
    private static readonly string[] Schemes = ["stun:", "stuns:", "turn:", "turns:"];

    /// <summary>Null when the entry is usable, or why not.</summary>
    public static string? Validate(IceServer server)
    {
        ArgumentNullException.ThrowIfNull(server);

        if (server.Urls is not { Count: > 0 } urls)
        {
            return "An ICE server names no url, so there is nowhere to ask.";
        }

        if (urls.FirstOrDefault(u => string.IsNullOrWhiteSpace(u)
                || u.Any(char.IsWhiteSpace)
                || !Schemes.Any(scheme => u.StartsWith(scheme, StringComparison.OrdinalIgnoreCase)))
            is { } broken)
        {
            return $"'{broken}' is not an ICE server url: one is written stun:, stuns:, turn: or "
                 + "turns: followed by host:port, with no spaces.";
        }

        // A RELAY ANSWERS NOBODY WITHOUT ONE, and the library would try it
        // anyway and spend the gathering window on a 401.
        return server.IsRelay
            && (string.IsNullOrEmpty(server.Username) || string.IsNullOrEmpty(server.Credential))
            ? $"'{urls[0]}' is a relay and this entry carries no credential for it."
            : null;
    }

    /// <summary>Whether any url of this entry is a relay.</summary>
    public bool IsRelay =>
        Urls.Any(u => u.StartsWith("turn:", StringComparison.OrdinalIgnoreCase)
                   || u.StartsWith("turns:", StringComparison.OrdinalIgnoreCase));

    /// <summary>A local <c>stun-servers</c> value as entries, one per url, with no credential.</summary>
    public static IReadOnlyList<IceServer> FromStun(IReadOnlyList<string> stunServers)
    {
        ArgumentNullException.ThrowIfNull(stunServers);

        return [.. stunServers.Select(url => new IceServer { Urls = [url] })];
    }

    /// <summary>
    /// The servers a channel uses: what its introduction carried, whole, or the
    /// local value when it carried none.
    /// </summary>
    /// <remarks>
    /// <b>Whole, never merged.</b> The library uses one server and prefers a
    /// relay, and the relay answers STUN itself, so a local entry beside a
    /// minted one is ignored at best and chosen over it at worst.
    /// </remarks>
    public static IReadOnlyList<IceServer> Choose(
        IReadOnlyList<IceServer> offered, IReadOnlyList<string> localStun)
    {
        ArgumentNullException.ThrowIfNull(offered);

        return offered.Count > 0 ? offered : FromStun(localStun);
    }
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
    /// <remarks>
    /// <b>The furthest either end went.</b> One relay end makes the channel
    /// relayed, because the packets cross the relay both ways. An end whose
    /// type this does not know, or a pair nobody nominated, is NOT reported
    /// direct: that is the comfortable wrong answer, and "through a NAT" is the
    /// one that sends nobody looking for a problem that is not there.
    /// </remarks>
    public static ChannelPath Of(string? local, string? remote)
    {
        if (IsRelay(local) || IsRelay(remote))
        {
            return ChannelPath.Relayed;
        }

        return IsHost(local) && IsHost(remote) ? ChannelPath.Direct : ChannelPath.ThroughNat;
    }

    /// <summary>The path in words a person reads in a status line.</summary>
    public static string Said(ChannelPath path) => path switch
    {
        ChannelPath.Direct => "direct",
        ChannelPath.ThroughNat => "direct, through NAT",
        ChannelPath.Relayed => "through the relay",
        _ => throw new ArgumentOutOfRangeException(nameof(path), path, null),
    };

    private static bool IsRelay(string? type) =>
        string.Equals(type, "relay", StringComparison.OrdinalIgnoreCase);

    private static bool IsHost(string? type) =>
        string.Equals(type, "host", StringComparison.OrdinalIgnoreCase);
}
