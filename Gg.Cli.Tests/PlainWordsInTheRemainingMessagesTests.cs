namespace Gg.Cli.Tests;

/// <summary>
/// The composition root's own sentences, which no verb returns and so no
/// renderer test reaches: a pool machine's first start and a hand-flight.
/// </summary>
/// <remarks>
/// Read from source because these are thrown or printed inside top-level
/// statements. Each fragment sits inside one string literal, so line wrapping
/// in the source cannot move it.
/// </remarks>
public class PlainWordsInTheRemainingMessagesTests
{
    private static string Program()
    {
        var here = new DirectoryInfo(AppContext.BaseDirectory);
        while (here is not null && !File.Exists(Path.Combine(here.FullName, "Gg.sln")))
        {
            here = here.Parent;
        }

        return File.ReadAllText(Path.Combine(
            here?.FullName ?? throw new InvalidOperationException("Gg.sln not found"),
            "Gg.Cli", "Program.cs"));
    }

    [Test]
    public async Task A_pool_machine_whose_code_is_spent_says_what_to_do()
    {
        var source = Program();

        await Assert.That(source).Contains("\"This pool machine cannot sign in: its one-time code has expired or has \"");
        await Assert.That(source).Contains("\"machine from the pool.\"");
        await Assert.That(source).DoesNotContain("nonce buys nothing")
            .Because("nonce, mint and redeem are words for the people who built it, not for "
                   + "the person reading a container's log.");
    }

    [Test]
    public async Task A_hand_flight_with_no_agent_names_the_setting()
    {
        var source = Program();

        await Assert.That(source).Contains("\"No agent is configured to run the flight on this machine. Nothing was run. Run \"");
        await Assert.That(source).DoesNotContain("the binary \"\n          + \"you want handed the flight");
    }

    [Test]
    public async Task Signed_out_refusals_share_one_sentence()
    {
        var source = Program();

        await Assert.That(source).DoesNotContain("\"Not signed in, so nothing was sent. `gg login` first.\"");
        await Assert.That(source).Contains("This machine is not signed in, so nothing was sent. Run gg login, then try again.");
    }
}
