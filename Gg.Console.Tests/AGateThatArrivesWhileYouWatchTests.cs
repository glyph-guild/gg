using Gg.Contracts;

namespace Gg.Console.Tests;

/// <summary>
/// A gate that opens while somebody is watching says so; the ones that were
/// already waiting do not.
/// </summary>
/// <remarks>
/// <para>
/// <b>An announcement is a transition, never a standing state.</b> Eleven gates
/// were waiting on this tenant when the rule was decided, the oldest six days
/// old — and a console that popped eleven notices at startup would have taught
/// its owner to dismiss them without reading, which is the whole feature spent
/// on its first screen. The owner's words: <i>it should only be gates that
/// appear 'live' while using the app</i>.
/// </para>
/// <para>
/// <b>So the first look is a baseline and announces nothing.</b> What makes
/// that expressible without a second flag is that <c>Announced</c> is null
/// until the console has looked once: <b>absent and empty are different
/// facts</b> — never looked, versus looked and there were none. The same
/// distinction <c>based-on</c> keeps one file over.
/// </para>
/// <para>
/// <b>And only gates this person may answer.</b> A gate names its approver, and
/// being told about work you cannot do is worse than not being told: it is an
/// interruption with no act at the end of it. ADR-0024's <i>a gate its person
/// answers</i>, applied to the notice rather than to the decision.
/// </para>
/// <para>
/// <b>It survives a terminal release, because the set is on the state.</b> A
/// person who hands the terminal to <c>$EDITOR</c> and comes back has not
/// stopped watching, and a rebuilt session that re-announced everything would
/// be the backlog problem arriving by a second door.
/// </para>
/// <para>
/// <b>One delivery, many sources.</b> A gate arrives as the same
/// <c>Notification</c> a watched-for flight does, into the same corner, under
/// the same keys. What differs is the <c>NotificationKind</c> and where it came
/// from - which is the whole point of harvesting the corner rather than
/// building a second one beside it.
/// </para>
/// </remarks>
public class AGateThatArrivesWhileYouWatchTests
{
    private const string Me = "kdeenanauth";

    private static readonly DateTimeOffset T0 = new(2026, 9, 24, 4, 0, 0, TimeSpan.Zero);

    private static PendingGate Gate(string flight, string obligation, string approver = Me) => new()
    {
        FlightNumber = flight,
        ObligationId = obligation,
        Approver = approver,
        ManifestHash = "sha256:none",
        Because = "this obligation declares no condition",
        AwaitingSince = T0,
        Attempt = 1,
    };

    private static AppState Watching(params PendingGate[] gates) => new()
    {
        Principal = Me,
        Gates = new GateList { Gates = [.. gates] },
    };

    [Test]
    public async Task The_first_look_announces_nothing_however_many_are_waiting()
    {
        // ELEVEN WERE WAITING when this was written. Opening the console is not
        // the moment any of them happened.
        var opened = Watching(
            Gate("GG-247", "maintenance-oncall"),
            Gate("GG-248", "maintenance-oncall"),
            Gate("GG-266", "maintenance-oncall"));

        var (said, announced) = Announcements.Arrived(opened);

        await Assert.That(said).IsEmpty()
            .Because("a console that greets somebody with their whole backlog teaches them "
                   + "to dismiss it unread, which spends the feature on its first screen.");
        await Assert.That(announced).Count().IsEqualTo(3)
            .Because("the baseline still has to REMEMBER them, or the second look calls all "
                   + "three new.");
    }

    [Test]
    public async Task One_that_opens_afterwards_is_announced()
    {
        var before = Watching(Gate("GG-247", "maintenance-oncall"));
        var (_, baseline) = Announcements.Arrived(before);

        var now = Watching(
            Gate("GG-247", "maintenance-oncall"),
            Gate("GG-271", "preview-reviewed")) with
        {
            Announced = baseline,
        };

        var (said, _) = Announcements.Arrived(now);

        await Assert.That(said).Count().IsEqualTo(1);
        await Assert.That(said[0].FlightNumber).IsEqualTo("GG-271");
        await Assert.That(said[0].Kind).IsEqualTo(NotificationKind.GateWaiting);
        await Assert.That(said[0].Obligation).IsEqualTo("preview-reviewed")
            .Because("which obligation is waiting is the thing a person decides about, so it "
                   + "is on the notice rather than one keypress away.");
    }

