namespace Gg.Console.Tests;

/// <summary>
/// The hint line names what the tab you are looking at can do.
/// </summary>
/// <remarks>
/// <para>
/// <b>THE LINE IS ONE LINE, and it was spending slots on keys that mean
/// nothing where a person is standing.</b> Sitting on the airspace tab, six of
/// the hints were about somewhere else: flight actions and a gate decision on a
/// flight not on the screen, an invite, flying by hand, the tab key, and the
/// key that focuses the field directly below the line. What was left had to be
/// read past to find the three keys that pull, apply and draft.
/// </para>
/// <para>
/// <b>Each one taken off has somewhere else to be, which is the rule
/// <c>KeyBinding.OffTheHintLine</c> already states.</b> `tab` is a convention
/// every terminal program shares; `enter` is written inside the box it acts on
/// - "airspace - enter to edit" - so the line was the second place saying it,
/// and the second place is the one that goes stale; `i` and `d` are in help,
/// like the two credential keys, and are pressed about as often. `a` and `y`
/// are not hidden but SCOPED: they act on a flight, so they are advertised
/// where flights are.
/// </para>
/// <para>
/// <b>Off the line, not out of the program.</b> Every key here still resolves
/// from every context it resolved from before, and help still names each one.
/// The keyboard is unchanged; only what the line spends its width on moved.
/// </para>
/// </remarks>
public class TheHintLineHoldsWhatTheTabCanDoTests
{
    private static KeymapContext On(TabId tab) => new(UiMode.Normal, tab);

    /// <summary>The two tabs that list flights, and the five that do not.</summary>
    private static readonly TabId[] Flights = [TabId.Queue, TabId.Flights];

    /// <summary>
    /// The tabs with no actions key at all.
    /// </summary>
    /// <remarks>
    /// <b>The airspace tab left this list.</b> `a' is the actions key on every
    /// tab; on the two that list flights it acts on the flight under the
    /// cursor, on the airspace tab it opens what can be done to the airspace,
    /// and on these five it is off the line because there is nothing under a
    /// cursor for it to act on.
    /// </remarks>
    private static readonly TabId[] Elsewhere =
        [TabId.Runners, TabId.Live, TabId.Browse, TabId.Repositories];

    [Test]
    public async Task The_airspace_tab_advertises_what_the_airspace_tab_does()
    {
        var line = Keymap.Hints(On(TabId.Envelope));

        // ONE KEY NOW, AND THAT IS THE CHANGE. The three acts plus the outcome
        // each had a letter here and the line outgrew the terminal; they are
        // behind `a' and each keeps its own letter inside it.
        await Assert.That(line).Contains("a actions", StringComparison.Ordinal)
            .Because("what a person does TO the airspace is the reason this tab has a "
                   + "line at all, and it is one key. Line: " + line);

        foreach (var act in (string[])
                 ["pull the airspace", "apply the airspace", "draft with an agent"])
        {
            await Assert.That(line).DoesNotContain(act, StringComparison.Ordinal)
                .Because("and what each one does is said inside the modal, where there is "
                       + "room for a sentence. Line: " + line);
        }

        var inside = Keymap.Hints(new KeymapContext(UiMode.AirspaceActions, TabId.Envelope));

        foreach (var (key, act) in new[]
        {
            ("p", "pull the airspace"),
            ("s", "apply the airspace"),
            ("m", "draft with an agent"),
        })
        {
            await Assert.That(inside).Contains($"{key} {act}", StringComparison.Ordinal)
                .Because("inside a modal the letters are free, so the line is the ONLY "
                       + "place somebody learns them. Line: " + inside);
        }
    }

    [Test]
    public async Task A_flight_key_is_advertised_where_flights_are()
    {
        foreach (var tab in Flights)
        {
            var line = Keymap.Hints(On(tab));

            await Assert.That(line).Contains("a actions", StringComparison.Ordinal)
                .Because($"{tab} is a list of flights and `a` is what you do to the one "
                       + "under the cursor. Line: " + line);

            await Assert.That(line).Contains("y fly by hand", StringComparison.Ordinal)
                .Because($"{tab} is where a flight is started from, and `n` beside it is "
                       + "already advertised there. Line: " + line);
        }
    }

