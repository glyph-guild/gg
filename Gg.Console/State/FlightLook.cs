using Gg.Contracts;

namespace Gg.Console;

/// <summary>
/// What a flight's state is worth saying with colour.
/// </summary>
/// <remarks>
/// <b>A tint rather than a colour, so this stays answerable without a
/// terminal.</b> Which shade a tint becomes is <c>Views/LookStyles</c>'s, the
/// one file in this console that builds one; what a state MEANS is a fact about
/// the vocabulary and belongs beside the rest of the model.
/// </remarks>
public enum FlightTint
{
    /// <summary>Nothing to say. The ordinary foreground.</summary>
    None,

    /// <summary>The destination admitted.</summary>
    Landed,

    /// <summary>A person stopped it.</summary>
    Grounded,

    /// <summary>The question ceased to apply.</summary>
    Withdrawn,

    /// <summary>The work concluded and the destination did not admit.</summary>
    Failed,

    /// <summary>No ending was recorded and none can be derived.</summary>
    Unknown,
}

/// <summary>
/// Which flights are over, and what their ending is worth saying in colour.
/// </summary>
/// <remarks>
/// <para>
/// <b>Only the endings are tinted.</b> Most rows on this tab are open, and a
/// colour every row carries is a colour that distinguishes nothing - so an open
/// flight keeps the ordinary foreground and the colour means "this one is
/// over, and here is how".
/// </para>
/// <para>
/// <b>Everything that is over recedes, failures included.</b> The argument for
/// keeping a failure bright is that somebody should see it; the argument
/// against is that this tab is a list of what HAS happened, and a flight that
/// failed is as finished as one that landed. What is still open is the thing
/// somebody can act on, so that is what stands out - and a failure still says
/// so in red, one shade quieter.
/// </para>
/// <para>
/// <b>A state this console does not know is neither.</b> Not tinted, because
/// there is no honest colour for a word nobody here has defined, and NOT dimmed
/// - pushing back a row this console cannot read would hide the one row worth
/// asking about.
/// </para>
/// </remarks>
public static class FlightLook
{
    /// <summary>What this state is worth saying in colour.</summary>
    public static FlightTint Tint(string? state) => state switch
    {
        FlightStates.Landed => FlightTint.Landed,
        FlightStates.Grounded => FlightTint.Grounded,
        FlightStates.Withdrawn => FlightTint.Withdrawn,
        FlightStates.Failed => FlightTint.Failed,
        FlightStates.Unknown => FlightTint.Unknown,

        // OPEN IS THE ORDINARY ONE, and so is anything this console has not
        // been taught. See the remarks: a word nobody defined here has no
        // honest colour.
        _ => FlightTint.None,
    };

    /// <summary>
    /// Whether this flight is over, and so should recede.
    /// </summary>
    /// <remarks>
    /// <b>Read off the tint rather than listed twice.</b> The endings are
    /// exactly the states worth tinting, so a state added to one and forgotten
    /// in the other is a row that is coloured and not dimmed, or the reverse.
    /// </remarks>
    public static bool IsOver(string? state) => Tint(state) is not FlightTint.None;
}
