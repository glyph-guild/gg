namespace Gg.Contracts;

/// <summary>
/// What a capability may be for.
/// </summary>
/// <remarks>
/// <para>
/// <b>One value, and the shortness is the narrowing.</b> ADR-0013 says the
/// control plane mints "a short-lived capability for one pair, one purpose".
/// A purpose that was a free string would make "one purpose" a description of
/// today rather than a property, and the second one would arrive as a typo
/// nobody noticed.
/// </para>
/// <para>
/// <b>Closed like <see cref="RunnerAskKinds"/>, and for the same reason.</b>
/// Widening what a capability authorises is exactly the change that should cost
/// a fingerprint and a version, because it is the change that turns an
/// introduction into a standing grant.
/// </para>
/// </remarks>
[VocabularyOf(VocabularyFingerprints.Contract)]
public static class RunnerCapabilityPurposes
{
    /// <summary>Ask a runner about itself, and nothing else.</summary>
    public const string TailYourOwnLog = "tail-your-own-log";

    public static IReadOnlyList<string> All { get; } = [TailYourOwnLog];
}

/// <summary>
/// Asks the control plane to introduce this console to one runner.
/// </summary>
/// <remarks>
/// <b>The ephemeral key is here so the capability can bind it.</b> Without it
/// the control plane would mint a capability naming a principal, and anything
/// holding that capability could answer as them. Binding a key the console
/// generated for this introduction means the runner can check that whoever it is
/// answering is who it was introduced to - without the control plane being able
/// to read either half of what they then say.
/// </remarks>
[PinnedId("b7e14d05-3c62-4a89-91fe-08c5d7a3b641")]
public sealed record RunnerIntroductionRequest
{
    /// <summary>
    /// The console's public key for this introduction and no other.
    /// </summary>
    /// <remarks>
    /// <b>Ephemeral on purpose.</b> A long-lived console key would make every
    /// introduction linkable to every other, and would be a second credential to
    /// look after on a machine that already has a session token.
    /// </remarks>
    public required string EphemeralPublicKey { get; init; }
}

/// <summary>
/// What the control plane will say about a runner, and nothing more.
/// </summary>
/// <remarks>
/// <para>
/// <b>Where and whether, never what.</b> This is the whole of the control
/// plane's role on this path, and it is the same role it already has for
/// evidence: it says which runner, that the caller may reach it, and for how
/// long. Nothing that passes between the two ends afterwards is readable here.
/// </para>
/// <para>
/// <b>The runner's key comes from the control plane, and that is trust on FIRST
/// use only.</b> Every introduction after the first is checked against what the
/// console pinned. Stated plainly rather than left to read as stronger than it
/// is: a control plane that is hostile at the moment of the very first
/// introduction can substitute this key. It can also schedule a flight onto that
/// runner and run code there, so this is not the weakest link.
/// </para>
/// </remarks>
[PinnedId("2a90f6c3-58d1-4e07-b3a5-9c14e8b7205f")]
public sealed record RunnerIntroduction
{
    /// <summary>The runner this is about.</summary>
    public required string RunnerId { get; init; }

    /// <summary>
    /// The key the runner registered at <c>gg runner up</c>.
    /// </summary>
    /// <remarks>
    /// What the console seals to, and what it pins. A runner registered before
    /// keys existed has none, and the control plane refuses to introduce it -
    /// which has to read as CANNOT rather than as no log.
    /// </remarks>
    public required string RunnerPublicKey { get; init; }

    /// <summary>
    /// The signed capability, opaque to the console and checked by the runner.
    /// </summary>
    /// <remarks>
    /// <b>Opaque HERE and structured THERE.</b> The console only relays it, so
    /// giving it members the console could read would invite it to make
    /// decisions on them; the runner verifies the signature and reads
    /// <see cref="RunnerCapabilityClaims"/> out of it. One value, two audiences,
    /// and only one of them is supposed to understand it.
    /// </remarks>
    public required string Capability { get; init; }

    /// <summary>When it stops working.</summary>
    public required DateTimeOffset ExpiresAt { get; init; }
}

