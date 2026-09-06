using Gg.Console;
using Gg.Client;
using Gg.Contracts;

namespace Gg.Console.Tests;

/// <summary>
/// The repositories a tenant can fly against, and choosing one.
/// </summary>
/// <remarks>
/// <para>
/// <b>S29.2-01 and S29.1-03, which are one feature.</b> Slice 28 reached the
/// same conclusion from the other side as S28.5-02 and moved it here: choosing
/// from a list and naming what you chose are not two things. A prompt asking a
/// person to type a repository name from memory, against a list the console can
/// show, is a key written to be rewritten.
/// </para>
/// <para>
/// <b>The read has existed since tier B and nothing displayed it.</b>
/// <c>ConsoleData.RepositoriesAsync</c> is the last entry on
/// <c>ConsoleDataReachTests</c>' exemption list, attributed to this step. This
/// is what takes it off.
/// </para>
/// <para>
/// <b>Choosing nothing is the ordinary state and stays legal.</b> A flight with
/// no repository named is one the envelope resolves, which is what happens
/// today; the choice is an override, not a requirement, and a console that made
/// it mandatory would refuse flights the CLI accepts.
/// </para>
/// </remarks>
public class WhatThisTenantCanFlyAgainstTests
{
    private static RegisteredRepositories Two() => new()
    {
        Repositories =
        [
            new RepositoryRegistered
            {
                Name = "widgets", Provider = "a-forge", Id = "r-1", Path = "acme/widgets",
                Credential = "local:acme/widgets", RegisteredBy = "somebody",
                RegisteredAt = DateTimeOffset.UnixEpoch,
            },
            new RepositoryRegistered
            {
                Name = "gadgets", Provider = "a-forge", Id = "r-2", Path = "acme/gadgets",
                Credential = "local:acme/gadgets", RegisteredBy = "somebody",
                RegisteredAt = DateTimeOffset.UnixEpoch,
            },
        ],
    };

    /// <summary>
    /// Read through the projection, which is the one path a verb result takes.
    /// </summary>
    /// <remarks>
    /// Not a bespoke reducer: <c>ConsoleProjection.Apply</c> already owns
    /// turning a <c>VerbResult</c> into state, and a second door would be a
    /// second place the mapping can drift - which is what
    /// <c>ProjectionParityTests</c> exists to stop.
    /// </remarks>
    private static AppState Listed() =>
        ConsoleProjection.Apply(new AppState(), new VerbResult.AirspaceRepositories(Two()));

    [Test]
    public async Task The_pane_shows_what_this_tenant_can_fly_against()
    {
        var text = PaneText.Repositories(Listed());

        await Assert.That(text).Contains("acme/widgets");
        await Assert.That(text).Contains("acme/gadgets");
    }

    [Test]
    public async Task A_tenant_with_nothing_registered_is_told_that_and_not_shown_a_box()
    {
        // The distinction the browse pane already draws: nothing registered is
        // an answer, and it is a different answer from never having asked.
        var read = ConsoleProjection.Apply(
            new AppState(),
            new VerbResult.AirspaceRepositories(new RegisteredRepositories { Repositories = [] }));

        await Assert.That(PaneText.Repositories(read)).Contains("nothing registered");
        await Assert.That(PaneText.Repositories(new AppState())).DoesNotContain("nothing registered")
            .Because("never asked is not the same as asked and told none.");
    }

    [Test]
    public async Task Choosing_one_marks_it_and_unmarks_the_others()
    {
        var chosen = Reducer.RepositoryChosen(Listed() with { RepositorySelected = 1 });

        await Assert.That(chosen.ChosenRepository).IsEqualTo("acme/gadgets");
        await Assert.That(PaneText.Repositories(chosen)).Contains("→ acme/gadgets");
        await Assert.That(PaneText.Repositories(chosen)).DoesNotContain("→ acme/widgets");
    }

    [Test]
    public async Task Choosing_the_one_already_chosen_clears_it()
    {
        // THE WAY BACK TO THE ORDINARY STATE. Without it, a person who chose a
        // repository by mistake can never return to letting the envelope
        // decide, which is the behaviour every flight has today.
        var chosen = Reducer.RepositoryChosen(Listed() with { RepositorySelected = 1 });
        var cleared = Reducer.RepositoryChosen(chosen);

        await Assert.That(cleared.ChosenRepository).IsNull();
    }

    [Test]
    public async Task The_chosen_repository_is_visible_without_opening_the_pane()
    {
        // INVISIBLE STATE THAT CHANGES WHAT A WRITE DOES IS THE WORST KIND. A
        // person who chose a repository an hour ago and forgot must not open a
        // flight against it without being told.
        var chosen = Reducer.RepositoryChosen(Listed() with { RepositorySelected = 0 });

        await Assert.That(PaneText.Activity(chosen)).Contains("acme/widgets");
        await Assert.That(PaneText.Activity(Listed())).DoesNotContain("acme/widgets")
            .Because("nothing chosen is the ordinary state and needs no announcement.");
    }

    [Test]
    public async Task Moving_through_the_repositories_does_not_move_the_queue_or_the_work_list()
    {
        var browsing = Listed() with { RepositoriesVisible = true, SelectedRow = 2, BrowseSelected = 3 };

        var down = Reducer.Reduce(browsing, Command.SelectNext);

        await Assert.That(down.RepositorySelected).IsEqualTo(1);
        await Assert.That(down.SelectedRow).IsEqualTo(2);
        await Assert.That(down.BrowseSelected).IsEqualTo(3)
            .Because("three lists, three cursors, and j moves whichever has the screen.");
    }

    [Test]
    public async Task The_key_is_bound_and_advertised_and_is_the_shells()
    {
        var normal = new KeymapContext(UiMode.Normal);

        await Assert.That(Keymap.Resolve(KeyStroke.Char('r'), normal))
            .IsEqualTo(Command.ToggleRepositories);

        // ADVERTISED ON ITS TAB rather than on the hint line, which is one line
        // and now keeps only the keys with nowhere else to be. The claim is
        // unchanged - a bound key a person cannot find is a key that does not
        // exist - and EveryTabIsOnTheBarTests checks the tab offers the key the
        // keymap resolves.
        await Assert.That(Tabs.Title(new AppState(), TabId.Repositories))
            .Contains("Repositories", StringComparison.Ordinal);
        await Assert.That(Tabs.Title(new AppState(), TabId.Repositories))
            .Contains("r", StringComparison.Ordinal);
        await Assert.That(ShellCommands.Handled).Contains(Command.ToggleRepositories)
            .Because("showing them is a read, and a session may not make one.");
    }

    [Test]
    public async Task One_screen_one_view()
    {
        // WAS One_region_one_pane. Opening this used to turn three other flags
        // off, because four views shared one region; a view takes the whole
        // screen now, so what it asserts is that exactly one of the four DRAWS.
        var state = Reducer.RepositoriesToggled(
            new AppState { BrowseVisible = true, EvidenceVisible = true, LiveVisible = true });

        await Assert.That(state.RepositoriesVisible).IsTrue();
        await Assert.That(state.BrowseVisible).IsTrue()
            .Because("the items somebody was browsing are still open behind this.");

        var drawn = Enum.GetValues<TabId>().Where(tab => Tabs.Showing(state, tab)).ToList();

        await Assert.That(drawn).IsEquivalentTo((TabId[])[TabId.Repositories])
            .Because("one screen, one view. Found: " + string.Join(", ", drawn));
    }
}
