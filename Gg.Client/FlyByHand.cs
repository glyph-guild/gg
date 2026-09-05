using Gg.Contracts;

namespace Gg.Client;

/// <summary>What became of a request to fly a flight by hand.</summary>
/// <remarks>
/// <b>Two outcomes and no third.</b> Either this machine could not run it and
/// nothing was created, or the flight exists. A shape that could carry both, or
/// neither, would be a caller's decision to make and there is nothing to decide.
/// </remarks>
/// <param name="Refused">
/// Why this machine may not, or null when it may.
/// </param>
/// <param name="Opened">
/// The flight, or null when it was refused.
/// </param>
public sealed record HandFlight(HandRefusal? Refused, VerbResult? Opened);

/// <summary>
/// The order a hand-flight happens in.
/// </summary>
/// <remarks>
/// <para>
/// <b>Rule 5 lives here and nowhere else.</b> Whether the refusal is right is
/// <see cref="HandRefusal"/>'s; that it is consulted <i>before anything is
/// created</i> is this function's, and it is the half that leaves a flight
/// nobody wanted in the tenant's queue when it is wrong. A flight created and
/// then abandoned because this laptop was wrong is litter with a number on it:
/// it appears in <c>gg flights</c>, and somebody has to decide what became of it.
/// </para>
/// <para>
/// <b>Its collaborators are passed in</b>, so the ordering is testable without a
/// control plane. That is <see cref="RunnerIdentity.EnsureAsync"/>'s own
/// arrangement and its own reason: the rule was untestable while it lived inside
/// a CLI entry point.
/// </para>
/// </remarks>
public static class FlyByHand
{
    /// <summary>Refuses, or opens the flight — never both and never neither.</summary>
    /// <param name="plan">
    /// What a flight opened now would need, priced against the live fleet. Read
    /// FIRST, because everything after it creates something.
    /// </param>
    /// <param name="advertised">
    /// What THIS machine advertises. The plan prices against the fleet, and a
    /// label some other runner has is useless to a person at this keyboard.
    /// </param>
    public static async Task<HandFlight> FlyAsync(
        Func<CancellationToken, Task<Checklist>> plan,
        IReadOnlyList<string> advertised,
        Func<CancellationToken, Task<VerbResult>> open,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(advertised);
        ArgumentNullException.ThrowIfNull(open);

        // FIRST, AND THE ORDER IS THE FEATURE. Reading it after the flight is
        // open answers the same question and leaves the flight behind.
        var required = await plan(cancellationToken);

        if (HandRefusal.For(required, advertised) is { } refused)
        {
            return new HandFlight(refused, null);
        }

        return new HandFlight(null, await open(cancellationToken));
    }
}
