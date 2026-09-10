using Gg.Local;
using System.Text;
using System.Text.Json;
using Gg.Client;
using Gg.Runner.Execution;

namespace Gg.Cli;

/// <summary>
/// The platform's own tool server: its tools, over line-delimited JSON-RPC on
/// standard input and output.
/// </summary>
/// <remarks>
/// <para>
/// <b>A structured channel, because prose is not an answer.</b> A classifier
/// has to hand back a VALUE - a work kind - and a closing summary that happens
/// to mention one is a sentence somebody could have written about anything. A
/// tool call is a thing the agent chose to make, in a shape the runner reads
/// mechanically, and it is a narrower thing to trust than a sentence the agent
/// was told to write. The same argument covers an agent saying it is stuck.
/// </para>
/// <para>
/// <b>ONE CHANNEL, FOUR TOOLS, granted on three different terms.</b> Two are
/// the output of a KIND of work and each has a move of its own, so an envelope
/// that declares neither grants neither: nominating a work kind, and proposing
/// a change to a work item. Asking for a decision is not a move at all and no
/// envelope may withhold it - one able to would be one that makes a stuck agent
/// silent. Submitting an intent is granted on almost no launch and declared on
/// every one. All four are listed here and every grant is decided in the one
/// place a grant is already decided - the launch's allow-list - because a
/// `tools/list` that varied by envelope would be a second place the same rule
/// lives, and the two would disagree the first time either was edited.
/// </para>
/// <para>
/// <b>STDOUT IS THE PROTOCOL.</b> Nothing here may narrate, log, or greet: one
/// stray line and the client sees a server that never initialized rather than a
/// tool that failed. This type therefore reaches no session store, no
/// configuration, and no control plane - it is a pure function of the lines it
/// is handed.
/// </para>
/// <para>
/// <b>It holds nothing open and needs nothing.</b> No credential, no session,
/// no round trip. That is what makes it safe to run as a child of a process the
/// threat model treats as compromised - an injected agent that reaches it can at
/// most record a request admission will refuse against a menu a person wrote.
/// </para>
/// <para>
/// <b>ONE TOOL WRITES A FILE, AND THAT SENTENCE USED TO SAY IT NEVER DID.</b>
/// <c>submit_intent</c> exists because a hosted agent has no transcript for gg
/// to read the call out of - the fleet path reads <c>tool_use</c> records from
/// <c>--output-format stream-json</c>, and an interactive session in a
/// pseudo-terminal produces none. So the value travels as a file between two gg
/// processes.
/// <br />What keeps that inside the threat model is that the PATH is not the
/// agent's to choose: it is handed to this server as an argument by the verb
/// that starts it, so the most an injected agent can do is put text where gg was
/// already going to look for text a person then reads. Nothing here reads the
/// environment, and nothing here writes anywhere it was not told to.
/// </para>
/// <para>
/// <b>Hand-written rather than an SDK.</b> <c>Gg.Runner</c> carries no package
/// references and this binary publishes AOT; the official server library is DI-
/// and reflection-shaped. What is needed is four methods over line-delimited
/// JSON - the shape the launch's own config writer already uses, and for the
/// same stated reason.
/// </para>
/// </remarks>
public static class PlatformToolServer
{
    /// <summary>What the agent needs decided. One argument, and it is required.</summary>
    private const string QuestionArgument = "question";

    private const string WorkKindArgument = "work_kind";
    private const string ReasonArgument = "reason";

    /// <summary>
    /// What the agent would tell whoever picks the work up. Optional, and the
    /// only argument on this tool that is.
    /// </summary>
    /// <remarks>
    /// <b>Offered, or it cannot be written.</b> The extractor reads it and the
    /// lease carries it to the next flight's prompt, and none of that happens if
    /// the schema never mentions it - an argument an agent is not offered is one
    /// nothing will ever produce.
    /// </remarks>
    private const string NoteArgument = "note";

    /// <summary>Where the work should run, when the destination permits a choice.</summary>
    private const string EnvironmentArgument = "environment";

    /// <summary>Which repository, when the destination permits a choice.</summary>
    private const string RepositoryArgument = "repository";

    /// <summary>What a proposal asks be done: one of <see cref="Gg.Contracts.WorkItemOperations"/>.</summary>
    private const string OperationArgument = "operation";

    /// <summary>Which work item, when the operation is about one that exists.</summary>
    private const string TargetArgument = "target";

    /// <summary>What the flight thinks the item is worth, in the rubric's terms.</summary>
    private const string ScoreArgument = "score";

    /// <summary>Which fields a `field` proposal would set, and to what.</summary>
    private const string FieldsArgument = "fields";

    /// <summary>
    /// The part of a proposal nothing here reads.
    /// </summary>
    /// <remarks>
    /// <b>Declared and never interpreted.</b> It exists so another agent on
    /// another day can re-evaluate what this one thought, and that only works if
    /// today's server takes it whole rather than validating it into the shape
    /// that made sense the week it was written. What the server does check is
    /// its size, because unbounded means a repository can arrive in a fact.
    /// </remarks>
    private const string DetailArgument = "detail";

    /// <summary>
    /// Serves until the input ends, and answers nothing else.
    /// </summary>
    /// <returns>
    /// Zero. A tool server's exit code is not a verdict on the work - the agent
    /// that launched it is long gone by the time anybody reads one.
    /// </returns>
    /// <param name="intentPath">
    /// Where a composing session's intent is to be written, or null when this
    /// server is not serving one - which is every fleet launch.
    /// </param>
    /// <param name="documentRoot">
    /// The working copy a drafted document lands in, or null when this server
    /// is not serving a drafting session - which is every fleet launch.
    /// </param>
    public static async Task<int> RunAsync(
        TextReader input,
        TextWriter output,
        string? intentPath = null,
        string? documentRoot = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(output);

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
                // SKIPPED, NEVER FATAL. A dead server loses the agent its tool
                // for the whole session, and there is no id to answer on - a
                // line that will not parse has no request to fail.
                continue;
            }

