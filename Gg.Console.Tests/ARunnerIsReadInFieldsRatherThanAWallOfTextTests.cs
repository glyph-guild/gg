using Gg.Console.Views;
using Gg.Contracts;

namespace Gg.Console.Tests;

/// <summary>
/// The runner modal is widgets over the model, not one string of preformatted
/// text.
/// </summary>
/// <remarks>
/// <para>
/// <b>The same shape the flight modal had, and the same argument.</b>
/// Everything a person opened this to read was in one <c>Label</c>: a status
/// sentence, whose runner it is, where its log is and the log itself. So
/// nothing could be selected and nothing copied — and the runner id is the
/// value most often wanted out of this modal, because it is what
/// <c>gg runner</c> takes.
/// </para>
/// <para>
/// <b>The log is a <c>TextView</c> and not a table, which is where this parts
/// from the flight modal.</b> A flight's log is STRUCTURED — a time, an
/// attempt, an event — so it is rows with columns. A runner's log is whatever a
/// child process wrote to its own stdout: a stack trace, a wrapped sentence, a
/// line of JSON. Giving that columns would be inventing a structure the content
/// does not have, and the widget that fits it is the one that scrolls text and
/// lets a person select an error out of it.
/// </para>
/// <para>
/// <b>Which fields exist is the model's decision and stays testable.</b> A
/// runner that holds no flight has no "working on", one nobody registered has
/// no "registered by", and one this console did not start has no pid and no
/// log path — so the field list is a function of the row, and the view binds
/// whatever it is handed.
/// </para>
/// </remarks>
public class ARunnerIsReadInFieldsRatherThanAWallOfTextTests
{
    private const string Me = "01a062f3-42a5-73a4-8bf5-29a4bbb36533";

    private const string Mine = "01a078bb-4b97-779b-81ff-554c4ea662c0";

    private static readonly DateTimeOffset Beat = new(2026, 9, 7, 18, 2, 22, TimeSpan.Zero);

    /// <summary>
    /// A fleet of exactly the runner named, with the cursor on it.
    /// </summary>
    /// <remarks>
    /// <b><c>LocalRunnerId</c> only when the runner IS this machine's.</b>
    /// <c>Rows.Runners</c> invents a row for a local runner the fleet has no
    /// record of - registered here and never heard from, which is a real state
    /// and one an operator has to see - and inserts it first. A fixture that
    /// named a local runner absent from its own fleet would put that invented
    /// row under the cursor and test the wrong subject.
    /// </remarks>
    private static AppState State(
        RunnerSummary runner, RunnerHere? here = null, int selected = 0) => new()
    {
        Mode = UiMode.Runner,
        ActiveTab = TabId.Runners,
        Machine = "Kevins-MBP",
        PrincipalId = Me,
        LocalRunnerId = string.Equals(runner.RunnerId, Mine, StringComparison.Ordinal)
            ? Mine
            : null,
        RunnerSelected = selected,
        Here = here,
        Runners = new RunnerList { Runners = [runner] },
    };

    private static RunnerSummary Busy() => new()
    {
        RunnerId = "01a06572-a784-72ae-b951-f147553cd48e",
        Label = "vmlinux001",
        State = RunnerStates.Busy,
        CurrentFlightNumber = "GG-54",
        LastHeartbeatAt = Beat,
        RegisteredByPrincipalId = Me,
        RegisteredBy = "Kevin Deenanauth",
        Labels = [new AdvertisedLabel { Name = "environment=dev", Disposition = LabelDispositions.Measured }],
    };

    [Test]
    public async Task The_title_names_the_runner_it_is_about()
    {
        // "The runner here" was true of whichever runner was open, which is
        // what made it worth nothing across the top of one - and it stopped
        // being true at all when enter began opening any row in the fleet.
        var title = RunnerDetails.Title(State(Busy(), selected: 0));

        await Assert.That(title).Contains("01a06572");
        await Assert.That(title).Contains("vmlinux001");
    }

    [Test]
    public async Task And_says_what_it_can_when_the_cursor_is_on_nothing()
    {
        await Assert.That(RunnerDetails.Title(State(Busy(), selected: 9))).IsNotEmpty();
    }

    [Test]
    public async Task The_id_is_a_field_of_its_own_and_it_is_the_whole_id()
    {
        // WHAT THE TABLE CANNOT GIVE. The grid shows eight characters because
        // fifteen rows of full uuid would be a column of noise, but `gg runner`
        // takes the whole one - so the modal is where it has to be complete,
        // and in something a cursor can enter.
        var fields = RunnerDetails.Fields(State(Busy()));

        await Assert.That(fields.Single(f => f.Label == "id").Value)
            .IsEqualTo("01a06572-a784-72ae-b951-f147553cd48e");
    }

