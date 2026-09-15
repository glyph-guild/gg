using Gg.Console;
using Gg.Local;

namespace Gg.Console.Tests;

/// <summary>
/// A filter survives closing the console, and is checked against what the
/// tracker still offers.
/// </summary>
/// <remarks>
/// <para>
/// <b>Narrowing is work, and it was thrown away every time.</b> Picking a team,
/// a sprint and two states is four or five keystrokes through lists somebody
/// else's tracker decides the length of - and every <c>gg</c> started from
/// nothing. A person who looks at one backlog every morning re-did that
/// every morning.
/// </para>
/// <para>
/// <b>Per tracker, because a filter means nothing anywhere else.</b> An area
/// path is one tenant's tree with their own punctuation in it; carried to a
/// second reader it narrows to nothing, and the person sees an empty list with
/// no reason for it. Keyed by the reader, so a machine serving two backlogs
/// keeps them apart.
/// </para>
/// <para>
/// <b>State, not configuration.</b> <c>config.json</c> is a document a person
/// opens, hand-edits, and has refused for an unmapped member; gg rewriting it
/// underneath them to record where a cursor was is not the same kind of file at
/// all. This lives beside the transcripts and the live views, under the state
/// root, where something a machine maintains belongs.
/// </para>
/// <para>
/// <b>And checked against what is still offered.</b> A sprint ends, a team is
/// renamed, and a remembered value that no longer exists narrows every listing
/// to nothing - which reads exactly like a backlog with no work in it. The
/// stale part is dropped and the rest of the filter survives, because losing a
/// team because a sprint ended would be the same defect one dimension over.
/// </para>
/// </remarks>
public class BrowseFiltersAreRememberedTests
{
    /// <summary>A state root of this test's own, so the suite can run four-wide.</summary>
    /// <remarks>
    /// <c>XDG_STATE_HOME</c> is process-global, which is the reason every other
    /// path in <c>LocalPaths</c> takes this override.
    /// </remarks>
    private sealed class Root : IDisposable
    {
        internal string Path { get; } = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "gg-filters-" + Guid.NewGuid().ToString("n"));

