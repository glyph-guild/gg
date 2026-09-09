using System.Text.Json;
using Gg.Cli;
using Gg.Contracts;
using Gg.Local;

namespace Gg.Cli.Tests;

/// <summary>
/// Calling the proposal tool records a request and does nothing else - and it
/// takes the whole gamut of operations, because narrowing them here would be an
/// admission decision taken in the wrong place.
/// </summary>
/// <remarks>
/// <para>
/// <b>S35.2-02 and S35.2-03.</b> The threat model is the reason both are
/// asserted rather than described. This server runs as a child of a process
/// that is treated as compromised, so what an injected agent can reach through
/// it is the whole question: if the answer is "a tracker credential" then the
/// slice's first rule - the agent proposes and never acts - is a sentence in a
/// document rather than a property of the system.
/// </para>
/// <para>
/// <b>Why the gamut is not narrowed here.</b> A schema that offered `update`
/// and withheld `create` would be refusing a proposal, and refusing proposals
/// is what admission does, against a menu a person wrote on the destination.
/// Doing it in the tool would put the same rule in two places and hide one of
/// them from the record: a refusal at admission is a verdict somebody can read,
/// and a tool that never offered the argument leaves nothing behind at all.
/// </para>
/// <para>
/// <b>Structural completeness is a different thing, and it IS the tool's.</b>
/// An `update` naming no target is half a proposal - a value the runner would
/// have to invent the rest of - which is the argument the nomination's own
/// refusal of a missing reason is written under. Refusing that is not policy.
/// </para>
/// </remarks>
public class TheProposalToolActsOnNothingTests
{
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

    /// <summary>A tools/call for the proposal tool, named from the declaration.</summary>
    private static string Propose(string arguments) =>
        """{"jsonrpc":"2.0","id":9,"method":"tools/call","params":{"name":"TOOL","arguments":ARGS}}"""
            .Replace("TOOL", WorkItemProposalTool.Name, StringComparison.Ordinal)
            .Replace("ARGS", arguments, StringComparison.Ordinal);

    private static bool IsError(JsonDocument answer) =>
        answer.RootElement.TryGetProperty("result", out var result)
        && result.TryGetProperty("isError", out var flag)
        && flag.GetBoolean();

    private static string Said(JsonDocument answer) =>
        answer.RootElement.GetProperty("result").GetProperty("content")[0]
            .GetProperty("text").GetString()!;

    [Test]
    public async Task The_full_gamut_is_accepted_because_narrowing_it_is_admissions_job()
    {
        foreach (var operation in WorkItemOperations.All)
        {
            var arguments =
                """{"operation":"OP","target":"1421","score":"P1","reason":"the repro is attached"}"""
                    .Replace("OP", operation, StringComparison.Ordinal);

            var answers = await RecordingAsync(intentPath: null, Propose(arguments));

            await Assert.That(IsError(answers[0])).IsFalse()
                .Because($"'{operation}' is one of the operations this platform declares, and "
                       + "a tool that refused it would be taking admission's decision where "
                       + $"nothing records it. Said: {Said(answers[0])}");
        }
    }

    [Test]
    public async Task The_receipt_says_it_recorded_and_says_it_did_nothing_else()
    {
        var answers = await RecordingAsync(intentPath: null, Propose(
            """{"operation":"score","target":"1421","score":"P1","reason":"three weeks stale"}"""));

        var said = Said(answers[0]);

        // ECHOED IN CANONICAL FORM, the nomination's rule: an agent can see what
        // was taken rather than assume its own spelling survived. And told, in
        // the receipt and not only in the description, that nothing happened -
        // because the description is read once and the receipt is read every
        // time, and an agent that believes it has written to the tracker stops.
        await Assert.That(said).Contains("1421", StringComparison.Ordinal);
        await Assert.That(said.Contains("nothing", StringComparison.OrdinalIgnoreCase)).IsTrue()
            .Because("the receipt has to say the tracker was not touched, or an agent reads "
                   + $"'recorded' as 'done'. Said: {said}");
    }

    [Test]
    public async Task Half_a_proposal_is_refused_rather_than_recorded()
    {
        // AN ERROR RESULT, NOT A PROTOCOL ERROR - the nomination's distinction.
        // The call reached the tool and the tool refused it, which is something
        // the agent can read and fix, and something the extractor must not read
        // as a proposal.
        foreach (var (what, arguments) in ((string, string)[])
            [("no operation", """{"target":"1421","reason":"stale"}"""),
             ("no reason", """{"operation":"update","target":"1421"}"""),
             ("an update with no target", """{"operation":"update","reason":"stale"}"""),
             ("a score with no score", """{"operation":"score","target":"1421","reason":"stale"}"""),
             ("an operation nobody declared", """{"operation":"delete","target":"1421","reason":"stale"}""")])
        {
            var answers = await RecordingAsync(intentPath: null, Propose(arguments));

            await Assert.That(IsError(answers[0])).IsTrue()
                .Because($"{what} is half a proposal, and half a proposal is a value the "
                       + $"runner would have to invent the rest of. Said: {Said(answers[0])}");

            await Assert.That(Said(answers[0]).Contains("Nothing was recorded", StringComparison.Ordinal))
                .IsTrue()
                .Because("the refusal has to say nothing was kept, or an agent retries a "
                       + $"proposal it believes it already made. Said: {Said(answers[0])}");
        }
    }

