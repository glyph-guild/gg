using Gg.Contracts;

namespace Gg.Client;

/// <summary>What a person did with the proposal they were shown.</summary>
/// <remarks>
/// <b>Three, and the third is a signal rather than a feature.</b> The escape has
/// to exist because a bad proposal is worse to edit than to replace - but people
/// taking it often means the inference is bad and everyone is writing the summary
/// after all, which is the failure this design was built to avoid.
/// </remarks>
public abstract record HandChoice
{
    /// <summary>Taken as proposed.</summary>
    public sealed record Accept : HandChoice;

    /// <summary>Corrected. What the premise holding looks like.</summary>
    public sealed record Edit(string Statement) : HandChoice;

    /// <summary>Written from scratch, the proposal discarded.</summary>
    public sealed record Replace(string Statement) : HandChoice;

    /// <summary>
    /// Walked away.
    /// </summary>
    /// <remarks>
    /// Not a fourth button. It is what happens when somebody closes the terminal,
    /// and it has to be representable or the code will invent a default for it -
    /// which is exactly the thing that must not happen.
    /// </remarks>
    public sealed record WalkedAway : HandChoice;
}

/// <summary>What came out of the confirmation, if anything.</summary>
public sealed record HandOutcome
{
    /// <summary>The person's account, or null when they left without confirming.</summary>
    public HumanAccount? Account { get; init; }

    /// <summary>What they chose, for the rate nobody exports.</summary>
    public required string Choice { get; init; }

    /// <summary>What to tell them, and what the flight says.</summary>
    public required string Detail { get; init; }
}

/// <summary>
/// Turns a proposal and a person's answer into an account, or into nothing.
/// </summary>
/// <remarks>
/// <para>
/// <b>The confirmation is the trust boundary, not a politeness.</b> An agent's
/// proposal about a human's work is a guess. A confirmed account is a human
/// assertion, attributed under Article XII. The confirmation is the step that
/// converts one into the other, which is why it cannot be skipped, defaulted, or
/// accepted after a timeout: recording an unconfirmed proposal as somebody's
/// account is putting words in their mouth and signing their name to them.
/// </para>
/// <para>
/// <b>So walking away records nothing.</b> Not an empty account, not the
/// proposal with a caveat - nothing, and the flight says there is nothing.
/// </para>
/// </remarks>
public static class HandConfirmation
{
    /// <summary>The account a person confirmed, or the absence of one.</summary>
    /// <param name="by">Who is asserting it. Never typed in.</param>
    /// <param name="proposal">What the agent proposed, if anything.</param>
    /// <param name="choice">What the person did about it.</param>
    /// <param name="at">When they did.</param>
    public static HandOutcome Confirm(
        string by, ProposedAccount? proposal, HandChoice choice, DateTimeOffset at)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(by);
        ArgumentNullException.ThrowIfNull(choice);

        switch (choice)
        {
            case HandChoice.Accept when proposal is { Present: true }:
                return Recorded(by, proposal.Proposal, AccountConfirmations.Accepted, at,
                    wasProposed: true,
                    "recorded as your account, as proposed");

            case HandChoice.Accept:
                // There was nothing to accept. Answering "accepted" here would
                // record an empty statement as somebody's assertion.
                return new HandOutcome
                {
                    Choice = AccountConfirmations.Accepted,
                    Detail = "There was no proposal to accept, so nothing was recorded. The flight "
                           + "has no account from you.",
                };

            case HandChoice.Edit(var edited) when edited.Trim().Length > 0:
                return Recorded(by, edited, AccountConfirmations.Edited, at,
                    wasProposed: proposal is { Present: true },
                    "recorded as your account, edited");

            case HandChoice.Replace(var written) when written.Trim().Length > 0:
                return Recorded(by, written, AccountConfirmations.Replaced, at,
                    wasProposed: proposal is { Present: true },
                    "recorded as your account, written from scratch");

            case HandChoice.Edit:
            case HandChoice.Replace:
                // An empty edit is somebody who opened the box and left. It is
                // the walk-away case wearing a different button.
                return WalkedAway();

            default:
                return WalkedAway();
        }
    }

    private static HandOutcome Recorded(
        string by, string statement, string confirmation, DateTimeOffset at,
        bool wasProposed, string detail)
    {
        // Stripped and bounded like any inline item, here at production. This is
        // text a person typed at a terminal and it goes back to one.
        var clean = ControlText.Strip(statement, allowLineBreaks: true).Trim();

        if (clean.Length > HumanAccount.MaxStatement)
        {
            clean = clean[..HumanAccount.MaxStatement];
        }

        return new HandOutcome
        {
            Account = new HumanAccount
            {
                By = by,
                Statement = clean,
                Confirmation = confirmation,
                ConfirmedAt = at,
                WasProposed = wasProposed,
            },
            Choice = confirmation,
            Detail = detail,
        };
    }

    /// <summary>
    /// Nothing recorded, and the flight says so.
    /// </summary>
    /// <remarks>
    /// The absence is the record. "This flight has no account from the person who
    /// worked on it" is a true and useful statement; a proposal shown as theirs
    /// would be a false one.
    /// </remarks>
    private static HandOutcome WalkedAway() =>
        new()
        {
            Choice = "walked-away",
            Detail = "Nothing was recorded. An account is a person's own statement, so it is not "
                   + "written for them - the flight has no account from you.",
        };
}

/// <summary>
/// How often the proposal was good enough to keep.
/// </summary>
/// <remarks>
/// <para>
/// <b>The number that says whether the premise held.</b> Accept and edit are the
/// design working; replace is everybody writing the summary after all, which is
/// the failure it was built to avoid.
/// </para>
/// <para>
/// A fact on the flight, exported nowhere - the same treatment as the attach
/// rate, for the same reason. The telemetry seam is inert by decision and this is
/// what it is inert for.
/// </para>
/// </remarks>
public sealed record HandConfirmationFact
{
    public required string FlightId { get; init; }

    /// <summary>One of <see cref="AccountConfirmations"/>, or walked-away.</summary>
    public required string Choice { get; init; }
}
