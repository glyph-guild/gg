using System.Text.Json;
using Gg.Cli;
using Gg.Contracts;
using Gg.Local;

namespace Gg.Cli.Tests;

/// <summary>
/// A drafting agent can ask what an envelope is, and is told.
/// </summary>
/// <remarks>
/// <para>
/// <b>WHAT A DRAFTING SESSION ACTUALLY HANDS THE AGENT TODAY: a directory and
/// a tool.</b> <c>PtyDraftSession</c> passes no prompt - deliberately, because
/// the person drives - and the airspace tree carries no <c>CLAUDE.md</c>. So
/// the only envelope text that reaches the model unasked is
/// <c>submit_document</c>'s own description, and the doctrine is nowhere: that
/// a narrowing has one key and cannot change or remove anything, that adding
/// an obligation tightens while removing or editing one widens and opens a
/// gate, that <c>based-on:</c> is a precondition gg states rather than
/// provenance the author writes.
/// </para>
/// <para>
/// <b>None of it is discoverable from the tree either, which is the part that
/// is easy to miss.</b> "In the form the working copy already uses" points at
/// nothing for a role the tenant has no document for - and a tenant with no
/// narrowings is the ordinary case for somebody about to write their first
/// one. The ADRs that hold the reasoning live in the gg repository, which is
/// not where the agent is standing.
/// </para>
/// <para>
/// <b>Pulled rather than pushed, and computed rather than recited.</b> A tool
/// costs nothing until it is called, which matters because this server also
/// serves unattended flights that want none of this; and being a tool means
/// the answer can be about THIS tenant - which documents exist, what they are
/// called, and one of them rendered as a worked example. An MCP server's
/// <c>instructions</c> could carry a static essay; only a call can show the
/// narrowing that is actually there.
/// </para>
/// </remarks>
public class TheAgentIsToldHowEnvelopesWorkTests
{
    /// <summary>The wire name, spelled as the agent sees it.</summary>
    /// <remarks>
    /// A literal here on purpose. This is the contract with the client, and a
    /// test that reads the same constant the server writes would agree with
    /// itself about a name nobody outside can see.
    /// </remarks>
    private const string Tool = "describe_airspace";