            using (message)
            {
                if (Answer(message.RootElement, intentPath, documentRoot) is { } answer)
                {
                    await output.WriteLineAsync(answer);
                    await output.FlushAsync(cancellationToken);
                }
            }
        }

        return 0;
    }

    /// <summary>
    /// The line to write back, or null where the protocol says to write none.
    /// </summary>
    private static string? Answer(
        JsonElement message, string? intentPath, string? documentRoot)
    {
        var method = message.TryGetProperty("method", out var named) ? named.GetString() : null;

        // A NOTIFICATION HAS NO ID AND TAKES NO RESPONSE. Answering one writes
        // a line the client cannot match to a request.
        if (!message.TryGetProperty("id", out var id) || id.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        return method switch
        {
            "initialize" => Initialized(id, message),
            "tools/list" => Listed(id),
            "tools/call" => Called(id, message, intentPath, documentRoot),

            // THE ID COMES BACK even on an error, or a client matching
            // responses to requests waits for ever.
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

            // ECHOED RATHER THAN DECLARED. The client has already said which
            // revision it speaks; claiming a different one would be this
            // server asserting something about a protocol it does not own.
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
            writer.WriteString("name", NominationTool.Server);
            // The binary's own version, the one `gg version` reports. The
            // client ignores it; a person reading a transcript does not.
            writer.WriteString("version", GgVersions.Binary);
            writer.WriteEndObject();

            writer.WriteEndObject();
        });

    private static string Listed(JsonElement id) =>
        Write(writer =>
        {
            Envelope(writer, id);
            writer.WriteStartObject("result");
            writer.WriteStartArray("tools");
            writer.WriteStartObject();

            writer.WriteString("name", NominationTool.Name);

            // THE PROMPT AN AGENT ACTUALLY READS. It says to call once, that
            // declining is a real answer, and that nominating grants nothing -
            // because an agent that thinks it has opened a flight stops
            // waiting for one, and an agent that thinks it must choose will
            // choose from an item that does not say.
            writer.WriteString("description",
                "Nominate the kind of work this item needs. Call it once with the kind you "
              + "choose and the reason, then stop and say what you nominated and why. "
              + "Nominating grants nothing and opens nothing: a person decides whether the "
              + "kind you name is one this work may become. If the item does not say enough "
              + "to choose, do NOT call this - say which question you could not answer and "
              + "stop. Declining is a real answer and it is not a failure.");

            writer.WriteStartObject("inputSchema");
            writer.WriteString("type", "object");

            writer.WriteStartObject("properties");
            writer.WriteStartObject(WorkKindArgument);
            writer.WriteString("type", "string");
            writer.WriteString("description", "One of the work kinds you were offered.");
            writer.WriteEndObject();
            writer.WriteStartObject(ReasonArgument);
            writer.WriteString("type", "string");
            writer.WriteString("description",
                "Why this kind and not the others, in your own words.");
            writer.WriteEndObject();
            // WRITTEN FOR THE SHAPE REAL NOTES TOOK. Three measured triage runs
            // each wrote a warning not to start coding, the evidence found, and
            // what to confirm with the reporter - so the description asks for
            // that rather than for a summary of the item, which the next agent
            // can already read for itself.
            // OFFERED HERE BECAUSE THE MENU IS IN THE PROMPT. Both are bounded
            // by the destination's may-select, which the prompt now lists - an
            // argument offered with no menu would be a field to guess at under
            // a rule that refuses rather than clamps.
            writer.WriteStartObject(EnvironmentArgument);
            writer.WriteString("type", "string");
            writer.WriteString("description",
                "Optional. One of the environments you were offered. Leave it out if the "
              + "work item does not say, or if none of them fits - naming one outside the "
              + "list opens nothing.");
            writer.WriteEndObject();
            writer.WriteStartObject(RepositoryArgument);
            writer.WriteString("type", "string");
            writer.WriteString("description",
                "Optional. One of the repositories you were offered, on the same terms.");
            writer.WriteEndObject();
            writer.WriteStartObject(NoteArgument);
            writer.WriteString("type", "string");
            writer.WriteString("description",
                "Optional. What you would tell whoever picks this up - what you found that "
              + "the item does not say, and what to check before starting. It is shown to "
              + "them as your words and grants nothing; leave it out if you have nothing to "
              + "add.");
            writer.WriteEndObject();
            writer.WriteEndObject();

            writer.WriteStartArray("required");
            writer.WriteStringValue(WorkKindArgument);
            writer.WriteStringValue(ReasonArgument);
            writer.WriteEndArray();

            writer.WriteEndObject();
            writer.WriteEndObject();

            // THE SECOND TOOL, and its description is the part that decides
            // whether it is ever used. An agent is otherwise being told to
            // complete a task by a system it cannot see, so it has to be told
            // in as many words that stopping to ask is a real answer - and
            // that it must not do a different piece of work instead, which is
            // the second-best-looking thing a stuck agent can do.
            writer.WriteStartObject();
            writer.WriteString("name", HelpTool.Name);
            writer.WriteString("description",
                "Ask for a decision you are not allowed to make: a question only a person "
              + "can answer, or two ways forward with nothing in the tree to choose between "
              + "them. Call it once with the question, then stop and say what you did and "
              + "what you were left with. Asking is not failing. Do not guess, and do not do "
              + "a different piece of work instead. This grants nothing and changes nothing: "
              + "a person reads the question and answers it, and the work comes back to you "
              + "with their answer.");

            writer.WriteStartObject("inputSchema");
            writer.WriteString("type", "object");
            writer.WriteStartObject("properties");
            writer.WriteStartObject(QuestionArgument);
            writer.WriteString("type", "string");
            writer.WriteString("description",
                "What you need decided, in your own words. Say what you were doing, what the "
              + "choices are, and what you could not tell from the work itself.");
            writer.WriteEndObject();
            writer.WriteEndObject();
            writer.WriteStartArray("required");
            writer.WriteStringValue(QuestionArgument);
            writer.WriteEndArray();
            writer.WriteEndObject();

            writer.WriteEndObject();

            // THE THIRD TOOL. Declared on every launch and granted on almost
            // none, which is the arrangement the note at the top of this file
            // argues for: the grant is decided in the launch's allow-list, so a
            // tools/list that varied by envelope would be a second place the
            // same rule lives.
            //
            // The description has to say the two things a composing agent
            // cannot work out for itself - that submitting is the END of its
            // job, and that submitting opens nothing. An agent that thinks it
            // has opened a flight stops waiting for one; an agent that does not
            // know it has finished keeps going and rewrites what it already
            // handed over.
            writer.WriteStartObject();
            writer.WriteString("name", IntentTool.Name);
            writer.WriteString("description",
                "Hand back the intent you have composed: the words that say what work should "
              + "happen, as somebody would have written them. Call it once when you and the "
              + "person you are working with are happy with it, then stop and say that you "
              + "submitted it. This opens nothing and grants nothing - a person reads what "
              + "you wrote and decides whether a flight is opened from it. Calling it again "
              + "replaces what you sent, so correcting yourself is fine; leaving without "
              + "calling it submits nothing at all.");

            writer.WriteStartObject("inputSchema");
            writer.WriteString("type", "object");
            writer.WriteStartObject("properties");
            writer.WriteStartObject(IntentTool.IntentArgument);
            writer.WriteString("type", "string");
            writer.WriteString("description",
                "The intent itself, in the words it should be recorded in. Not a summary of "
              + "your conversation and not a report on what you did - the thing a person "
              + "would have typed if they had written it themselves.");
            writer.WriteEndObject();
            writer.WriteEndObject();
            writer.WriteStartArray("required");
            writer.WriteStringValue(IntentTool.IntentArgument);
            writer.WriteEndArray();
            writer.WriteEndObject();

            writer.WriteEndObject();

            // THE FIFTH TOOL, and the second that writes a file. Its
            // description carries more weight than most: an agent handed
            // something called `submit_document` while looking at a governance
            // tree will assume calling it puts the document in force, and it
            // does not - it writes a draft into a working copy that a person
            // then reads and submits.
            writer.WriteStartObject();
            writer.WriteString("name", DocumentTool.Name);
            writer.WriteString("description",
                "Hand back an envelope document you have drafted, for one name in this "
              + "tenant's topology. It is written into the working copy beside the others, "
              + "where a person reads the change and decides whether to submit it - so this "
              + "applies nothing and grants nothing, and submitting it later opens a flight "
              + "that may wait for an approver. The document is checked before it is "
              + "written: if it does not read as the role you named, nothing is written and "
              + "you are told why, so fix it and call again. Calling it again replaces what "
              + "you wrote. "
                // THE POINTER, HERE BECAUSE THIS IS THE ONE THAT IS READ.
                // The tool list reaches the model before its first token, and
                // this is the description an agent reads when it decides to
                // write a document - which is exactly when not knowing the
                // rules costs something. An instruction anywhere else is one
                // it has to already be looking for.
              + $"BEFORE YOU DRAFT, call {AirspaceContextTool.Name}: these documents have "
              + "rules you cannot see from the file, and it shows you one that already "
              + "exists.");

            writer.WriteStartObject("inputSchema");
            writer.WriteString("type", "object");
            writer.WriteStartObject("properties");

            writer.WriteStartObject(DocumentTool.RoleArgument);
            writer.WriteString("type", "string");
            writer.WriteString("description",
                "What this document is: one of " + string.Join(", ", Gg.Contracts.Roles.All)
              + ". It decides which rules the document is read by, so naming the wrong one "
              + "is refused rather than guessed past.");
            writer.WriteEndObject();

            writer.WriteStartObject(DocumentTool.NameArgument);
            writer.WriteString("type", "string");
            writer.WriteString("description",
                "Which name in the topology this document is for - the file names in the "
              + "working copy are these names. Lower case, digits and hyphens; a name that "
              + "no path can carry is refused.");
            writer.WriteEndObject();

            writer.WriteStartObject(DocumentTool.DocumentArgument);
            writer.WriteString("type", "string");
            writer.WriteString("description",
                "The document itself, as YAML, in the form the working copy already uses. "
              + "Leave out `based-on:` - that line is a statement about which version of "
              + "the stream a change was made against, and gg states it rather than you.");
            writer.WriteEndObject();

            writer.WriteEndObject();
            writer.WriteStartArray("required");
            writer.WriteStringValue(DocumentTool.RoleArgument);
            writer.WriteStringValue(DocumentTool.NameArgument);
            writer.WriteStringValue(DocumentTool.DocumentArgument);
            writer.WriteEndArray();
            writer.WriteEndObject();

            writer.WriteEndObject();

            // THE SIXTH TOOL, AND THE ONLY ONE THAT ANSWERS RATHER THAN ACTS.
            // The argument for it is that a drafting session hands an agent a
            // directory and a tool and tells it nothing: no prompt, no
            // CLAUDE.md in the tree, and no example of a role the tenant has
            // no document for. What it needs to know is not in the working
            // copy and not in this repository either.
            //
            // A TOOL RATHER THAN THE SERVER'S `instructions`, which reach a
            // model with no call at all: instructions are the SERVER's, and
            // this one also serves nomination, decision and triage flights
            // that want no envelope doctrine. A tool costs nothing until it is
            // called, the call is in the transcript so reading the rules is
            // observable rather than assumed, and only a call can answer about
            // THIS tenant.
            writer.WriteStartObject();
            writer.WriteString("name", AirspaceContextTool.Name);
            writer.WriteString("description",
                "Read how this tenant's envelope documents work before you draft one. It "
              + "answers with the rules a document is read by - which you cannot work out "
              + "from the files - plus what this working copy holds and one existing "
              + "document in full. It changes nothing and takes no arguments. Call it "
              + "first: the rules decide whether what you write applies straight away or "
              + "waits for a person to approve it.");

            writer.WriteStartObject("inputSchema");
            writer.WriteString("type", "object");
            writer.WriteStartObject("properties");
            writer.WriteEndObject();
            writer.WriteEndObject();

            writer.WriteEndObject();

            // THE FOURTH TOOL, and the one that most needs its description
            // read. An agent given something called `propose_work_item` while
            // looking at a backlog will assume it changes the backlog - so the
            // description says three things it cannot work out for itself, and
            // the receipt says the first of them again on every call.
            //
            //   - proposing changes nothing in the tracker,
            //   - declining to propose is a real answer,
            //   - and a refusal is an ordinary outcome, not a wall to route
            //     around by doing the work some other way.
            //
            // The third is the one this feature adds to the nomination's
            // wording. A nomination is refused or it is not, and the flight is
            // over either way; a triage proposes a dozen things and expects
            // some of them back, and an agent that reads a refusal as a failure
            // will either retry it or stop.
            writer.WriteStartObject();
            writer.WriteString("name", WorkItemProposalTool.Name);
            writer.WriteString("description",
                "Propose a change to a work item: create one, update its text, set a field, "
              + "link it to another, or score it. Call it once for each change you are "
              + "proposing, then stop and say what you proposed and why. Proposing changes "
              + "nothing in the tracker and grants nothing - a person decides which of your "
              + "proposals are performed, and the platform performs the ones they admit. "
              + "Some of what you propose may be refused; that is an ordinary answer and not "
              + "a failure, so do not retry a refused proposal and do not look for another "
              + "way to make the change. If an item does not say enough to propose anything, "
              + "do NOT call this - say which question you could not answer. Declining is a "
              + "real answer.");

            writer.WriteStartObject("inputSchema");
            writer.WriteString("type", "object");
            writer.WriteStartObject("properties");

            // THE MENU IS THE CONTRACT'S, not a list typed here. The extractor
            // checks what came back against the same one and the control plane
            // writes conditions over it, so a second spelling would make an
            // operation proposable and unadmittable at once.
            writer.WriteStartObject(OperationArgument);
            writer.WriteString("type", "string");
            writer.WriteString("description",
                "What you are proposing be done to the item.");
            writer.WriteStartArray("enum");
            foreach (var operation in Gg.Contracts.WorkItemOperations.All)
            {
                writer.WriteStringValue(operation);
            }

            writer.WriteEndArray();
            writer.WriteEndObject();

            writer.WriteStartObject(TargetArgument);
            writer.WriteString("type", "string");
            writer.WriteString("description",
                "The id of the work item, as the tracker gave it to you. Leave it out only "
              + "for `create`, where the item does not exist yet.");
            writer.WriteEndObject();

            writer.WriteStartObject(ScoreArgument);
            writer.WriteString("type", "string");
            writer.WriteString("description",
                "The score, in whatever terms your rubric asks for - a level, a number, a "
              + "short phrase. Required for `score` and left out otherwise.");
            writer.WriteEndObject();

            // THE THIRD SPELLING OF ONE THING, which is why it is declared
            // here rather than left to the detail: the contract names these
            // members, the extractor reads them, and this offers them. A
            // schema that did not would leave an agent unable to make a
            // `field` proposal the contract accepts - which is exactly what
            // happened for one commit.
            writer.WriteStartObject(FieldsArgument);
            writer.WriteString("type", "array");
            writer.WriteString("description",
                "Required for `field` and left out otherwise. Each entry is a field to set: "
              + "`path` as the tracker spells it, and `value`. A person decided in advance "
              + "which paths may be written here, so naming one outside that list changes "
              + "nothing - and leaving a value blank is not how a field is cleared.");
            writer.WriteStartObject("items");
            writer.WriteString("type", "object");
            writer.WriteStartObject("properties");
            writer.WriteStartObject("path");
            writer.WriteString("type", "string");
            writer.WriteString("description", "The field's reference path.");
            writer.WriteEndObject();
            writer.WriteStartObject("value");
            writer.WriteString("type", "string");
            writer.WriteString("description", "What to set it to.");
            writer.WriteEndObject();
            writer.WriteEndObject();
            writer.WriteStartArray("required");
            writer.WriteStringValue("path");
            writer.WriteStringValue("value");
            writer.WriteEndArray();
            writer.WriteEndObject();
            writer.WriteEndObject();

            writer.WriteStartObject(ReasonArgument);
            writer.WriteString("type", "string");
            writer.WriteString("description",
                "Why you are proposing this, in your own words. It is what the person "
              + "deciding reads, so say what you found rather than what you did.");
            writer.WriteEndObject();

            // NO `properties` UNDER IT, and that absence is the declaration. A
            // shape here would be this week deciding what a reader in six
            // months may ask, which is the one thing this member exists to
            // avoid.
            writer.WriteStartObject(DetailArgument);
            writer.WriteString("type", "object");
            writer.WriteString("description",
                "Optional. Anything else about this proposal that somebody re-reading it "
              + "later would want - the rubric you scored against, what you considered and "
              + "ruled out, what you were unsure of. Nothing reads it now; it is kept whole "
              + "for whoever does.");
            writer.WriteEndObject();

            writer.WriteEndObject();

            writer.WriteStartArray("required");
            writer.WriteStringValue(OperationArgument);
            writer.WriteStringValue(ReasonArgument);
            writer.WriteEndArray();

            writer.WriteEndObject();
            writer.WriteEndObject();

            writer.WriteEndArray();
            writer.WriteEndObject();
        });

    private static string Called(
        JsonElement id, JsonElement message, string? intentPath, string? documentRoot)
    {
        var parameters = message.TryGetProperty("params", out var given) ? given : default;

        var arguments = parameters.ValueKind == JsonValueKind.Object
            && parameters.TryGetProperty("arguments", out var supplied)
            && supplied.ValueKind == JsonValueKind.Object
                ? supplied
                : default;

        var called = parameters.ValueKind == JsonValueKind.Object
            && parameters.TryGetProperty("name", out var name)
                ? name.GetString()
                : null;

        if (string.Equals(called, HelpTool.Name, StringComparison.Ordinal))
        {
            return Asked(id, arguments);
        }

        if (string.Equals(called, IntentTool.Name, StringComparison.Ordinal))
        {
            return Submitted(id, arguments, intentPath);
        }

        if (string.Equals(called, DocumentTool.Name, StringComparison.Ordinal))
        {
            return Drafted(id, arguments, documentRoot);
        }

        if (string.Equals(called, AirspaceContextTool.Name, StringComparison.Ordinal))
        {
            return Described(id, documentRoot);
        }

        if (string.Equals(called, WorkItemProposalTool.Name, StringComparison.Ordinal))
        {
            return Proposed(id, arguments);
        }

        // NOT AN UNKNOWN-TOOL ARM, deliberately. The nomination tool is what
        // this server answered before there were two, and a call with no name
        // at all is still that one - which keeps every client written against
        // the one-tool server working rather than making its calls vanish.
        var workKind = Text(arguments, WorkKindArgument);
        var reason = Text(arguments, ReasonArgument);

        // AN ERROR RESULT RATHER THAN A PROTOCOL ERROR. The call reached the
        // tool and the tool refused it, which is something the agent can read
        // and fix - and something the extractor must not read as a nomination,
        // because half a nomination is a value the runner would have to invent
        // the rest of.
        if (workKind is null || reason is null)
        {
            return Content(id, isError: true,
                $"Refused: a nomination needs both '{WorkKindArgument}' and "
              + $"'{ReasonArgument}'. Nothing was recorded.");
        }

        // THE NOTE IS TAKEN OR REFUSED, never trimmed into shape. A blank one is
        // a caller that produced a field instead of leaving it out, and a note
        // past the bound is an analysis - both are things the agent can read
        // this and fix, which a silent truncation is not.
        // A NAME, NEVER PROSE, and the bound is the work kind's rather than the
        // reason's - admission matches these exactly against a menu.
        foreach (var (what, selected) in ((string, string?)[])
            [(EnvironmentArgument, Text(arguments, EnvironmentArgument)),
             (RepositoryArgument, Text(arguments, RepositoryArgument))])
        {
            if (selected is not null
                && selected.Length > Gg.Contracts.FlightNomination.MaxWorkKind)
            {
                return Content(id, isError: true,
                    $"Refused: a nominated {what} is at most "
                  + $"{Gg.Contracts.FlightNomination.MaxWorkKind} characters and this one is "
                  + $"{selected.Length}. It is a name, not a sentence. Nothing was recorded.");
            }
        }

        var note = Text(arguments, NoteArgument);
        if (note is not null && note.Length > Gg.Contracts.FlightNomination.MaxNote)
        {
            return Content(id, isError: true,
                $"Refused: a note is at most {Gg.Contracts.FlightNomination.MaxNote} characters "
              + $"and this one is {note.Length}. Nothing was recorded - shorten it and call "
              + "again, or leave it out.");
        }

        // ECHOED BACK IN CANONICAL FORM, so an agent can see what was taken
        // rather than assume its own spelling survived.
        return Content(id, isError: false,
            $"Recorded: work kind '{workKind}'. This grants nothing and opens nothing - a "
          + "person decides whether a flight of that kind is opened. Your part is done: stop "
          + "now and say what you nominated and why.");
    }

    /// <summary>
    /// Takes a proposed change to a work item, checks it is whole, and answers.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>It performs nothing, and the receipt says so every time.</b> The
    /// record of what was proposed is the tool CALL, which the runner reads out
    /// of the transcript - so this method's whole job is to refuse what a
    /// reader could not use and to tell the agent what was taken. Nothing is
    /// written, nothing is sent, and no credential is within reach of the
    /// process this runs in.
    /// </para>
    /// <para>
    /// <b>Structural completeness only, never policy.</b> An update naming no
    /// target is half a proposal - a value the runner would have to invent the
    /// rest of - and that is the same argument the nomination refuses a missing
    /// reason under. What this must NOT do is refuse an operation somebody
    /// might not want performed: that is admission's answer, against a menu a
    /// person wrote, and taking it here would move a refusal from a place that
    /// records one to a place that leaves nothing behind.
    /// </para>
    /// </remarks>
    private static string Proposed(JsonElement id, JsonElement arguments)
    {
        var operation = Text(arguments, OperationArgument);
        var reason = Text(arguments, ReasonArgument);

        if (operation is null || reason is null)
        {
            return Content(id, isError: true,
                $"Refused: a proposal needs both '{OperationArgument}' and "
              + $"'{ReasonArgument}'. Nothing was recorded.");
        }

        // AGAINST THE CONTRACT'S MENU, not a list here. An operation nobody
        // declared is not a proposal the control plane could admit or refuse -
        // it is one nothing downstream can read, which is worse than either.
        if (!Gg.Contracts.WorkItemOperations.All.Contains(operation, StringComparer.Ordinal))
        {
            return Content(id, isError: true,
                $"Refused: '{operation}' is not an operation this platform has. It has "
              + $"{string.Join(", ", Gg.Contracts.WorkItemOperations.All)}. Nothing was recorded.");
        }

        var target = Text(arguments, TargetArgument);

        // CREATE IS THE EXCEPTION AND IT IS THE ONLY ONE. An item that does not
        // exist has no id; every other operation is ABOUT an item, and one that
        // does not say which is a change the runner would have to guess the
        // subject of.
        if (target is null && !string.Equals(operation, Gg.Contracts.WorkItemOperations.Create, StringComparison.Ordinal))
        {
            return Content(id, isError: true,
                $"Refused: '{operation}' is about a work item and this one names none. Give "
              + $"'{TargetArgument}' as the tracker's own id, or propose "
              + $"'{Gg.Contracts.WorkItemOperations.Create}' if the item does not exist yet. Nothing was "
              + "recorded.");
        }

        // BUILT HERE AND JUDGED BY THE CONTRACT, like everything else on this
        // path: one definition of a whole proposal, read by the server before
        // it answers and by the extractor before it ships.
        var edits = new List<Gg.Contracts.WorkItemFieldEdit>();

        if (arguments.ValueKind == JsonValueKind.Object
            && arguments.TryGetProperty(FieldsArgument, out var fields)
            && fields.ValueKind == JsonValueKind.Array)
        {
            foreach (var field in fields.EnumerateArray())
            {
                if (field.ValueKind != JsonValueKind.Object)
                {
                    return Content(id, isError: true,
                        $"Refused: every entry in '{FieldsArgument}' is an object with a "
                      + "path and a value. Nothing was recorded.");
                }

                edits.Add(new Gg.Contracts.WorkItemFieldEdit
                {
                    Path = Text(field, "path") ?? "",
                    Value = Text(field, "value") ?? "",
                });
            }
        }

        var score = Text(arguments, ScoreArgument);

        if (score is null && string.Equals(operation, Gg.Contracts.WorkItemOperations.Score, StringComparison.Ordinal))
        {
            return Content(id, isError: true,
                $"Refused: a '{Gg.Contracts.WorkItemOperations.Score}' proposal with no '{ScoreArgument}' "
              + "says an item should be scored without saying what to. Nothing was recorded.");
        }

        // BOUNDED, NEVER TRIMMED, the note's rule one fact over: a silent
        // truncation hands the next reader a value that looks whole.
        foreach (var (what, given, bound) in ((string, string?, int)[])
            [(TargetArgument, target, Gg.Contracts.WorkItemProposalLimits.MaxTarget),
             (ScoreArgument, score, Gg.Contracts.WorkItemProposalLimits.MaxScore),
             (ReasonArgument, reason, Gg.Contracts.WorkItemProposalLimits.MaxReason)])
        {
            if (given is not null && given.Length > bound)
            {
                return Content(id, isError: true,
                    $"Refused: '{what}' is at most {bound} characters and this one is "
                  + $"{given.Length}. Nothing was recorded.");
            }
        }

        // THE ONE MEASUREMENT TAKEN OF SOMETHING NOBODY HERE READS. Its shape is
        // the agent's and stays the agent's; its size is not, because the facts
        // of one flight travel together in one batch.
        if (arguments.ValueKind == JsonValueKind.Object
            && arguments.TryGetProperty(DetailArgument, out var detail)
            && detail.ValueKind != JsonValueKind.Null)
        {
            if (detail.ValueKind != JsonValueKind.Object)
            {
                return Content(id, isError: true,
                    $"Refused: '{DetailArgument}' is an object - whatever a later reader "
                  + "would want, in fields it can find. Nothing was recorded.");
            }

            var written = detail.GetRawText().Length;
            if (written > Gg.Contracts.WorkItemProposalLimits.MaxDetail)
            {
                return Content(id, isError: true,
                    $"Refused: '{DetailArgument}' is at most "
                  + $"{Gg.Contracts.WorkItemProposalLimits.MaxDetail} characters and this one is "
                  + $"{written}. Nothing was recorded.");
            }
        }

        // ONE DEFINITION OF A WHOLE PROPOSAL, and this is where the server
        // reads it rather than growing a second. The checks above are the ones
        // whose wording an agent can act on directly; everything the CONTRACT
        // says about shape - a field proposal that sets nothing, an edit with
        // a blank value, edits on an operation that does not set fields - is
        // answered by the contract, so the server and the extractor cannot
        // come to different conclusions about the same call.
        var proposed = new Gg.Contracts.WorkItemProposal
        {
            Operation = operation,
            Reason = reason,
            Target = target,
            Score = score,
            Fields = edits.Count > 0 ? edits : null,
        };

        if (Gg.Contracts.WorkItemProposal.Validate(proposed) is { } refused)
        {
            return Content(id, isError: true, $"Refused: {refused} Nothing was recorded.");
        }

        // ECHOED IN CANONICAL FORM AND SAYING WHAT DID NOT HAPPEN. The
        // description is read once at the top of a session; this is read after
        // every call, which is where an agent decides whether it is done.
        return Content(id, isError: false,
            $"Recorded: proposed to {operation} "
          + (target is null ? "a new work item" : $"work item {target}")
          + (score is null ? "" : $", scoring it '{score}'")
          + ". Nothing has been changed in the tracker: a person decides which proposals are "
          + "performed. Propose the next change, or stop and say what you proposed and why.");
    }

    /// <summary>
    /// Takes the composed intent, writes it where gg is looking, and answers.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>WRITTEN BY RENAME, and that is not tidiness.</b> gg watches this path
    /// so it can tell the person their intent has landed - which means it can
    /// read the file at any moment, including halfway through a write. A write
    /// in place would hand somebody a flight opened with a truncated sentence.
    /// A rename is atomic on the same filesystem, so gg sees either nothing or
    /// the whole thing. The same lesson as installing gg's own binary.
    /// </para>
    /// <para>
    /// <b>Refused rather than silent when there is nowhere to write.</b> That is
    /// the ordinary case rather than an error: every fleet launch runs this
    /// server with no composing session. An agent that cannot submit must not
    /// look like one that chose not to.
    /// </para>
    /// </remarks>

    /// <summary>
    /// Takes a drafted document, validates it, and writes it into the working
    /// copy.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Validated before it lands, and the diagnosis is the answer.</b>
    /// <c>EnvelopeCommands.Validate</c> contacts nothing, so it is safe inside
    /// a server that is a pure function of the lines it is handed - and its
    /// refusal is what teaches an agent the schema. Nothing is written when
    /// nothing parsed: a half-written document is one the next diff reports as
    /// unreadable and every apply stops on.
    /// </para>
    /// <para>
    /// <b>The path comes from the name, through the table pull renders
    /// with.</b> So a drafted document sits exactly where a pulled one would,
    /// and diff compares them without knowing which wrote it. Nothing about the
    /// layout is the agent's to choose.
    /// </para>
    /// <para>
    /// <b>And the precondition is preserved, never taken from the call.</b>
    /// A pulled file carries <c>based-on: pci@v4</c>, which is what makes apply
    /// refuse when somebody else's amendment landed first. ADR-0016 § 4:
    /// <i>"a precondition about the stream, honored because the applier states
    /// it"</i> - and the applier is gg, not the agent. A version an agent chose
    /// is a claim about a stream it cannot have read, and a draft that cleared
    /// the line would turn the next apply into a blind overwrite of a
    /// colleague's work.
    /// </para>
    /// </remarks>
    /// <summary>
    /// Says how this tenant's documents are read, and what its tree holds.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>EVERY CLAIM HERE IS SOURCED FROM THE TYPE THAT ENFORCES IT.</b> The
    /// one-key rule is <c>EnvelopeNarrowing</c>'s shape, whose own remark is
    /// <i>"What this layer adds. Never what it changes - there is no such
    /// member."</i>; the direction rules are
    /// <c>EnvelopeDirection.Obligations</c>, whose comment says additions
    /// tighten and that a changed body is <i>"a DIFFERENT gate, not a tighter
    /// one"</i>; and <c>based-on:</c> being a precondition rather than
    /// provenance is <c>EnvelopeYaml</c>'s. A second wording of any of them
    /// here would be a second thing to keep in agreement, and the one that
    /// drifts is the one nobody reads.
    /// </para>
    /// <para>
    /// <b>The roles and their paths are computed, not typed.</b>
    /// <c>Roles.All</c> and <c>AirspaceNames.PathFor</c> answer both, so a role
    /// added to the vocabulary appears here without anybody remembering to add
    /// it - and one with nowhere to live in the tree says so rather than being
    /// quietly listed.
    /// </para>
    /// <para>
    /// <b>An empty tree is answered, not refused.</b> The rules are true
    /// whether or not anything has been pulled, and the one situation where an
    /// agent certainly does not know them is the one where nothing is there to
    /// read. Refusing would leave it with the doctrine nowhere and a directory
    /// it cannot interpret.
    /// </para>
    /// <para>
    /// <b>No working copy IS refused, though</b>, for the reason
    /// <c>Drafted</c> refuses: the root is set only for a drafting launch, so
    /// its absence means this is a flight that never asked, and
    /// <c>TheProposalToolActsOnNothingTests</c> states why what an agent can
    /// reach through this server is the whole question.
    /// </para>
    /// </remarks>
    private static string Described(JsonElement id, string? documentRoot)
    {
        if (string.IsNullOrEmpty(documentRoot))
        {
            return Content(id, isError: true,
                "Refused: this session has no airspace working copy, so there is nothing "
              + "to describe. This tool is for a drafting session.");
        }

        var said = new StringBuilder();

        said.AppendLine(
            "HOW THIS TENANT'S ENVELOPE DOCUMENTS WORK, and what its working copy holds.");
        said.AppendLine();
        said.AppendLine(
            "WHAT THESE FILES ARE. A rendering of the stream, not the record. Nothing you "
          + "write here applies anything: a person reads the change and decides whether to "
          + "submit it, and submitting opens a flight that may wait for an approver.");
        said.AppendLine();

        // FROM THE VOCABULARY AND THE PATH RULE, so a role added to either
        // shows up here without anybody remembering this file exists.
        said.AppendLine(
            "THE ROLES. A document's role decides which rules it is read by, and the role "
          + "comes from WHERE THE FILE SITS rather than from what the document looks like - "
          + "so a complete envelope saved as a narrowing is refused rather than guessed "
          + "past.");

        foreach (var role in Gg.Contracts.Roles.All)
        {
            string at;
            try
            {
                at = $"{Gg.Client.AirspaceTree.Directory}/"
                   + Gg.Contracts.AirspaceNames.PathFor(role, "<name>");
            }
            catch (ArgumentException)
            {
                // A ROLE WITH NOWHERE TO LIVE says so rather than being listed
                // as though a document could be written for it.
                at = "(no place in the rendered tree)";
            }

            said.AppendLine($"  {role,-10} {at}");
        }

        said.AppendLine();
        said.AppendLine(
            "A NARROWING HAS ONE KEY: `obligations`. There is no member for changing or "
          + "removing what a layer above declared - that is enforced by the document's "
          + "shape, not by a check. At least one obligation is required.");
        said.AppendLine();
        said.AppendLine(
            "WHAT YOU WRITE DECIDES WHETHER IT LANDS. Adding an obligation TIGHTENS: it "
          + "applies and mints a version. Removing one WIDENS, because obligations union - "
          + "adding constrains anyone, and the beneficiary owns removal. Changing an "
          + "obligation's body WIDENS too: a changed body is a different gate, not a "
          + "tighter one. A widening is not a failure and not a refusal; it opens a flight "
          + "that waits for an approver.");
        said.AppendLine();
        said.AppendLine(
            "LEAVE OUT `based-on:`. It is a precondition the applier states, honoured at "
          + "apply and then gone - not provenance you author. gg writes that line, and "
          + "keeps the one already in the file when you submit.");
        said.AppendLine();

        Holdings(said, documentRoot);

        return Content(id, isError: false, said.ToString().TrimEnd());
    }

    /// <summary>What is in the working copy, and one document in full.</summary>
    /// <remarks>
    /// <b>The example is the half no wording can replace.</b> A description can
    /// say what a narrowing is; only the tree can show one this tenant wrote.
    /// A narrowing is preferred where there is one, because it is the role
    /// somebody is most often drafting and the one whose shape is least like
    /// the root document beside it.
    /// </remarks>
    private static void Holdings(StringBuilder said, string documentRoot)
    {
        Gg.Client.TreeRead tree;
        try
        {
            tree = Gg.Client.AirspaceTree.Read(documentRoot);
        }
        catch (Exception unreadable) when (
            unreadable is IOException or UnauthorizedAccessException)
        {
            // SAID, NOT THROWN, for the reason every refusal here is: a throw
            // kills the server and takes the agent's other tools with it.
            said.AppendLine($"THE WORKING COPY could not be read ({unreadable.Message}).");
            return;
        }

        if (!tree.Present || tree.Documents.Count == 0)
        {
            said.AppendLine(
                "THIS WORKING COPY HOLDS NOTHING YET. Nothing has been pulled into it, so "
              + "there are no documents to read and no example to follow. A pull renders "
              + "the tenant's airspace here; everything above is true either way.");
            return;
        }

        said.AppendLine($"WHAT THIS WORKING COPY HOLDS ({tree.Documents.Count}):");

        foreach (var document in tree.Documents)
        {
            var basis = document.BasedOn is { Length: > 0 } version
                ? $"  based on {version}"
                : "  never applied";

            said.AppendLine($"  {document.Role,-10} {document.Name,-24} {document.Path}{basis}");
        }

        foreach (var broken in tree.Unreadable)
        {
            // NAMED, because one unreadable file refuses the whole apply - so
            // an agent drafting beside it should know before it spends a turn.
            said.AppendLine($"  UNREADABLE {broken.Path}");
        }

        var example = tree.Documents
            .FirstOrDefault(d => string.Equals(
                d.Role, Gg.Contracts.Roles.Narrowing, StringComparison.Ordinal))
            ?? tree.Documents[0];

        string text;
        try
        {
            text = File.ReadAllText(Path.Combine(
                documentRoot, Path.Combine(example.Path.Split('/'))));
        }
        catch (Exception unreadable) when (
            unreadable is IOException or UnauthorizedAccessException)
        {
            return;
        }

        // CAPPED, because a tenant floor can be long and an example that fills
        // the context is worse than a shorter one. Said when it happens, so
        // nobody reads a truncated document as a complete one.
        const int Most = 4000;
        var shown = text.Length > Most ? text[..Most] + "\n... (truncated)" : text;

        said.AppendLine();
        said.AppendLine($"ONE OF THEM IN FULL, {example.Path} - the form to follow, except "
                      + "for the based-on line, which is gg's:");
        said.AppendLine();
        said.AppendLine(shown.TrimEnd());
    }

    private static string Drafted(JsonElement id, JsonElement arguments, string? documentRoot)
    {
        if (string.IsNullOrEmpty(documentRoot))
        {
            return Content(id, isError: true,
                "Refused: this session has no working copy, so there is nowhere for a "
              + "document to go. Nothing was written. Say what you would have submitted and "
              + "stop.");
        }

        var role = Text(arguments, DocumentTool.RoleArgument);
        var name = Text(arguments, DocumentTool.NameArgument);
        var document = Text(arguments, DocumentTool.DocumentArgument);

        if (role is null || name is null || document is null)
        {
            return Content(id, isError: true,
                $"Refused: a document needs all three of '{DocumentTool.RoleArgument}', "
              + $"'{DocumentTool.NameArgument}' and '{DocumentTool.DocumentArgument}'. "
              + "Nothing was written.");
        }

        if (Gg.Contracts.AirspaceNames.Invalid(name) is { } malformed)
        {
            return Content(id, isError: true, "Refused: " + malformed + " Nothing was written.");
        }

        // ROOT IGNORES THE NAME, so this has to not. PathFor answers root.yaml
        // for the root role whatever name it is handed, which would let a
        // mismatched pair overwrite the tenant floor while naming something
        // else - the one document whose loss nothing else constrains.
        if (string.Equals(role, Gg.Contracts.Roles.Root, StringComparison.Ordinal)
            && !string.Equals(name, Gg.Contracts.Roles.Root, StringComparison.Ordinal))
        {
            return Content(id, isError: true,
                $"Refused: the root role has one document and it is called root, not "
              + $"'{name}'. Nothing was written.");
        }

        string relative;
        try
        {
            relative = $"{Gg.Client.AirspaceTree.Directory}/"
                     + Gg.Contracts.AirspaceNames.PathFor(role, name);
        }
        catch (ArgumentException)
        {
            // THE ROLE DECIDES WHICH RULES A DOCUMENT IS READ BY, so an unknown
            // one cannot be guessed past - and PathFor throws rather than
            // inventing a directory, which is the answer this turns into a
            // sentence.
            return Content(id, isError: true,
                $"Refused: '{role}' is not a role a document can be applied to. Nothing was "
              + "written.");
        }

        // VALIDATED AGAINST THE PATH, not against the text's shape. Location
        // first is what refuses a complete envelope submitted as a narrowing -
        // which is what an agent gets by copying root.yaml to start from.
        if (Gg.Client.EnvelopeCommands.Validate(document, relative)
                is Gg.Client.VerbResult.EnvelopeValidated read
            && read.Value.Diagnosis is { Length: > 0 } wrong)
        {
            return Content(id, isError: true,
                "Refused: " + wrong + " Nothing was written - fix it and submit again.");
        }

        var at = Path.Combine(documentRoot, Path.Combine(relative.Split('/')));

        try
        {
            var beside = Path.GetDirectoryName(at);
            if (!string.IsNullOrEmpty(beside))
            {
                Directory.CreateDirectory(beside);
            }

            var text = Precondition(at) + WithoutPrecondition(document);

            // The temp name sits in the SAME directory, because a rename across
            // filesystems is a copy and a copy is not atomic - so a reader
            // between the two sees a whole document or the old one, never half.
            var partial = at + ".writing";
            File.WriteAllText(partial, text);
            File.Move(partial, at, overwrite: true);
        }
        catch (Exception failure) when (
            failure is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            // SAID, NOT THROWN. A throw here kills the server and takes the
            // agent's other tools with it for the rest of the session, over a
            // failure that belongs to one call.
            return Content(id, isError: true,
                $"Refused: the document could not be written ({failure.Message}). Nothing was "
              + "written - say what you would have submitted and stop.");
        }

        return Content(id, isError: false,
            $"Written to {relative}. This applies nothing: a person reads the change and "
          + "decides whether to submit it, and submitting it opens a flight that may wait at "
          + "a gate. Calling this again replaces what you wrote.");
    }

    /// <summary>
    /// The <c>based-on:</c> line the working copy already holds, or nothing.
    /// </summary>
    /// <remarks>
    /// Read as the first line, which is where pull writes it and the only place
    /// it means what it says. Absent for a document nothing has pulled, because
    /// genesis has no predecessor and inventing one would refuse the first
    /// apply of a document that never had a version.
    /// </remarks>
    private static string Precondition(string at)
    {
        try
        {
            if (!File.Exists(at))
            {
                return "";
            }

            using var reader = new StreamReader(at);
            var first = reader.ReadLine();

            return first is not null
                && first.StartsWith("based-on:", StringComparison.Ordinal)
                    ? first + "\n"
                    : "";
        }
        catch (Exception unreadable) when (
            unreadable is IOException or UnauthorizedAccessException)
        {
            // A FILE THAT WILL NOT READ LOSES ITS PRECONDITION, which makes the
            // apply state none and the control plane accept without one. That
            // is the safe direction only because the write below will fail for
            // the same reason a moment later.
            return "";
        }
    }

    /// <summary>The document without any precondition the caller sent.</summary>
    /// <remarks>
    /// Dropped rather than refused, because an agent that pulled the line
    /// through from what it read is doing the reasonable thing - it just has no
    /// standing to state it. What it says about the document is kept; what it
    /// says about the stream is not.
    /// </remarks>
    private static string WithoutPrecondition(string document) =>
        document.StartsWith("based-on:", StringComparison.Ordinal)
            ? document[(document.IndexOf('\n', StringComparison.Ordinal) + 1)..]
            : document;

    private static string Submitted(JsonElement id, JsonElement arguments, string? intentPath)
    {
        if (string.IsNullOrEmpty(intentPath))
        {
            return Content(id, isError: true,
                "Refused: this session has nowhere to record an intent, so there is nothing "
              + "for this tool to do here. Nothing was recorded. Say what you would have "
              + "submitted and stop.");
        }

        var intent = Text(arguments, IntentTool.IntentArgument);

        // BORROWED FROM FlightIntent.Validate RATHER THAN INVENTED. An intent of
        // kind text whose text is blank is already refused on the wire, so
        // taking one here would only move the refusal somewhere less useful -
        // and a flight opened with nothing in it is worse than no flight.
        if (intent is null)
        {
            return Content(id, isError: true,
                $"Refused: an intent needs words in it. Give '{IntentTool.IntentArgument}' the "
              + "text that says what work should happen. Nothing was recorded.");
        }

        // NO LENGTH BOUND, deliberately, and it is worth saying why: the editor
        // path has none either. Whatever a person could have typed into $EDITOR
        // is what an agent may compose here, and inventing a ceiling on one of
        // the two would make the same intent acceptable or not depending on how
        // it was written.
        try
        {
            var beside = Path.GetDirectoryName(intentPath);
            if (!string.IsNullOrEmpty(beside))
            {
                Directory.CreateDirectory(beside);
            }

            // The temp name sits in the SAME directory, because a rename across
            // filesystems is a copy and a copy is not atomic.
            var partial = intentPath + ".partial";
            File.WriteAllText(partial, intent);
            File.Move(partial, intentPath, overwrite: true);
        }
        catch (Exception failure) when (
            failure is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            // SAID, NOT THROWN. A throw here kills the server and takes the
            // agent's other two tools with it for the rest of the session, over
            // a failure that belongs to one call.
            return Content(id, isError: true,
                $"Refused: the intent could not be written ({failure.Message}). Nothing was "
              + "recorded - say what you would have submitted and stop.");
        }

        return Content(id, isError: false,
            "Recorded. This opens nothing and grants nothing - a person reads what you wrote "
          + "and decides whether a flight is opened from it. Your part is done: stop now and "
          + "say that you submitted it.");
    }

    /// <summary>
    /// Takes a question and returns a receipt. It does not wait.
    /// </summary>
    /// <remarks>
    /// <b>Rule 4: ask, stop, release.</b> A tool that waited for a person would
    /// hold a lease across human latency, and a lease held that long is a lease
    /// that expires and a flight that gets taken over - so every question would
    /// arrive on the takeover path instead of the gate path. The runner does
    /// not depend on the agent obeying the instruction to stop either: the
    /// outcome is decided from the stream, so an agent that asks and then keeps
    /// working is recorded accurately rather than taken at its word.
    /// </remarks>
    private static string Asked(JsonElement id, JsonElement arguments)
    {
        var question = Text(arguments, QuestionArgument);

        // AN ERROR RESULT RATHER THAN A PROTOCOL ERROR, the same as the tool
        // beside it: the call reached the tool and the tool refused it, which
        // is something the agent can read and fix. And an empty question is
        // worse than none - it opens a gate a person cannot answer.
        return question is null
            ? Content(id, isError: true,
                $"Refused: a question needs words in it. Say what you were doing, what the "
              + "choices are, and what you could not tell from the work itself. Nothing was "
              + "recorded.")
            : Content(id, isError: false,
                "Recorded. Your part is done: stop now and say what you did and what you were "
              + "left with. This grants nothing and changes nothing - a person reads the "
              + "question and answers it, and the work comes back to you with their answer.");
    }

    private static string? Text(JsonElement arguments, string name) =>
        arguments.ValueKind == JsonValueKind.Object
        && arguments.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.String
        && value.GetString() is { } text
        && !string.IsNullOrWhiteSpace(text)
            ? text
            : null;

    private static string Content(JsonElement id, bool isError, string text) =>
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

            if (isError)
            {
                writer.WriteBoolean("isError", true);
            }

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

    /// <summary>
    /// One JSON-RPC message, as one line.
    /// </summary>
    /// <remarks>
    /// <c>Utf8JsonWriter</c> rather than a serializer, because this binary is
    /// published AOT and there is no model to reflect over anyway - the shape
    /// is the protocol's, not ours. It also escapes every value, which matters:
    /// the reason a receipt echoes came from an agent.
    /// </remarks>
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
