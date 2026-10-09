using System.Text;
using System.Text.Json;
using Gg.Client;
using Gg.Local;

namespace Gg.Console;

/// <summary>
/// A plan drafted in the mux, beside the agent drafting it: ADR-0038 build step 4, the third hosted
/// session after compose and draft. Slice sixty-six.
/// </summary>
/// <remarks>
/// <para>
/// <b>The agent drafts with the planning tool server, and the person edits the same draft from
/// gg's panel</b> (Decision 8). The server reads the draft fresh on every call, so a panel edit is
/// in the agent's next result with nothing passed between them.
/// </para>
/// <para>
/// <b>The panel reads two files and nothing else</b> (rule 3): the draft, and the last result the
/// server left beside it. A hosted session may not start anything, resolve a credential, or block,
/// so the verdicts come from the server's own check, read off disk.
/// </para>
/// <para>
/// <b>It drafts a file of its own</b>, kept when the session closes. A new session starts on an
/// empty draft (<see cref="FreshDraft"/>); it used to reopen <c>console</c>, which put the last
/// plan in front of every new agent.
/// </para>
/// </remarks>
public sealed class PtyPlanSession
{
    /// <summary>The draft the mux plans in.</summary>
    public const string Draft = "console";

    /// <summary>
    /// The draft a new plan session starts on: <c>console</c>, or <c>console-2</c> and on - the
    /// first name no draft, result or proposal has used, and no live plan agent holds.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Always an empty one</b> (owner, 2026-10-07: "the last plan is always stuck whenever I
    /// start a new itinerary session"). Every session used to draft <c>console</c>, so a plan
    /// already proposed was in front of each new agent, and its opening prompt told it to carry
    /// on. Older drafts are kept exactly as they were.
    /// </para>
    /// <para>
    /// <b>Two agents never edit one file</b> (slice sixty-nine), which slice sixty-eight's
    /// one-live-plan-per-draft rule relies on - so a name a live agent holds is skipped even
    /// before its agent has written anything.
    /// </para>
    /// </remarks>
    /// <param name="labels">The live agents' labels; a plan agent is "plan · &lt;draft&gt;".</param>
    public static string FreshDraft(ItineraryDrafts drafts, IEnumerable<string> labels)
    {
        ArgumentNullException.ThrowIfNull(drafts);

        var held = labels.Where(label => label.StartsWith("plan · ", StringComparison.Ordinal))
            .Select(label => label["plan · ".Length..])
            .ToHashSet(StringComparer.Ordinal);

        for (var n = 1; ; n++)
        {
            var name = n == 1 ? Draft : $"{Draft}-{n}";
            if (!held.Contains(name) && !drafts.Used(name))
            {
                return name;
            }
        }
    }

    /// <summary>A fresh draft for a plan about <paramref name="seed"/>, its intent already set.</summary>
    /// <remarks>
    /// <b>Written before the agent starts</b>, so the draft is taken - no second session can open
    /// on it - and the agent's first <c>show_plan</c> already shows the item.
    /// </remarks>
    public static string SeedDraft(ItineraryDrafts drafts, IEnumerable<string> labels, PlanSeed seed)
    {
        ArgumentNullException.ThrowIfNull(drafts);
        ArgumentNullException.ThrowIfNull(seed);

        var draft = FreshDraft(drafts, labels);
        _ = drafts.Change(draft, empty => empty with
        {
            Intent = Gg.Contracts.FlightIntent.Of(null, provider: seed.Provider, id: seed.Id),
        });

        return draft;
    }

    private readonly PlanSeed? _seed;
    private readonly Gg.Local.IntentReader? _reader;
    private readonly string _agentCommand;
    private readonly Func<IHostTerminal?> _terminal;
    private readonly SelfInvocation? _self;
    private readonly HostRun _host;
    private readonly ItineraryDrafts _drafts;
    private readonly Action<string> _say;
    private readonly string _draft;

    public PtyPlanSession(
        string? agentCommand = null,
        Func<IHostTerminal?>? terminal = null,
        SelfInvocation? self = null,
        HostRun? host = null,
        ItineraryDrafts? drafts = null,
        Action<string>? say = null,
        string draft = Draft,
        PlanSeed? seed = null,
        Gg.Local.IntentReader? reader = null)
    {
        _seed = seed;
        _reader = reader;
        _draft = draft;
        _agentCommand = agentCommand
            ?? Environment.GetEnvironmentVariable("GG_TAKE_COMMAND")
            ?? "claude";
        _terminal = terminal ?? OwnedTerminal.Open;
        _self = self ?? SelfInvocation.Current;
        _host = host ?? PtyHost.RunAsync;
        _drafts = drafts ?? ItineraryDrafts.ForThisMachine();
        _say = say ?? System.Console.WriteLine;
    }

