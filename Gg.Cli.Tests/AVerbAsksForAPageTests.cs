namespace Gg.Cli.Tests;

/// <summary>
/// <c>gg flights</c> and <c>gg board</c> ask for a page, and say how to ask for
/// the next one.
/// </summary>
/// <remarks>
/// <para>
/// <b>Asked for 2026-09-20</b>, with the console's infinite scroll: neither
/// list had a cap, so both grew with a tenant's history for ever. The control
/// plane answers a page and a cursor since contract 0.212.0; this is the half a
/// person types.
/// </para>
/// <para>
/// <b>A page by default, because a person at a terminal is not asking for two
/// hundred rows.</b> The size is the contract's <c>Paging.DefaultLimit</c> -
/// one number both sides read, so a page is the same length whoever asked.
/// </para>
/// <para>
/// <b>Refused here, before the round trip.</b> A limit the control plane will
/// refuse is one this side can refuse first, with the same sentence, and
/// without spending a request to learn it.
/// </para>
/// </remarks>
public class AVerbAsksForAPageTests
{
    [Test]
    public async Task Flights_takes_a_page_size_and_a_cursor()
    {
        var sized = CliArgs.Parse(["flights", "--limit", "50"]) as CliAction.Flights;
        await Assert.That(sized!.Limit).IsEqualTo(50);

        var continued = CliArgs.Parse(["flights", "--after", "R0ctMTA4"]) as CliAction.Flights;
        await Assert.That(continued!.After).IsEqualTo("R0ctMTA4");
    }

    [Test]
    public async Task The_board_takes_them_too()
    {
        var sized = CliArgs.Parse(["board", "--limit", "20"]) as CliAction.Board;
        await Assert.That(sized!.Limit).IsEqualTo(20);

        var continued = CliArgs.Parse(["board", "--after", "MTIzOmFiYw"]) as CliAction.Board;
        await Assert.That(continued!.After).IsEqualTo("MTIzOmFiYw");
    }

    [Test]
    public async Task Neither_verb_loses_what_it_already_took()
    {
        // THE FLAGS THAT WERE THERE FIRST. A parameter added to a verb that
        // dropped one of its own would be a change nobody asked for arriving
        // with one that was.
        var flights = CliArgs.Parse(["flights", "--all", "--json", "--limit", "5"]) as CliAction.Flights;
        await Assert.That(flights!.All).IsTrue();
        await Assert.That(flights.Json).IsTrue();
        await Assert.That(flights.Limit).IsEqualTo(5);

        var correlated = CliArgs.Parse(
            ["flights", "--intent", "ado#18490", "--limit", "5"]) as CliAction.Flights;
        await Assert.That(correlated!.Intent).IsEqualTo("ado#18490");
        await Assert.That(correlated.Limit).IsEqualTo(5);

        var board = CliArgs.Parse(["board", "--all", "--json", "--after", "x"]) as CliAction.Board;
        await Assert.That(board!.Ended).IsTrue();
        await Assert.That(board.Json).IsTrue();
        await Assert.That(board.After).IsEqualTo("x");
    }

    [Test]
    public async Task A_page_nobody_can_serve_is_refused_before_the_round_trip()
    {
        foreach (var argv in (string[][])
                 [
                     ["flights", "--limit", "0"],
                     ["flights", "--limit", "nine"],
                     ["flights", "--limit"],
                     ["board", "--limit", "999999"],
                     ["board", "--after"],
                 ])
        {
            await Assert.That(CliArgs.Parse(argv)).IsTypeOf<CliAction.Unknown>()
                .Because($"'{string.Join(' ', argv)}' cannot be served, and spending a request "
                       + "to be told so is a round trip for a sentence this side already has.");
        }
    }

    [Test]
    public async Task The_refusal_says_what_a_page_may_be()
    {
        var refused = (CliAction.Unknown)CliArgs.Parse(["flights", "--limit", "999999"]);

        await Assert.That(refused.Message).Contains(
            Gg.Contracts.Paging.MaxLimit.ToString(System.Globalization.CultureInfo.InvariantCulture))
            .Because("a bound a person cannot read from the refusal is one they find by "
                   + "bisecting.");
    }

    [Test]
    public async Task The_usage_says_both_verbs_page()
    {
        var message = ((CliAction.Unknown)CliArgs.Parse(["frobnicate"])).Message;

        await Assert.That(message).Contains("--limit")
            .Because("a flag that is parsed, validated and absent from the usage is one "
                   + "nobody finds - which is how a ticket flight came to be opened with no "
                   + "repository on 2026-09-19.");
        await Assert.That(message).Contains("--after");
    }
}
