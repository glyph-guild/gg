namespace Gg.Contracts;

/// <summary>
/// Ask for an enrollment token: a person deciding, ahead of time, that some
/// machines may join the fleet as one profile (slice forty-three, rule 17).
/// </summary>
/// <remarks>
/// <para>
/// <b>The decision moves from typing at the machine to minting the token</b>
/// (ADR-0025 section 1). Registration stays a person's act: the minter is the
/// recorded registrar of every machine it enrolls, and <see cref="Uses"/> is a
/// person deciding how many.
/// </para>
/// <para>
/// <b>Both bounds are required</b> (rule 18). A token with no expiry or no use
/// count would be a standing grant that happened to be written down.
/// </para>
/// </remarks>
[PinnedId("7c2e9b41-5d8a-4f63-9e17-a4b6c3d2f809")]
public sealed record EnrollmentTokenRequest
{
    /// <summary>The fleet profile every machine enrolled with it runs under.</summary>
    public required string Profile { get; init; }

    /// <summary>How many machines it may enroll. At least one.</summary>
    public required int Uses { get; init; }

    /// <summary>How long it lasts, in seconds - at most <see cref="EnrollmentBounds.MaxLifetime"/>.</summary>
    public required int ExpiresInSeconds { get; init; }

    /// <summary>
    /// Whose each enrolled machine starts as: <see cref="RunnerOwnerships.Tenant"/>
    /// (an admin's to mint), <see cref="RunnerOwnerships.Open"/>, or
    /// <see cref="RunnerOwnerships.Claimed"/> by the minter.
    /// </summary>
    public string Ownership { get; init; } = RunnerOwnerships.Open;

    /// <summary>Whether a claimed machine starts reserved to the minter's flights.</summary>
    public bool Reserve { get; init; }
}

/// <summary>The bounds every enrollment token keeps, stated once for both repositories.</summary>
public static class EnrollmentBounds
{
    /// <summary>
    /// Seven days: an invitation's lifetime, for a thing weaker than an
    /// invitation (rule 18). Long enough to image a batch of machines, short
    /// enough that a token in a forgotten cloud-init file is dead.
    /// </summary>
    public static readonly TimeSpan MaxLifetime = TimeSpan.FromDays(7);

    /// <summary>Why this request cannot be minted, or null when it can.</summary>
    /// <remarks>
    /// <b>Who may mint a tenant token is not here</b> - that is the caller's
    /// session, which only the control plane can read. This is the shape.
    /// </remarks>
    public static string? Validate(EnrollmentTokenRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Profile))
        {
            return "An enrollment token names the fleet profile its machines run under.";
        }

        if (request.Uses < 1)
        {
            return "An enrollment token enrolls at least one machine - say how many with --uses.";
        }

        if (request.ExpiresInSeconds < 1 || request.ExpiresInSeconds > MaxLifetime.TotalSeconds)
        {
            return "An enrollment token lasts at most seven days, and says how long with "
                 + "--expires - a standing grant that happened to be written down is what the "
                 + "bound exists to prevent.";
        }

        if (request.Ownership is not (RunnerOwnerships.Tenant or RunnerOwnerships.Open or RunnerOwnerships.Claimed))
        {
            return $"'{request.Ownership}' is not whose a machine can start as. It starts as the "
                 + "tenant's, open, or claimed by whoever mints the token.";
        }

        return request.Reserve && request.Ownership != RunnerOwnerships.Claimed
            ? "Only a claimed machine can start reserved - a reservation keeps a machine to its "
            + "owner's flights, and a tenant or open machine has no owner."
            : null;
    }
}

/// <summary>A minted enrollment token. The secret is here once and never again.</summary>
[PinnedId("3f8d1a6e-9b24-4c57-8e0f-b1a7d5c9e362")]
public sealed record EnrollmentTokenMinted
{
    /// <summary>What names the token afterwards - to list it, and to revoke it.</summary>
    public required string TokenId { get; init; }

    /// <summary>The bearer value. Shown once; the control plane keeps only its hash.</summary>
    public required string Token { get; init; }

    public required string Profile { get; init; }

    public required int Uses { get; init; }

    public required DateTimeOffset ExpiresAt { get; init; }

    public required string Ownership { get; init; }

    public bool Reserved { get; init; }
}

/// <summary>An enrollment token as a list shows it: everything but the secret.</summary>
[PinnedId("b5c9e2d7-1f3a-4b86-a0d4-6e8f2c7b9a15")]
public sealed record EnrollmentTokenSummary
{
    public required string TokenId { get; init; }

    public required string Profile { get; init; }

    /// <summary>How many machines it may still enroll.</summary>
    public required int UsesLeft { get; init; }

    public required DateTimeOffset ExpiresAt { get; init; }

    public required string Ownership { get; init; }

    public bool Reserved { get; init; }

    /// <summary>Who minted it, as a display.</summary>
    public string MintedBy { get; init; } = "";

    public DateTimeOffset MintedAt { get; init; }

    /// <summary>When it was revoked, or null.</summary>
    public DateTimeOffset? RevokedAt { get; init; }
}

/// <summary>This tenant's enrollment tokens, secrets never included.</summary>
[PinnedId("e1a4c8f3-6d29-4b7e-9c52-8f0b3d6a1e97")]
public sealed record EnrollmentTokenList
{
    public required IReadOnlyList<EnrollmentTokenSummary> Tokens
    {
        get => field ?? [];
        init;
    }
}

/// <summary>Ask to revoke an enrollment token. Empty: the path names it.</summary>
[PinnedId("9d6b3e8a-2c71-4f05-b8e4-5a1c7f9d2b36")]
public sealed record EnrollmentTokenRevocation;

/// <summary>
/// A machine presenting an enrollment token for a runner credential of its own
/// (rule 19) - the enrolled way in, where registration is the signed-in one.
/// </summary>
/// <remarks>
/// <b>Authorized by the token alone</b>, as a pool member's redemption is by its
/// nonce: the machine has no session, and needing one is the thing enrollment
/// ends. What it becomes - tenant, registrar, labels, ownership - is settled by
/// the token, never by anything here.
/// </remarks>
[PinnedId("4a7f2c9e-8b13-4d60-95e1-c3d8b6a2f174")]
public sealed record RunnerEnrollmentRequest
{
    /// <summary>The enrollment token.</summary>
    public required string Token { get; init; }

    /// <summary>What the runner is called, as registration's label is.</summary>
    public required string Label { get; init; }

    public required int ProtocolVersion { get; init; }

    /// <summary>What a console will seal to, on the runner's word, as registration takes it.</summary>
    public string? PublicKey { get; init; }

    /// <summary>Where it runs, on the runner's word. A display grouping.</summary>
    public string? Machine { get; init; }
}

/// <summary>A machine enrolled: its own credential, and what the token made it.</summary>
[PinnedId("c8e3b1f6-4a95-4d27-b6c0-2f7e9a5d8b41")]
public sealed record RunnerEnrolled
{
    public required string RunnerId { get; init; }

    /// <summary>The runner's bearer credential. Shown once.</summary>
    public required string RunnerToken { get; init; }

    public required DateTimeOffset ExpiresAt { get; init; }

    /// <summary>The profile it enrolled under - which the machine writes down as its own.</summary>
    public required string Profile { get; init; }

    /// <summary>Whose it starts as.</summary>
    public required string Ownership { get; init; }
}
