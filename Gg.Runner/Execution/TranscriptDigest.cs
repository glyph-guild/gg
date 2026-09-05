using System.Text.Json;
using Gg.Contracts;

namespace Gg.Runner.Execution;

/// <summary>
/// Turns a loop's event stream into the fact that crosses in its place.
/// </summary>
/// <remarks>
/// <para>
/// <b>A function of the text it is handed, and nothing else.</b> It reads no
/// file, starts no process and reaches no network - which is what makes "no
/// model is in this path" a property of the code rather than a promise. The
/// tempting implementation is one call away and would look like an improvement;
/// what it would actually do is turn the one artifact that crosses into an
/// injection surface, because the transcript can contain text addressed to a
/// model.
/// </para>
/// <para>
/// <b>Extraction, not summary.</b> Every field here is something the stream
/// says literally: a path on a tool call, a pattern on a search, the text of a
/// failure. Nothing is interpreted, so the same stream produces the same digest,
/// and digests are comparable across flights - which is the whole of Article
/// XIII's hardening.
/// </para>
/// <para>
/// <b>Bounded, because this crosses.</b> A digest is machine-comparable history
/// sized for diffing thirty flights, not for reading one. Long values are cut
/// and the cut is visible; long lists stop and say they stopped.
/// </para>
/// </remarks>
public static class TranscriptDigest
{
    /// <summary>How much of one value crosses.</summary>
    private const int MaxDetail = 240;

    /// <summary>How many of anything crosses.</summary>
    /// <remarks>
    /// A cap rather than a truncation nobody sees: when it bites, the list says
    /// so in its own last entry. A digest that silently stopped listing would
    /// read as a loop that stopped looking.
    /// </remarks>
    private const int MaxItems = 64;