    [Test]
    public async Task Every_scalar_the_fleet_knows_is_a_field()
    {
        var fields = RunnerDetails.Fields(State(Busy()));
        var labels = fields.Select(f => f.Label).ToList();

        await Assert.That(labels).Contains("state");
        await Assert.That(labels).Contains("working on");
        await Assert.That(labels).Contains("advertises");
        await Assert.That(labels).Contains("last heard");
        await Assert.That(labels).Contains("registered by");

        await Assert.That(fields.Single(f => f.Label == "working on").Value).IsEqualTo("GG-54");
        await Assert.That(fields.Single(f => f.Label == "registered by").Value)
            .IsEqualTo("Kevin Deenanauth");
    }

    [Test]
    public async Task A_runner_holding_nothing_has_no_field_saying_so()
    {
        // An empty value beside a label is a fact that is missing; no label at
        // all is a fact that does not apply. An idle runner is not working on
        // nothing, it is not working.
        var idle = Busy() with { State = RunnerStates.Idle, CurrentFlightNumber = null };

        var labels = RunnerDetails.Fields(State(idle)).Select(f => f.Label).ToList();

        await Assert.That(labels).DoesNotContain("working on");
        await Assert.That(labels).Contains("state");
    }

    [Test]
    public async Task A_runner_nobody_is_recorded_as_having_registered_has_no_field_for_it()
    {
        var anonymous = Busy() with { RegisteredBy = "", RegisteredByPrincipalId = "" };

        await Assert.That(RunnerDetails.Fields(State(anonymous)).Select(f => f.Label))
            .DoesNotContain("registered by");
    }

    [Test]
    public async Task The_child_this_console_holds_adds_what_only_it_knows()
    {
        // A pid and a log path are facts about a process on THIS machine, and
        // the fleet has neither. They are fields rather than sentences for the
        // same reason as the rest: a path is a thing people copy.
        var ours = Busy() with { RunnerId = Mine, Label = "Kevins-MBP" };
        var here = new RunnerHere { Pid = 4242, LogPath = "/tmp/gg/runner.log" };

        var fields = RunnerDetails.Fields(State(ours, here));

        await Assert.That(fields.Single(f => f.Label == "process").Value).IsEqualTo("4242");
        await Assert.That(fields.Single(f => f.Label == "log").Value)
            .IsEqualTo("/tmp/gg/runner.log");
    }

    [Test]
    public async Task Another_hosts_runner_gets_no_pid_from_ours()
    {
        // THE DEFECT THIS FILE'S NEIGHBOUR IS ABOUT, one layer down. The child
        // belongs to whatever this console started; attaching its pid to a row
        // on a build host would say a process is running here that is not.
        var here = new RunnerHere { Pid = 4242, LogPath = "/tmp/gg/runner.log" };

        var labels = RunnerDetails.Fields(State(Busy(), here)).Select(f => f.Label).ToList();

        await Assert.That(labels).DoesNotContain("process");
        await Assert.That(labels).DoesNotContain("log");
    }

    [Test]
    public async Task The_log_is_the_lines_the_child_wrote()
    {
        var ours = Busy() with { RunnerId = Mine, Label = "Kevins-MBP" };
        var here = new RunnerHere
        {
            Pid = 4242,
            Log = ["listening on the pool", "registered as 01a06572"],
        };

        await Assert.That(RunnerDetails.Log(State(ours, here)))
            .IsEqualTo("listening on the pool\nregistered as 01a06572");
    }

    [Test]
    public async Task A_runner_this_console_did_not_start_has_no_log_to_show()
    {
        var here = new RunnerHere { Pid = 4242, Log = ["listening on the pool"] };

        await Assert.That(RunnerDetails.Log(State(Busy(), here))).IsEmpty()
            .Because("that log belongs to a different process, and showing it under another "
                   + "host's runner would attribute one machine's output to another.");
    }

    [Test]
    public async Task What_stands_in_for_an_empty_log_says_which_kind_of_empty_it_is()
    {
        // Two silences that mean opposite things: a child of ours that has not
        // spoken yet, and a runner whose output was never coming here at all.
        var ours = Busy() with { RunnerId = Mine, Label = "Kevins-MBP" };

        await Assert.That(RunnerDetails.LogAbsence(State(ours, new RunnerHere { Pid = 4242 })))
            .Contains("nothing yet");

        await Assert.That(RunnerDetails.LogAbsence(State(Busy())))
            .Contains("did not start it");

        await Assert.That(RunnerDetails.LogAbsence(
                State(ours, new RunnerHere { Pid = 4242, Log = ["something"] })))
            .IsEmpty()
            .Because("a log with lines in it needs nothing standing in for it.");
    }

    [Test]
    public async Task The_view_binds_the_producers_rather_than_formatting_its_own()
    {
        var screen = Sources.Read("Gg.Console", "Views", "ConsoleScreen.cs");

        // Title reaches the frame through PaneText.ModalTitle, which every
        // modal's title already goes through - a second assignment beside it
        // would be two authors for one string.
        await Assert.That(Sources.Read("Gg.Console", "State", "PaneText.cs"))
            .Contains("RunnerDetails.Title");

        foreach (var producer in (string[])
                 ["RunnerDetails.Fields", "RunnerDetails.Lines", "RunnerDetails.LogAbsence"])
        {
            await Assert.That(screen).Contains(producer)
                .Because($"{producer} is where the model becomes what a person reads; a view "
                       + "that formatted its own would be a second layout nothing can test.");
        }
    }

