using System.Globalization;
using Gg.Contracts;
using Gg.Local;

namespace Gg.Cli.Tests;

/// <summary>
/// A landing proposal's description has room for an investigation's finding, and the tool
/// that takes it says how much (S79.1-01, ADR-0041 Decision 8).
/// </summary>
/// <remarks>
/// <para>
/// <b>The finding is the analysis.</b> GG-1086's first proposal was 5,518 characters and
/// GG-1088's 6,926; both were refused at 4,000, and both agents cut about 40% of what they
/// found to fit. The bound becomes a work-item field value's, 16 KiB, as that one did in
/// contract 0.295.0.
/// </para>
/// <para>
/// <b>In <c>Gg.Cli.Tests</c></b> because it is the one assembly that sees both the contract
/// and the tool server that offers it to an agent.
/// </para>
/// </remarks>
public class ALandingProposalHasRoomForAFindingTests
{
    [Test]
    public async Task A_description_is_admitted_up_to_sixteen_KiB_and_refused_past_it()
    {
        var fits = new LandingProposal { Title = "Investigate 18492", Description = new string('a', 16 * 1024) };
        var over = fits with { Description = new string('a', 16 * 1024 + 1) };

        await Assert.That(LandingProposal.Validate(fits)).IsNull()
            .Because("GG-1088's 6,926-character analysis was refused at 4,000, and the agent cut "
                   + "its finding to fit.");
        await Assert.That(LandingProposal.Validate(over)).IsNotNull();
        var proposalBound = LandingProposal.MaxDescription;
        await Assert.That(proposalBound).IsEqualTo(WorkItemProposalLimits.MaxDetail)
            .Because("one bound for an analysis, whether it lands as a proposal or as a field value.");
    }

    [Test]
    public async Task The_propose_landing_tool_says_the_bound()
    {
        var answers = await PlatformToolServerTests.ExchangeAsync(
            """{"jsonrpc":"2.0","id":1,"method":"tools/list","params":{}}""");

        var tool = answers[0].RootElement.GetProperty("result").GetProperty("tools")
            .EnumerateArray()
            .Single(t => t.GetProperty("name").GetString() == LandingProposalTool.Name);
        var said = tool.GetProperty("inputSchema").GetProperty("properties")
            .GetProperty(LandingProposalTool.DescriptionArgument).GetProperty("description").GetString()!;

        await Assert.That(said).Contains(LandingProposal.MaxDescription.ToString(CultureInfo.InvariantCulture))
            .Because("an agent that is not told the bound finds it by being refused, and then cuts "
                   + "its finding to a length it guesses. Said: " + said);
    }
}
