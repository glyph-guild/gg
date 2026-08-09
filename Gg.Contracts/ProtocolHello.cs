namespace Gg.Contracts;

/// <summary>
/// First frame either side of a connection sends. The full runner protocol is
/// not designed yet; this exists so the package and the version handshake have
/// something real to chew on.
/// </summary>
[PinnedId("b5e32b6d-1347-47e0-90e0-af7ab77e7452")]
public sealed record ProtocolHello
{
    /// <summary>Wire protocol revision, bumped on breaking changes.</summary>
    public required int ProtocolVersion { get; init; }

    /// <summary>Which role is speaking: "console", "runner", or "control-plane".</summary>
    public required string Component { get; init; }

    /// <summary>Semantic version of the speaking component's distribution.</summary>
    public required string ComponentVersion { get; init; }
}
