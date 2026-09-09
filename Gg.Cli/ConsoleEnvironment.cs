using Gg.Local;
using Gg.Runner;
using Gg.Runner.Execution;
using Gg.Runner.Vcs;

namespace Gg.Cli;

/// <summary>
/// Every setting gg reads, what each decides, and where its value came from.
/// </summary>
/// <remarks>
/// <para>
/// <b>Here because this is where they are already read.</b> The composition
/// root is the one place that touches all of them — the runner's and the
/// console's alike. <c>Gg.Console</c> also cannot see <c>Gg.Runner</c>, so a
/// list built there would have to re-declare names as literals. What was
/// <i>resolution</i> now lives one project down in <see cref="Settings"/>,
/// because three projects need the same answer; what stays here is the
/// declaration and the reason attached to each.
/// </para>
/// <para>
/// <b>DECLARED, NEVER SWEPT.</b> Walking the process environment would put
/// whatever else a person exports onto a screen they may be sharing, and into
/// the state dump. Nothing here holds a secret by design: the credential
/// variables carry a LOCATOR, which names a secret without being one.
/// </para>
/// <para>
/// <b>THE LIST WAS HALF THE TRUTH UNTIL NOW.</b> It named ten while production
/// read about twenty, so everything a runner reads — the executor binary, the
/// labels, the hold, the pool endpoint, the relay servers — appeared on no
/// surface at all. <c>EveryValueSaysWhereItCameFromTests</c> holds this list to
/// a scan of production source, so the next one cannot go unnamed.
/// </para>
/// <para>
/// <b>Every name now comes from the code that reads it.</b> Four of them were
/// string literals here beside constants that existed three files away, which
/// is the drift a declared list is supposed to prevent.
/// </para>
/// </remarks>
public static class ConsoleEnvironment
{
    /// <summary>Every setting, resolved against the file that is in force.</summary>
    /// <param name="file">
    /// The configuration on disk, or null when there is none. Resolution goes
    /// through <see cref="Settings.Resolve"/> — the same one the rest of the
    /// product uses — so what this page says and what the machine does cannot
    /// disagree.
    /// </param>
    /// <param name="environment">
    /// How to read a variable. Null reads the process — and the override is here
    /// for the reason <c>LocalPaths</c> states about the same hazard: the
    /// environment is process-global, and a suite that runs four-wide cannot
    /// have one test setting a variable while another reads it. Without this the
    /// page's own test passes alone and fails beside its neighbours.
    /// </param>
    public static IReadOnlyList<EnvironmentSetting> Read(
        Configuration? file = null, Func<string, string?>? environment = null) =>
    [
        Of("EDITOR", file, environment,
           "the editor `n` hands the terminal to. An editor that forks and returns "
         + "instead of holding it — most GUI editors — comes back with nothing written, "
         + "and the console reports no intent rather than a broken key."),

        Of("GG_TAKE_COMMAND", file, environment,
           "what `t` starts to hand you a flight's tree."),

        Of("GG_CONTROL_PLANE", file, environment,
           "the control plane this console reads and writes."),

        Of(IntentConfiguration.ServedVariable, file, environment,
           "which trackers this binary reads work items from itself, as "
         + "provider=host|locator. What the browse pane offers."),

        Of(IntentConfiguration.ReadersVariable, file, environment,
           "trackers read by a tool server somebody installed, for a tracker this "
         + "binary has no shape for. A key may appear in only one of the two."),

        Of(VcsConfiguration.HostsVariable, file, environment,
           "which forge each provider key clones from. A flight against a provider "
         + "with no host here is refused before anything is fetched."),

        Of(DestinationConfiguration.ApisVariable, file, environment,
           "where a proposal is opened, per provider key."),

        // THE RUNNER'S OWN, none of which were on this page. An operator
        // standing up a machine reads them here or nowhere.
        Of(ExecutorConfiguration.BinaryVariable, file, environment,
           "the agent binary a runner invokes. Unset means the runner takes work, "
         + "materializes it and ships facts, and invokes no agent at all."),

        Of("GG_RUNNER_LABELS", file, environment,
           "the labels this machine's runner advertises, as key=value pairs. A flight "
         + "is offered only to a runner carrying what it asks for."),

        Of("GG_RUNNER_HOLD_SECONDS", file, environment,
           "how long a claim waits for work before coming back empty."),

        Of("GG_POOL_ENDPOINT", file, environment,
           "the scope-enforcing proxy `gg runner maintain` works through. Unset means "
         + "maintain refuses rather than reaching a host directly."),

        Of(StunConfiguration.Variable, file, environment,
           "relay addresses for the connection between a runner and a console. Empty "
         + "is a real answer: host candidates only."),

        // AND THE FOUR THE FILE CANNOT CARRY, said rather than left out.
        Of("XDG_CONFIG_HOME", file, environment,
           "where the session, the credential store and this configuration live. "
         + "Unset means ~/.config."),

        Of("XDG_STATE_HOME", file, environment,
           "where a flight's live view is written and tailed from."),

        Of("XDG_CACHE_HOME", file, environment,
           "where a runner clones a working tree, and where a held one waits."),

        Of("GG_STATE_DUMP", file, environment,
           "a file this console writes its whole model to on exit, for a bug report. "
         + "It carries work item titles; it does not carry the live channel."),
    ];

    private static EnvironmentSetting Of(
        string name, Configuration? file, Func<string, string?>? environment, string why)
    {
        var resolved = Settings.Resolve(name, file, environment);

        return new EnvironmentSetting
        {
            Name = name,
            Value = resolved.Value,
            Why = why,
            Source = resolved.Source,
            Shadowed = resolved.Shadowed,
            EnvironmentOnly = !Settings.CanBeInTheFile(name),
        };
    }
}
