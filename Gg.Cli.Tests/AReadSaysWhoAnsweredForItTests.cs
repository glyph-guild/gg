using Gg.Client;
using Gg.Contracts;

namespace Gg.Cli.Tests;

/// <summary>
/// What is recorded about a person is readable by one: whose flight it is,
/// and as whom a sweep read.
/// </summary>
/// <remarks>
/// <para>
/// <b>Found on slice forty-two's walk, 2026-09-20.</b> A personal watch swept
/// on its person's machine, opened a flight only that machine could take, and
/// attested the account its credential acts as. Every one of those facts was
/// recorded and acted upon, and NONE of them could be read back: `gg show`
/// carried no person at all, and `gg watches` carried no account. The platform
/// knew whose work it was and nobody else could.
/// </para>
/// <para>
/// <b>The person a flight is answered for</b> is its line of work when it has
/// one, its steward when a tenant watch opened it, and whoever opened it by
/// hand otherwise - which is what the flight already carries as its author. A
/// flight nobody is answerable for says nothing, as a tenant board row does.
/// </para>
/// </remarks>
public class AReadSaysWhoAnsweredForItTests
{
    private const string Subject = "entra:dd4520ce-d238-4afd-a246-ac62248712de/d383-1676";

    private static FlightStory AStory(string? forWhom = null, string? display = null) => new()
    {
        FlightId = "8b28f244-5919-7b6f-8539-ec07f6d5b28a",
        FlightNumber = "GG-213",
        WorkKind = "review",
        Stage = "created",
        State = "open",
        For = forWhom,
        ForDisplay = display,
    };

    private static WatchStanding AStanding(string? account = null) => new()
    {
        Name = "my-queue",
        Version = "my-queue@v1",
        Executor = WatchExecutors.Instructions,
        LastHeardAt = new DateTimeOffset(2026, 9, 20, 4, 13, 37, TimeSpan.Zero),
        Outcome = WatchOutcomes.Swept,
        Nominated = 1,
        Opened = 1,
        Window = "24h",
        Budgeted = 2,
        Account = account,
    };

    [Test]
    public async Task A_flight_says_who_answers_for_it()
    {
        var text = VerbOutput.ToText(new VerbResult.Story(AStory(Subject, "Kevin Deenanauth")));

        await Assert.That(text).Contains("Kevin Deenanauth")
            .Because("the walk opened a flight that only one person's machines could take, "
                   + "and this read said nothing about whose it was.");
        await Assert.That(text).Contains(Subject)
            .Because("the display is what a reader recognises and the subject is what tells "
                   + "two people with one name apart - `gg board` carries both for the same "
                   + "reason.");
    }

    [Test]
    public async Task A_flight_nobody_answers_for_says_nothing()
    {
        var text = VerbOutput.ToText(new VerbResult.Story(AStory()));

        await Assert.That(text).DoesNotContain("for:")
            .Because("a flight with nobody behind it belongs to the tenant, which the board "
                   + "already says by saying nothing.");
    }

    [Test]
    public async Task A_watch_says_as_whom_its_sweep_read()
    {
        var text = VerbOutput.ToText(new VerbResult.Watches(
            new WatchStandingList { Standings = [AStanding("svc-triage")] }));

        await Assert.That(text).Contains("svc-triage")
            .Because("\"swept, 1 nominated\" reads the same whoever it read as, and the "
                   + "account is the half that says whose queue was emptied.");
    }

    [Test]
    public async Task A_watch_whose_pair_named_no_account_says_nothing_about_one()
    {
        var text = VerbOutput.ToText(new VerbResult.Watches(
            new WatchStandingList { Standings = [AStanding()] }));

        await Assert.That(text).DoesNotContain("as ")
            .Because("a pair that named no account attests none, and a line inventing one "
                   + "would be this reading a declaration that was never written.");
    }

    [Test]
    public async Task The_usage_says_a_personal_name_rides_a_gate()
    {
        // THE OWNER CHOSE A GATE ITS PERSON ANSWERS over an ungated exception
        // (2026-09-19), and this line kept promising the exception. The walk
        // ran the command and a gate opened.
        var usage = ((CliAction.Unknown)CliArgs.Parse(["nonsense"])).Message;

        await Assert.That(usage).DoesNotContain("with no gate")
            .Because("declaring a personal watch opens a gate its person answers - the line "
                   + "promised the opposite of what the command does.");
        await Assert.That(usage).Contains("--personal");
    }
}
