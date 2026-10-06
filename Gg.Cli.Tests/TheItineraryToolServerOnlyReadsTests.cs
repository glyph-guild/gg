using Gg.Client;

namespace Gg.Cli.Tests;

/// <summary>
/// <b>S63.4-03</b> - nothing the server sends writes: its only control-plane calls are the menu
/// and the check route.
/// </summary>
/// <remarks>
/// <b>Held by shape</b> (rule 10). The server is handed an <see cref="IPlanningReads"/> and
/// nothing else that reaches the control plane, and that interface has the two reads and no
/// third member - so a tool that proposed, opened or nominated would have to be given something
/// new, and this test is where that becomes visible.
/// </remarks>
public class TheItineraryToolServerOnlyReadsTests
{
    [Test]
    public async Task The_reads_are_the_menu_and_the_check_and_nothing_else()
    {
        var members = typeof(IPlanningReads).GetMethods().Select(m => m.Name).ToList();

        await Assert.That(members).IsEquivalentTo((string[])["MenuAsync", "CheckAsync"]);
    }

    [Test]
    public async Task The_session_reads_call_only_the_two_routes()
    {
        var source = await File.ReadAllTextAsync(Path.Combine(Root(), "Gg.Client", "PlanningReads.cs"));

        var calls = System.Text.RegularExpressions.Regex.Matches(source, @"_client\.(\w+)\(")
            .Select(m => m.Groups[1].Value)
            .Distinct()
            .ToList();

        await Assert.That(calls).IsEquivalentTo((string[])["ItineraryMenuAsync", "CheckItineraryAsync"]);
    }

    [Test]
    public async Task The_server_reaches_no_client_of_its_own()
    {
        var source = await File.ReadAllTextAsync(Path.Combine(Root(), "Gg.Cli", "ItineraryToolServer.cs"));

        await Assert.That(source).DoesNotContain("ControlPlaneClient");
        await Assert.That(source).DoesNotContain("HttpClient");
        await Assert.That(source).DoesNotContain("FlightCommands");
    }

    private static string Root()
    {
        var at = new DirectoryInfo(AppContext.BaseDirectory);
        while (at is not null && !File.Exists(Path.Combine(at.FullName, "Gg.sln")))
        {
            at = at.Parent;
        }

        return at!.FullName;
    }
}
