namespace Gg.Runner.Pools;

/// <summary>
/// Where an environment instance's daemon listens, derived from its name.
/// </summary>
/// <remarks>
/// <para>
/// <b>A convention held on both sides, like <see cref="PoolNaming"/>'s prefix.</b>
/// The other half is <c>deploy/pool-host/environments.md</c>, which tells a
/// person to create the socket there by hand; this is what connects to it, and a
/// test compares the two rather than trusting that both were updated. Two
/// spellings of one path is a stack that comes up where nothing looks for it.
/// </para>
/// <para>
/// <b>Why the control plane sends a name and not this.</b> An instance is a UNIX
/// user on a pool host, and where its daemon listens is deployment knowledge. The
/// control plane holds references rather than values, so <c>gg-env-1</c> crosses
/// the wire and the path is built here — which also means changing the layout is
/// a change to this binary and its runbook, not a migration.
/// </para>
/// <para>
/// <b>Not the runtime-dir socket.</b> A rootless daemon's default is
/// <c>/run/user/&lt;uid&gt;/docker.sock</c>, and measured on the pool host that
/// directory is <c>drwx------</c> on a tmpfs recreated each boot — the runner is
/// refused and an ACL would not survive a restart. The durable path is a second
/// listener the instance's unit is given.
/// </para>
/// </remarks>
public static class EnvironmentNaming
{
    /// <summary>Where every instance's home lives, and so its socket.</summary>
    public const string Root = "/srv/env";

    /// <summary>
    /// The daemon address for an instance, or null when there is no instance.
    /// </summary>
    /// <remarks>
    /// <b>Null rather than a path built from nothing.</b> Every flight today is
    /// hosted nowhere, and <c>/srv/env//run/docker.sock</c> normalises to a
    /// directory that exists - so a blank name would produce an address that
    /// connects to the wrong daemon rather than an error. Absence has to stay
    /// absence.
    /// </remarks>
    public static string? SocketFor(string? instance) =>
        string.IsNullOrWhiteSpace(instance)
            ? null
            : $"unix://{Root}/{instance}/run/docker.sock";
}