    /// <summary>Runs the session, and says what it left.</summary>
    public string Run()
    {
        if (_self is null)
        {
            return "gg cannot name its own executable here, so it cannot serve the planning "
                 + "tools an agent drafts with. Nothing was planned.";
        }

        var terminal = _terminal();
        if (terminal is null)
        {
            _say("gg cannot plan with an agent here: there is no terminal to host one in.");
            return "Nothing was planned: there is no terminal to host an agent in.";
        }

        // HELD HERE, because the host keeps nothing between calls and the bar is pure.
        var panel = new HostedPanel(HostedView.Closed, 0);
        var notice = (string?)null;
        var staleAgainst = (string?)null;
        var room = 0;
        var width = 0;
        const string asking = "gg · planning with an agent — ask it to draft legs · ctrl-g shows the plan, "
                            + "where J/K move a leg and x drops one · ask it to propose when it is ready";

        // ASKED ON EVERY FRAME, because a proposal lands while the session runs (slice sixty-eight:
        // "the bar needs to be clear when itinerary plans have been submitted"). Once proposed it
        // leads with what the draft became, and says when an edit since means proposing replaces it.
        string Bar() => _drafts.ProposalLine(_draft) is { } proposed
            ? $"gg · {proposed} · ctrl-g shows the plan"
            : asking;

        string Body() => Plan(panel.Leg, notice, staleAgainst);

        try
        {
            var parts = _agentCommand.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            _host(
                terminal,
                parts[0],
                [.. parts.Skip(1),
                 // THE OPENING, BEFORE EVERY FLAG: --allowedTools takes a list, and a prompt after it
                 // is one more tool name.
                 AgentOpening.Plan(_draft, _seed),
                 "--mcp-config", ServerConfig(_self, _draft, _reader),
                 // NAMED, NOT GRANTED BY PREFIX: a prefix widens with every tool the server adds.
                 // AND THE ITEM'S READER, when the plan starts from one: reading it is the
                 // agent's first move.
                 "--allowedTools", .. PlanningTool.All.Select(PlanningTool.Qualified),
                 .. Reading(_reader)],
                Directory.GetCurrentDirectory(),
                (most, wide) =>
                {
                    room = most;
                    width = wide;
                    return new HostedRows(
                        HostedBar.Rows(panel, Bar(), panel.Showing == HostedView.Plan ? Body() : "", most, wide),
                        panel.Showing != HostedView.Closed);
                },
                (gesture, typed) =>
                {
                    if (!HostedBar.Takes(panel, gesture, typed.Span))
                    {
                        return false;
                    }

                    var edit = HostedBar.Edit(panel, gesture, typed.Span);
                    if (edit != PlanEdit.None)
                    {
                        (notice, staleAgainst, panel) = Applied(edit, panel, staleAgainst);
                        return true;
                    }

                    panel = HostedBar.Next(
                        panel, gesture, typed.Span, Body(), room, width, opensOn: HostedView.Plan, status: Bar());
                    panel = panel with { Leg = Math.Clamp(panel.Leg, 0, Math.Max(Legs() - 1, 0)) };
                    return true;
                },
                CancellationToken.None).GetAwaiter().GetResult();
        }
        catch (Exception missing) when (
            missing is DllNotFoundException or EntryPointNotFoundException)
        {
            _say("gg could not open its own terminal view: libporta_pty is not installed "
               + "beside gg, so there is no way to host an agent.");
            return "Nothing was planned: libporta_pty is not installed beside gg.";
        }

        return Left();
    }

    /// <summary>
    /// What the session leaves (rule 7): the plan it proposed and what that waits on, or the draft,
    /// kept. Read off the server's last result - the console opens, answers and sends nothing.
    /// </summary>
    private string Left()
    {
        // THE RECORD FIRST: the last result moves on with every tool call after a proposal.
        if (_drafts.ProposalLine(_draft) is { } proposed)
        {
            return $"The plan session ended: {proposed}. The draft is kept at {_drafts.PathOf(_draft)}.";
        }

        if (_drafts.LastResult(_draft) is { } last
            && last.StartsWith("proposed ITN-", StringComparison.Ordinal))
        {
            var said = last.Split('\n')
                .TakeWhile(line => line.Length > 0)
                .Select(line => line.Trim());
            return "The plan session ended: " + string.Join("; ", said) + ".";
        }

        return $"The plan session ended. The draft is kept at {_drafts.PathOf(_draft)}; plan with "
             + "an agent again to carry on, or ask it to propose.";
    }

    private int Legs() =>
        _drafts.Read(_draft) is DraftRead.Held { Draft: var held } ? held.Legs.Count : 0;