    [Test]
    public async Task The_same_gate_is_not_announced_twice()
    {
        // THE REFRESH TICK RUNS FOREVER. A gate nobody has answered is still
        // there on the next read, and the one after that.
        var first = Watching(Gate("GG-271", "preview-reviewed"));
        var (_, baseline) = Announcements.Arrived(first);

        var again = first with { Announced = baseline };
        var (said, _) = Announcements.Arrived(again);

        await Assert.That(said).IsEmpty();
    }

    [Test]
    public async Task A_gate_somebody_else_answers_is_not_announced()
    {
        var before = Watching();
        var (_, baseline) = Announcements.Arrived(before);

        var now = Watching(Gate("GG-271", "preview-reviewed", approver: "dana")) with
        {
            Announced = baseline,
        };

        var (said, announced) = Announcements.Arrived(now);

        await Assert.That(said).IsEmpty()
            .Because("being told about a decision you cannot make is an interruption with no "
                   + "act at the end of it.");
        await Assert.That(announced).IsEmpty()
            .Because("and it must not be remembered either, or it goes unannounced forever "
                   + "if it is later reassigned to me.");
    }

    [Test]
    public async Task A_gate_that_is_answered_stops_being_remembered()
    {
        // SO IT CAN ANNOUNCE AGAIN. A flight can reopen the same obligation on a
        // later attempt, and that is a new thing happening while somebody
        // watches - not a repeat of the old one.
        var before = Watching(Gate("GG-271", "preview-reviewed"));
        var (_, baseline) = Announcements.Arrived(before);

        var answered = Watching() with { Announced = baseline };
        var (_, afterwards) = Announcements.Arrived(answered);

        await Assert.That(afterwards).IsEmpty();
    }

    [Test]
    public async Task Nothing_is_announced_before_the_gates_have_ever_been_read()
    {
        // NULL IS NOT EMPTY. A console that has not asked yet must not treat
        // "no gates" as the answer, or the first real read announces the lot.
        var (said, announced) = Announcements.Arrived(new AppState { Principal = Me });

        await Assert.That(said).IsEmpty();
        await Assert.That(announced).IsNull()
            .Because("not having looked is a different fact from having looked and found "
                   + "none, and the next read has to be able to tell them apart.");
    }

    [Test]
    public async Task Folding_a_boot_read_arms_it_and_shows_nothing()
    {
        // WHERE THE BASELINE ACTUALLY HAPPENS. ConsoleStart sets Gates once at
        // boot; ConsoleRefresh sets it on every tick afterwards. That is the
        // whole of "live": the first is arming, the rest are news.
        var booted = Announcements.Folded(Watching(Gate("GG-247", "maintenance-oncall")));

        await Assert.That(booted.Notifications).IsEmpty();
        await Assert.That(booted.Announced).Count().IsEqualTo(1)
            .Because("the boot read has to arm it, or the first refresh calls everything new.");
    }

    [Test]
    public async Task Folding_a_later_read_puts_it_in_the_corner()
    {
        var booted = Announcements.Folded(Watching(Gate("GG-247", "maintenance-oncall")));

        var later = Announcements.Folded(booted with
        {
            Gates = new GateList
            {
                Gates = [Gate("GG-247", "maintenance-oncall"), Gate("GG-271", "preview-reviewed")],
            },
        });

        await Assert.That(later.Notifications).Count().IsEqualTo(1);
        await Assert.That(later.Notifications[0].FlightNumber).IsEqualTo("GG-271");
        await Assert.That(later.NotificationAt).IsEqualTo(0)
            .Because("it is the only one, so the corner is showing it.");
    }

    [Test]
    public async Task Folding_changes_nothing_when_there_is_nothing_new()
    {
        // SAME STATE BACK, so a refresh tick that found no news does not make
        // the console rebuild anything.
        var booted = Announcements.Folded(Watching(Gate("GG-247", "maintenance-oncall")));

        await Assert.That(Announcements.Folded(booted)).IsSameReferenceAs(booted);
    }
}
