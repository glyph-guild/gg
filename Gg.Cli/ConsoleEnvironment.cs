using Gg.Local;

namespace Gg.Cli;

/// <summary>
/// The environment variables gg reads, and what each one decides.
/// </summary>
/// <remarks>
/// <para>
/// <b>Here because this is where they are already read.</b> The composition
/// root is the one place that touches all of them — the runner's and the
/// console's alike — and <c>ExecutorConfiguration</c>'s rule is that one place
/// reads the environment so nothing downstream reaches a different answer.
/// <c>Gg.Console</c> also cannot see <c>Gg.Runner</c>, so a list built there
/// would have to re-declare names as literals, which is the drift this avoids.
/// </para>
/// <para>
/// <b>DECLARED, NEVER SWEPT.</b> Walking the process environment would put
/// whatever else a person exports onto a screen they may be sharing, and into
/// the state dump. Nothing here holds a secret by design: the credential
/// variables carry a LOCATOR, which names a secret without being one.
/// </para>
/// <para>
/// <b>Every name comes from the code that reads it</b> where that code is
/// reachable. Where it is not — a runner variable this binary reads in its own
/// composition — the literal is here beside the read, not copied from a third
/// place.
/// </para>
/// </remarks>
public static class ConsoleEnvironment
{
    public static IReadOnlyList<EnvironmentSetting> Read() =>
    [
        Of("EDITOR",
           "the editor `n` hands the terminal to. An editor that forks and returns "
         + "instead of holding it — most GUI editors — comes back with nothing written, "
         + "and the console reports no intent rather than a broken key."),

        Of("GG_TAKE_COMMAND",
           "what `t` starts to hand you a flight's tree. Unset means the console says "
         + "so rather than taking one."),

        Of("GG_CONTROL_PLANE",
           "the control plane this console reads and writes. Unset means "
         + "http://localhost:5199."),

        Of(IntentConfiguration.ServedVariable,
           "which trackers this binary reads work items from itself, as "
         + "provider=host|locator. What the browse pane offers."),

        Of(IntentConfiguration.ReadersVariable,
           "trackers read by a tool server somebody installed, for a tracker this "
         + "binary has no shape for. A key may appear in only one of the two."),

        Of("GG_VCS_HOSTS",
           "which forge each provider key clones from. A flight against a provider "
         + "with no host here is refused before anything is fetched."),

        Of("GG_DESTINATION_APIS",
           "where a proposal is opened, per provider key."),

        Of("XDG_CONFIG_HOME",
           "where the session and the credential store live. Unset means ~/.config."),

        Of("XDG_STATE_HOME",
           "where a flight's live view is written and tailed from. Unset means "
         + "~/.local/state."),

        Of("GG_STATE_DUMP",
           "a file this console writes its whole model to on exit, for a bug report. "
         + "It carries work item titles; it does not carry the live channel."),
    ];

    private static EnvironmentSetting Of(string name, string why) => new()
    {
        Name = name,
        Value = Environment.GetEnvironmentVariable(name),
        Why = why,
    };
}