        internal Root() => Directory.CreateDirectory(Path);

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }

    private static RememberedFilters Some() => new()
    {
        AreaPath = @"Widgets\Platform",
        Iteration = @"Widgets\Sprint 42",
        States = ["Active", "New"],
    };

    // ---- the store ----

    [Test]
    public async Task What_was_remembered_comes_back()
    {
        using var root = new Root();

        BrowseFilterStore.Write("a-tracker", Some(), root.Path);

        var back = BrowseFilterStore.Read("a-tracker", root.Path);

        await Assert.That(back.AreaPath).IsEqualTo(@"Widgets\Platform");
        await Assert.That(back.Iteration).IsEqualTo(@"Widgets\Sprint 42");
        await Assert.That(back.States).IsEquivalentTo((string[])["Active", "New"]);
    }

    [Test]
    public async Task Two_trackers_are_kept_apart()
    {
        // THE WHOLE REASON THIS IS KEYED. One machine serves several backlogs,
        // and one tenant's area path names nothing in another's tree - so a
        // shared filter would narrow the second to nothing and look like a
        // backlog with no work in it.
        using var root = new Root();

        BrowseFilterStore.Write("a-tracker", Some(), root.Path);
        BrowseFilterStore.Write(
            "another-tracker", new RememberedFilters { Iteration = "Q3" }, root.Path);

        await Assert.That(BrowseFilterStore.Read("a-tracker", root.Path).AreaPath)
            .IsEqualTo(@"Widgets\Platform");
        await Assert.That(BrowseFilterStore.Read("another-tracker", root.Path).AreaPath)
            .IsNull();
        await Assert.That(BrowseFilterStore.Read("another-tracker", root.Path).Iteration)
            .IsEqualTo("Q3");
    }

    [Test]
    public async Task A_tracker_nobody_has_filtered_remembers_nothing()
    {
        using var root = new Root();

        var back = BrowseFilterStore.Read("never-seen", root.Path);

        await Assert.That(back.AreaPath).IsNull();
        await Assert.That(back.Iteration).IsNull();
        await Assert.That(back.States).IsEmpty();
    }

    [Test]
    public async Task A_file_that_will_not_parse_remembers_nothing_rather_than_dying()
    {
        // A CONSOLE MUST NOT DIE BECAUSE A CONVENIENCE FILE IS CORRUPT. This is
        // a record of where somebody's cursor was; the worst honest answer is
        // "no filter", and the alternative is a terminal that will not open.
        using var root = new Root();

        Directory.CreateDirectory(LocalPaths.StateRoot(root.Path));
        File.WriteAllText(BrowseFilterStore.PathFor(root.Path), "{ not json");

        await Assert.That(BrowseFilterStore.Read("a-tracker", root.Path).AreaPath).IsNull();
    }

    [Test]
    public async Task Writing_one_tracker_leaves_the_others_alone()
    {
        // Read-modify-write, because the file holds every tracker. Replacing it
        // wholesale would make browsing one backlog forget the other, which is
        // the bug this file exists to prevent wearing a different hat.
        using var root = new Root();

        BrowseFilterStore.Write("a-tracker", Some(), root.Path);
        BrowseFilterStore.Write(
            "another-tracker", new RememberedFilters { Iteration = "Q3" }, root.Path);
        BrowseFilterStore.Write(
            "a-tracker", new RememberedFilters { AreaPath = "Widgets" }, root.Path);

        await Assert.That(BrowseFilterStore.Read("another-tracker", root.Path).Iteration)
            .IsEqualTo("Q3");
        await Assert.That(BrowseFilterStore.Read("a-tracker", root.Path).AreaPath)
            .IsEqualTo("Widgets");
        await Assert.That(BrowseFilterStore.Read("a-tracker", root.Path).Iteration)
            .IsNull()
            .Because("a write replaces that tracker's filter rather than merging into it - "
                   + "what is not in the new one was taken off.");
    }

    // ---- reconciliation ----

    private static BrowseFacets Offering() => new()
    {
        AreaPaths = ["Widgets", @"Widgets\Platform"],
        Iterations = [@"Widgets\Sprint 43"],
        States = ["Active", "Closed"],
    };

    [Test]
    public async Task A_sprint_that_ended_is_dropped_and_the_rest_of_the_filter_survives()
    {
        // THE CASE THIS EXISTS FOR. Sprint 42 finished; the team and the states
        // are still real. Dropping the whole filter would cost somebody their
        // narrowing for a reason that had nothing to do with it.
        var reconciled = Reducer.FilterOffered(
            new AppState
            {
                ChosenAreaPath = @"Widgets\Platform",
                ChosenIteration = @"Widgets\Sprint 42",
                ChosenStates = ["Active"],
            },
            Offering());

        await Assert.That(reconciled.ChosenIteration).IsNull();
        await Assert.That(reconciled.ChosenAreaPath).IsEqualTo(@"Widgets\Platform");
        await Assert.That(reconciled.ChosenStates).IsEquivalentTo((string[])["Active"]);
    }

    [Test]
    public async Task Only_the_states_that_went_are_dropped()
    {
        // A set, so this is a filter and not a value: losing `Active` because
        // `Triaged` was retired would narrow to something nobody asked for.
        var reconciled = Reducer.FilterOffered(
            new AppState { ChosenStates = ["Active", "Triaged"] }, Offering());

        await Assert.That(reconciled.ChosenStates).IsEquivalentTo((string[])["Active"]);
    }

    [Test]
    public async Task What_the_tracker_still_offers_is_left_exactly_as_it_was()
    {
        // THE LIVENESS HALF. A reconciliation that dropped everything would
        // pass both assertions above and make the feature useless.
        var reconciled = Reducer.FilterOffered(
            new AppState
            {
                ChosenAreaPath = "Widgets",
                ChosenIteration = @"Widgets\Sprint 43",
                ChosenStates = ["Active", "Closed"],
            },
            Offering());

        await Assert.That(reconciled.ChosenAreaPath).IsEqualTo("Widgets");
        await Assert.That(reconciled.ChosenIteration).IsEqualTo(@"Widgets\Sprint 43");
        await Assert.That(reconciled.ChosenStates)
            .IsEquivalentTo((string[])["Active", "Closed"]);
    }

    [Test]
    public async Task A_tracker_offering_nothing_of_a_dimension_takes_nothing_off_it()
    {
        // AN EMPTY LIST IS NOT AN ANSWER ABOUT THE FILTER. A reader that
        // answered no sprints at all - this project files nothing by sprint -
        // has said nothing about whether the one somebody chose still exists,
        // and treating it as "none of them do" would silently clear a filter
        // every time a dimension was unpopulated.
        var reconciled = Reducer.FilterOffered(
            new AppState { ChosenIteration = @"Widgets\Sprint 42" },
            new BrowseFacets { AreaPaths = ["Widgets"], Iterations = [], States = ["Active"] });

        await Assert.That(reconciled.ChosenIteration).IsEqualTo(@"Widgets\Sprint 42");
    }

    // ---- the reset ----

    [Test]
    public async Task Clearing_takes_the_cursors_back_with_the_picks()
    {
        // FULLY, or the next key press picks row nine of a list the person is
        // no longer looking at - which is the argument FilterOffered already
        // makes about replacing the lists.
        var cleared = Reducer.Reduce(
            new AppState
            {
                Mode = UiMode.BrowseFilter,
                ChosenAreaPath = "Widgets",
                ChosenIteration = @"Widgets\Sprint 43",
                ChosenStates = ["Active"],
                AreaSelected = 4,
                IterationSelected = 2,
                StateSelected = 1,
            },
            Command.ClearFilter);

        await Assert.That(cleared.ChosenAreaPath).IsNull();
        await Assert.That(cleared.ChosenIteration).IsNull();
        await Assert.That(cleared.ChosenStates).IsEmpty();
        await Assert.That(cleared.AreaSelected).IsEqualTo(0);
        await Assert.That(cleared.IterationSelected).IsEqualTo(0);
        await Assert.That(cleared.StateSelected).IsEqualTo(0);
    }
}
