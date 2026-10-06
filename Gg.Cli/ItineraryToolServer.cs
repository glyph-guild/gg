using System.Text;
using System.Text.Json;
using Gg.Client;
using Gg.Contracts;

namespace Gg.Cli;

/// <summary>
/// <c>gg itinerary tools</c>: a plan drafted with tools, from any Claude Code session. Slice
/// sixty-three, ADR-0038 build step 3.
/// </summary>
/// <remarks>
/// <para>
/// <b>The same hand-written protocol as its two siblings</b>, line-delimited JSON-RPC on stdio,
/// because gg publishes AOT and the SDK is reflection-shaped. Stdout is the protocol, so nothing
/// here may print.
/// </para>
/// <para>
/// <b>Its own key and its own process</b> (rule 9). It holds the person's session, and
/// <c>gg</c> is the runner's server - the reason <see cref="WorkItemToolServer"/> is separate
/// too: a credential stays in a server that does one thing.
/// </para>
/// <para>
/// <b>Every result is the whole draft</b> (rule 8), a refusal included, because outside the mux
/// the result is the only panel there is. The draft is a file the server reads fresh and writes
/// whole on every call (<see cref="ItineraryDrafts"/>), so a person's hand edit and an agent's
/// tool call are edits to the same plan.
/// </para>
/// </remarks>
public static class ItineraryToolServer
{
    /// <summary>The key a person registers this server under, and the name it gives itself.</summary>
    public const string Server = "gg-itinerary";

    public const string SetIntent = "set_intent";
    public const string DraftLeg = "draft_leg";
    public const string ReviseLeg = "revise_leg";
    public const string DropLeg = "drop_leg";
    public const string ShowPlan = "show_plan";

    /// <summary>
    /// The <c>claude mcp add</c> line that registers this server for <paramref name="draft"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b><c>gg</c> when it is on the path</b>, so a person who runs <c>gg update</c> is not left
    /// registered to a binary that moved. The absolute path otherwise, which is the one thing
    /// that certainly runs.
    /// </para>
    /// <para>
    /// <b>One key per draft</b>: two drafts are two registrations, and two under one key would
    /// replace each other. The default draft keeps the bare key.
    /// </para>
    /// </remarks>
    public static string Registration(string draft, string? ggOnPath, Gg.Local.SelfInvocation? self)
    {
        ArgumentNullException.ThrowIfNull(draft);

        var key = draft == "draft" ? Server : $"{Server}-{draft}";
        var verb = draft == "draft" ? "itinerary tools" : $"itinerary tools --draft {draft}";

        if (ggOnPath is not null)
        {
            return $"claude mcp add {key} -- gg {verb}";
        }

        if (self is null)
        {
            return "This gg cannot name its own executable, so there is no line to print. Put gg "
                 + $"on your PATH and run: claude mcp add {key} -- gg {verb}";
        }

        var lead = string.Join(' ', self.Arguments.Take(self.Arguments.Count - 2).Prepend(self.Command));
        return $"claude mcp add {key} -- {lead} {verb}";
    }

    public static async Task<int> RunAsync(
        TextReader input,
        TextWriter output,
        ItineraryDrafts drafts,
        string draft,
        IPlanningReads reads,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(drafts);
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(reads);

        // THE MENU, READ ONCE AT START (rule 6), for the planner the draft names. A menu that
        // cannot be read leaves the server answering - a dead server costs the agent its tools
        // for the session - and every tool call says why instead of taking a free string.
        var menu = await MenuAsync(drafts, draft, reads, cancellationToken);

        while (await input.ReadLineAsync(cancellationToken) is { } line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            JsonDocument message;
            try
            {
                message = JsonDocument.Parse(line);
            }
            catch (JsonException)
            {
                // SKIPPED, NEVER FATAL, as the siblings do: a line that will not parse has no id
                // to answer on, and a dead server loses the agent its tools for the session.
                continue;
            }

            using (message)
            {
                if (await AnswerAsync(message.RootElement, drafts, draft, menu, reads, cancellationToken)
                    is { } answer)
                {
                    await output.WriteLineAsync(answer);
                    await output.FlushAsync(cancellationToken);
                }
            }
        }

        return 0;
    }

