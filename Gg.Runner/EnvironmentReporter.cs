using Gg.Contracts;
using Gg.Local;

namespace Gg.Runner;

/// <summary>
/// Says which environment instances this host has, no more often than a set of
/// UNIX users can change.
/// </summary>
/// <remarks>
/// <para>
/// <b>It maps a look at the disk onto the record that crosses.</b>
/// <c>Gg.Local</c> cannot reference the wire contract, so the two shapes are
/// separate by construction and this is the one place they meet — the division
/// <see cref="MachineReporter"/> describes, applied to the other host fact.
/// </para>
/// <para>
/// <b>Null and empty are different answers, and keeping them apart is this
/// file's one real job.</b> The scan's null is "this machine hosts no
/// environments" — no root, which is every developer's Mac and every member
/// container — and must buy no request at all. The scan's empty list is a host
/// that HAS the root and nothing in it, which is a statement: it retires
/// whatever it used to have, and is the only thing in the system that can.
/// <see cref="MachineReporter"/> collapses "every figure absent" into silence
/// and is right to; doing the same here would leave a lost slot grantable for
/// ever, and every flight sent to one fails at a socket.
/// </para>
/// <para>
/// <b>Nothing here identifies anybody.</b> Instance names and environment names,
/// both of which the operator chose and the tenant charted: no path, no
/// hostname, no account, nothing a customer wrote. Which host is reporting is
/// the credential it presented, never a field — a machine that could name the
/// reporter could retire another host's instances.
/// </para>
/// </remarks>
/// <param name="scan">What this host reads about its own disk.</param>
/// <param name="cadence">How often it is worth looking.</param>
public sealed class EnvironmentReporter(
    Func<DateTimeOffset, IReadOnlyList<SeenInstance>?> scan, TimeSpan cadence)
{
    private DateTimeOffset? _last;

    /// <summary>
    /// How often a host looks at what it has.
    /// </summary>
    /// <remarks>
    /// The machine reading's thirty seconds. A slot is made by hand, once, when
    /// a host is built — so this could be far slower, and is not, because the
    /// case that matters is a slot going AWAY: every second between a daemon
    /// dying and this report is a second the claim may hand somebody its name.
    /// </remarks>
    public static readonly TimeSpan Cadence = TimeSpan.FromSeconds(30);

    /// <summary>The real one, reading this host's own environment root.</summary>
    public static EnvironmentReporter OfThisHost() =>
        new(_ => EnvironmentSlotScan.OfThisHost(Pools.EnvironmentNaming.Root).Read(), Cadence);

    /// <summary>
    /// A reading if one is due and this machine hosts environments, or null.
    /// </summary>
    public EnvironmentInstanceReading? Read(DateTimeOffset now)
    {
        if (_last is { } when && now - when < cadence)
        {
            return null;
        }

        _last = now;

        IReadOnlyList<SeenInstance>? seen;

        try
        {
            seen = scan(now);
        }
        catch (IOException)
        {
            // A DISK THAT CANNOT BE READ IS NOT A RUNNER THAT SHOULD STOP. The
            // report is bookkeeping; a permissions problem on one host must not
            // become an outage, and the loop's own guard catches what escapes.
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }

        // NULL IS THE ONLY SILENCE. An empty list goes out, and has to: see the
        // note on this type.
        return seen is null
            ? null
            : new EnvironmentInstanceReading
            {
                MeasuredAt = now,
                Instances = [.. seen.Select(one => new EnvironmentInstanceSeen
                {
                    Environment = one.Environment,
                    Instance = one.Instance,
                })],
            };
    }
}
