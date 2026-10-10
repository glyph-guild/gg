using Gg.Local;

namespace Gg.Console;

/// <summary>What an agent in the mux is doing, as its own hooks say.</summary>
public enum AgentActivity
{
    /// <summary>Nothing said yet - an agent with no hooks, or one that has not started a turn.</summary>
    None,

    /// <summary>A turn is under way.</summary>
    Working,

    /// <summary>The turn is over and the agent waits for the person's next prompt.</summary>
    Waiting,

    /// <summary>The agent is blocked on the person - a permission question.</summary>
    NeedsYou,
}

/// <summary>Claude Code's hooks, telling the mux what each agent is doing.</summary>
public static class MuxActivity
{
    public const string Variable = "GG_MUX_ACTIVITY";

    public static string Folder(string? stateHome = null) => Path.Combine(LocalPaths.StateRoot(stateHome), "mux", "activity");

    public static AgentActivity Interpret(string word, string? hookInput) => AgentActivity.None;

    public static void Mark(string? path, string word, string? hookInput, string folder) { }

    public static AgentActivity Read(string? path) => AgentActivity.None;

    public static string Settings(SelfInvocation self) => "{}";
}