/// <summary>
/// What a capability says, once the runner has checked who signed it.
/// </summary>
/// <remarks>
/// <b>Four narrowings, and each is a member rather than a convention.</b> One
/// console, one runner, one purpose, one expiry - ADR-0013's phrase made into a
/// shape, so a capability missing any of them cannot be constructed. A fifth
/// member that widened any of these would move a fingerprint.
/// </remarks>
[PinnedId("d5c30b78-91af-4e26-8073-6f2b1ac94e83")]
public sealed record RunnerCapabilityClaims
{
    /// <summary>The console's principal. One person, not a tenant.</summary>
    public required string PrincipalId { get; init; }

    /// <summary>The runner. One machine, not a label several answer to.</summary>
    public required string RunnerId { get; init; }

    /// <summary>One of <see cref="RunnerCapabilityPurposes"/>.</summary>
    public required string Purpose { get; init; }

    /// <summary>When it stops working.</summary>
    public required DateTimeOffset ExpiresAt { get; init; }

    /// <summary>
    /// A hash of the console's ephemeral key, so the runner can tell that
    /// whoever it is answering is who it was introduced to.
    /// </summary>
    /// <remarks>
    /// <b>A hash rather than the key</b>, because the runner already receives
    /// the key in the sealed offer and only needs to check that the two agree.
    /// Carrying it twice would invite a reader to wonder which one wins.
    /// </remarks>
    public required string EphemeralKeyHash { get; init; }
}

/// <summary>
/// An offer, sealed to the runner's registered key.
/// </summary>
/// <remarks>
/// <para>
/// <b>There is deliberately no member the SDP could be read out of.</b>
/// <c>EvidenceReference</c>'s remark is the precedent - "there is deliberately
/// no field a body could travel in" - and this is the same sentence about a
/// different body. What the control plane relays is bytes it cannot read, so
/// that "introduces and holds nothing" is a property rather than a promise.
/// </para>
/// <para>
/// <b>What is inside, and why it is not here.</b> ICE candidates are the
/// customer's private subnet addresses, host ports and egress IP. This platform
/// already declined to carry a hashed HOSTNAME - <c>EnvironmentIdentity</c>'s
/// own remark calls it "an identifier nobody needs" - and a raw private address
/// is more identifying than that.
/// </para>
/// </remarks>
[PinnedId("9f24e8a1-70bd-4c53-a916-3e08d7f2b5c6")]
public sealed record RunnerSealedOffer
{
    /// <summary>The capability, which the runner verifies before decrypting.</summary>
    public required string Capability { get; init; }

    /// <summary>The sealed offer. Opaque to everything that carries it.</summary>
    public required byte[] Sealed { get; init; }
}

/// <summary>
/// An answer, sealed to the console's ephemeral key.
/// </summary>
/// <remarks>
/// <b>The other direction, and it carries no capability.</b> The runner is
/// answering an introduction it already verified; a capability here would be a
/// second grant travelling backwards, which is a shape nobody asked for.
/// </remarks>
[PinnedId("4c8b1e93-2d70-45fa-b681-05a9c3e74d20")]
public sealed record RunnerSealedAnswer
{
    /// <summary>The runner this came from.</summary>
    public required string RunnerId { get; init; }

    /// <summary>The sealed answer. Opaque to everything that carries it.</summary>
    public required byte[] Sealed { get; init; }
}

/// <summary>
/// A runner answering one introduction.
/// </summary>
/// <remarks>
/// <b>The runner posts this; the control plane files it under the id and hands
/// it to whoever is waiting.</b> It carries no capability, because the
/// authorisation was checked when the offer was opened - a capability travelling
/// back would be a grant going the wrong way, which is a shape nobody asked for.
/// </remarks>
[PinnedId("1c6ea940-8b73-4d52-a087-f45c2e91b378")]
public sealed record RunnerSignalAnswer
{
    /// <summary>Which introduction this answers.</summary>
    public required string IntroductionId { get; init; }

    /// <summary>The answer, sealed to the console's ephemeral key.</summary>
    public required RunnerSealedAnswer Answer { get; init; }
}
