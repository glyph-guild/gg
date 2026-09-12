using System.Text.RegularExpressions;
using Gg.Console;
using Gg.Contracts;

namespace Gg.Console.Tests;

/// <summary>
/// A key a modal's own text offers is a key that modal resolves.
/// </summary>
/// <remarks>
/// <para>
/// <b>REPORTED FROM A REAL TENANT: two gates that would not take an
/// answer.</b> The actions modal — whose entire content is a menu — listed
/// <c>d decide a gate on this flight</c> and <c>v the evidence behind it</c>,
/// and <c>UiMode.FlightActions</c> bound nothing but <c>esc</c>. So a person
/// pressed `a`, read the two things they could do, pressed either, and the
/// console did nothing.
/// </para>
/// <para>
/// <b>This is Article XI inside a single screen.</b> "A key that appears to
/// work is worse than one that is not offered" — and a menu is the strongest
/// form of offering there is. The keymap and the words drifted because nothing
/// held them together: <c>Keymap.Hints</c> is generated from the bindings, so
/// the hint LINE cannot lie, but a modal's BODY is prose and could.
/// </para>
/// <para>
/// <b>So the guard is over the prose.</b> Any line shaped like a key and a
/// description is read as an offer, and the mode showing it has to resolve
/// that key. It is deliberately shallow — one character, two spaces — because
/// that is the shape a menu takes here, and a guard that tried to understand
/// sentences would either miss the next one or fail on a paragraph.
/// </para>
/// </remarks>
public class AModalDoesNotAdvertiseDeadKeysTests
{
    /// <summary>A line offering a key: two spaces, one character, two spaces.</summary>
    /// <remarks>
    /// <b>Anchored to the menu shape rather than hunting for letters in
    /// prose.</b> "  d  decide a gate" is an offer; "press d to answer" is a
    /// sentence, and one of the two is a list a person's eye runs down.
    /// </remarks>
    private static readonly Regex Offer = new(
        @"^\s{2}(?<key>[a-z0-9])\s{2}\S", RegexOptions.Multiline | RegexOptions.ExplicitCapture);

    private static AppState Waiting() => new()
    {
        Mode = UiMode.FlightActions,
        Queue =
        [
            new QueueRow
            {
                FlightId = "01a092f2-fba6-73a6-91e0-6b7f8f278991",
                FlightNumber = "GG-89",
                Name = "widen repositories",
                Reason = QueueReason.AwaitingDecision,
                Since = DateTimeOffset.UnixEpoch,
            },
        ],
        SelectedRow = 0,
    };

    [Test]
    public async Task The_actions_modal_resolves_every_key_it_lists()
    {
        var state = Waiting();
        var context = KeymapContext.For(state);
        var body = PaneText.Modal(state);

        var offered = Offer.Matches(body)
            .Select(m => m.Groups["key"].Value[0])
            .Distinct()
            .ToList();

        await Assert.That(offered).IsNotEmpty()
            .Because("this modal IS a menu - if nothing reads as an offer the guard is "
                   + $"asserting nothing. Body:\n{body}");

        var dead = offered
            .Where(key => Keymap.Resolve(KeyStroke.Char(key), context) is null)
            .ToList();

        await Assert.That(dead).IsEmpty()
            .Because("a menu whose items do nothing is the worst form of the advertised-key "
                   + "defect: the person did not guess the key, they were told it. Dead: "
                   + string.Join(", ", dead));
    }

    [Test]
    public async Task Deciding_a_gate_is_reachable_from_the_menu_that_offers_it()
    {
        // THE ONE SOMEBODY NEEDED. Two gates were waiting and the modal that
        // exists to say what can be done about a flight could not be used to
        // do it.
        var context = KeymapContext.For(Waiting());

        await Assert.That(Keymap.Resolve(KeyStroke.Char('d'), context))
            .IsEqualTo(Command.OpenGate)
            .Because("`d  decide a gate on this flight' is what the modal says, and it is "
                   + "the whole reason a person opens it while something waits.");
    }

    [Test]
    public async Task Every_drawn_modal_is_held_to_the_same_rule()
    {
        // THE GENERAL FORM. The actions modal was the one somebody hit; nothing
        // stops another growing a menu, and the hint line's own guard does not
        // reach a modal's body because that body is prose.
        var dead = new List<string>();

        foreach (var mode in Modals.Drawn)
        {
            var state = Waiting() with { Mode = mode };
            var context = KeymapContext.For(state);

            foreach (Match match in Offer.Matches(PaneText.Modal(state)))
            {
                var key = match.Groups["key"].Value[0];

                if (Keymap.Resolve(KeyStroke.Char(key), context) is null)
                {
                    dead.Add($"{mode}: '{key}'");
                }
            }
        }

        await Assert.That(dead).IsEmpty()
            .Because("a modal's words and its keymap drifted once and nothing was holding "
                   + "them together. Dead: " + string.Join(", ", dead));
    }
}