    private static async Task<IReadOnlyList<JsonDocument>> RecordingAsync(
        string? documentRoot, string? inForce, params string[] lines)
    {
        var output = new StringWriter();
        await PlatformToolServer.RunAsync(
            new StringReader(string.Join('\n', lines)), output,
            intentPath: null, documentRoot: documentRoot, inForce: inForce);

        return output.ToString()
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => JsonDocument.Parse(line))
            .ToList();
    }

    private static string Call() => JsonSerializer.Serialize(new
    {
        jsonrpc = "2.0",
        id = 9,
        method = "tools/call",
        @params = new { name = Tool, arguments = new Dictionary<string, string>() },
    });

    private static string Listing() =>
        """{"jsonrpc":"2.0","id":1,"method":"tools/list","params":{}}""";

    private static DirectoryInfo Somewhere() => Directory.CreateDirectory(Path.Combine(
        Path.GetTempPath(), "gg-context-test-" + Guid.NewGuid().ToString("N")[..8]));

    private static string Said(JsonDocument answer) =>
        answer.RootElement.GetProperty("result").GetProperty("content")[0]
            .GetProperty("text").GetString() ?? "";

    private static bool Failed(JsonDocument answer) =>
        answer.RootElement.GetProperty("result").TryGetProperty("isError", out var flag)
        && flag.GetBoolean();

    [Test]
    public async Task The_server_offers_it()
    {
        var answers = await RecordingAsync(null, null, Listing());

        var names = answers[0].RootElement.GetProperty("result").GetProperty("tools")
            .EnumerateArray()
            .Select(tool => tool.GetProperty("name").GetString())
            .ToList();

        await Assert.That(names).Contains(Tool)
            .Because("a tool nobody declares is one no agent can call, whatever the "
                   + "description says. Declared: " + string.Join(", ", names));
    }

    [Test]
    public async Task Its_description_tells_an_agent_to_call_it_before_drafting()
    {
        // THE ONLY CHANNEL PROVEN TO ARRIVE. The tool list is in context before
        // the agent's first token, so submit_document's description is where a
        // pointer to this one has to live - an instruction anywhere else is one
        // the agent has to already be looking for.
        var answers = await RecordingAsync(null, null, Listing());

        var submit = answers[0].RootElement.GetProperty("result").GetProperty("tools")
            .EnumerateArray()
            .Single(tool => tool.GetProperty("name").GetString() == "submit_document")
            .GetProperty("description").GetString() ?? "";

        await Assert.That(submit).Contains(Tool, StringComparison.Ordinal)
            .Because("the submit tool is the one an agent reads when it decides to write a "
                   + "document, which is exactly when it is too late to not know the rules. "
                   + "Description: " + submit);
    }

    [Test]
    public async Task Without_a_working_copy_it_refuses_like_its_neighbour()
    {
        // STRUCTURALLY INERT ON AN UNATTENDED FLIGHT, which is how this server
        // keeps a tool from being reachable where it does not belong: the root
        // is only ever set for a drafting launch, and TheProposalToolActsOn-
        // NothingTests states why what an injected agent can reach through this
        // server is the whole question.
        var answers = await RecordingAsync(null, null, Call());

        await Assert.That(Failed(answers[0])).IsTrue()
            .Because("no working copy means no drafting session, and answering anyway "
                   + "would offer envelope doctrine to a flight that never asked for it.");
    }

    [Test]
    public async Task An_empty_tree_is_answered_rather_than_refused()
    {
        var tree = Somewhere();
        try
        {
            var said = Said((await RecordingAsync(tree.FullName, null, Call()))[0]);

            await Assert.That(Failed((await RecordingAsync(tree.FullName, null, Call()))[0])).IsFalse()
                .Because("the doctrine is true whether or not anything has been pulled, and "
                       + "refusing would leave an agent with no way to learn the rules in "
                       + "the one situation where it certainly does not know them.");

            await Assert.That(said).Contains("pull", StringComparison.OrdinalIgnoreCase)
                .Because("an empty tree has one useful next step and the answer has to name "
                       + "it. Said: " + said);
        }
        finally
        {
            tree.Delete(recursive: true);
        }
    }

    [Test]
    public async Task It_names_every_role_from_the_contract()
    {
        var tree = Somewhere();
        try
        {
            var said = Said((await RecordingAsync(tree.FullName, null, Call()))[0]);

            foreach (var role in Roles.All)
            {
                await Assert.That(said).Contains(role, StringComparison.Ordinal)
                    .Because($"'{role}' is a role a document can be applied to, and one left "
                           + "out is one an agent will never write. Said: " + said);
            }
        }
        finally
        {
            tree.Delete(recursive: true);
        }
    }

    [Test]
    public async Task It_says_the_three_things_the_tree_cannot_show()
    {
        // EACH IS SOURCED FROM THE TYPE THAT ENFORCES IT, not from prose:
        // EnvelopeNarrowing declares one member and says "never what it
        // changes - there is no such member"; EnvelopeDirection.Obligations
        // says additions tighten while a removal or an edited body widens; and
        // EnvelopeYaml's BasedOn is "a precondition the applier states,
        // honoured at apply and then gone".
        var tree = Somewhere();
        try
        {
            var said = Said((await RecordingAsync(tree.FullName, null, Call()))[0]);

            await Assert.That(said).Contains("obligations", StringComparison.OrdinalIgnoreCase)
                .Because("a narrowing has exactly one key and an agent that does not know "
                       + "its name cannot write one. Said: " + said);

            await Assert.That(said).Contains("tighten", StringComparison.OrdinalIgnoreCase)
                .Because("what the agent writes decides whether the apply lands or waits at "
                       + "a gate, and it cannot work that out from a document. Said: " + said);

            await Assert.That(said).Contains("widen", StringComparison.OrdinalIgnoreCase)
                .Because("the other half of the same fact, and the expensive one. Said: "
                       + said);

            await Assert.That(said).Contains("based-on", StringComparison.OrdinalIgnoreCase)
                .Because("a document that carries its own based-on line states a "
                       + "precondition its author invented. Said: " + said);
        }
        finally
        {
            tree.Delete(recursive: true);
        }
    }

    [Test]
    public async Task The_rules_in_force_reach_the_agent_when_the_session_has_them()
    {
        // THE READ ALREADY HAPPENS AND THE AGENT NEVER SEES IT. PtyDraftSession
        // fetches the composed envelope before the child starts - "READ ONCE,
        // BEFORE THE CHILD HAS THE SCREEN" - and renders it into gg's own
        // panel for the PERSON to toggle. The agent is drafting a document
        // that will be composed into exactly that, and cannot see it.
        //
        // HANDED, NOT FETCHED. The server holds no control-plane client and
        // must go on holding none; this is the same arrangement the working
        // copy arrives by, one variable over.
        var tree = Somewhere();
        try
        {
            var said = Said((await RecordingAsync(
                tree.FullName,
                "in force: v7, last changed 2026-09-10 by an-owner\n\n"
                + "context:\n  scope: \"payments/**\"\n"
                + "obligations:\n  pci-review:\n    check: human\n"
                + "    # layer: narrowing, pci\n",
                Call()))[0]);

            await Assert.That(said).Contains("in force: v7", StringComparison.Ordinal)
                .Because("a document is drafted AGAINST the rules it will be composed into, "
                       + "and which version those are is the difference between advice and "
                       + "a precondition. Said: " + said);

            await Assert.That(said).Contains("pci-review", StringComparison.Ordinal)
                .Because("the composed obligations are what a new narrowing has to sit "
                       + "beside without duplicating. Said: " + said);

            await Assert.That(said).Contains("# layer:", StringComparison.Ordinal)
                .Because("RenderComposed annotates each obligation with the layer that "
                       + "declared it, which is the one thing a composed view can say that "
                       + "the documents separately cannot - and it had no caller anywhere "
                       + "in either repository until now. Said: " + said);
        }
        finally
        {
            tree.Delete(recursive: true);
        }
    }

    [Test]
    public async Task Not_having_them_is_said_rather_than_left_to_look_like_none()
    {
        // SILENCE WOULD READ AS "THERE ARE NO RULES", which is the one wrong
        // conclusion available here. A console with no reachable control plane
        // starts a drafting session anyway - deliberately, since the working
        // copy is local - so this is an ordinary state, not a fault.
        var tree = Somewhere();
        try
        {
            var said = Said((await RecordingAsync(tree.FullName, null, Call()))[0]);

            await Assert.That(said).Contains("in force", StringComparison.OrdinalIgnoreCase)
                .Because("an agent told nothing about the rules in force will assume the "
                       + "documents in front of it are all there is. Said: " + said);
        }
        finally
        {
            tree.Delete(recursive: true);
        }
    }

    [Test]
    public async Task Every_key_a_document_may_carry_is_explained()
    {
        // A KEY THE EXPLANATION DOES NOT NAME IS ONE AN AGENT WILL NEVER
        // WRITE. The parser accepts a fixed vocabulary and an agent has no
        // way to discover it: the tenant's own documents show the keys the
        // tenant happens to use, which is a fraction of them, and there is
        // nothing else in the working copy to read.
        //
        // ENUMERATED FROM THE PARSER rather than typed here, so a key added
        // to the schema turns this red instead of quietly becoming a key
        // nobody is told about.
        var tree = Somewhere();
        try
        {
            var said = Said((await RecordingAsync(tree.FullName, null, Call()))[0]);

            var missing = EnvelopeYamlKeys()
                .Where(key => !said.Contains(key, StringComparison.Ordinal))
                .ToList();

            await Assert.That(missing).IsEmpty()
                .Because("these are keys a document may carry and nothing tells an agent "
                       + "they exist. Missing: " + string.Join(", ", missing));
        }
        finally
        {
            tree.Delete(recursive: true);
        }
    }

    [Test]
    public async Task Every_value_a_key_will_accept_is_listed()
    {
        // THE SECOND HALF, AND THE ONE A REFUSAL TEACHES SLOWLY. Knowing that
        // `check:` exists does not tell anybody it takes `machine` or
        // `human` and nothing else - an agent finds that out by being refused
        // once per guess, and only for the key it happened to guess at.
        //
        // READ OFF THE CONTRACT, so the list cannot say something the parser
        // would refuse.
        var tree = Somewhere();
        try
        {
            var said = Said((await RecordingAsync(tree.FullName, null, Call()))[0]);

            var vocabularies = new (string Key, IReadOnlyList<string> Values)[]
            {
                ("check", ObligationChecks.All),
                ("rule", ObligationPredicates.All),
                ("moves", LoopMoves.All),
                ("executor", ExecutorRungs.All),
                ("on-exhaustion", ExhaustionPolicies.All),
                ("kind", DestinationKinds.All),
                ("evidence", EvidenceItems.All),
            };

            foreach (var (key, values) in vocabularies)
            {
                foreach (var value in values)
                {
                    await Assert.That(said).Contains(value, StringComparison.Ordinal)
                        .Because($"`{key}` takes '{value}' and an agent that has not been "
                               + "told guesses, is refused, and guesses again.");
                }
            }
        }
        finally
        {
            tree.Delete(recursive: true);
        }
    }

    [Test]
    public async Task It_says_what_an_envelope_is_for_in_words_anybody_reads()
    {
        // THE PART THAT IS NOT A SCHEMA. An agent asked "what does this
        // envelope do?" has to be able to answer, and a list of keys does not
        // let it: the words this platform turns on - a flight, a gate, an
        // approver, a work kind - are its own, and nothing defines them
        // anywhere the agent can reach.
        var tree = Somewhere();
        try
        {
            var said = Said((await RecordingAsync(tree.FullName, null, Call()))[0]);

            foreach (var word in (string[])["flight", "gate", "approver", "compose"])
            {
                await Assert.That(said).Contains(word, StringComparison.OrdinalIgnoreCase)
                    .Because($"'{word}' is one of this platform's own words and an agent "
                           + "cannot explain the rules to somebody without it.");
            }
        }
        finally
        {
            tree.Delete(recursive: true);
        }
    }

    /// <summary>Every key the envelope parser reads, from the parser.</summary>
    /// <remarks>
    /// <b>A superset, deliberately</b>, in the trade <c>Sources.EveryGgVariable</c>
    /// makes for the same kind of sweep: it matches a quoted lower-case word in
    /// the parser, so it reports a few that are values rather than keys.
    /// Over-reporting costs an exemption with a reason; under-reporting costs a
    /// key nobody can find.
    /// </remarks>
    private static IReadOnlyList<string> EnvelopeYamlKeys()
    {
        var at = new DirectoryInfo(AppContext.BaseDirectory);

        while (at is not null && !File.Exists(Path.Combine(at.FullName, "Gg.sln")))
        {
            at = at.Parent;
        }

        var parser = File.ReadAllText(Path.Combine(
            at!.FullName, "Gg.Contracts", "EnvelopeYaml.cs"));

        // NOT KEYS, and each says why rather than being quietly dropped.
        var exempt = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["true"] = "a boolean value, not a key.",
            ["false"] = "the other one.",
            ["based-on"] = "gg writes this line and an agent must NOT, which the "
                         + "explanation says in those terms rather than by listing it "
                         + "among the keys somebody may write.",
            ["provenance"] = "assigned by the composer and refused when authored, so "
                           + "naming it as writable would invite exactly the document "
                           + "the parser rejects.",
        };

        return [.. System.Text.RegularExpressions.Regex
            .Matches(parser, "\"([a-z][a-z-]{2,})\"")
            .Select(match => match.Groups[1].Value)
            .Distinct(StringComparer.Ordinal)
            .Where(key => !exempt.ContainsKey(key))
            .Order(StringComparer.Ordinal)];
    }

    [Test]
    public async Task A_tree_with_documents_is_described_and_one_is_shown()
    {
        var tree = Somewhere();
        try
        {
            var at = Path.Combine(tree.FullName, "airspace", "narrowings");
            Directory.CreateDirectory(at);
            await File.WriteAllTextAsync(Path.Combine(at, "pci.yaml"),
                "based-on: pci@v2\nobligations:\n  pci-review:\n    check: human\n"
              + "    approver: an-auditor\n");

            var said = Said((await RecordingAsync(tree.FullName, null, Call()))[0]);

            await Assert.That(said).Contains("pci", StringComparison.Ordinal)
                .Because("the answer is about THIS tenant or it is an essay. Said: " + said);

            await Assert.That(said).Contains("an-auditor", StringComparison.Ordinal)
                .Because("a worked example is the one thing a tenant's own tree can give "
                       + "that no wording can, and it is why this is a tool rather than a "
                       + "paragraph in a description. Said: " + said);
        }
        finally
        {
            tree.Delete(recursive: true);
        }
    }
}