    /// <summary>The menu, or why there is none.</summary>
    private sealed record Menu(ItineraryMenu? Offered, string? Unavailable);

    private static async Task<Menu> MenuAsync(
        ItineraryDrafts drafts, string draft, IPlanningReads reads, CancellationToken cancellationToken)
    {
        var planner = drafts.Read(draft) is DraftRead.Held { Draft: var held }
            ? held.Planner
            : PlanDraft.DefaultPlanner;

        try
        {
            var offered = await reads.MenuAsync(planner, cancellationToken);
            return offered.Refused is { Length: > 0 } refused
                ? new Menu(null, refused)
                : new Menu(offered, null);
        }
        catch (Exception unread) when (unread is not OperationCanceledException)
        {
            return new Menu(null,
                $"The menu of what a leg may name under '{planner}' could not be read, so nothing "
              + $"can be drafted until it can - restart this server once it answers. {unread.Message}");
        }
    }

    private static async Task<string?> AnswerAsync(
        JsonElement message, ItineraryDrafts drafts, string draft, Menu menu, IPlanningReads reads,
        CancellationToken cancellationToken)
    {
        var method = message.TryGetProperty("method", out var named) ? named.GetString() : null;

        if (!message.TryGetProperty("id", out var id) || id.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        return method switch
        {
            "initialize" => Initialized(id, message),
            "tools/list" => Listed(id, menu.Offered),
            "tools/call" => await CalledAsync(id, message, drafts, draft, menu, reads, cancellationToken),
            _ => Error(id, -32601,
                $"'{method}' is not a method this server has. It has initialize, tools/list "
              + "and tools/call."),
        };
    }

    private static string Initialized(JsonElement id, JsonElement message) =>
        Write(writer =>
        {
            Envelope(writer, id);
            writer.WriteStartObject("result");
            writer.WriteString("protocolVersion",
                message.TryGetProperty("params", out var parameters)
                && parameters.TryGetProperty("protocolVersion", out var version)
                && version.GetString() is { Length: > 0 } spoken
                    ? spoken
                    : "2024-11-05");
            writer.WriteStartObject("capabilities");
            writer.WriteStartObject("tools");
            writer.WriteEndObject();
            writer.WriteEndObject();
            writer.WriteStartObject("serverInfo");
            writer.WriteString("name", Server);
            writer.WriteString("version", GgVersions.Binary);
            writer.WriteEndObject();
            writer.WriteEndObject();
        });

    private static string Listed(JsonElement id, ItineraryMenu? menu) =>
        Write(writer =>
        {
            Envelope(writer, id);
            writer.WriteStartObject("result");
            writer.WriteStartArray("tools");

            Tool(writer, SetIntent,
                "Say what the plan is about: one of a sentence (text), a link (uri), a work item "
              + "(provider and id), or a file in a registered repository (repository, path, and "
              + "an optional ref). Replaces the intent the draft had. Answers with the whole draft.",
                [
                    ("text", "What the plan is about, in words.", null),
                    ("uri", "A link the plan is about.", null),
                    ("provider", "The tracker a work item is in, with id.", null),
                    ("id", "The work item's id in that tracker, with provider.", null),
                    ("repository", "The registered repository a file intent is in, with path.", null),
                    ("path", "The file's path inside that repository.", null),
                    ("ref", "The branch, tag or commit to read the file at; absent is the default branch.", null),
                ],
                []);

            Tool(writer, DraftLeg,
                "Add one leg: a piece of work a flight will do. The subject says which piece - "
              + "every leg needs one, and two legs of one kind need different subjects. "
              + "Answers with the whole draft.",
                LegFields(menu, includeSubject: true),
                ["subject", "work_kind", "reason"]);

            Tool(writer, ReviseLeg,
                "Change one leg, named by its subject (and of_kind when two legs share a subject). "
              + "Only the fields given change; an empty string clears after, repository, "
              + "environment or note. rename_to gives the leg a new subject, and legs that came "
              + "after it follow the rename. Answers with the whole draft.",
                [
                    ("subject", "The subject of the leg to change.", null),
                    ("of_kind", "Its work kind, when two legs share the subject.", null),
                    ("rename_to", "A new subject for the leg.", null),
                    .. LegFields(menu, includeSubject: false),
                ],
                ["subject"]);

            Tool(writer, DropLeg,
                "Remove one leg, named by its subject (and of_kind when two legs share a subject). "
              + "A leg another comes after is not dropped until that one is revised. Answers "
              + "with the whole draft.",
                [
                    ("subject", "The subject of the leg to remove.", null),
                    ("of_kind", "Its work kind, when two legs share the subject.", null),
                ],
                ["subject"]);

            Tool(writer, ShowPlan, "The whole draft, changing nothing.", [], []);

            writer.WriteEndArray();
            writer.WriteEndObject();
        });

    /// <summary>
    /// A leg's fields, the three choices as enums from the menu (ADR-0038 Decision 8).
    /// </summary>
    /// <remarks>
    /// <b>A choice the destination permits none of is left out</b>: no bound is no choice
    /// (<c>SelectionBound</c>), so the field could only ever be refused. Without a menu - it could
    /// not be read - the fields are listed without enums, and every call says why it cannot draft.
    /// </remarks>
    private static (string Name, string Description, IReadOnlyList<string>? Choices)[] LegFields(
        ItineraryMenu? menu, bool includeSubject) =>
    [
        .. includeSubject
            ? new (string, string, IReadOnlyList<string>?)[]
            {
                ("subject", "Which piece of work this leg is, in a few words.", null),
            }
            : [],
        ("work_kind", "The kind of flight that does this leg.", menu?.WorkKinds),
        ("reason", "Why this leg exists, for the person approving the plan.", null),
        ("after", "The subject of a leg this one must wait for.", null),
        .. menu is { Repositories.Count: 0 }
            ? []
            : new (string, string, IReadOnlyList<string>?)[]
            {
                ("repository", "The registered repository this leg works in, when not the plan's own.",
                    menu?.Repositories),
            },
        .. menu is { Environments.Count: 0 }
            ? []
            : new (string, string, IReadOnlyList<string>?)[]
            {
                ("environment", "The charted environment this leg runs in.", menu?.Environments),
            },
        ("note", "Advice for whoever flies this leg.", null),
    ];

    private static void Tool(
        Utf8JsonWriter writer, string name, string description,
        IReadOnlyList<(string Name, string Description, IReadOnlyList<string>? Choices)> fields,
        IReadOnlyList<string> required)
    {
        writer.WriteStartObject();
        writer.WriteString("name", name);
        writer.WriteString("description", description);
        writer.WriteStartObject("inputSchema");
        writer.WriteString("type", "object");
        writer.WriteStartObject("properties");
        foreach (var (field, about, choices) in fields)
        {
            writer.WriteStartObject(field);
            writer.WriteString("type", "string");
            writer.WriteString("description", about);
            if (choices is { Count: > 0 })
            {
                writer.WriteStartArray("enum");
                foreach (var choice in choices)
                {
                    writer.WriteStringValue(choice);
                }

                writer.WriteEndArray();
            }

            writer.WriteEndObject();
        }

        writer.WriteEndObject();
        writer.WriteStartArray("required");
        foreach (var field in required)
        {
            writer.WriteStringValue(field);
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
        writer.WriteEndObject();
    }

    private static async Task<string> CalledAsync(
        JsonElement id, JsonElement message, ItineraryDrafts drafts, string draft, Menu menu,
        IPlanningReads reads, CancellationToken cancellationToken)
    {
        var parameters = message.TryGetProperty("params", out var given) ? given : default;
        var name = parameters.ValueKind == JsonValueKind.Object
                && parameters.TryGetProperty("name", out var named)
            ? named.GetString()
            : null;
        var arguments = parameters.ValueKind == JsonValueKind.Object
                && parameters.TryGetProperty("arguments", out var supplied)
            ? supplied
            : default;

        if (menu.Offered is not { } offered)
        {
            return Content(id, menu.Unavailable + "\n\n" + Rendered(drafts, draft), isError: true);
        }

        DraftChange? change = name switch
        {
            SetIntent => drafts.Change(draft, d => Intended(d, arguments, offered)),
            DraftLeg => drafts.Change(draft, d => Drafted(d, arguments, offered)),
            ReviseLeg => drafts.Change(draft, d => Revised(d, arguments, offered)),
            DropLeg => drafts.Change(draft, d => Dropped(d, arguments, offered)),
            ShowPlan => null,
            _ => new DraftChange.Refused(
                $"'{name}' is not a tool this server has. It has {SetIntent}, {DraftLeg}, "
              + $"{ReviseLeg}, {DropLeg} and {ShowPlan}."),
        };

        // THE DRAFT THAT STANDS, read back after the change: written, or untouched by a refusal -
        // and what admission would do with it, from the check itself (rule 7).
        var preview = await PreviewAsync(drafts, draft, reads, cancellationToken);
        var shown = Rendered(drafts, draft, preview);

        return change is DraftChange.Refused { Diagnosis: var why }
            ? Content(id, why + "\n\n" + shown, isError: true)
            : Content(id, shown, isError: false);
    }

    private static DraftChange Intended(PlanDraft draft, JsonElement arguments, ItineraryMenu menu)
    {
        var repository = Argument(arguments, "repository");
        var path = Argument(arguments, "path");
        var text = Argument(arguments, "text");
        var uri = Argument(arguments, "uri");
        var provider = Argument(arguments, "provider");
        var item = Argument(arguments, "id");

        // THE DERIVATION gg fly AND A PLAN FILE BOTH USE, so a file beside anything else stays
        // on the intent and the contract refuses the pair as two payloads.
        var intent = repository is not null || path is not null
            ? FlightIntent.ForFile(repository ?? "", path ?? "", Argument(arguments, "ref")) with
            {
                Text = text, Uri = uri, Provider = provider, Id = item,
            }
            : FlightIntent.Of(text, uri, provider, item);

        return FlightIntent.Validate(intent) is { } refused
            ? DraftChange.Refuse(refused)
            : Checked(draft with { Intent = intent }, menu);
    }

    private static DraftChange Drafted(PlanDraft draft, JsonElement arguments, ItineraryMenu menu)
    {
        var leg = new FlightNomination
        {
            Subject = Argument(arguments, "subject"),
            WorkKind = Argument(arguments, "work_kind") ?? string.Empty,
            Reason = Argument(arguments, "reason") ?? string.Empty,
            After = Cleared(Argument(arguments, "after")),
            Repository = Cleared(Argument(arguments, "repository")),
            Environment = Cleared(Argument(arguments, "environment")),
            Note = Cleared(Argument(arguments, "note")),
        };

        return Checked(draft with { Legs = [.. draft.Legs, leg] }, menu);
    }

    private static DraftChange Revised(PlanDraft draft, JsonElement arguments, ItineraryMenu menu)
    {
        if (Found(draft, arguments) is not { } at)
        {
            return DraftChange.Refuse(Missing(draft, arguments));
        }

        var leg = draft.Legs[at];
        var renamed = Argument(arguments, "rename_to") is { Length: > 0 } to ? to : leg.Subject;

        var revised = leg with
        {
            Subject = renamed,
            WorkKind = Argument(arguments, "work_kind") ?? leg.WorkKind,
            Reason = Argument(arguments, "reason") ?? leg.Reason,
            After = Argument(arguments, "after") is { } after ? Cleared(after) : leg.After,
            Repository = Argument(arguments, "repository") is { } repository ? Cleared(repository) : leg.Repository,
            Environment = Argument(arguments, "environment") is { } environment ? Cleared(environment) : leg.Environment,
            Note = Argument(arguments, "note") is { } note ? Cleared(note) : leg.Note,
        };

        // A RENAME CARRIES TO THE LEGS AFTER IT, so it never strands an ordering.
        var legs = draft.Legs
            .Select((other, index) => index == at
                ? revised
                : string.Equals(other.After, leg.Subject, StringComparison.Ordinal)
                    ? other with { After = renamed }
                    : other)
            .ToList();

        return Checked(draft with { Legs = legs }, menu);
    }

    private static DraftChange Dropped(PlanDraft draft, JsonElement arguments, ItineraryMenu menu)
    {
        if (Found(draft, arguments) is not { } at)
        {
            return DraftChange.Refuse(Missing(draft, arguments));
        }

        var subject = draft.Legs[at].Subject;
        var waiting = draft.Legs
            .Where((other, index) => index != at
                && string.Equals(other.After, subject, StringComparison.Ordinal))
            .Select(other => $"'{other.Subject}'")
            .ToList();

        if (waiting.Count > 0)
        {
            return DraftChange.Refuse(
                $"'{subject}' is not dropped: {string.Join(" and ", waiting)} come after it, and "
              + "would be left waiting for a leg that no longer exists. Revise their 'after' "
              + "first, or drop them.");
        }

        return Checked(draft with { Legs = [.. draft.Legs.Where((_, index) => index != at)] }, menu);
    }

    /// <summary>
    /// The draft held to the contract's own leg rules, so nothing reaches the file that
    /// <c>gg itinerary check</c> would refuse for its shape.
    /// </summary>
    private static DraftChange Checked(PlanDraft draft, ItineraryMenu menu)
    {
        if (draft.Legs.Count > ItineraryDraft.MaxLegs)
        {
            return DraftChange.Refuse(
                $"A plan carries at most {ItineraryDraft.MaxLegs} legs, and this would be "
              + $"{draft.Legs.Count}.");
        }

        for (var i = 0; i < draft.Legs.Count; i++)
        {
            var leg = draft.Legs[i];

            if (string.IsNullOrWhiteSpace(leg.Subject))
            {
                return DraftChange.Refuse(
                    $"A leg ('{leg.WorkKind}') names no subject. Every leg says which piece of "
                  + "work it is - without one, every such leg is the same piece.");
            }

            if (FlightNomination.Validate(leg) is { } badLeg)
            {
                return DraftChange.Refuse($"'{leg.Subject}': {badLeg}");
            }

            // THE MENU, ENFORCED HERE TOO: not every client holds a tool call to its schema.
            if (Outside("work kind", leg.WorkKind, menu.WorkKinds) is { } kind)
            {
                return DraftChange.Refuse($"'{leg.Subject}': {kind}");
            }

            if (leg.Repository is { } repository
                && Outside("repository", repository, menu.Repositories) is { } where)
            {
                return DraftChange.Refuse($"'{leg.Subject}': {where}");
            }

            if (leg.Environment is { } environment
                && Outside("environment", environment, menu.Environments) is { } place)
            {
                return DraftChange.Refuse($"'{leg.Subject}': {place}");
            }

            if (leg.After is { } after
                && !draft.Legs.Any(other => !ReferenceEquals(other, leg)
                    && string.Equals(other.Subject, after, StringComparison.Ordinal)))
            {
                return DraftChange.Refuse(
                    $"'{leg.Subject}' comes after '{after}', and the draft has no leg about "
                  + $"'{after}'. It holds {Subjects(draft)}.");
            }

            for (var j = i + 1; j < draft.Legs.Count; j++)
            {
                if (string.Equals(leg.WorkKind, draft.Legs[j].WorkKind, StringComparison.Ordinal)
                    && string.Equals(leg.Subject, draft.Legs[j].Subject, StringComparison.Ordinal))
                {
                    return DraftChange.Refuse(
                        $"Two legs would both be '{leg.WorkKind}' about '{leg.Subject}', so they "
                      + "are one piece of work written twice. Give them subjects that say how "
                      + "they differ, or revise the one that is there.");
                }
            }
        }

        return new DraftChange.Written(draft);
    }

    private static string? Outside(string what, string chosen, IReadOnlyList<string> offered) =>
        offered.Contains(chosen, StringComparer.Ordinal)
            ? null
            : offered.Count == 0
                ? $"'{chosen}' is not a {what} this plan's destination lets a leg name - it permits none."
                : $"'{chosen}' is not a {what} this plan's destination lets a leg name. It permits: "
                + string.Join(", ", offered) + ".";

    /// <summary>The index of the leg the arguments name, or null when none or more than one does.</summary>
    private static int? Found(PlanDraft draft, JsonElement arguments)
    {
        var subject = Argument(arguments, "subject");
        var kind = Argument(arguments, "of_kind");

        var matches = draft.Legs
            .Select((leg, index) => (leg, index))
            .Where(m => string.Equals(m.leg.Subject, subject, StringComparison.Ordinal)
                     && (kind is null || string.Equals(m.leg.WorkKind, kind, StringComparison.Ordinal)))
            .ToList();

        return matches.Count == 1 ? matches[0].index : null;
    }

    private static string Missing(PlanDraft draft, JsonElement arguments)
    {
        var subject = Argument(arguments, "subject");
        var kinds = draft.Legs
            .Where(leg => string.Equals(leg.Subject, subject, StringComparison.Ordinal))
            .Select(leg => $"'{leg.WorkKind}'")
            .ToList();

        return kinds.Count > 1
            ? $"Legs of {string.Join(" and ", kinds)} are both about '{subject}'. Say which "
            + "with of_kind."
            : $"The draft has no leg about '{subject}'. It holds {Subjects(draft)}.";
    }

    private static string Subjects(PlanDraft draft) =>
        draft.Legs.Count == 0
            ? "no legs yet"
            : string.Join(", ", draft.Legs.Select(leg => $"'{leg.Subject}'"));

    /// <summary>What admission would do with the draft, or why that is not known.</summary>
    private sealed record Preview(ItineraryCheck? Check, string? Instead);

    /// <summary>
    /// The check, called on every result once the draft has an intent and a leg (rule 7).
    /// </summary>
    /// <remarks>
    /// <b>Never computed here</b>: a second implementation of admission is what ADR-0038
    /// Decision 9 forbids. <b>A check that fails keeps the draft</b> - it is a read about the
    /// plan, not part of the edit, so it is said beside the draft and the change stands.
    /// </remarks>
    private static async Task<Preview> PreviewAsync(
        ItineraryDrafts drafts, string draft, IPlanningReads reads, CancellationToken cancellationToken)
    {
        if (drafts.Read(draft) is not DraftRead.Held { Draft: var held })
        {
            return new Preview(null, null);
        }

        if (held.Intent is null || held.Legs.Count == 0)
        {
            return new Preview(null,
                "Preview: waits for "
              + (held.Intent is null && held.Legs.Count == 0 ? "an intent (set_intent) and a leg (draft_leg)"
                 : held.Intent is null ? "an intent (set_intent) - a plan about nothing is refused whole"
                 : "a leg (draft_leg)")
              + ", then every result shows what admission would do with each leg.");
        }

        try
        {
            return new Preview(await reads.CheckAsync(new ItineraryDraft
            {
                Planner = held.Planner,
                Intent = held.Intent,
                Legs = held.Legs,
            }, cancellationToken), null);
        }
        catch (Exception unread) when (unread is not OperationCanceledException)
        {
            return new Preview(null,
                $"Preview: the check could not be read, so what admission would do is not known "
              + $"yet. The draft above stands. {unread.Message}");
        }
    }

    /// <summary>The whole draft as text: the result is the only panel there is (rule 8).</summary>
    private static string Rendered(ItineraryDrafts drafts, string draft, Preview? preview = null)
    {
        var text = new StringBuilder();
        text.Append($"Draft '{draft}', kept at {drafts.PathOf(draft)}\n");

        if (drafts.Read(draft) is DraftRead.Unreadable { Diagnosis: var unreadable })
        {
            text.Append(unreadable).Append('\n');
            return text.ToString();
        }

        var held = ((DraftRead.Held)drafts.Read(draft)).Draft;
        text.Append($"Planner: {held.Planner}\n");

        text.Append("Intent: ").Append(held.Intent switch
        {
            null => "none yet - set_intent says what the plan is about.",
            { Kind: FlightIntentKinds.File } file =>
                $"the file {file.Path} in {file.Repository}"
              + (file.Ref is { } at ? $" at {at}" : " on its default branch"),
            { Kind: FlightIntentKinds.Ticket } ticket => $"work item {ticket.Provider}#{ticket.Id}",
            { Kind: FlightIntentKinds.Uri } link => $"the link {link.Uri}",
            { } words => words.Text,
        }).Append('\n');

        if (held.Legs.Count == 0)
        {
            text.Append("Legs: none yet - draft_leg adds one.\n");
            if (preview?.Instead is { } waiting)
            {
                text.Append(waiting).Append('\n');
            }

            return text.ToString();
        }

        text.Append($"Legs ({held.Legs.Count}):\n");
        foreach (var (leg, index) in held.Legs.Select((leg, index) => (leg, index)))
        {
            text.Append($"  {index + 1}. {leg.Subject} - {leg.WorkKind}");
            if (leg.After is { } after)
            {
                text.Append($", after '{after}'");
            }

            text.Append('\n');
            text.Append($"     reason: {leg.Reason}\n");
            if (leg.Repository is { } repository)
            {
                text.Append($"     repository: {repository}\n");
            }

            if (leg.Environment is { } environment)
            {
                text.Append($"     environment: {environment}\n");
            }

            if (leg.Note is { } note)
            {
                text.Append($"     note: {note}\n");
            }

            // THE VERDICT, UNDER ITS LEG, matched by kind and subject - the identity the check
            // judged it by. Verdict first, then admission's own sentence.
            if (preview?.Check?.Legs.FirstOrDefault(judged =>
                    string.Equals(judged.Subject, leg.Subject, StringComparison.Ordinal)
                    && string.Equals(judged.WorkKind, leg.WorkKind, StringComparison.Ordinal))
                is { } verdict)
            {
                text.Append($"     {verdict.Verdict}: {verdict.Reason}\n");
            }
        }

        if (preview?.Check?.Refused is { } whole)
        {
            text.Append($"The plan as a whole would be refused: {whole}\n");
        }

        if (preview?.Instead is { } instead)
        {
            text.Append(instead).Append('\n');
        }

        return text.ToString();
    }

    /// <summary>An argument as given, or null when absent.</summary>
    private static string? Argument(JsonElement arguments, string name) =>
        arguments.ValueKind == JsonValueKind.Object
        && arguments.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    /// <summary>An optional field: an empty string clears it.</summary>
    private static string? Cleared(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private static string Content(JsonElement id, string text, bool isError) =>
        Write(writer =>
        {
            Envelope(writer, id);
            writer.WriteStartObject("result");
            writer.WriteStartArray("content");
            writer.WriteStartObject();
            writer.WriteString("type", "text");
            writer.WriteString("text", text);
            writer.WriteEndObject();
            writer.WriteEndArray();
            writer.WriteBoolean("isError", isError);
            writer.WriteEndObject();
        });

    private static string Error(JsonElement id, int code, string message) =>
        Write(writer =>
        {
            Envelope(writer, id);
            writer.WriteStartObject("error");
            writer.WriteNumber("code", code);
            writer.WriteString("message", message);
            writer.WriteEndObject();
        });

    private static void Envelope(Utf8JsonWriter writer, JsonElement id)
    {
        writer.WriteStartObject();
        writer.WriteString("jsonrpc", "2.0");
        writer.WritePropertyName("id");
        id.WriteTo(writer);
    }

    private static string Write(Action<Utf8JsonWriter> body)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            body(writer);
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }
}
