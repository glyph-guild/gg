using Gg.Console;
using Gg.Contracts;

namespace Gg.Console.Tests;

/// <summary>
/// What is over recedes, and its ending says which one it was.
/// </summary>
/// <remarks>
/// <para>
/// <b>The flights tab is mostly history.</b> Every flight this tenant has
/// opened is on it, newest first, and the ones a person can still act on are a
/// handful at the top - so a list where everything reads equally loud is a list
/// somebody has to filter with their eyes every time they open it.
/// </para>
/// <para>
/// <b>Only the endings are tinted, because most rows are open.</b> A colour
/// every row carries distinguishes nothing. An open flight keeps the ordinary
/// foreground; colour means "this one is over, and here is how".
/// </para>
/// <para>
/// <b>And everything over recedes, failures included</b> - the owner's call,
/// and the argument for it is that this tab is a record. A failure is as
/// finished as a landing; what is still open is the thing anybody can do
/// something about. The failure still says so in red, one shade quieter.
/// </para>
/// </remarks>
public class TheFlightsTabShowsWhatIsOverTests
{
    [Test]
    public async Task An_open_flight_is_the_ordinary_foreground()
    {
        // THE ONE THAT MUST NOT BE COLOURED. Most rows are open, and a tint on
        // the common case is a tint that says nothing.
        await Assert.That(FlightLook.Tint(FlightStates.Open)).IsEqualTo(FlightTint.None);
        await Assert.That(FlightLook.IsOver(FlightStates.Open)).IsFalse();
    }

    [Test]
    public async Task Every_ending_has_its_own_tint()
    {
        await Assert.That(FlightLook.Tint(FlightStates.Landed)).IsEqualTo(FlightTint.Landed);
        await Assert.That(FlightLook.Tint(FlightStates.Grounded)).IsEqualTo(FlightTint.Grounded);
        await Assert.That(FlightLook.Tint(FlightStates.Withdrawn)).IsEqualTo(FlightTint.Withdrawn);
        await Assert.That(FlightLook.Tint(FlightStates.Failed)).IsEqualTo(FlightTint.Failed);
        await Assert.That(FlightLook.Tint(FlightStates.Unknown)).IsEqualTo(FlightTint.Unknown);
    }

    [Test]
    public async Task And_every_ending_recedes()
    {
        foreach (var over in (string[])
        [
            FlightStates.Landed, FlightStates.Grounded, FlightStates.Withdrawn,
            FlightStates.Failed, FlightStates.Unknown,
        ])
        {
            await Assert.That(FlightLook.IsOver(over)).IsTrue()
                .Because($"'{over}' is an ending, and this tab is a record of what happened.");
        }
    }

    [Test]
    public async Task A_state_this_console_does_not_know_is_left_alone()
    {
        // NEITHER TINTED NOR DIMMED. There is no honest colour for a word
        // nobody here defined, and pushing the row back would hide the one row
        // worth asking about.
        await Assert.That(FlightLook.Tint("something-new")).IsEqualTo(FlightTint.None);
        await Assert.That(FlightLook.IsOver("something-new")).IsFalse();
        await Assert.That(FlightLook.IsOver(null)).IsFalse();
    }

    [Test]
    public async Task What_is_tinted_and_what_recedes_cannot_disagree()
    {
        // ONE LIST, NOT TWO. A state added to the tints and forgotten in the
        // dimming is a row that is coloured and not dim, or the reverse.
        foreach (var state in (string[])
        [
            FlightStates.Open, FlightStates.Landed, FlightStates.Grounded,
            FlightStates.Withdrawn, FlightStates.Failed, FlightStates.Unknown, "made-up",
        ])
        {
            await Assert.That(FlightLook.IsOver(state))
                .IsEqualTo(FlightLook.Tint(state) is not FlightTint.None);
        }
    }

    [Test]
    public async Task Every_state_in_the_vocabulary_is_answered()
    {
        // ARTICLE XI OVER THE VOCABULARY ITSELF: a state the contract declares
        // and this switch has never heard of would render as an open flight,
        // which is the one thing it certainly is not.
        var declared = typeof(FlightStates)
            .GetFields()
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!)
            .ToList();

        await Assert.That(declared).IsNotEmpty();

        var unanswered = declared
            .Where(s => s != FlightStates.Open && FlightLook.Tint(s) is FlightTint.None)
            .ToList();

        await Assert.That(unanswered).IsEmpty()
            .Because("every ending the contract declares needs a tint, or it reads as an open "
                   + "flight. Unanswered: " + string.Join(", ", unanswered));
    }
}