    [Test]
    public async Task And_nowhere_else()
    {
        // THE HALF THAT IS THE REQUEST. A key that acts on a flight, named on a
        // tab with no flight on it, is a slot spent teaching somebody about
        // somewhere they are not.
        foreach (var tab in Elsewhere)
        {
            var line = Keymap.Hints(On(tab));

            await Assert.That(line).DoesNotContain("a actions", StringComparison.Ordinal)
                .Because($"{tab} has no flight under the cursor. Line: " + line);

            await Assert.That(line).DoesNotContain("y fly by hand", StringComparison.Ordinal)
                .Because($"{tab} is not where flights are read or started. Line: " + line);
        }
    }

    [Test]
    public async Task Four_keys_leave_the_line_altogether()
    {
        foreach (var tab in (TabId[])[.. Flights, .. Elsewhere])
        {
            var line = Keymap.Hints(On(tab));

            await Assert.That(line).DoesNotContain("d decide", StringComparison.Ordinal)
                .Because("deciding a gate is what the flight modal is for, and it is in "
                       + "help. Line: " + line);

            await Assert.That(line).DoesNotContain("tab next tab", StringComparison.Ordinal)
                .Because("every terminal program in the world moves focus with tab, and "
                       + "this one prints the six tab keys on the tabs themselves. Line: "
                       + line);

            await Assert.That(line).DoesNotContain("i invite", StringComparison.Ordinal)
                .Because("inviting somebody happens when a tenant is set up and then about "
                       + "twice a year, which is the argument that took the two credential "
                       + "keys off this line. Line: " + line);

            await Assert.That(line).DoesNotContain("enter ", StringComparison.Ordinal)
                .Because("enter opens the row under the cursor, which nobody needs telling, "
                       + "and on the airspace tab it focuses a box whose own title says "
                       + "\"enter to edit\" - so the line was the second place saying it. "
                       + "Line: " + line);
        }
    }

    [Test]
    public async Task Every_one_of_them_still_answers_its_key()
    {
        // OFF THE LINE, NOT OUT OF THE PROGRAM, and asserted rather than
        // assumed: taking a hint away by deleting the binding would look
        // identical on screen and be a different change entirely.
        foreach (var tab in (TabId[])[.. Flights, .. Elsewhere])
        {
            foreach (var (stroke, command) in new (KeyStroke, Command)[]
            {
                // THE AIRSPACE TAB IS NOT IN EITHER LIST HERE. `a' means the
                // airspace's actions there rather than a flight's, which is
                // the one place this key is not the same everywhere - and it
                // is why that tab left Elsewhere.
                (KeyStroke.Char('a'), Command.ToggleFlightActions),
                (KeyStroke.Char('d'), Command.OpenGate),
                (KeyStroke.Char('i'), Command.Invite),
                (KeyStroke.Char('y'), Command.AskHowToFlyByHand),
                (KeyStroke.TabKey, Command.FocusNextPane),
            })
            {
                await Assert.That(Keymap.Resolve(stroke, On(tab))).IsEqualTo(command)
                    .Because($"{stroke.Name} still does this on {tab}; only where it is "
                           + "advertised changed.");
            }
        }
    }

    [Test]
    public async Task And_help_still_names_every_one()
    {
        // THE PAGE ASKS THE OTHER QUESTION. A key nobody is told about on the
        // line has to be findable by somebody looking for it - the distinction
        // OffTheHintLine and Untaught were split into two properties to keep.
        var named = Keymap.Catalogue()
            .Where(entry => !entry.Binding.Untaught)
            .Select(entry => entry.Binding.Command)
            .ToHashSet();

        foreach (var command in new[]
        {
            Command.ToggleFlightActions,
            Command.OpenGate,
            Command.Invite,
            Command.AskHowToFlyByHand,
            Command.FocusNextPane,
            Command.FocusAirspacePath,
        })
        {
            await Assert.That(named.Contains(command)).IsTrue()
                .Because($"{command} left the hint line, so help is the only place left "
                       + "that names its key.");
        }
    }
}