    [Test]
    public async Task And_the_log_is_a_list_of_lines_rather_than_a_table_or_a_TextView()
    {
        // NOT A TABLE, which is where this parts from the flight modal: a
        // flight's log has a time, an attempt and an event, and a runner's is
        // whatever a child wrote. Columns would be a structure invented for
        // content that does not have one.
        //
        // AND NOT A TextView, WHICH WAS THE FIRST ANSWER HERE. It is the widget
        // that fits - it scrolls text and selects across lines - and 2.4.17
        // marks it obsolete in favour of an editor that is not in this library.
        // Taking a deprecated widget for a modal that shows six lines of output
        // buys a selection nobody asked for and a migration somebody will have
        // to do, so the log is a list of lines this code wrapped itself.
        var screen = Sources.Read("Gg.Console", "Views", "ConsoleScreen.cs");

        await Assert.That(screen).DoesNotContain("new TextView")
            .Because("it is obsolete in this version, and obsolete warnings are errors here.");

        await Assert.That(screen).Contains("RunnerDetails.Lines")
            .Because("a list truncates what it cannot fit, and a runner's longest lines are "
                   + "its paths and stack frames - the ones somebody opened the log to read.");
    }

    [Test]
    public async Task A_line_longer_than_the_frame_is_wrapped_rather_than_cut()
    {
        var ours = Busy() with { RunnerId = Mine, Label = "Kevins-MBP" };
        var here = new RunnerHere
        {
            Pid = 4242,
            Log = ["/Users/somebody/git/a-very-long-path/that/keeps/going/runner.log is missing"],
        };

        var lines = RunnerDetails.Lines(State(ours, here), width: 20);

        await Assert.That(lines.Count).IsGreaterThan(1);

        foreach (var line in lines)
        {
            await Assert.That(line.Length).IsLessThanOrEqualTo(20);
        }

        // NOTHING IS LOST, which is the difference between wrapping and cutting.
        await Assert.That(string.Concat(lines).Replace(" ", "", StringComparison.Ordinal))
            .Contains("that/keeps/going/runner.log");
    }

    [Test]
    public async Task Before_anything_is_laid_out_it_gives_back_whole_lines()
    {
        // A RENDER RUNS BEFORE A LAYOUT DOES, so the first pass asks with a
        // viewport of zero. #315 lost its whole unwrap to this: wrapping to
        // nothing returns a line per character, and there is no second pass
        // unless something asks for one.
        var ours = Busy() with { RunnerId = Mine, Label = "Kevins-MBP" };
        var here = new RunnerHere { Pid = 4242, Log = ["listening on the pool"] };

        await Assert.That(RunnerDetails.Lines(State(ours, here), width: 0))
            .IsEquivalentTo((string[])["listening on the pool"]);
    }

    [Test]
    public async Task Focus_lands_on_the_log_because_that_is_the_part_with_a_cursor()
    {
        // #315's lesson, and it says the decision belongs in a value rather
        // than a line in the view - because where focus lands in this file has
        // been wrong three times. The log is where it goes for the flight
        // modal's reason: it is the part somebody scrolls, and the fields are a
        // tab away.
        await Assert.That(FocusChange.Wanted(
                UiMode.Runner, TabId.Runners, landed: null, modalHasFocus: false))
            .IsEqualTo(FocusTarget.RunnerLog);

        await Assert.That(FocusChange.Wanted(
                UiMode.Runner, TabId.Runners, landed: null, modalHasFocus: true))
            .IsEqualTo(FocusTarget.LeaveAlone)
            .Because("a person who moved focus inside the modal keeps it, which is the same "
                   + "rule every other mode here follows.");
    }

    [Test]
    public async Task The_modal_is_a_document_so_it_gets_the_room_one_needs()
    {
        // It was drawn into 52 columns by 12 rows, the size of a question with
        // two answers. A runner's log is the thing it is open to show.
        await Assert.That(PaneText.ModalIsADocument(UiMode.Runner)).IsTrue();
    }

    [Test]
    public async Task Every_label_fits_the_gutter_the_view_leaves_for_it()
    {
        var ours = Busy() with { RunnerId = Mine, Label = "Kevins-MBP" };
        var every = RunnerDetails.Fields(
            State(ours, new RunnerHere { Pid = 4242, LogPath = "/tmp/gg/runner.log" }));

        foreach (var field in every)
        {
            await Assert.That(field.Label.Length).IsLessThanOrEqualTo(13)
                .Because("the view lays labels out in a fixed gutter, and one that overruns "
                       + "is truncated into a word that is not the label.");
        }
    }
}