    /// <summary>
    /// The digest for one loop's stream.
    /// </summary>
    /// <param name="transcript">The line-delimited events, as they were written.</param>
    /// <param name="loopId">Which loop, from the envelope.</param>
    /// <param name="treeRoots">
    /// Every spelling of the tree, so paths can be made relative to it.
    /// </param>
    /// <param name="outcome">Where it stopped, from <see cref="LoopOutcomes"/>.</param>
    /// <param name="declared">
    /// The moves the envelope named, so a refusal can be told from a tool that
    /// simply was not asked for.
    /// </param>
    /// <remarks>
    /// <para>
    /// <b>The declared moves come in and a set difference does not go out.</b> This
    /// used to be handed a list of "refused" moves computed as every tool the loop
    /// reached for that the envelope did not name - which is a statement about the
    /// ENVELOPE, and was reported as a statement about the RUN. Measured against a
    /// real blocked flight, it named <c>Bash</c> as refused in a run where Bash was
    /// called and worked.
    /// </para>
    /// <para>
    /// What is refused is what the stream says came back an error, every time it
    /// was tried. The declared set still matters, because a declared tool that
    /// fails is a failure and not a refusal - and telling those apart by reading
    /// the failure's TEXT would be matching on a vendor's wording, which changes
    /// without telling us and would fail in the permissive direction.
    /// </para>
    /// </remarks>
    public static LoopDigest Extract(
        string transcript,
        string loopId,
        IReadOnlyList<string> treeRoots,
        string outcome,
        IReadOnlyList<string> declared)
    {
        ArgumentNullException.ThrowIfNull(transcript);
        ArgumentNullException.ThrowIfNull(treeRoots);
        ArgumentNullException.ThrowIfNull(declared);

        // First-appearance order, deduplicated. The order is part of the value -
        // it is the sequence in which the loop looked at things.
        var read = new List<string>();
        var edited = new List<string>();
        var searches = new List<string>();
        var errors = new List<DigestError>();

        // Which tool a result belongs to. The failure text arrives on a separate
        // event from the call that caused it, and "something failed" without the
        // tool is a sentence nobody can act on.
        var toolById = new Dictionary<string, string>(StringComparer.Ordinal);

        // AND WHICH PATH, for the same reason and one the digest used to get
        // wrong. A refused write is a tool_use carrying a path followed by an
        // error carrying an id, and with nothing joining them the path was
        // recorded as edited. The tree said otherwise.
        var pathById = new Dictionary<string, string>(StringComparer.Ordinal);

        // Calls that came back an error, and every call that was made. A tool is
        // refused when it never once got through; one failure among successes is
        // a tool that works and a call that did not.
        var calls = new Dictionary<string, int>(StringComparer.Ordinal);
        var failures = new Dictionary<string, int>(StringComparer.Ordinal);
        var failedPaths = new HashSet<string>(StringComparer.Ordinal);
        var succeededPaths = new HashSet<string>(StringComparer.Ordinal);

        var attempts = 0;

        foreach (var line in transcript.Split('\n'))
        {
            if (line.Length == 0)
            {
                continue;
            }

            JsonDocument document;
            try
            {
                document = JsonDocument.Parse(line);
            }
            catch (JsonException)
            {
                // A half-written last line is the ordinary case while a file is
                // still being appended to. Skipping it loses one event; throwing
                // would lose every signal before it.
                continue;
            }

            using (document)
            {
                var root = document.RootElement;
                if (root.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                if (Turns(root) is { } turns)
                {
                    attempts = turns;
                }

                if (!root.TryGetProperty("message", out var message)
                    || message.ValueKind != JsonValueKind.Object
                    || !message.TryGetProperty("content", out var content)
                    || content.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var block in content.EnumerateArray())
                {
                    if (block.ValueKind != JsonValueKind.Object
                        || !block.TryGetProperty("type", out var type))
                    {
                        continue;
                    }

                    switch (type.GetString())
                    {
                        case "tool_use":
                            Use(block, treeRoots, toolById, pathById, calls, read, edited, searches);
                            break;

                        case "tool_result":
                            Result(block, toolById, pathById, failures, failedPaths,
                                succeededPaths, errors);
                            break;
                    }
                }
            }
        }

        return new LoopDigest
        {
            LoopId = loopId,
            // Read AND edited is the work, not the thinking. What is left is the
            // proxy for considered-and-ruled-out, which is the point of all this.
            FilesReadNotEdited = Bounded(read.Where(p => !edited.Contains(p, StringComparer.Ordinal))),
            // WHAT SURVIVED, not what was attempted. A path whose every write came
            // back an error is not an edit - the tree said so, and the digest said
            // otherwise for a whole slice. The attempt is not erased: it is on the
            // error, with the path in its detail, which is where a thing that did
            // not happen belongs.
            FilesEdited = Bounded(edited.Where(p =>
                succeededPaths.Contains(p) || !failedPaths.Contains(p))),
            Searches = Bounded(searches),
            Errors = [.. errors.Take(MaxItems)],
            // REFUSED IS NEVER ONCE GOT THROUGH, and only for a tool the envelope
            // did not name. A declared tool that fails is a failure; an undeclared
            // one that works is the envelope being out of step with the work, which
            // the control plane derives for itself from loop.outcome's moves and
            // the envelope it holds - the runner is not an authority on the
            // envelope, and it was making a claim about one.
            RefusedMoves = Bounded(calls.Keys
                .Where(t => !declared.Contains(t, StringComparer.Ordinal))
                // ASKING FOR A DECISION IS NOT A MOVE, so it can be neither
                // declared nor refused. A successful call never reached this
                // filter - refused means never once got through - but a call
                // the TOOL turned down is undeclared and always-failing, and
                // reporting it would tell a person their agent reached outside
                // its envelope because it tried to ask them a question. Named
                // here rather than added to `declared`, because adding it there
                // would say an envelope granted it and no envelope can.
                .Where(t => !string.Equals(t, HelpTool.Qualified, StringComparison.Ordinal))
                .Where(t => failures.GetValueOrDefault(t) >= calls[t])
                .Order(StringComparer.Ordinal)),
            Attempts = attempts,
            StopReason = outcome,
        };
    }

    /// <summary>Records one tool call.</summary>
    private static void Use(
        JsonElement block,
        IReadOnlyList<string> treeRoots,
        Dictionary<string, string> toolById,
        Dictionary<string, string> pathById,
        Dictionary<string, int> calls,
        List<string> read,
        List<string> edited,
        List<string> searches)
    {
        if (!block.TryGetProperty("name", out var name)
            || name.GetString() is not { Length: > 0 } tool)
        {
            return;
        }

        calls[tool] = calls.GetValueOrDefault(tool) + 1;

        string? callId = null;
        if (block.TryGetProperty("id", out var id) && id.GetString() is { Length: > 0 } value)
        {
            callId = value;
            toolById[callId] = tool;
        }

        if (!block.TryGetProperty("input", out var input) || input.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        var path = Text(input, "file_path");
        if (path is not null)
        {
            var relative = Relative(path, treeRoots);

            // Anything that names a file and is not a read is a change to it.
            // Erring this way keeps a changed file out of the ruled-out list,
            // which is the mistake that would matter: it would report the work
            // as thinking.
            (string.Equals(tool, "Read", StringComparison.Ordinal) ? read : edited).Add(relative);

            // REMEMBERED AGAINST THE CALL, so the result can say whether it
            // happened. Without this a refused write is a path in filesEdited and
            // an untouched file on disk, which is what the tree found.
            if (callId is not null)
            {
                pathById[callId] = relative;
            }
        }

        if (Text(input, "pattern") is { } pattern)
        {
            searches.Add(Short(pattern));
        }
    }

    /// <summary>
    /// Records what became of one call: a failure against its tool, and whether
    /// the path it named survived.
    /// </summary>
    /// <remarks>
    /// <b>Both outcomes, not only the failures.</b> A result that succeeded used to
    /// be skipped at the first line, which is why nothing could tell a tool that
    /// never worked from one that did - and why a path whose write was refused
    /// stayed in the edited list. What a call DID is as much a fact as what it
    /// failed to do.
    /// </remarks>
    /// <summary>
    /// The work kind this loop nominated, or null where it nominated none.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Its own walk rather than a member of the digest.</b> A nomination is
    /// not a measurement of the episode - it is a request the loop made - and
    /// hanging it off <see cref="LoopDigest"/> would put an ask inside the
    /// record of what was measured, which is the confusion the fact's own slot
    /// exists to prevent. It is a separate fact for the same reason.
    /// </para>
    /// <para>
    /// <b>From the CALL, never from prose.</b> A classifier's closing summary
    /// names a work kind because it just nominated one, and a sentence is
    /// something an agent can be told to write - by a file in a customer's tree
    /// among other things. A tool call is a thing the agent chose to make.
    /// </para>
    /// <para>
    /// <b>The last SUCCESSFUL call wins.</b> The rule the seed composer already
    /// follows, and for its reason: an agent that nominated twice changed its
    /// mind, and the newest answer is the one. A call with no paired result is
    /// not an answer - the run was cut off and nobody knows whether the tool
    /// recorded it - and a call whose result is an error is one the tool
    /// refused, which must not become a fact.
    /// </para>
    /// <para>
    /// <b>The WHOLE qualified name.</b> A tool called <c>nominate_work_kind</c>
    /// on somebody else's server is not a nomination this runner served; an
    /// operator reader keyed like the platform's own server is refused at
    /// configuration, and this is what that refusal protects.
    /// </para>
    /// <para>
    /// <b>Pure, like <see cref="Extract"/>.</b> A function of the text it is
    /// handed: it reads no file, starts no process and reaches no network.
    /// </para>
    /// </remarks>
    /// <summary>
    /// Whether this run asked for a decision and then stopped.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Declared, never inferred.</b> The evidence is a successful call to
    /// the help tool - a <c>tool_use</c> block with a <c>tool_result</c> that
    /// did not error - and never the closing prose. A classifier over
    /// repository content is injectable: a file in a customer's tree could make
    /// a flight declare itself blocked, or keep a genuinely stuck one quiet. A
    /// tool call the agent chose to make is a narrower thing to trust than a
    /// sentence it was told to write.
    /// </para>
    /// <para>
    /// <b>ASKED AND STOPPED versus ASKED AND THEN FINISHED, and the line is a
    /// tree-changing call after the question.</b> An agent that asked and then
    /// edited went on to do the work; one that asked and did nothing else
    /// stopped; and one that asked and then re-read the file it was asking
    /// about was still stopping - which is why the line is not <i>any later
    /// tool call</i>. Anything read later than the tool stream - a turn count,
    /// what the summary says - is the inference this refuses.
    /// </para>
    /// <para>
    /// <b>A refused call is not a question</b>, the rule the nomination
    /// extractor already follows one tool over. A flight recorded as waiting on
    /// a question nobody received waits for ever.
    /// </para>
    /// <para>
    /// <b>The tool names arrive as an argument, and a guard in this file's own
    /// suite is why.</b> Which tools can put bytes on disk is the launcher's
    /// knowledge, and naming it here would make this file reference the thing
    /// that invokes a model - which the digest path structurally may not do,
    /// because a digest produced by a model is a claim rather than a fact and
    /// carries whatever the transcript told it to. Handed in, this stays a pure
    /// function of what it is given and there is still one mapping.
    /// </para>
    /// </remarks>
    /// <param name="transcript">The stream, as it was written.</param>
    /// <param name="changeTheTree">
    /// What the tools that can put bytes on disk are called, handed in by
    /// whoever launched the agent.
    /// </param>
    /// <summary>
    /// The question this run asked, or null when it asked none.
    /// </summary>
    /// <remarks>
    /// <b>The last successfully-answered call wins</b>, the rule the nomination
    /// extractor already follows for the tool beside it: an agent that asked
    /// twice refined its question, and taking the first would record something
    /// it moved on from. A call with no paired result is not a question, and
    /// neither is one whose result came back an error - a flight recorded as
    /// waiting on a question nobody received waits for ever.
    /// </remarks>
    public static Gg.Contracts.LoopQuestion? Question(string transcript)
    {
        ArgumentNullException.ThrowIfNull(transcript);

        var asked = new List<(string Id, string Question)>();
        var answered = new HashSet<string>(StringComparer.Ordinal);

        foreach (var line in transcript.Split('\n'))
        {
            if (line.Length == 0)
            {
                continue;
            }

            JsonDocument document;
            try
            {
                document = JsonDocument.Parse(line);
            }
            catch (JsonException)
            {
                continue;
            }

            using (document)
            {
                if (document.RootElement.ValueKind != JsonValueKind.Object
                    || !document.RootElement.TryGetProperty("message", out var message)
                    || message.ValueKind != JsonValueKind.Object
                    || !message.TryGetProperty("content", out var content)
                    || content.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var block in content.EnumerateArray())
                {
                    if (block.ValueKind != JsonValueKind.Object
                        || !block.TryGetProperty("type", out var type))
                    {
                        continue;
                    }

                    switch (type.GetString())
                    {
                        case "tool_use":
                            AskedAPerson(block, asked);
                            break;

                        case "tool_result":
                            Answered(block, answered);
                            break;
                    }
                }
            }
        }

        for (var i = asked.Count - 1; i >= 0; i--)
        {
            if (answered.Contains(asked[i].Id))
            {
                return new Gg.Contracts.LoopQuestion
                {
                    Question = Bound(
                        asked[i].Question, Gg.Contracts.LoopQuestion.MaxQuestion, prose: true),
                };
            }
        }

        return null;
    }

    /// <summary>Records a call to the help tool, when that is what it is.</summary>
    private static void AskedAPerson(JsonElement block, List<(string Id, string Question)> asked)
    {
        if (!block.TryGetProperty("name", out var name)
            || !string.Equals(name.GetString(), HelpTool.Qualified, StringComparison.Ordinal)
            || !block.TryGetProperty("id", out var id)
            || id.GetString() is not { Length: > 0 } callId
            || !block.TryGetProperty("input", out var input)
            || input.ValueKind != JsonValueKind.Object
            || !input.TryGetProperty("question", out var question)
            || question.GetString() is not { Length: > 0 } text)
        {
            return;
        }

        asked.Add((callId, text));
    }

    public static bool Blocked(string transcript, IReadOnlyList<string> changeTheTree)
    {
        ArgumentNullException.ThrowIfNull(transcript);
        ArgumentNullException.ThrowIfNull(changeTheTree);

        // Positions rather than a flag, because what decides this is ORDER: the
        // last successful question, against the last call that could have
        // changed the tree. Two passes over one walk, because a result always
        // follows its call and nothing guarantees they are adjacent.
        var asked = new List<(string Id, int At)>();
        var answered = new HashSet<string>(StringComparer.Ordinal);
        var changed = -1;
        var at = 0;

        foreach (var line in transcript.Split('\n'))
        {
            if (line.Length == 0)
            {
                continue;
            }

            JsonDocument document;
            try
            {
                document = JsonDocument.Parse(line);
            }
            catch (JsonException)
            {
                // A half-written last line is ordinary while a file is still
                // being appended to, and throwing would lose every signal
                // before it. The digest's own rule beside it.
                continue;
            }

            using (document)
            {
                if (document.RootElement.ValueKind != JsonValueKind.Object
                    || !document.RootElement.TryGetProperty("message", out var message)
                    || message.ValueKind != JsonValueKind.Object
                    || !message.TryGetProperty("content", out var content)
                    || content.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var block in content.EnumerateArray())
                {
                    at++;

                    if (block.ValueKind != JsonValueKind.Object
                        || !block.TryGetProperty("type", out var type))
                    {
                        continue;
                    }

                    switch (type.GetString())
                    {
                        case "tool_use":
                            if (Named(block) is { } tool)
                            {
                                if (string.Equals(
                                    tool, HelpTool.Qualified, StringComparison.Ordinal)
                                    && block.TryGetProperty("id", out var callId)
                                    && callId.GetString() is { Length: > 0 } identifier)
                                {
                                    asked.Add((identifier, at));
                                }
                                else if (changeTheTree.Contains(tool, StringComparer.Ordinal))
                                {
                                    changed = at;
                                }
                            }

                            break;

                        case "tool_result":
                            Answered(block, answered);
                            break;
                    }
                }
            }
        }

        for (var i = asked.Count - 1; i >= 0; i--)
        {
            if (answered.Contains(asked[i].Id))
            {
                return changed < asked[i].At;
            }
        }

        return false;
    }

    private static string? Named(JsonElement block) =>
        block.TryGetProperty("name", out var name) ? name.GetString() : null;

    public static Gg.Contracts.FlightNomination? Nomination(string transcript)
    {
        ArgumentNullException.ThrowIfNull(transcript);

        // Nominated by call id, in the order the calls appear, and the ids whose
        // result came back without an error. Two passes over one walk, because
        // a result always follows its call but nothing guarantees they are
        // adjacent.
        var asked = new List<(string Id, Gg.Contracts.FlightNomination Nomination)>();
        var answered = new HashSet<string>(StringComparer.Ordinal);

        foreach (var line in transcript.Split('\n'))
        {
            if (line.Length == 0)
            {
                continue;
            }

            JsonDocument document;
            try
            {
                document = JsonDocument.Parse(line);
            }
            catch (JsonException)
            {
                // The digest's own rule beside it: a half-written last line is
                // ordinary while a file is still being appended to, and
                // throwing would lose every signal before it.
                continue;
            }

            using (document)
            {
                if (document.RootElement.ValueKind != JsonValueKind.Object
                    || !document.RootElement.TryGetProperty("message", out var message)
                    || message.ValueKind != JsonValueKind.Object
                    || !message.TryGetProperty("content", out var content)
                    || content.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var block in content.EnumerateArray())
                {
                    if (block.ValueKind != JsonValueKind.Object
                        || !block.TryGetProperty("type", out var type))
                    {
                        continue;
                    }

                    switch (type.GetString())
                    {
                        case "tool_use":
                            Asked(block, asked);
                            break;

                        case "tool_result":
                            Answered(block, answered);
                            break;
                    }
                }
            }
        }

        for (var i = asked.Count - 1; i >= 0; i--)
        {
            if (answered.Contains(asked[i].Id))
            {
                return asked[i].Nomination;
            }
        }

        return null;
    }

    /// <summary>Records a call to the nomination tool, when that is what it is.</summary>
    private static void Asked(
        JsonElement block, List<(string Id, Gg.Contracts.FlightNomination Nomination)> asked)
    {
        if (!block.TryGetProperty("name", out var name)
            || !string.Equals(name.GetString(), NominationTool.Qualified, StringComparison.Ordinal)
            || !block.TryGetProperty("id", out var id)
            || id.GetString() is not { Length: > 0 } callId
            || !block.TryGetProperty("input", out var input)
            || input.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        // HALF A NOMINATION IS NOT ONE. The server refuses these, so this is
        // defence against a transcript that came from somewhere else - and the
        // extractor may not invent the missing half.
        if (Argument(input, "work_kind") is not { } workKind
            || Argument(input, "reason") is not { } reason)
        {
            return;
        }

        asked.Add((callId, new Gg.Contracts.FlightNomination
        {
            WorkKind = Bound(workKind, Gg.Contracts.FlightNomination.MaxWorkKind, prose: false),
            Reason = Bound(reason, Gg.Contracts.FlightNomination.MaxReason, prose: true),
        }));
    }

    /// <summary>Records that a call came back, and came back without an error.</summary>
    private static void Answered(JsonElement block, HashSet<string> answered)
    {
        if (block.TryGetProperty("is_error", out var flag) && flag.ValueKind == JsonValueKind.True)
        {
            return;
        }

        if (block.TryGetProperty("tool_use_id", out var id)
            && id.GetString() is { Length: > 0 } callId)
        {
            answered.Add(callId);
        }
    }

    private static string? Argument(JsonElement input, string name) =>
        input.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.String
        && value.GetString() is { } text
        && !string.IsNullOrWhiteSpace(text)
            ? text
            : null;

    /// <summary>
    /// Bounded on this machine, before it crosses.
    /// </summary>
    /// <remarks>
    /// <b>Prose keeps its line breaks and a name does not.</b> The reason is
    /// something an agent wrote for a reader, and it crosses under the same
    /// disposition a person's statement does; a work kind is a name in a
    /// topology, so a line break in one is a name nobody declared. An agent
    /// asked for a reason can write a document, and an unbounded one is refused
    /// at ingress - losing the classification for a reason that has nothing to
    /// do with the work.
    /// </remarks>
    private static string Bound(string value, int most, bool prose)
    {
        var clean = ControlText.Strip(value) ?? "";
        clean = prose ? clean.Trim() : clean.ReplaceLineEndings(" ").Trim();

        return clean.Length <= most ? clean : clean[..most].TrimEnd();
    }

    private static void Result(
        JsonElement block,
        Dictionary<string, string> toolById,
        Dictionary<string, string> pathById,
        Dictionary<string, int> failures,
        HashSet<string> failedPaths,
        HashSet<string> succeededPaths,
        List<DigestError> errors)
    {
        var callId = block.TryGetProperty("tool_use_id", out var id)
            ? id.GetString()
            : null;

        var failed = block.TryGetProperty("is_error", out var flag)
            && flag.ValueKind == JsonValueKind.True;

        if (callId is { Length: > 0 } && pathById.TryGetValue(callId, out var path))
        {
            (failed ? failedPaths : succeededPaths).Add(path);
        }

        if (!failed)
        {
            return;
        }

        var source = callId is { Length: > 0 } && toolById.TryGetValue(callId, out var tool)
            ? tool
            : "unknown";

        failures[source] = failures.GetValueOrDefault(source) + 1;
        errors.Add(new DigestError { Source = source, Detail = Short(Content(block)) });
    }

    /// <summary>A result's text, whichever shape it arrived in.</summary>
    private static string Content(JsonElement block)
    {
        if (!block.TryGetProperty("content", out var content))
        {
            return "";
        }

        if (content.ValueKind == JsonValueKind.String)
        {
            return content.GetString() ?? "";
        }

        if (content.ValueKind != JsonValueKind.Array)
        {
            return "";
        }

        // A result is sometimes a list of blocks rather than a string, and a
        // digest that only understood one of the two would report no detail for
        // half the failures.
        foreach (var part in content.EnumerateArray())
        {
            if (part.ValueKind == JsonValueKind.Object && Text(part, "text") is { } text)
            {
                return text;
            }
        }

        return "";
    }

    /// <summary>The turn count, when this event carries one.</summary>
    private static int? Turns(JsonElement root) =>
        root.TryGetProperty("num_turns", out var turns) && turns.ValueKind == JsonValueKind.Number
            ? turns.GetInt32()
            : null;

    private static string? Text(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value)
        && value.ValueKind == JsonValueKind.String
        && value.GetString() is { Length: > 0 } text
            ? text
            : null;

    /// <summary>
    /// A path relative to the tree.
    /// </summary>
    /// <remarks>
    /// <para>
    /// An absolute path is a machine detail crossing a boundary, and a digest
    /// carrying <c>/home/runner/...</c> is not comparable with one carrying
    /// <c>/work/...</c> - which quietly ends the cross-flight comparison this
    /// exists for.
    /// </para>
    /// <para>
    /// <b>More than one root, because a tree has more than one name.</b> Found
    /// against a real agent: on macOS the tree is handed over as
    /// <c>/var/folders/…</c> and the agent reports every path as
    /// <c>/private/var/folders/…</c>, because one is a symlink to the other. A
    /// single-spelling comparison matched nothing, left the paths absolute, and
    /// would have shipped this machine's directory layout inside the one fact
    /// that is supposed to compare across machines.
    /// </para>
    /// <para>
    /// The longest matching root wins, so a nested tree does not lose its
    /// prefix to a shorter one that also matches.
    /// </para>
    /// </remarks>
    private static string Relative(string path, IReadOnlyList<string> treeRoots)
    {
        var clean = ControlText.Strip(path) ?? "";

        var matched = treeRoots
            .Where(r => !string.IsNullOrEmpty(r))
            .Select(r => r.TrimEnd('/'))
            .Where(r => clean.StartsWith(r + "/", StringComparison.Ordinal))
            .OrderByDescending(r => r.Length)
            .FirstOrDefault();

        var relative = matched is not null ? clean[(matched.Length + 1)..] : clean.TrimStart('/');

        // A CWD-RELATIVE SPELLING IS THE SAME FILE. An agent whose working
        // directory is the tree writes './src/config.py'; the './' is its
        // spelling of "here", not part of the path, and two spellings of one
        // path end the cross-flight comparison this fact exists for.
        while (relative.StartsWith("./", StringComparison.Ordinal))
        {
            relative = relative[2..];
        }

        return relative;
    }

    /// <summary>
    /// Stripped and cut, in that order.
    /// </summary>
    /// <remarks>
    /// <b>Stripping happens here, in the runner, before the digest.</b> Doing it
    /// on the far side would mean the stored bytes disagree with the hash that
    /// proves what they were, and a control plane holding an escape sequence is
    /// one that can drive a terminal.
    /// </remarks>
    private static string Short(string value)
    {
        var clean = (ControlText.Strip(value) ?? "").ReplaceLineEndings(" ").Trim();

        return clean.Length <= MaxDetail ? clean : clean[..MaxDetail] + "…";
    }

    /// <summary>
    /// The first <see cref="MaxItems"/>, deduplicated, saying so when it cuts.
    /// </summary>
    /// <remarks>
    /// A list that silently stopped would read as a loop that stopped looking,
    /// which is a different thing and the one somebody would act on.
    /// </remarks>
    private static IReadOnlyList<string> Bounded(IEnumerable<string> values)
    {
        var distinct = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var value in values)
        {
            if (value.Length > 0 && seen.Add(value))
            {
                distinct.Add(value);
            }
        }

        return distinct.Count <= MaxItems
            ? distinct
            : [.. distinct.Take(MaxItems), $"… and {distinct.Count - MaxItems} more"];
    }
}
