using Gg.Console;

namespace Gg.Console.Tests;

/// <summary>
/// A field value too wide for the column is wrapped onto more rows, not cut off.
/// </summary>
/// <remarks>
/// <para>
/// <b>REPORTED FROM USE: the `awaiting` field is cut off.</b> Every field is
/// laid as a single-line <c>TextField</c>, one row per field, and a
/// <c>TextField</c> clips. What tipped these past the column was a real
/// improvement: `awaiting` became <c>$"{sentence}: {said}"</c> so that a person
/// could see what an agent actually asked, which is what
/// <see cref="WhatIsOwedSaysWhatWasAskedTests"/> is about. GG-991's reads
/// <i>"The loop used its whole wall-clock budget of 20m and stopped. This flight
/// is waiting for a person."</i> - far wider than any modal.
/// </para>
/// <para>
/// <b>The wrap belongs to the model, which is why it is tested here.</b>
/// <c>Lay</c> builds widgets and cannot be asserted on without a terminal; the
/// decision about how many rows a value needs is arithmetic, and
/// <c>Rows.Wrapped</c> already does it for the log-detail pane.
/// </para>
/// <para>
/// <b>A width of zero wraps nothing</b>, which is the rule
/// <c>FlightDetails.Lines</c> already records: a viewport has no width until it
/// has been laid out, and wrapping to no width is one row per character.
/// </para>
/// </remarks>
public class AWideFieldWrapsRatherThanClipsTests
{
    private const string Long =
        "The loop used its whole wall-clock budget of 20m and stopped. This flight is "
      + "waiting for a person.";

    [Test]
    public async Task A_value_wider_than_the_column_takes_more_than_one_row()
    {
        var rows = FlightDetails.Wrapped([new FlightField("awaiting", Long)], 40);

        await Assert.That(rows.Count).IsGreaterThan(1)
            .Because("one row means a TextField clipping it, which is what a person reported "
                   + "as the field being cut off.");

        await Assert.That(rows.All(r => r.Value.Length <= 40)).IsTrue()
            .Because("a row wider than the column it is drawn in is the same clip one level up.");
    }

    [Test]
    public async Task The_label_is_on_the_first_row_and_not_repeated()
    {
        var rows = FlightDetails.Wrapped([new FlightField("awaiting", Long)], 40);

        await Assert.That(rows[0].Label).IsEqualTo("awaiting");

        await Assert.That(rows.Skip(1).All(r => r.Label.Length == 0)).IsTrue()
            .Because("a label repeated down the continuation rows reads as several things owed "
                   + "rather than one sentence, and ONE FIELD PER THING OWED is what the label "
                   + "means here.");
    }

    [Test]
    public async Task Nothing_is_lost_in_the_wrapping()
    {
        var rows = FlightDetails.Wrapped([new FlightField("awaiting", Long)], 40);

        var joined = string.Join(" ", rows.Select(r => r.Value.Trim()));

        await Assert.That(joined).IsEqualTo(Long)
            .Because("wrapping is a layout decision; dropping a word would make it an editorial "
                   + "one, and the tail of a sentence about why a flight stopped is the part "
                   + "that says what to do.");
    }

    [Test]
    public async Task A_value_that_fits_is_left_alone()
    {
        var rows = FlightDetails.Wrapped([new FlightField("stage", "evaluated")], 40);

        await Assert.That(rows.Count).IsEqualTo(1);
        await Assert.That(rows[0].Value).IsEqualTo("evaluated");
    }

    [Test]
    public async Task A_width_of_zero_wraps_nothing()
    {
        // BEFORE THE FIRST LAYOUT a viewport has no width, and wrapping to none
        // is one row per character - a modal that becomes a column of letters.
        var rows = FlightDetails.Wrapped([new FlightField("awaiting", Long)], 0);

        await Assert.That(rows.Count).IsEqualTo(1);
        await Assert.That(rows[0].Value).IsEqualTo(Long);
    }
}
