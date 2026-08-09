namespace Gg.Contracts;

/// <summary>
/// Registers a runner with the control plane.
/// </summary>
/// <remarks>
/// Registration only. Heartbeat, lease acquisition and the ready queue are a
/// later step and are deliberately absent from this contract.
/// </remarks>
[PinnedId("34fd5791-5fce-4c7b-ad8e-31e4d35461f6")]
public sealed record RunnerRegistrationRequest
{
    /// <summary>Human-readable label for this runner.</summary>
    public required string Label { get; init; }

    /// <summary>Protocol revision this runner speaks.</summary>
    public required int ProtocolVersion { get; init; }
}

/// <summary>
/// The registered runner and the credential it will authenticate with.
/// </summary>
/// <remarks>
/// <para>
/// The token ATTRIBUTES to the principal that registered the runner, so every
/// action remains traceable to a person. It is AUTHORIZED for the runner
/// protocol surface and nothing else: it cannot register another runner, and
/// it cannot reach a developer or tenant surface.
/// </para>
/// <para>
/// It expires on its own schedule, independent of the session that minted it,
/// so closing a console never drops a running runner.
/// </para>
/// </remarks>
[PinnedId("2c5b6a1e-7d84-4f39-b0c6-5e9a8d3f1742")]
public sealed record RunnerRegistered
{
    public required string RunnerId { get; init; }

    /// <summary>The runner's bearer credential. Shown once.</summary>
    public required string RunnerToken { get; init; }

    /// <summary>When the runner credential expires, independent of any session.</summary>
    public required DateTimeOffset ExpiresAt { get; init; }
}
