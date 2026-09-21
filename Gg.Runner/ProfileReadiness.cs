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
                        // BOTH WAYS TO SAY IT, because the reader may be
                        // standing anywhere. A bring-up ask reaches somebody who
                        // has never opened a shell on this machine, and the
                        // machine may not be built yet: at build time the answer
                        // is one flag on the install line, and afterwards it is
                        // one command on the machine.
                        ? $"the profile runs {wanted}, and this machine declares no agent. The "
                        + "binary is the machine's to have; say where it is with "
                        + "`gg service install --agent-binary <path>` when the machine is "
                        + "built, or `gg config set executor-binary <path>` on it."
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

        // A TRACKER'S CREDENTIAL, DERIVED FROM THE ENTRY. A profile states a
        // tracker once - `key=host|reference` - so asking it to repeat the
        // reference under `credentials` would be two places to keep in agreement
        // and one to forget. Measured the same way a named credential is, which
        // is what lets a bring-up ask name it.
        //
        // AND A DECLARATION THIS BUILD CANNOT READ IS ALSO SOMETHING IT LACKS.
        // TrackerConfiguration skips an entry it cannot parse rather than
        // refusing to start (rule 2), so without this the machine would simply
        // not write and say nothing about why.
        foreach (var (field, entries) in
                 (IReadOnlyList<(string, IReadOnlyList<string>)>)
                 [("trackers", profile.Profile.Trackers), ("triage", profile.Profile.Triage)])
        {
            foreach (var entry in entries)
            {
                if (!Gg.Contracts.FleetProfile.IsTracker(entry))
                {
                    items.Add(new ReadinessItem
                    {
                        Kind = ReadinessKinds.Credential,
                        Subject = entry,
                        Met = false,
                        Diagnosis = $"this build cannot read the {field} entry '{entry}'. It is "
                                  + "written key=host|reference; a value from a newer contract "
                                  + "reaches an older machine this way, and the machine says so "
                                  + "rather than refusing to start.",
                    });

                    continue;
                }

                var reference = entry[(entry.LastIndexOf('|') + 1)..];
                var unresolved = await resolve(reference, cancellationToken);

                items.Add(new ReadinessItem
                {
                    Kind = ReadinessKinds.Credential,
                    Subject = reference,
                    Met = unresolved is null,
                    Diagnosis = unresolved,
                });
            }
        }

        foreach (var forge in profile.Profile.Forges)
        {
            // THE ONE PARSER, NOT A THIRD COPY OF IT. A forge is written the way
            // GG_VCS_HOSTS is written - `key=host`, with `!pathscoped` and a base
            // path both allowed - and splitting it here on `=` alone asked the
            // network for `forge.example.com/org!pathscoped`. Measured on
            // vmlinux002 (S43.8-01): every machine with a path-scoped forge
            // opened a bring-up flight saying its forge could not be reached,
            // naming a host that is not one, which nobody could ever clear.
            string key, authority;
            try
            {
                var declared = Vcs.HostDeclaration.Parse(forge, "this profile's forges");
                (key, authority) = (declared.Key, declared.Authority);
            }
            catch (InvalidOperationException malformed)
            {
                // AND A FORGE NOBODY CAN PARSE IS AN UNMET ITEM, not a crash and
                // not a silence: the profile says this machine needs it, and the
                // sentence a person needs is the parser's own.
                items.Add(new ReadinessItem
                {
                    Kind = ReadinessKinds.Forge,
                    Subject = forge,
                    Met = false,
                    Diagnosis = malformed.Message,
                });

                continue;
            }

            var why = await reach(authority, cancellationToken);
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
