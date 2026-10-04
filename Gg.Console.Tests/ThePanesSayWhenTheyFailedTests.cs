using Gg.Contracts;

namespace Gg.Console.Tests;

/// <summary>
/// A pane showing a read that did not happen says so; a pane showing an
/// empty answer does not.
/// </summary>
/// <remarks>
/// <para>
/// <b>The browse tab printed this as its entire content:</b>
/// <i>"Response status code does not indicate success: 401 (Unauthorized)."</i>
/// — in the same voice, the same colour and the same place the console uses
/// for everything it has to say. Nothing told a person that was a failure
/// rather than the answer.
/// </para>
/// <para>
/// <b>EMPTY IS NOT AN ERROR</b>, which is the whole difficulty. "No work to
/// show" and "nothing needs you" are successful reads of a quiet tenant and
/// are the ordinary state of this console — marking those would have it
/// crying wolf on a good day, which teaches a person to ignore the mark.
/// </para>
/// </remarks>
public class ThePanesSayWhenTheyFailedTests
{
    private static AppState Failed() => new()
    {
        Diagnosis = "Nobody is signed in here, so nothing could be read.",
    };

    [Test]
    public async Task Every_tab_answers_and_none_falls_through_to_fine()
    {
        // DERIVED, NOT LISTED. A tab added later that quietly answered "fine"
        // would show a failure as ordinary prose - which is the bug this
        // exists for, and the shape the Itineraries tab's missing edge
        // subscription had on the same evening.
        foreach (var tab in Enum.GetValues<TabId>())
        {
            await Assert.That(PaneText.Trouble(Failed(), tab)).IsTrue()
                .Because($"{tab} has read nothing and something said why, so its pane is "
                       + "showing a refusal rather than content.");
        }
    }

    [Test]
    public async Task A_quiet_tenant_is_not_a_failure()
    {
        // THE HALF THAT MATTERS MOST. Every one of these is a read that
        // SUCCEEDED and came back with nothing in it.
        var quiet = new AppState
        {
            Flights = new FlightList { Flights = [] },
            Board = new BoardPage { Nominations = [], IncludedEnded = true },
            Itineraries = new BoardPage { Nominations = [], IncludedEnded = true },
            Runners = new RunnerList { Runners = [] },
            Browse = new BrowseListing { ProviderKey = "ado" },
        };

        foreach (var tab in (TabId[])
            [TabId.Queue, TabId.Flights, TabId.Board, TabId.Itineraries,
             TabId.Runners, TabId.Browse])
        {
            await Assert.That(PaneText.Trouble(quiet, tab)).IsFalse()
                .Because($"{tab} answered and had nothing to show, which is the ordinary "
                       + "state of a tenant that is not busy.");
        }
    }

    [Test]
    public async Task A_pane_still_reading_is_not_a_failure_either()
    {
        // NOTHING HAS SAID WHY, so nothing has gone wrong yet. A pane marked
        // on the way to its first answer would flash an error on every boot.
        foreach (var tab in Enum.GetValues<TabId>())
        {
            await Assert.That(PaneText.Trouble(new AppState(), tab)).IsFalse()
                .Because($"{tab} has read nothing and nobody has said why - that is a read "
                       + "in flight, which is how every console starts.");
        }
    }

    [Test]
    public async Task A_reader_that_refused_is_a_failure_even_when_the_console_is_well()
    {
        // BROWSE ANSWERS FOR ITSELF. A reader is a separate process talking to
        // somebody else's service, so it can fail while the control plane is
        // perfectly reachable - and that is exactly the 401 that started this.
        var refused = new AppState
        {
            Browse = new BrowseListing
            {
                ProviderKey = "ado",
                Absence = "The reader for 'ado' could not answer: 401 (Unauthorized).",
            },
        };

        await Assert.That(PaneText.Trouble(refused, TabId.Browse)).IsTrue();

        await Assert.That(PaneText.Trouble(refused, TabId.Flights)).IsFalse()
            .Because("one reader refusing says nothing about the flights pane, and marking "
                   + "every tab because one failed is the crying-wolf this avoids.");
    }

    [Test]
    public async Task The_mark_is_a_word_rather_than_a_colour()
    {
        // TWO PALETTES HAVE NO COLOUR TO SPEND. Palette.Default returns no
        // scheme at all, and Mono's own remark is the rule: "anything that
        // only reads because of hue stops reading here."
        await Assert.That(PaneText.TroubleMark).IsNotEmpty();

        await Assert.That(PaneText.TroubleMark.Any(char.IsLetter)).IsTrue()
            .Because("a mark made only of punctuation or colour is one a person reading a "
                   + "themed terminal, a screenshot, or Mono cannot see.");
    }

    [Test]
    public async Task Every_prose_pane_goes_through_the_one_place_that_marks_it()
    {
        // SEVEN ASSIGNMENTS IS A LIST, AND A LIST IS WHAT GETS ONE SHORT -
        // measured on the same evening, when the Itineraries tab was missing
        // from the edge-subscription list while the comment above it said
        // "every tab's table".
        var screen = Sources.Read("Gg.Console", "Views", "ConsoleScreen.cs");

        var direct = new List<string>();

        foreach (var pane in (string[])
            ["_flights", "_board", "_runners", "_browse", "_repositories",
             "_itineraries", "_allowances"])
        {
            if (screen.Contains($"{pane}.Text = PaneText.", StringComparison.Ordinal))
            {
                direct.Add(pane);
            }
        }

        await Assert.That(direct).IsEmpty()
            .Because("a pane assigned straight from PaneText never asks whether what it is "
                   + "showing is a refusal, so it cannot say so. Assigned directly: "
                   + string.Join(", ", direct));
    }
}
