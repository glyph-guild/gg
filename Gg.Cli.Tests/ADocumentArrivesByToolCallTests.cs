using System.Text.Json;
using Gg.Contracts;
using Gg.Contracts.Authoring;
using Gg.Local;

namespace Gg.Cli.Tests;

/// <summary>
/// The tool an agent hands a drafted envelope document back with.
/// </summary>
/// <remarks>
/// <para>
/// <b>The refusal is the feature.</b>
/// <c>EnvelopeCommands.Validate</c> parses, diagnoses and picks the role from
/// the path, and it contacts nothing — so the tool can answer <i>accepted</i>
/// or the diagnosis, and an agent that got the schema wrong is told exactly
/// what by the door it is knocking on. That is worth more than any amount of
/// prompt: the schema an agent learns from a paragraph is the schema as it was
/// the week the paragraph was written.
/// </para>
/// <para>
/// <b>The agent names a document; gg decides where that lives.</b> A role and
/// a name go through <c>AirspaceNames.PathFor</c> — the one table read both ways
/// — so nothing about the layout is the agent's to choose, and a name no path
/// can carry is refused rather than written somewhere approximate.
/// </para>
/// <para>
/// <b>And the precondition is not the agent's to state.</b> A pulled file
/// carries <c>based-on: pci@v2</c>, which is what makes apply refuse when
/// somebody else's amendment landed first. ADR-0016 § 4 is explicit that it is
/// <i>"a precondition about the stream, honored because the applier states
/// it"</i> — and the applier is gg. So the held line is preserved and any the
/// agent sent is dropped: a draft that silently cleared it would turn every
/// apply after it into a blind overwrite of a colleague's work.
/// </para>
/// </remarks>
public class ADocumentArrivesByToolCallTests
{
    private static async Task<IReadOnlyList<JsonDocument>> RecordingAsync(
        string? documentRoot, params string[] lines)
    {
        var output = new StringWriter();
        await PlatformToolServer.RunAsync(
            new StringReader(string.Join('\n', lines)), output,
            intentPath: null, documentRoot: documentRoot);

        return output.ToString()
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => JsonDocument.Parse(line))
            .ToList();
    }

    private static DirectoryInfo Somewhere() => Directory.CreateDirectory(Path.Combine(
        Path.GetTempPath(), "gg-document-test-" + Guid.NewGuid().ToString("N")[..8]));

    /// <summary>A tools/call for the document tool, named from the declaration.</summary>
    private static string Call(string role, string name, string document) =>
        JsonSerializer.Serialize(new
        {
            jsonrpc = "2.0",
            id = 7,
            method = "tools/call",
            @params = new
            {
                name = DocumentTool.Name,
                arguments = new Dictionary<string, string>
                {
                    [DocumentTool.RoleArgument] = role,
                    [DocumentTool.NameArgument] = name,
                    [DocumentTool.DocumentArgument] = document,
                },
            },
        });

    private static bool IsError(JsonDocument answer) =>
        answer.RootElement.TryGetProperty("result", out var result)
        && result.TryGetProperty("isError", out var flag)
        && flag.GetBoolean();

    private static string Said(JsonDocument answer) =>
        answer.RootElement.GetProperty("result").GetProperty("content")[0]
            .GetProperty("text").GetString()!;

    /// <summary>A valid narrowing, rendered rather than typed.</summary>
    private static string ANarrowing(string obligation = "pci-review") =>
        EnvelopeText.Render(new EnvelopeNarrowing
        {
            Obligations =
            [
                new Obligation
                {
                    Id = obligation,
                    Check = ObligationChecks.Human,
                    Approver = "an-auditor",
                },
            ],
        });

    [Test]
    public async Task The_tool_is_named_once_and_the_three_spellings_agree()
    {
        // THE FOURTH INSTANCE OF THIS AGREEMENT. The launch grants the
        // qualified name, tools/list declares the bare one, and whatever reads
        // the result looks for the qualified one - and every previous
        // disagreement was silent in the worst direction.
        // LISTED AGAINST A DRAFTING SESSION, which is the only shape this
        // tool is offered in - a session with no working copy has nowhere for
        // a document to go, so offering it there would be a wrong answer
        // somebody has to be talked out of. One was.
        var declared = (await RecordingAsync(
                Somewhere().FullName, """{"jsonrpc":"2.0","id":1,"method":"tools/list"}"""))[0]
            .RootElement.GetProperty("result").GetProperty("tools")
            .EnumerateArray()
            .Select(t => t.GetProperty("name").GetString())
            .ToList();

        await Assert.That(declared).Contains(DocumentTool.Name)
            .Because("an agent granted a tool the server does not declare is an agent "
                   + "holding a grant for nothing.");

        await Assert.That(DocumentTool.Qualified)
            .IsEqualTo($"mcp__{DocumentTool.Server}__{DocumentTool.Name}");

        await Assert.That(DocumentTool.Server).IsEqualTo(IntentTool.Server)
            .Because("one server key, tools granted individually - a second key is a second "
                   + "prefix to keep straight, and it shadows the first if an operator "
                   + "configures one.");
    }

    [Test]
    public async Task A_document_lands_where_its_name_and_role_put_it()
    {
        var root = Somewhere();
        try
        {
            var answers = await RecordingAsync(
                root.FullName, Call(Roles.Narrowing, "pci", ANarrowing()));

            await Assert.That(IsError(answers[0])).IsFalse().Because(Said(answers[0]));

            var landed = Path.Combine(root.FullName, "airspace", "narrowings", "pci.yaml");

            await Assert.That(File.Exists(landed)).IsTrue()
                .Because("the path comes from AirspaceNames.PathFor, which is the same table "
                       + "pull renders through - so a drafted document sits exactly where a "
                       + "pulled one would and diff compares them without knowing which "
                       + "wrote it.");

            await Assert.That(await File.ReadAllTextAsync(landed))
                .Contains("pci-review", StringComparison.Ordinal);
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    [Test]
    public async Task A_document_that_does_not_parse_is_refused_with_its_diagnosis()
    {
        // THE FEEDBACK LOOP, and the whole reason this is a tool rather than a
        // path the agent writes to. It is told what is wrong, in the parser's
        // own words, and can fix it and call again.
        var root = Somewhere();
        try
        {
            var answers = await RecordingAsync(
                root.FullName,
                Call(Roles.Narrowing, "pci", "obligations:\n  - id: pci\n    check: banana\n"));

            await Assert.That(IsError(answers[0])).IsTrue();
            await Assert.That(Said(answers[0])).IsNotEmpty();

            await Assert.That(File.Exists(
                    Path.Combine(root.FullName, "airspace", "narrowings", "pci.yaml")))
                .IsFalse()
                .Because("nothing is written when nothing parsed - a half-written document "
                       + "is one the next diff reports as unreadable and every apply stops "
                       + "on.");
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    [Test]
    public async Task A_document_carrying_a_context_block_is_refused_as_a_narrowing()
    {
        // THE MECHANISM BEHIND ADR-0018 § 7's FOURTH REFUSAL, which is the one
        // easy to miss because the document is not malformed, only misplaced:
        // copying root.yaml as a starting point is the obvious way for an agent
        // to begin, and a complete envelope is a valid document and an invalid
        // narrowing. What is asserted here is narrower than that sentence and
        // is what implements it - the fragment parser has no field for
        // `context:` at all, so the role chosen from the PATH is what refuses
        // it rather than anything about the shape being noticed later.
        var root = Somewhere();
        try
        {
            var answers = await RecordingAsync(
                root.FullName,
                Call(Roles.Narrowing, "pci",
                    "context:\n  scope: \"src/**\"\n  constitution: \"1.0.0\"\n"
                  + ANarrowing()));

            await Assert.That(IsError(answers[0])).IsTrue()
                .Because("scope and constitution are the two fields a narrowing exists to "
                       + "keep away from a team, and the strongest form of that rule is one "
                       + "the type cannot express - so the parser has nowhere to put them.");
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    [Test]
    public async Task A_name_no_path_can_carry_is_refused()
    {
        var root = Somewhere();
        try
        {
            var answers = await RecordingAsync(
                root.FullName, Call(Roles.Narrowing, "../../etc/passwd", ANarrowing()));

            await Assert.That(IsError(answers[0])).IsTrue()
                .Because("twelve candidate names were once declared against a live control "
                       + "plane and eleven were accepted, that one among them. The name rule "
                       + "is the guard and this is the door it stands in.");
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    [Test]
    public async Task The_held_precondition_survives_a_draft()
    {
        // THE PRECONDITION IS NOT THE AGENT'S TO STATE. `based-on:` is what
        // makes apply refuse when somebody else's amendment landed first, and a
        // draft that cleared it would turn the next apply into a blind
        // overwrite of a colleague's work.
        var root = Somewhere();
        try
        {
            var at = Path.Combine(root.FullName, "airspace", "narrowings");
            Directory.CreateDirectory(at);
            await File.WriteAllTextAsync(
                Path.Combine(at, "pci.yaml"), "based-on: pci@v4\n" + ANarrowing());

            _ = await RecordingAsync(
                root.FullName, Call(Roles.Narrowing, "pci", ANarrowing("pci-audit")));

            var text = await File.ReadAllTextAsync(Path.Combine(at, "pci.yaml"));

            await Assert.That(text).StartsWith("based-on: pci@v4")
                .Because("pull wrote that line and apply states it back; an agent's draft is "
                       + "a change to the document, not a claim about which version of the "
                       + "stream it was made against.");
            await Assert.That(text).Contains("pci-audit", StringComparison.Ordinal)
                .Because("and the draft itself did land - preserving the precondition must "
                         + "not mean keeping the old document.");
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    [Test]
    public async Task A_precondition_the_agent_invented_is_dropped()
    {
        var root = Somewhere();
        try
        {
            var at = Path.Combine(root.FullName, "airspace", "narrowings");
            Directory.CreateDirectory(at);
            await File.WriteAllTextAsync(
                Path.Combine(at, "pci.yaml"), "based-on: pci@v4\n" + ANarrowing());

            _ = await RecordingAsync(
                root.FullName,
                Call(Roles.Narrowing, "pci", "based-on: pci@v99\n" + ANarrowing("pci-audit")));

            var text = await File.ReadAllTextAsync(Path.Combine(at, "pci.yaml"));

            await Assert.That(text).StartsWith("based-on: pci@v4")
                .Because("a version an agent chose is a claim about the stream it cannot have "
                       + "read, and stating a later one than was pulled would defeat the "
                       + "stale-working-copy refusal on purpose.");
            await Assert.That(text).DoesNotContain("pci@v99", StringComparison.Ordinal);
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    [Test]
    public async Task A_new_document_has_no_precondition_to_keep()
    {
        // GENESIS. Nothing was pulled for this name, so there is no version to
        // sit below - and inventing one would refuse the first apply of a
        // document that has no predecessor.
        var root = Somewhere();
        try
        {
            _ = await RecordingAsync(
                root.FullName, Call(Roles.Narrowing, "brand-new", ANarrowing()));

            var text = await File.ReadAllTextAsync(
                Path.Combine(root.FullName, "airspace", "narrowings", "brand-new.yaml"));

            await Assert.That(text).DoesNotContain("based-on", StringComparison.Ordinal);
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    [Test]
    public async Task Without_a_working_copy_the_tool_refuses_rather_than_guessing()
    {
        var answers = await RecordingAsync(null, Call(Roles.Narrowing, "pci", ANarrowing()));

        await Assert.That(IsError(answers[0])).IsTrue()
            .Because("a server with nowhere to put a document has been started wrong, and "
                   + "writing one somewhere derived would put a governance document in a "
                   + "directory nobody is looking at.");
    }
}
