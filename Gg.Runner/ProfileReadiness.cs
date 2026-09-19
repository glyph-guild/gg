using Gg.Contracts;

namespace Gg.Runner;

/// <summary>
/// What a machine measures against the profile it enrolled under (slice
/// forty-three, rule 25): the agent it declares, each credential it can
/// resolve, each forge it can reach.
/// </summary>
/// <remarks>
/// <para>
/// <b>Pure except for the two questions it is handed</b> - can this reference be
/// resolved, can this host be reached - so what counts as met is testable and
/// the machine's stores and network stay the composition root's.
/// </para>
/// <para>
/// <b>A subject is what was checked, never what a check produced.</b> A
/// credential item names its reference; nothing a resolution returned reaches a
/// reading, and a diagnosis is the resolver's sentence about the reference.
/// </para>
/// </remarks>
public static class ProfileReadiness
{
    /// <summary>How often an idle runner measures itself again, so a gate clears soon after its item verifies.</summary>
    public static readonly TimeSpan Every = TimeSpan.FromMinutes(5);

    /// <param name="declaredAgent">The agent this machine declares, by provider name, or null for none.</param>
    /// <param name="resolve">Null when a reference resolves, otherwise why not.</param>
    /// <param name="reach">Null when a host answers, otherwise why not.</param>
    public static async Task<ReadinessReading> MeasureAsync(
        FleetProfileState profile,
        string? declaredAgent,
        Func<string, CancellationToken, Task<string?>> resolve,
        Func<string, CancellationToken, Task<string?>> reach,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(resolve);
        ArgumentNullException.ThrowIfNull(reach);

        var items = new List<ReadinessItem>();

        if (profile.Profile.Agent is { } wanted)
        {
            var met = string.Equals(wanted, declaredAgent, StringComparison.Ordinal);
            items.Add(new ReadinessItem
            {
                Kind = ReadinessKinds.Agent,
                Subject = wanted,
                Met = met,
                Diagnosis = met
                    ? null
                    : declaredAgent is null
                        ? $"the profile runs {wanted}, and this machine declares no agent - set "
                        + "executor-binary to where its binary is."
                        : $"the profile runs {wanted}, and this machine declares {declaredAgent}.",
            });
        }

        foreach (var reference in profile.Profile.Credentials)
        {
            var why = await resolve(reference, cancellationToken);
            items.Add(new ReadinessItem
            {
                Kind = ReadinessKinds.Credential,
                Subject = reference,
                Met = why is null,
                Diagnosis = why,
            });
        }

        foreach (var forge in profile.Profile.Forges)
        {
            var at = forge.IndexOf('=', StringComparison.Ordinal);
            var (key, host) = at > 0 ? (forge[..at], forge[(at + 1)..]) : (forge, forge);

            var why = await reach(host, cancellationToken);
            items.Add(new ReadinessItem
            {
                Kind = ReadinessKinds.Forge,
                Subject = key,
                Met = why is null,
                Diagnosis = why,
            });
        }

        return new ReadinessReading
        {
            Profile = profile.Name,
            Version = profile.Version,
            Items = items,
            MeasuredAt = now,
        };
    }
}

/// <summary>A runner's two readiness calls: what it enrolled as, and whether it meets it.</summary>
/// <remarks>
/// Apart from <see cref="IRunnerProtocol"/>, for <see cref="IRunnerCredential"/>'s
/// reason: a test about claiming work should not have to answer questions about
/// profiles.
/// </remarks>
public interface IRunnerReadiness
{
    /// <summary>The profile this runner enrolled under, or null for one enrolled under none.</summary>
    Task<FleetProfileState?> ProfileAsync(CancellationToken cancellationToken = default);

    /// <summary>Reports a measurement. Best-effort at the caller.</summary>
    Task ReportReadinessAsync(ReadinessReading reading, CancellationToken cancellationToken = default);
}
