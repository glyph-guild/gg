using Gg.Contracts;

namespace Gg.Console.Tests;

/// <summary>
/// The fleet table says what each runner advertises.
/// </summary>
/// <remarks>
/// <para>
/// <b>It is the column that answers why a flight will not fly here.</b> A
/// machine advertising nothing is refused with "this machine does not advertise
/// 'environment=dev'", and until now the console's own fleet tab could not have
/// told anybody that - the labels were in the model, fetched at boot, and
/// rendered nowhere. `gg runner labels' had the answer and the tab did not.
/// </para>
/// <para>
/// <b>A stated label is marked and a measured one is not.</b> Measured means
/// the name has a registered meaning - a predicate evaluated from produced
/// facts - and stated means somebody claimed it and nothing checks. That
/// difference is the whole of what the disposition is for, so the one worth
/// noticing is the one that gets a word.
/// </para>
/// <para>
/// <b>Empty rather than a dash.</b> Advertising nothing is an answer, and the
/// row this console adds for a runner the fleet has never heard from cannot
/// know either way - which is a different silence and must not read as "it
/// advertises nothing".
/// </para>
/// </remarks>
public class TheFleetShowsWhatItAdvertisesTests
{
    private static readonly DateTimeOffset T0 = new(2026, 9, 6, 12, 0, 0, TimeSpan.Zero);

    private const string Mine = "01a078bb-0000-0000-0000-000000000001";

    private static AppState Fleet(params AdvertisedLabel[] labels) => new()
    {
        ActiveTab = TabId.Runners,
        LocalRunnerId = Mine,
        Runners = new RunnerList
        {
            Runners =
            [
                new RunnerSummary
                {
                    RunnerId = Mine,
                    Label = "Kevins-MBP",
                    State = RunnerStates.Idle,
                    LastHeartbeatAt = T0,
                    Labels = labels,
                },
            ],
        },
    };

    private static AdvertisedLabel Advertised(string name, string disposition) =>
        new() { Name = name, Disposition = disposition };

    [Test]
    public async Task The_table_has_a_column_for_them()
    {
        await Assert.That(Rows.RunnerColumns).Contains("advertises")
            .Because("the column that answers why a flight will not fly here.");
    }

    [Test]
    public async Task What_a_runner_advertises_is_in_its_row()
    {
        var row = Rows.Runners(Fleet(
            Advertised("environment=dev", LabelDispositions.Measured),
            Advertised("gpu", LabelDispositions.Measured)))[0];

        await Assert.That(row.Labels).IsEqualTo("environment=dev, gpu")
            .Because("all of them, in the order the control plane sent - a runner is eligible "
                   + "for a flight whose labels are contained in these, so a person reading "
                   + $"the row is reading the whole set. Found: '{row.Labels}'");
    }

    [Test]
    public async Task A_stated_label_says_so_and_a_measured_one_does_not()
    {
        var stated = Rows.Runners(Fleet(
            Advertised("environment=dev", LabelDispositions.Stated)))[0];

        await Assert.That(stated.Labels).IsEqualTo("environment=dev (stated)")
            .Because("stated means somebody claimed it and nothing checks, which is the half "
                   + "worth noticing.");

        var measured = Rows.Runners(Fleet(
            Advertised("environment=dev", LabelDispositions.Measured)))[0];

        await Assert.That(measured.Labels).IsEqualTo("environment=dev")
            .Because("and a registered meaning is the ordinary case, so it costs no words.");
    }

    [Test]
    public async Task A_runner_advertising_nothing_has_an_empty_cell()
    {
        await Assert.That(Rows.Runners(Fleet()).Single().Labels).IsEmpty()
            .Because("advertising nothing is an answer, and a placeholder for it would read "
                   + "as one.");
    }

    [Test]
    public async Task And_a_runner_the_fleet_has_never_seen_says_nothing_either_way()
    {
        // THE ROW THIS CONSOLE ADDS, for a machine registered here that has
        // never heartbeated. It cannot know what that runner would advertise -
        // which is a different silence from advertising nothing, and the pane's
        // own sentence is where that difference is stated.
        var never = new AppState { ActiveTab = TabId.Runners, LocalRunnerId = Mine };

        await Assert.That(Rows.Runners(never).Single().Labels).IsEmpty();
        await Assert.That(Rows.Runners(never).Single().Heard).IsEqualTo("never");
    }

    [Test]
    public async Task The_view_puts_it_in_the_cell()
    {
        var screen = Sources.Read("Gg.Console", "Views", "ConsoleScreen.cs");

        await Assert.That(screen).Contains("r.Labels")
            .Because("a column with no cell behind it is a heading over nothing, which is "
                   + "the shape the fill ratchet in this suite already watches for.");
    }
}