    [Test]
    public async Task A_create_needs_no_target_because_the_tracker_has_not_made_one_yet()
    {
        var answers = await RecordingAsync(intentPath: null, Propose(
            """{"operation":"create","reason":"the crash has no item and three people hit it"}"""));

        await Assert.That(IsError(answers[0])).IsFalse()
            .Because("an item that does not exist has no id, and requiring one would make "
                   + $"`create` the one operation nobody can propose. Said: {Said(answers[0])}");
    }

    [Test]
    public async Task Nothing_it_is_handed_is_written_anywhere()
    {
        // THE INTENT TOOL WRITES A FILE AND THIS ONE MUST NOT. They are on one
        // server and the path is handed to both, so the assertion is that the
        // proposal arm leaves it alone rather than that the server has nowhere
        // to write - which would prove nothing about the arm under test.
        var somewhere = Directory.CreateDirectory(Path.Combine(
            Path.GetTempPath(), "gg-proposal-test-" + Guid.NewGuid().ToString("N")[..8]));
        var path = Path.Combine(somewhere.FullName, "intent.txt");

        try
        {
            var answers = await RecordingAsync(path, Propose(
                """{"operation":"update","target":"1421","reason":"the repro is attached"}"""));

            await Assert.That(IsError(answers[0])).IsFalse()
                .Because("a receipt, not a refusal: " + Said(answers[0]));

            await Assert.That(somewhere.EnumerateFileSystemInfos().Any()).IsFalse()
                .Because("proposing writes nothing: the record is the tool CALL in the "
                       + "transcript, which the runner reads, and a file here would be a "
                       + "second channel nobody admits over. Found: " + string.Join(", ",
                           somewhere.EnumerateFileSystemInfos().Select(f => f.Name)));
        }
        finally
        {
            somewhere.Delete(recursive: true);
        }
    }

    [Test]
    public async Task No_tracker_credential_is_anywhere_near_the_process_that_serves_this()
    {
        // The same scan HelpToolLaunchTests runs for things that WAIT, one
        // hazard over: things that REACH. An injected agent that gets to this
        // server can at most record a request admission will refuse, and that
        // sentence is only true while this list stays empty.
        var source = await File.ReadAllTextAsync(PlatformSource("PlatformToolServer.cs"));

        foreach (var reaching in (string[])
            ["ICredentialResolver", "Credential", "Secret", "Token", "IWorkItemSource",
             "HttpClient", "Environment.GetEnvironmentVariable"])
        {
            await Assert.That(source.Contains(reaching, StringComparison.Ordinal)).IsFalse()
                .Because($"'{reaching}' in this server would put a way OUT in the one process "
                       + "the threat model treats as reachable by an injected agent. The "
                       + "write is the runner's, after admission, and the runner is not this.");
        }
    }

    [Test]
    public async Task The_score_is_carried_as_given_rather_than_parsed()
    {
        // S35.3-04 arriving one step early, because the tool's schema is where
        // it would first get decided. What a score MEANS is the envelope's and
        // the skill's; a tool that took an integer would have settled that here,
        // for every tracker and every rubric, in a JSON type declaration.
        var answers = await RecordingAsync(intentPath: null, Propose(
            """
            {"operation":"score","target":"1421","score":"high / 2 of 3 reporters blocked",
             "reason":"two independent repros"}
            """));

        await Assert.That(IsError(answers[0])).IsFalse()
            .Because("a score is whatever the rubric says it is, and this one is a sentence "
                   + $"because somebody's rubric asks for one. Said: {Said(answers[0])}");

        var schema = await SchemaAsync();
        await Assert.That(schema.GetProperty("properties").GetProperty("score")
            .GetProperty("type").GetString()).IsEqualTo("string")
            .Because("typing it as a number decides what a score is, here, for everybody.");
    }

    [Test]
    public async Task The_detail_is_opaque_and_the_tool_does_not_read_it()
    {
        // THE MEMBER NOBODY HERE INTERPRETS. It exists so another agent on
        // another day can re-evaluate what this one thought, which only works
        // if today's tool takes it whole instead of validating it into a shape
        // that made sense on the day it was written.
        var answers = await RecordingAsync(intentPath: null, Propose(
            """
            {"operation":"score","target":"1421","score":"P1","reason":"two repros",
             "detail":{"rubric":"reach x severity","reach":{"reporters":3,"tenants":2},
                       "confidence":0.7,"considered":["dup of 1189","not a regression"]}}
            """));

        await Assert.That(IsError(answers[0])).IsFalse()
            .Because("nothing here knows what a rubric is, and that is the point: a tool "
                   + "that validated this would be deciding today what a later reader may "
                   + $"ask. Said: {Said(answers[0])}");

        var schema = await SchemaAsync();
        await Assert.That(schema.GetProperty("properties").GetProperty("detail")
            .TryGetProperty("properties", out _)).IsFalse()
            .Because("an opaque member with a declared shape is not opaque. It is a field "
                   + "whose contents are the agent's, and the record says so by having none.");
    }

    private static async Task<JsonElement> SchemaAsync()
    {
        var answers = await RecordingAsync(
            intentPath: null, """{"jsonrpc":"2.0","id":1,"method":"tools/list"}""");

        return answers[0].RootElement.GetProperty("result").GetProperty("tools").EnumerateArray()
            .Single(tool => tool.GetProperty("name").GetString() == WorkItemProposalTool.Name)
            .GetProperty("inputSchema");
    }

    private static string PlatformSource(string file)
    {
        var here = new DirectoryInfo(AppContext.BaseDirectory);
        while (here is not null && !Directory.Exists(Path.Combine(here.FullName, "Gg.Cli")))
        {
            here = here.Parent;
        }

        return Path.Combine(here!.FullName, "Gg.Cli", file);
    }
}