    /// <summary>One edit from the panel, through the draft's own store and rules (rule 4).</summary>
    private (string? Notice, string? StaleAgainst, HostedPanel Panel) Applied(
        PlanEdit edit, HostedPanel panel, string? staleAgainst)
    {
        var before = _drafts.LastResult(_draft);
        var moved = 0;

        var change = _drafts.Change(_draft, draft =>
        {
            var at = Math.Clamp(panel.Leg, 0, Math.Max(draft.Legs.Count - 1, 0));
            switch (edit)
            {
                case PlanEdit.Drop:
                    return PlanEdits.Dropped(draft, at);
                case PlanEdit.MoveUp:
                    moved = -1;
                    return PlanEdits.Moved(draft, at, -1);
                default:
                    moved = 1;
                    return PlanEdits.Moved(draft, at, 1);
            }
        });

        if (change is DraftChange.Refused { Diagnosis: var why })
        {
            return ($"Not changed: {why}", staleAgainst, panel);
        }

        var said = edit == PlanEdit.Drop
            ? "Dropped. The agent sees it in its next tool result."
            : "Moved. The agent sees it in its next tool result.";

        // STALE FROM HERE, until the server says something new (rule 5).
        return (said, before ?? "", panel with { Leg = Math.Max(panel.Leg + moved, 0) });
    }

    /// <summary>The plan view's body: the draft, the chosen leg marked, then the agent's last verdicts.</summary>
    private string Plan(int chosen, string? notice, string? staleAgainst)
    {
        var text = new StringBuilder();

        // WHAT THE DRAFT BECAME, above the draft (slice sixty-eight).
        if (_drafts.ProposalLine(_draft) is { } proposed)
        {
            text.Append(proposed).Append("\n\n");
        }

        switch (_drafts.Read(_draft))
        {
            case DraftRead.Unreadable { Diagnosis: var unreadable }:
                text.Append(unreadable).Append('\n');
                break;

            case DraftRead.Held { Draft: var held }:
                text.Append("Intent: ").Append(held.Intent switch
                {
                    null => "none yet",
                    { Text: { Length: > 0 } words } => words,
                    { Uri: { Length: > 0 } uri } => uri,
                    { Path: { Length: > 0 } path } intent => $"{path} in {intent.Repository}",
                    { } ticket => $"{ticket.Provider}#{ticket.Id}",
                }).Append('\n');

                if (held.Legs.Count == 0)
                {
                    text.Append("No legs yet - ask the agent to draft some.\n");
                }

                foreach (var (leg, index) in held.Legs.Select((leg, index) => (leg, index)))
                {
                    text.Append(index == chosen ? "▸ " : "  ")
                        .Append($"{index + 1}. {leg.Subject} - {leg.WorkKind}")
                        .Append(leg.After is { } after ? $", after '{after}'" : "")
                        .Append('\n');
                }

                break;
        }

        if (notice is { Length: > 0 })
        {
            text.Append('\n').Append(notice).Append('\n');
        }

        var last = _drafts.LastResult(_draft);
        var stale = staleAgainst is not null && string.Equals(last ?? "", staleAgainst, StringComparison.Ordinal);
        text.Append('\n')
            .Append(stale
                ? "The agent's last result, from before your edit:"
                : "The agent's last result:")
            .Append('\n')
            .Append(last ?? "The agent has not called a planning tool yet.");

        return text.ToString();
    }

    /// <summary>The server: `gg itinerary tools --draft console`, under its own key (rule 2).</summary>
    /// <summary>The reader's three reads, by their qualified names under its key.</summary>
    private static IReadOnlyList<string> Reading(Gg.Local.IntentReader? reader) =>
        reader is { Key: { Length: > 0 } key }
            ? [.. new[] { ItemTool.Name, ItemTool.HistoryName, ItemTool.FieldsName }.Select(tool => $"mcp__{key}__{tool}")]
            : [];

    private static string ServerConfig(SelfInvocation self, string draft, Gg.Local.IntentReader? reader = null)
    {
        using var buffer = new MemoryStream();
        using (var json = new Utf8JsonWriter(buffer))
        {
            json.WriteStartObject();
            json.WriteStartObject("mcpServers");
            json.WriteStartObject(PlanningTool.Server);
            json.WriteString("command", self.Command);
            json.WriteStartArray("args");
            foreach (var argument in self.Under("itinerary", "tools", "--draft", draft))
            {
                json.WriteStringValue(argument);
            }

            json.WriteEndArray();
            json.WriteEndObject();

            // THE TRACKER'S READER, as the console itself runs it: its command and arguments, no
            // environment - it resolves its own credential.
            if (reader is { Key: { Length: > 0 } key } item)
            {
                json.WriteStartObject(key);
                json.WriteString("command", item.Command);
                json.WriteStartArray("args");
                foreach (var argument in item.Arguments)
                {
                    json.WriteStringValue(argument);
                }

                json.WriteEndArray();
                json.WriteEndObject();
            }

            json.WriteEndObject();
            json.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }
}
