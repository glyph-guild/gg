using Gg.Console;

namespace Gg.Console.Tests;

/// <summary>
/// Which tab's enter is which, and where there is none.
/// </summary>
/// <remarks>
/// <para>
/// <b>Three keys called enter, advertised as though they were one.</b> The
/// help page listed "open this flight", "open this runner" and "say where the
/// airspace is" together under the keys that always work, because none of them
/// said when it applied - so the page taught three contradictory things about
/// one key.
/// </para>
/// <para>
/// <b>And the flight one answered everywhere.</b> It was the fallback arm, so
/// every tab that is not Runners or Envelope offered it - Live, Browse,
/// Repositories, Allowances and the rest, none of which has a flight under the
/// cursor. A key advertised where it does nothing is the dead key Article XI
/// names.
/// </para>
/// <para>
/// <b>Queue keeps it, because Queue is a list of flights.</b> The keymap's own
/// comment says so - "the two lists that answer it are a table and a queue" -
/// and the console opens on that tab, so taking enter off it would be the
/// change nobody asked for hiding inside the one somebody did.
/// </para>
/// </remarks>
public class EnterBelongsToATabTests
{
    private static Command? Enter(TabId showing) =>
        Keymap.Resolve(KeyStroke.EnterKey, new KeymapContext(UiMode.Normal, showing));

    [Test]
    public async Task The_two_lists_of_flights_open_one()
    {
        await Assert.That(Enter(TabId.Flights)).IsEqualTo(Command.ShowFlight);

        await Assert.That(Enter(TabId.Queue)).IsEqualTo(Command.ShowFlight)
            .Because("the queue is a list of flights and the console opens on it, so enter "
                   + "there is the obvious thing to try and it works.");
    }

    [Test]
    public async Task The_runners_tab_opens_a_runner()
    {
        await Assert.That(Enter(TabId.Runners)).IsEqualTo(Command.ShowRunner);
    }

    [Test]
    public async Task The_airspace_tab_answers_where_the_airspace_is()
    {
        await Assert.That(Enter(TabId.Envelope)).IsEqualTo(Command.FocusAirspacePath);
    }

    [Test]
    public async Task Every_other_tab_has_no_enter_at_all()
    {
        foreach (var tab in Tabs.All.Where(t =>
                     t is not (TabId.Flights or TabId.Queue or TabId.Runners or TabId.Envelope)))
        {
            await Assert.That(Enter(tab)).IsNull()
                .Because($"the {tab} tab has no row enter could open, and a key offered "
                       + "where it does nothing is worse than one that is missing - it "
                       + "teaches somebody to press it and then does not answer.");
        }
    }

    [Test]
    public async Task Each_one_says_which_tab_it_belongs_to()
    {
        foreach (var entry in Keymap.Catalogue()
                     .Where(e => e.Binding.Key == KeyStroke.EnterKey && e.Mode == UiMode.Normal))
        {
            await Assert.That(entry.Binding.When).IsNotNull()
                .Because("the help page lists all three together, so one that does not say "
                       + "when it applies makes the other two look wrong.");
            await Assert.That(entry.Binding.When!.Length).IsGreaterThan(0);
        }
    }
}
