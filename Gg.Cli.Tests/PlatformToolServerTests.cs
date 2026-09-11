using Gg.Local;
using System.Text;
using System.Text.Json;
using Gg.Cli;
using Gg.Runner.Execution;

namespace Gg.Cli.Tests;

/// <summary>
/// The platform's own tool server: the channel an agent declares a value
/// through, rather than writing prose about it.
/// </summary>
/// <remarks>
/// <para>
/// <b>STDOUT IS THE PROTOCOL, which is the hazard this file exists for.</b> One
/// stray line of narration and the agent sees a dead server - not a failed
/// tool, a server that never initialized - and the flight produces no
/// nomination for a reason nothing records. So every assertion here parses
/// EVERY line the server wrote, rather than looking for the lines it expected.
/// </para>
/// <para>
/// <b>Hand-written rather than an SDK, and the reason is the binary.</b>
/// <c>Gg.Runner</c> carries no package references and the CLI is published
/// AOT; the official server library is DI- and reflection-shaped. What is
/// needed here is four methods over line-delimited JSON, which is the shape
/// the launch's own config writer already uses and for the same stated reason.
/// </para>
/// <para>
/// <b>It holds nothing open and needs nothing.</b> No credential, no session,
/// no control-plane call - it validates two strings and returns a receipt. That
/// is what makes it safe to launch as a child of a process the threat model
/// treats as hostile: an injected agent that reaches it can at most record a
/// request that admission will refuse.
/// </para>
/// </remarks>
public class PlatformToolServerTests
{
    /// <summary>A conversation with the server, with somewhere to record an intent.</summary>
    /// <remarks>
    /// <b>The path is an ARGUMENT, not an environment read, and that is what
    /// keeps this type a function of what it is handed.</b> Reading
    /// <c>GG_INTENT_PATH</c> in here would make the server depend on process
    /// state, and would make every test that exercised it mutate something
    /// global while the rest of the suite ran beside it in parallel. The verb
    /// that starts the server reads the environment; the server is told.
    /// </remarks>
    private static async Task<IReadOnlyList<JsonDocument>> RecordingAsync(
        string? intentPath, params string[] lines)
    {
        var output = new StringWriter();
        await PlatformToolServer.RunAsync(
            new StringReader(string.Join('\n', lines)), output, intentPath);

        return output.ToString()
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => JsonDocument.Parse(line))
            .ToList();
    }

    /// <summary>A directory this test owns, for an intent to land in.</summary>
    private static DirectoryInfo Somewhere() => Directory.CreateDirectory(Path.Combine(
        Path.GetTempPath(), "gg-intent-test-" + Guid.NewGuid().ToString("N")[..8]));

    /// <summary>A tools/call for the intent tool, named from the declaration.</summary>
    /// <remarks>
    /// The NAME comes from <see cref="IntentTool"/> rather than being typed here,
    /// so a test cannot go on passing against a tool the server has renamed.
    /// </remarks>
    private static string CallIntent(string intent) =>
        """
        {"jsonrpc":"2.0","id":7,"method":"tools/call","params":{"name":"TOOL","arguments":{"intent":"SAID"}}}
        """
        .Replace("TOOL", IntentTool.Name, StringComparison.Ordinal)
        .Replace("SAID", intent, StringComparison.Ordinal);

    private static bool IsError(JsonDocument answer) =>
        answer.RootElement.TryGetProperty("result", out var result)
        && result.TryGetProperty("isError", out var flag)
        && flag.GetBoolean();

    private static string Said(JsonDocument answer) =>
        answer.RootElement.GetProperty("result").GetProperty("content")[0].GetProperty("text").GetString()!;

    [Test]
    public async Task One_declaration_owns_the_intent_tools_three_spellings()
    {
        // THE THIRD INSTANCE OF THIS HAZARD IN THIS FEATURE AREA, and every
        // one so far has been silent rather than loud. The launch's allow-list
        // uses the qualified name, tools/list declares the bare one, and
        // whatever reads the result looks for the qualified one - so a
        // disagreement means either an agent granted a tool that does not
        // exist, or gg waiting for a call the agent was never offered.
        await Assert.That(IntentTool.Qualified)
            .IsEqualTo($"mcp__{IntentTool.Server}__{IntentTool.Name}")
            .Because("the qualified name is composed from the other two rather than typed "
                   + "beside them, which is the only arrangement that cannot drift.");

        await Assert.That(IntentTool.Server).IsEqualTo(NominationTool.Server)
            .Because("four tools on ONE server. A second server key would shadow the first "
                   + "if an operator ever configured a reader under it.");
    }

    [Test]
    public async Task The_intent_tool_is_declared_beside_the_other_two()
    {
        // Declared always, granted never by default: the server's own note says
        // the grant is decided in the launch's allow-list rather than by varying
        // tools/list, because a list that varied by envelope would be a second
        // place the same rule lives.
        var answers = await RecordingAsync(intentPath: null,
            """{"jsonrpc":"2.0","id":1,"method":"tools/list"}""");

        var declared = answers[0].RootElement
            .GetProperty("result").GetProperty("tools").EnumerateArray()
            .Select(tool => tool.GetProperty("name").GetString())
            .ToList();

        await Assert.That(declared).Contains(IntentTool.Name)
            .Because("an agent cannot call a tool it was never offered, however the launch "
                   + "granted it. Found: " + string.Join(", ", declared));
    }

    [Test]
    public async Task An_intent_arrives_as_a_file_written_whole()
    {
        // THE HAND-BACK. The tool call is the structured channel - the moment an
        // agent SAYS it is done - and the file is transport between two gg
        // processes rather than something gg has to guess the meaning of.
        var notes = Somewhere();
        var path = Path.Combine(notes.FullName, "intent.txt");

        try
        {
            var answers = await RecordingAsync(path, CallIntent("Make the pool decider stop scanning"));

            await Assert.That(IsError(answers[0])).IsFalse()
                .Because("a receipt, not a refusal: " + Said(answers[0]));

            await Assert.That(File.Exists(path)).IsTrue();
            await Assert.That(await File.ReadAllTextAsync(path))
                .IsEqualTo("Make the pool decider stop scanning")
                .Because("what the agent composed is what gg reads - no wrapper, no format to "
                       + "version, the same bytes the editor path would have produced.");

            // WRITTEN BY RENAME, which is why gg may watch it. A write in place
            // is visible half-finished, and gg watching would read a truncated
            // intent and open a flight with it. The observable consequence is
            // that nothing is left lying beside it.
            await Assert.That(notes.GetFiles().Select(f => f.Name)).IsEquivalentTo((string[])["intent.txt"])
                .Because("a temp file left behind is the tell that the write was not a rename.");
        }
        finally
        {
            notes.Delete(recursive: true);
        }
    }

    [Test]
    public async Task A_session_with_nowhere_to_record_says_so()
    {
        // S33.3-09. An agent that cannot submit must not look like one that
        // chose not to - and this is the ordinary case on the fleet path, where
        // the server runs with no composing session at all.
        var answers = await RecordingAsync(intentPath: null, CallIntent("something worth doing"));

        await Assert.That(IsError(answers[0])).IsTrue()
            .Because("silence here is indistinguishable from an agent that declined.");
        await Assert.That(Said(answers[0])).Contains("Nothing was recorded")
            .Because("the same words the other two tools use for the same fact.");
    }

    [Test]
    public async Task An_empty_intent_is_refused_rather_than_written()
    {
        // Borrowed from FlightIntent.Validate rather than invented here: an
        // intent of kind text whose text is empty is already refused on the
        // wire, so accepting one would only move the refusal somewhere less
        // helpful.
        var notes = Somewhere();
        var path = Path.Combine(notes.FullName, "intent.txt");

        try
        {
            var answers = await RecordingAsync(path, CallIntent("   "));

            await Assert.That(IsError(answers[0])).IsTrue();
            await Assert.That(File.Exists(path)).IsFalse()
                .Because("a flight opened with nothing in it is worse than no flight.");
        }
        finally
        {
            notes.Delete(recursive: true);
        }
    }

    [Test]
    public async Task The_last_intent_before_the_session_ends_is_the_one_that_counts()
    {
        // A person correcting themselves is the ordinary case, not an error, so
        // the second call replaces the first rather than being refused.
        var notes = Somewhere();
        var path = Path.Combine(notes.FullName, "intent.txt");

        try
        {
            var answers = await RecordingAsync(path,
                CallIntent("first thought"), CallIntent("what I actually meant"));

            await Assert.That(answers.Count).IsEqualTo(2);
            await Assert.That(IsError(answers[1])).IsFalse();
            await Assert.That(await File.ReadAllTextAsync(path)).IsEqualTo("what I actually meant");
        }
        finally
        {
            notes.Delete(recursive: true);
        }
    }

    private static async Task<IReadOnlyList<JsonDocument>> ExchangeAsync(params string[] lines)
    {
        var output = new StringWriter();
        await PlatformToolServer.RunAsync(new StringReader(string.Join('\n', lines)), output);

        // EVERY LINE, parsed. A server that wrote one unparseable line among
        // valid ones is a dead server, and a test that looked only for what it
        // expected would not notice.
        return output.ToString()
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => JsonDocument.Parse(line))
            .ToList();
    }

    // BUILT BY CONCATENATION rather than interpolated: this is JSON, and a raw
    // interpolated literal ending in three closing braces is a brace-counting
    // exercise that the compiler loses too.
    private static string Initialize(int id = 0) =>
        "{\"jsonrpc\":\"2.0\",\"id\":" + id + ",\"method\":\"initialize\",\"params\":"
      + "{\"protocolVersion\":\"2024-11-05\",\"capabilities\":{},"
      + "\"clientInfo\":{\"name\":\"probe\",\"version\":\"1\"}}}";

    private static string Call(int id, string arguments) =>
        "{\"jsonrpc\":\"2.0\",\"id\":" + id + ",\"method\":\"tools/call\",\"params\":"
      + "{\"name\":\"" + NominationTool.Name + "\",\"arguments\":" + arguments + "}}";

    [Test]
    public async Task Everything_it_writes_is_json_rpc()
    {
        var answers = await ExchangeAsync(
            Initialize(),
            """{"jsonrpc":"2.0","method":"notifications/initialized"}""",
            """{"jsonrpc":"2.0","id":1,"method":"tools/list","params":{}}""",
            Call(2, """{"work_kind":"research","reason":"the item asks a question"}"""));

        await Assert.That(answers.Count).IsGreaterThan(0);

        foreach (var answer in answers)
        {
            await Assert.That(answer.RootElement.GetProperty("jsonrpc").GetString()).IsEqualTo("2.0");
        }
    }

    [Test]
    public async Task It_answers_initialize_with_the_protocol_the_client_asked_for()
    {
        var answers = await ExchangeAsync(Initialize());

        await Assert.That(answers.Count).IsEqualTo(1);

        var result = answers[0].RootElement.GetProperty("result");
        await Assert.That(result.GetProperty("protocolVersion").GetString()).IsEqualTo("2024-11-05")
            .Because("echoed rather than declared, so the version this server claims is one the "
                   + "client has already said it speaks.");
        await Assert.That(result.GetProperty("serverInfo").GetProperty("name").GetString())
            .IsEqualTo(NominationTool.Server)
            .Because("the server key is half the identity of every tool it hosts, so it comes "
                   + "from the one place that names it.");
    }

    [Test]
    public async Task A_notification_is_answered_with_nothing()
    {
        // A RESPONSE TO A NOTIFICATION IS A PROTOCOL ERROR, and one written to
        // this stream is a line the client cannot match to a request.
        var answers = await ExchangeAsync(
            """{"jsonrpc":"2.0","method":"notifications/initialized"}""");

        await Assert.That(answers).IsEmpty();
    }

    /// <summary>The tools a server started this way declares.</summary>
    private static async Task<IReadOnlyList<string>> OfferedAsync(
        string? intentPath, string? documentRoot)
    {
        var output = new StringWriter();
        await PlatformToolServer.RunAsync(
            new StringReader("""{"jsonrpc":"2.0","id":1,"method":"tools/list","params":{}}"""),
            output,
            intentPath: intentPath,
            documentRoot: documentRoot);

        using var answer = JsonDocument.Parse(output.ToString().Trim());

        return [.. answer.RootElement.GetProperty("result").GetProperty("tools")
            .EnumerateArray()
            .Select(tool => tool.GetProperty("name").GetString()!)
            .Order(StringComparer.Ordinal)];
    }

    [Test]
    public async Task A_session_is_offered_only_the_tools_it_can_use()
    {
        // THE DEFECT, AND A PERSON WATCHED IT HAPPEN. A drafting session was
        // offered all seven tools - the server declares them unconditionally
        // and refuses at call time, which is right for SAFETY and wrong for
        // guidance. The agent reached for `submit_intent`, whose description
        // is "hand back the intent you have composed" and which reads exactly
        // like the tool you want when you have something to hand back. It was
        // refused, correctly, and then told the person:
        //
        //   "No tool I have applies these documents or opens a flight."
        //
        // Which is a WRONG conclusion reached honestly, out of five tools that
        // do not belong in that session. A turn was spent and a person was
        // misinformed.
        //
        // REFUSING AT CALL TIME IS STILL THE BACKSTOP. This is about what is
        // OFFERED: a tool a session cannot use is a wrong answer somebody has
        // to be talked out of.
        await Assert.That(await OfferedAsync(intentPath: null, documentRoot: "/tmp/tree"))
            .IsEquivalentTo((string[])
            [
                AirspaceContextTool.Name, AirspacePullTool.Name, DocumentTool.Name,
            ])
            .Because("a drafting session reads the airspace, pulls it and hands documents "
                   + "back. It is not a flight: it nominates nothing, asks nobody for a "
                   + "decision, proposes no work item and composes no intent.");
    }

    [Test]
    public async Task Composing_an_intent_is_offered_the_one_tool_that_records_one()
    {
        await Assert.That(await OfferedAsync(intentPath: "/tmp/intent", documentRoot: null))
            .IsEquivalentTo((string[])[IntentTool.Name])
            .Because("the console's compose path has somewhere to record an intent and "
                   + "nothing else to do. Offering it the airspace tools would invite a "
                   + "document into a session with no working copy.");
    }

    [Test]
    public async Task A_fleet_flight_is_offered_what_a_flight_needs_and_no_more()
    {
        // NO ENVIRONMENT AT ALL is how the runner starts this server -
        // ClaudeCodeExecutor says so in as many words - so these three are
        // what is left when nothing is handed over. Which is also the answer
        // to "what can an injected agent reach through this server": less
        // than it could yesterday.
        await Assert.That(await OfferedAsync(intentPath: null, documentRoot: null))
            .IsEquivalentTo((string[])
            [
                HelpTool.Name, NominationTool.Name, WorkItemProposalTool.Name,
            ])
            .Because("a flight nominates a work kind, asks a person for a decision it may "
                   + "not make, and proposes work items. The envelope decides which of "
                   + "those it is granted; the server should not be offering it two more "
                   + "that belong to the console.");
    }

    [Test]
    public async Task The_document_tool_says_what_it_does_before_what_it_does_not()
    {
        // WHY THE AGENT TALKED ITSELF OUT OF IT. The description is accurate
        // and opens with the disclaimer - "applies nothing and grants
        // nothing" - which is exactly what an agent scanning for "the tool
        // that submits my work" reads as "not this one". The honest order is
        // the act first and the limits after: somebody looking for the door
        // should find it before they find the sign about what is beyond it.
        var described = (await DescriptionsAsync())[DocumentTool.Name];

        var act = described.IndexOf("hand back", StringComparison.OrdinalIgnoreCase);
        var limit = described.IndexOf("applies nothing", StringComparison.OrdinalIgnoreCase);

        await Assert.That(act).IsGreaterThanOrEqualTo(0)
            .Because("it has to say what it is for. Description: " + described);

        await Assert.That(limit).IsGreaterThan(act)
            .Because("and say it FIRST. An agent that reads the limit first concludes the "
                   + "tool is not the one it wants, which is what happened. Description: "
                   + described);
    }

    /// <summary>Each declared tool's description, for a drafting session.</summary>
    private static async Task<IReadOnlyDictionary<string, string>> DescriptionsAsync()
    {
        var output = new StringWriter();
        await PlatformToolServer.RunAsync(
            new StringReader("""{"jsonrpc":"2.0","id":1,"method":"tools/list","params":{}}"""),
            output,
            intentPath: null,
            documentRoot: "/tmp/tree");

        using var answer = JsonDocument.Parse(output.ToString().Trim());

        return answer.RootElement.GetProperty("result").GetProperty("tools")
            .EnumerateArray()
            .ToDictionary(
                tool => tool.GetProperty("name").GetString()!,
                tool => tool.GetProperty("description").GetString()!,
                StringComparer.Ordinal);
    }

    [Test]
    public async Task It_declares_seven_tools_and_an_eighth_has_to_argue_for_itself()
    {
        // TWO NOW, AND THE OLD REASON WAS THE WRONG ONE. This asserted one tool
        // because "a second on this server would be granted by the same move" -
        // and the second is granted on the OPPOSITE terms. Nominating a work
        // kind is the whole output of one kind of work, so an envelope that
        // never declares `propose` has no business granting it; asking for a
        // decision is not a move at all and no envelope may withhold it,
        // because one able to would be one that makes a stuck agent silent.
        //
        // Which is why the count is asserted rather than the absence: a THIRD
        // tool has to make its own argument, and neither of the two above is
        // it.
        //
        // THREE NOW, AND HERE IS THE ARGUMENT THE COUNT ASKED FOR. `submit_intent`
        // exists because a HOSTED agent has no transcript. The other two tools
        // record nothing: the runner reads their calls out of Claude Code's
        // `--output-format stream-json`, which an interactive session in a
        // pseudo-terminal does not produce - so a composing agent has no way at
        // all to hand back what it composed, and reading its prose instead is
        // forbidden by `instructions-in-the-envelope` rule 7 because opening a
        // flight is a governance decision.
        //
        // It is granted on terms unlike either of the others again, which is the
        // pattern this count keeps surfacing: nomination is granted by a
        // declared move, help may never be withheld, and this one is granted
        // only by the launch that asked for an intent - almost never, and never
        // on a fleet launch. Where it is not granted it is still declared, and
        // it refuses out loud rather than silently, because an agent that cannot
        // submit must not look like one that chose not to.
        //
        // FOUR NOW, AND HERE IS THE ARGUMENT THE COUNT ASKED FOR AGAIN.
        // `propose_work_item` is the whole output of a KIND of work, the way
        // the nomination is - a triage flight reads a backlog and its product
        // is a set of proposed changes to it. So it is granted the way the
        // nomination is: by a declared move, LoopMoves.ProposeWorkItem, whole
        // and by name.
        //
        // Which makes it the FIRST tool that does not extend the pattern this
        // count keeps surfacing. Three tools were three grant terms; the fourth
        // reuses the first. That is the argument rather than a hole in it: the
        // alternative was widening `propose` to grant both, which would
        // retroactively change what every envelope already declaring it
        // permits, with nothing in the record marking the day it changed.
        //
        // What it does NOT do is act. It records what was proposed and answers
        // that it did; admission decides which proposals are performed and the
        // runner performs them. So the tool is one more thing an injected agent
        // can reach and it is not one more thing an injected agent can DO,
        // which is the property that made a fourth affordable at all.
        //
        // FIVE NOW, AND HERE IS THE ARGUMENT THE COUNT ASKED FOR.
        // `submit_document` is the drafting half of `submit_intent`, and it
        // exists for the same reason: a hosted agent produces no transcript, so
        // an interactive session has no other way to hand a value back. It is
        // granted on the fourth distinct terms - by the launch that asked for a
        // draft, like the intent, and never on a fleet launch.
        //
        // What makes it affordable is NOT that it cannot act, because it does:
        // it is the second tool that writes a file. The argument is narrower and
        // has to be stated as narrowly. The console's own launch does not pass
        // `--strict-mcp-config`, and `--allowedTools` does not remove a built-in
        // Write - so an agent in a drafting session can already put a file in
        // that working copy, and this tool takes away nothing it had. What the
        // tool buys is that the document is VALIDATED before it lands, that its
        // path is computed from a name rather than chosen, and that the
        // `based-on:` precondition survives - which a hand-written file would
        // silently clear, turning the next apply into a blind overwrite of
        // somebody else's amendment.
        //
        // And it still applies nothing. A draft in a working copy is exactly as
        // authoritative as a person typing into it, which is to say not at all:
        // the stream is the record, and one flight per document with a gate is
        // what makes a change effective. So the fifth tool is one more thing an
        // injected agent can reach, and it is not one more thing an injected
        // agent can DECIDE - which is the same property that made the fourth
        // affordable, said about a write rather than a record.
        //
        // THE SIXTH, AND IT IS THE FIRST THAT ONLY ANSWERS. Every tool above
        // records something a person or a runner later acts on. describe_-
        // airspace reads the working copy the session was already handed and
        // says how the documents in it are read: it writes nothing, decides
        // nothing, and reaches nothing the session did not already have.
        //
        // WHY IT HAD TO EXIST AT ALL. A drafting session hands an agent a
        // directory and a tool and tells it nothing else - no prompt, by
        // design, because the person drives; no CLAUDE.md in a customer tree,
        // because gg has no business writing one there. So the rules that
        // decide whether a document applies or waits at a gate were reachable
        // from nowhere: not from the files, which cannot state the rule that a
        // narrowing has no member for removal, and not from this repository,
        // which is not where the agent is standing. An agent that cannot learn
        // the rules writes documents a person has to correct, which is the
        // cost this was weighed against.
        //
        // WHY A TOOL RATHER THAN THE SERVER'S `instructions`, which reach a
        // model with no call: instructions belong to the SERVER, and this one
        // also serves nomination, decision and triage flights that want no
        // envelope doctrine at all. A tool costs nothing until it is called,
        // its call is in the transcript so whether the rules were read is
        // observable, and only a call can answer about THIS tenant - which
        // documents exist and what one of them looks like.
        //
        // AND IT IS INERT WHERE IT DOES NOT BELONG, by the same mechanism the
        // fifth uses: no working copy, no answer. The root is written into
        // this server's environment only by a drafting launch, so on a fleet
        // flight the tool is declared and refuses - which is what keeps
        // "what can an injected agent reach through this server" the same
        // answer it was.
        //
        // THE SEVENTH, AND THE ONLY ONE THAT STARTS A PROCESS. That is the
        // line worth pausing on, because this server's safety argument is that
        // it is a function of what it is handed. It still is: what it starts
        // is gg, under a verb, with a root it was given - and the credential
        // and the network stay in the child, which is the process whose job
        // they are. SelfInvocation.Under exists to cross exactly this
        // boundary, and the runner is already spawned across it.
        //
        // AND IT GRANTS THE AGENT NOTHING IT DID NOT HAVE. A drafting session
        // is attended; --allowedTools auto-approves rather than restricts
        // (--tools is the restricting flag, and the launch does not pass it);
        // gg is on the path. `gg airspace pull` is one guessed command line
        // away today. What the tool adds is the working copy FORCED rather
        // than inferred - the verb falls back to the current directory, which
        // for this server is wherever the client started it - and gg's own
        // refusals relayed as themselves.
        //
        // WHY IT IS GRANTED RATHER THAN LEFT TO A PROMPT, which is the one
        // thing that genuinely changes: an auto-approved tool pulls without
        // asking, and a pull rewrites a working copy from the network.
        // AirspacePullAsync raises DirtyWorkingCopyException as its FIRST
        // statement, before it touches the network, so it cannot bury
        // uncommitted work - including drafts written moments earlier. The
        // refusal is what makes the grant affordable, and the description says
        // so before an agent hits it.
        //
        // AN EIGHTH still has to make its own argument. None of these seven is it.
        var answers = await ExchangeAsync(
            """{"jsonrpc":"2.0","id":1,"method":"tools/list","params":{}}""");

        var tools = answers[0].RootElement.GetProperty("result").GetProperty("tools");
        await Assert.That(tools.GetArrayLength()).IsEqualTo(7)
            .Because("one channel, seven tools. An eighth is a decision somebody has to "
                   + "argue for, in this comment, where the last five were argued for.");

        var listed = tools.EnumerateArray()
            .Select(t => t.GetProperty("name").GetString()!).ToList();
        await Assert.That(listed).IsEquivalentTo(
            new[] { NominationTool.Name, HelpTool.Name, IntentTool.Name,
                    WorkItemProposalTool.Name, DocumentTool.Name,
                    AirspaceContextTool.Name, AirspacePullTool.Name })
            .Because("named rather than counted, so a tool cannot arrive by swapping which "
                   + "ones are declared. Found: " + string.Join(", ", listed));

        var tool = tools[0];
        await Assert.That(tool.GetProperty("name").GetString()).IsEqualTo(NominationTool.Name);

        var properties = tool.GetProperty("inputSchema").GetProperty("properties");
        await Assert.That(properties.TryGetProperty("work_kind", out _)).IsTrue();
        await Assert.That(properties.TryGetProperty("reason", out _)).IsTrue();

        var required = tool.GetProperty("inputSchema").GetProperty("required")
            .EnumerateArray().Select(r => r.GetString()!).ToList();
        await Assert.That(required).IsEquivalentTo(new[] { "work_kind", "reason" })
            .Because("a reason is required, because a nomination with none is a decision with "
                   + "no record of what it rested on.");
    }

    [Test]
    public async Task A_call_is_answered_with_a_receipt_naming_what_was_taken()
    {
        var answers = await ExchangeAsync(
            Call(1, """{"work_kind":"research","reason":"nobody has diagnosed it yet"}"""));

        var result = answers[0].RootElement.GetProperty("result");
        var text = result.GetProperty("content")[0].GetProperty("text").GetString()!;

        await Assert.That(text).Contains("research")
            .Because("echoed back, so an agent can see what was taken rather than assume.");
        await Assert.That(result.TryGetProperty("isError", out var flag) && flag.GetBoolean())
            .IsFalse();
    }

    [Test]
    public async Task The_receipt_says_it_grants_nothing()
    {
        // THE WORDING IS LOAD-BEARING. An agent that believed calling this had
        // opened a flight would stop waiting for one, or would try again. What
        // it is told is that a request was recorded and the work is over.
        var answers = await ExchangeAsync(
            Call(1, """{"work_kind":"research","reason":"nobody has diagnosed it yet"}"""));

        var text = answers[0].RootElement.GetProperty("result")
            .GetProperty("content")[0].GetProperty("text").GetString()!;

        await Assert.That(text).Contains("grants nothing");
    }

    [Test]
    public async Task A_call_missing_an_argument_is_an_error_result_rather_than_a_receipt()
    {
        // AN ERROR RESULT, NOT A PROTOCOL ERROR. The call reached the tool and
        // the tool refused it - which is a thing the agent can read and fix,
        // and a thing the extractor must not read as a nomination.
        foreach (var arguments in (string[])
            ["""{"reason":"no kind"}""", """{"work_kind":"research"}""", "{}"])
        {
            var answers = await ExchangeAsync(Call(1, arguments));
            var result = answers[0].RootElement.GetProperty("result");

            await Assert.That(result.GetProperty("isError").GetBoolean()).IsTrue()
                .Because($"'{arguments}' is not a nomination, and a receipt for it would be a "
                       + "value the runner then had to invent half of.");
        }
    }

    [Test]
    public async Task A_method_nobody_declared_is_an_error_carrying_the_id()
    {
        var answers = await ExchangeAsync(
            """{"jsonrpc":"2.0","id":7,"method":"resources/list","params":{}}""");

        await Assert.That(answers[0].RootElement.GetProperty("id").GetInt32()).IsEqualTo(7)
            .Because("a client matching responses to requests needs the id back even on an "
                   + "error, or it waits for ever.");
        await Assert.That(answers[0].RootElement.TryGetProperty("error", out _)).IsTrue();
    }

    [Test]
    public async Task A_line_that_will_not_parse_does_not_stop_the_server()
    {
        // A DEAD SERVER IS THE WORST OUTCOME, worse than a skipped line: the
        // agent loses the tool for the rest of the session and the flight
        // produces no nomination for a reason nothing records.
        var answers = await ExchangeAsync(
            "{not json",
            """{"jsonrpc":"2.0","id":1,"method":"tools/list","params":{}}""");

        await Assert.That(answers.Count).IsEqualTo(1);
        await Assert.That(answers[0].RootElement.GetProperty("id").GetInt32()).IsEqualTo(1);
    }

    [Test]
    public async Task It_returns_when_the_input_ends()
    {
        // HOLDS NOTHING OPEN. The agent's process owns this one's lifetime, so
        // a server that kept a handle after stdin closed would be a child the
        // runner has to reap.
        await Assert.That(await PlatformToolServer.RunAsync(
            new StringReader(string.Empty), new StringWriter())).IsEqualTo(0);
    }
}
