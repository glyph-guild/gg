using System.Text.Json;

using Gg.Local;

namespace Gg.Runner.Execution;

/// <summary>
/// Whether the tool servers this runner configured actually came up.
/// </summary>
/// <remarks>
/// <para>
/// <b>A server that dies at startup is invisible from this side.</b> The launch
/// hands the agent binary a configuration and the binary starts each server
/// itself; when one fails, the agent is launched anyway with that server's tools
/// absent from its list. Nothing in the exit code, the stderr or the result
/// record says so. The only place it is written down is the stream's own opening
/// line, as <c>{"name":"gg","status":"failed"}</c>.
/// </para>
/// <para>
/// <b>Which made a whole tier silently unavailable.</b> An agent that cannot
/// call <c>ask_for_decision</c> cannot say it is stuck, so it does what it can
/// and reports that it finished - and a reader concludes the model chose not to
/// ask. That is worse than no channel at all: the record then contains a
/// judgement the agent never made.
/// </para>
/// <para>
/// <b>Read from the first line, so nothing is spent.</b> The init record
/// precedes any turn, so refusing here costs a process launch. This is
/// <see cref="ToolServers.Unservable"/>'s answer one step later: that one
/// catches a server this runner cannot NAME, and this one catches a server it
/// named that did not START.
/// </para>
/// </remarks>
public static class ToolServers
{
    /// <summary>The one status that means the agent can call what it was offered.</summary>
    private const string Connected = "connected";

    /// <summary>
    /// Why this launch cannot proceed, or null when every configured server
    /// answered.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Anything but connected, rather than a list of bad statuses.</b> This
    /// runner clears the operator's setting sources and configures its servers
    /// strictly, so the only entries in that record are the ones it put there -
    /// and any of them not answering is a tool the agent was told about and
    /// cannot call. Enumerating the failure words would be somebody else's
    /// vocabulary to keep in step with, and a status this build has not heard of
    /// would read as healthy.
    /// </para>
    /// <para>
    /// <b>Silent on anything that is not that record.</b> A line with no
    /// servers, one that will not parse, and - importantly - the RESULT record
    /// are all ordinary. The result carries the same list at the end of a run,
    /// and reading it would turn a server that dropped late into a refusal
    /// claiming nothing was spent, which is a worse lie than the silence this
    /// replaces.
    /// </para>
    /// </remarks>
    public static string? Unstarted(string? line)
    {
        if (line is not { Length: > 0 })
        {
            return null;
        }

        JsonElement root;
        try
        {
            using var document = JsonDocument.Parse(line);
            root = document.RootElement.Clone();
        }
        catch (JsonException)
        {
            return null;
        }

        // THE INIT RECORD AND ONLY IT. The result record carries this list
        // again, at the END of a run - so a server that connected and later
        // dropped would turn a run that actually happened into a refusal
        // claiming nothing was spent, which is a worse lie than the silence
        // this replaces. What is being asked is "did the agent START without a
        // tool it was offered", and only the opening line answers that.
        if (root.ValueKind != JsonValueKind.Object
            || !root.TryGetProperty("type", out var type)
            || type.GetString() != "system"
            || !root.TryGetProperty("subtype", out var subtype)
            || subtype.GetString() != "init"
            || !root.TryGetProperty("mcp_servers", out var servers)
            || servers.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var silent = new List<string>();

        foreach (var server in servers.EnumerateArray())
        {
            if (server.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var name = server.TryGetProperty("name", out var named) ? named.GetString() : null;
            var status = server.TryGetProperty("status", out var reported)
                ? reported.GetString()
                : null;

            if (name is { Length: > 0 }
                && !string.Equals(status, Connected, StringComparison.Ordinal))
            {
                silent.Add($"'{name}' ({status ?? "no status"})");
            }
        }

        return silent.Count == 0
            ? null
            : "This runner configured " + (silent.Count == 1 ? "a tool server" : "tool servers")
            + " the agent binary could not start: " + string.Join(", ", silent)
            + ". Nothing was spent. An agent told a tool exists and given one that does not "
            + "answer spends its turns calling nothing - and a loop that cannot ask for a "
            + "decision cannot say it is stuck, so it reports that it finished.";
    }

    /// <summary>
    /// Why this runner cannot serve a loop that declared <c>propose</c>, or
    /// null when it can.
    /// </summary>
    /// <remarks>
    /// <b>Article XI, before anything is spent</b> - the shape the
    /// unreadable-tracker refusal beside it already has. A flight whose loop
    /// declares a move this machine cannot serve is refused with a reason,
    /// rather than handed to an agent that will establish the same thing slowly
    /// and report it as prose. A loop that never asked to nominate is not
    /// blocked by a tool nobody needs.
    /// </remarks>
    public static string? Unservable(IReadOnlyList<string> moves, SelfInvocation? self)
    {
        ArgumentNullException.ThrowIfNull(moves);

        return moves.Contains(Gg.Contracts.LoopMoves.Propose, StringComparer.Ordinal)
            && self is null
            ? $"This loop declares '{Gg.Contracts.LoopMoves.Propose}' and this runner cannot "
            + "name its own executable, so it cannot serve the tool that move grants. Nothing "
            + "was spent. A runner serves that tool by starting another copy of itself, and a "
            + "process that cannot say where it is would start something else."
            : null;
    }
}
