using Gg.Contracts;

namespace Gg.Console;

/// <summary>
/// What a board row's state word means, as a colour.
/// </summary>
/// <remarks>
/// <b>Fewer tints than there are words, deliberately.</b> Six endings and
/// three watch standings would be nine colours nobody can hold in their head;
/// these are the four distinctions a person acts on — it became a flight,
/// somebody or something said no, it stopped applying, and the watch is
/// broken.
/// </remarks>
public enum BoardTint
{
    /// <summary>The ordinary foreground: standing, swept, or a word we do not know.</summary>
    None,

    /// <summary>A flight came of it.</summary>
    Opened,

    /// <summary>A person or a rule said no.</summary>
    Refused,

    /// <summary>It stopped applying, and nothing came of it.</summary>
    Moot,

    /// <summary>A watch that cannot do its job.</summary>
    Broken,

    /// <summary>A watch that should have swept by now and has not.</summary>
    Quiet,
}

/// <summary>
/// The board's state column, read as colour — <c>FlightLook</c>'s shape over a
/// tab with two kinds of row on it.
/// </summary>
/// <remarks>
/// <para>
/// <b>Three vocabularies share that column.</b> A nomination shows its mode
/// while it stands (<c>gated</c>, <c>auto</c>), its ending once it has one, and
/// a watch shows how it is doing. So this maps words rather than a closed enum,
/// and <see cref="FlightLook"/>'s rule carries over: a word nobody defined here
/// gets no colour, because inventing one teaches a person something false.
/// </para>
/// <para>
/// <b>Only an ENDING is over.</b> The flights tab recedes what is finished
/// because that tab is a record, and half of this one is too — but a watch is
/// the live thing on the board, and dimming an unreachable one would push back
/// the row most worth reading.
/// </para>
/// </remarks>
public static class BoardLook
{
    /// <summary>What a watch says while it has swept nothing lately.</summary>
    /// <remarks>
    /// <c>Rows.Board</c> writes this word rather than the contract, because it
    /// is a derivation — a standing with a <c>QuietSince</c> — rather than
    /// something the control plane said. Named here so the two cannot drift.
    /// </remarks>
    public const string Quiet = "quiet";

    public static BoardTint Tint(string? state) => state switch
    {
        // THE ONE GOOD ENDING. Green is the one colour nobody has to be taught,
        // and the flights tab spends it on the same thing: the outcome somebody
        // wanted.
        NominationEndings.Opened => BoardTint.Opened,

        // SOMEBODY SAID NO, and the two ways of saying it read alike on
        // purpose: `declined` is a person and `refused` is a rule, and what a
        // reader does next - find out why - is the same either way. The `why`
        // column is where they differ, and it is right there.
        //
        // YELLOW, NOT RED, for the flights tab's reason about `grounded`: a
        // decision that went against something is not a fault, and red on
        // somebody's own decision is the cry of wolf that teaches people to
        // stop reading colour.
        NominationEndings.Declined or NominationEndings.Refused => BoardTint.Refused,

        // NOTHING CAME OF IT, and nothing was decided either. Withdrawn is the
        // subject ending, superseded is a newer version arriving, lapsed is a
        // window closing - three ways of ceasing to apply, which should look
        // like nothing rather than like a result.
        NominationEndings.Withdrawn
            or NominationEndings.Superseded
            or NominationEndings.Lapsed => BoardTint.Moot,

        // THE WATCH THAT CANNOT DO ITS JOB. The one red on this tab, and it is
        // the flights tab's `failed`: it wanted something and did not get it.
        WatchOutcomes.Unreachable => BoardTint.Broken,

        // AND THE ONE THAT IS NOT FAILING, just silent. A watch sweeps on its
        // own period; one that has missed its window has not errored, so it is
        // a warning rather than a fault - and without a colour it is invisible,
        // which is exactly how a nominator stops nominating without anybody
        // noticing.
        Quiet => BoardTint.Quiet,

        // STANDING, SWEPT, NEVER SWEPT, and anything this console has not been
        // taught. Most rows are one of these.
        _ => BoardTint.None,
    };

    /// <summary>
    /// Whether this row is a finished nomination, and so belongs in the
    /// background.
    /// </summary>
    public static bool IsOver(string? state) =>
        state is { Length: > 0 } && NominationEndings.All.Contains(state, StringComparer.Ordinal);
}
