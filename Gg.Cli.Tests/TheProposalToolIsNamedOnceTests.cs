using System.Text.Json;
using Gg.Cli;
using Gg.Contracts;
using Gg.Local;

namespace Gg.Cli.Tests;

/// <summary>
/// The tool a triage flight proposes through: one name, and a description that
/// tells the agent proposing is not doing.
/// </summary>
/// <remarks>
/// <para>
/// <b>S35.2-01 and S35.2-04.</b> The name is asserted for the reason the same
/// assertion exists three times already in this feature area: the launch's
/// allow-list carries the qualified spelling, <c>tools/list</c> declares the
/// bare one, and the extractor looks for the qualified one in the transcript.
/// A disagreement between any two of them is SILENT - an agent granted a tool
/// that does not exist, or a value declared and never found.
/// </para>
/// <para>
/// <b>The description is not decoration and it is not documentation.</b> It is
/// the only thing that tells an agent what calling this means, and the three
/// claims asserted below are the three an agent gets wrong on its own: that a
/// proposal is not a write, that declining to propose is a real answer, and
/// that a refusal is an ordinary outcome rather than a failure it should route
/// around. <c>NominationTool</c>'s wording is the model because a deployment
/// proved it - three measured triage runs, and none of them tried to do the
/// work instead.
/// </para>
/// </remarks>
public class TheProposalToolIsNamedOnceTests
{
    private static async Task<IReadOnlyList<JsonDocument>> RecordingAsync(params string[] lines)
    {
        var output = new StringWriter();
        await PlatformToolServer.RunAsync(
            new StringReader(string.Join('\n', lines)), output, intentPath: null);

        return output.ToString()
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => JsonDocument.Parse(line))
            .ToList();
    }

    private static async Task<JsonElement> DeclaredAsync(string name)
    {
        var answers = await RecordingAsync("""{"jsonrpc":"2.0","id":1,"method":"tools/list"}""");

        return answers[0].RootElement.GetProperty("result").GetProperty("tools").EnumerateArray()
            .Single(tool => tool.GetProperty("name").GetString() == name);
    }

    [Test]
    public async Task One_declaration_owns_the_proposal_tools_three_spellings()
    {
        await Assert.That(WorkItemProposalTool.Qualified)
            .IsEqualTo($"mcp__{WorkItemProposalTool.Server}__{WorkItemProposalTool.Name}")
            .Because("the qualified name is composed from the other two rather than typed "
                   + "beside them, which is the only arrangement that cannot drift.");

        await Assert.That(WorkItemProposalTool.Server).IsEqualTo(NominationTool.Server)
            .Because("one server, and a second key would shadow the first if an operator "
                   + "ever configured a reader under it.");
    }

    [Test]
    public async Task It_is_declared_on_every_launch_and_granted_on_almost_none()
    {
        // The arrangement the server's own note argues for: the grant is decided
        // in the launch's allow-list, so a tools/list that varied by envelope
        // would be a second place the same rule lives - and the two places would
        // disagree the first time one of them was edited.
        var declared = (await RecordingAsync("""{"jsonrpc":"2.0","id":1,"method":"tools/list"}"""))[0]
            .RootElement.GetProperty("result").GetProperty("tools").EnumerateArray()
            .Select(tool => tool.GetProperty("name").GetString())
            .ToList();

        await Assert.That(declared).Contains(WorkItemProposalTool.Name)
            .Because("an agent cannot call a tool it was never offered, however the launch "
                   + "granted it. Declared: " + string.Join(", ", declared));

        await Assert.That(declared).Contains(NominationTool.Name)
            .Because("the tools already here keep working; this adds one rather than "
                   + "replacing the list.");
    }

    [Test]
    public async Task Its_description_says_proposing_is_not_doing()
    {
        var described = (await DeclaredAsync(WorkItemProposalTool.Name))
            .GetProperty("description").GetString()!;

        // THREE CLAIMS, and each one is a thing an agent gets wrong by default.
        // Asserted as phrases rather than as a length, because a description
        // that got shorter by losing one of them would still be a description.
        foreach (var (claim, said) in ((string, string)[])
            [("proposing is not doing", "changes nothing in the tracker"),
             ("declining is a real answer", "Declining is a real answer"),
             ("a refusal is ordinary", "refused")])
        {
            await Assert.That(described.Contains(said, StringComparison.OrdinalIgnoreCase))
                .IsTrue()
                .Because($"an agent has to be told that {claim}, and this description does "
                       + $"not say it. Looked for '{said}'. Description:\n{described}");
        }
    }

    [Test]
    public async Task An_agent_is_offered_every_argument_the_contract_requires()
    {
        // FOUND BY A POISON, and the poison was checking something else. The
        // contract began refusing a `field` proposal that sets nothing, and
        // for one commit the tool had no way to offer the fields - so an agent
        // could make a call the platform would always refuse. Nothing failed:
        // the extraction suite caught it indirectly and this suite, which is
        // about the tool's shape, said nothing at all.
        //
        // The rule is the one this file opens with, one argument over. Three
        // things must agree - the contract's members, this schema, and the
        // extractor - and a schema missing an argument the contract REQUIRES
        // is the disagreement that cannot be seen from either end alone.
        var schema = (await DeclaredAsync(WorkItemProposalTool.Name)).GetProperty("inputSchema");
        var offered = schema.GetProperty("properties").EnumerateObject()
            .Select(p => p.Name)
            .ToList();

        await Assert.That(offered).Contains("fields")
            .Because("a `field` proposal must name what it sets, and an argument an agent is "
                   + "not offered is one nothing will ever produce. Offered: "
                   + string.Join(", ", offered));

        var entry = schema.GetProperty("properties").GetProperty("fields")
            .GetProperty("items").GetProperty("properties").EnumerateObject()
            .Select(p => p.Name)
            .ToList();

        await Assert.That(entry).IsEquivalentTo(new[] { "path", "value" })
            .Because("the same two members the contract declares on a field edit, because a "
                   + "third spelling is how one of them stops agreeing. Found: "
                   + string.Join(", ", entry));
    }

    [Test]
    public async Task The_operations_it_offers_are_the_contracts_and_not_a_second_list()
    {
        var schema = (await DeclaredAsync(WorkItemProposalTool.Name)).GetProperty("inputSchema");

        var offered = schema.GetProperty("properties")
            .GetProperty("operation").GetProperty("enum").EnumerateArray()
            .Select(value => value.GetString()!)
            .ToList();

        // THE SAME HAZARD AS THE NAME, one field over. The tool offers the menu,
        // the extractor reads what came back against it, and admission writes
        // conditions over it - three readers of one closed vocabulary, so it is
        // declared in the contract rather than typed into the schema.
        await Assert.That(offered).IsEquivalentTo(WorkItemOperations.All)
            .Because("the menu an agent is offered is the vocabulary the control plane "
                   + "admits on. Offered: " + string.Join(", ", offered));
    }
}
