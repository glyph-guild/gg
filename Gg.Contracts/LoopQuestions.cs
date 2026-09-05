namespace Gg.Contracts;

/// <summary>
/// A question an agent could not answer from the work itself.
/// </summary>
/// <remarks>
/// <para>
/// <b>The second fact in this vocabulary that is an agent's REQUEST.</b>
/// Everything else is measured - a diff, a commit, a session, a registry entry -
/// and <see cref="HumanAccount"/> is a person's own words. This one asks for
/// something: that a person decide. It sits beside
/// <see cref="FlightNomination"/> for that reason, and it is the narrower of
/// the two - a nomination names a value from a menu, and this names nothing at
/// all.
/// </para>
/// <para>
/// <b>Recorded whenever the tool is called, and it decides nothing.</b> Rule 3:
/// whether a recorded question opens a gate is the envelope's to say, through
/// an ordinary obligation. An agent that could open a gate by asking could
/// stall a tenant's work at will.
/// </para>
/// <para>
/// <b>Two facts, not one state.</b> The question is recorded whenever the tool
/// is called; the OUTCOME says what actually happened, and an agent that asked
/// and then went on to finish is <c>completed</c> with a question beside it.
/// Collapsing them would make one clarifying question turn a finished flight
/// into a chore, which is how a feature gets switched off.
/// </para>
/// <para>
/// <b>One member, and the shape is a ratchet.</b> The pressure runs one way:
/// every field somebody will want - what the agent thinks it needs, which
/// files, how long, where it should land - makes the question more useful and
/// makes it configuration an agent writes. What crosses is a value, never a
/// permission.
/// </para>
/// </remarks>
[FactKind(FactKinds.LoopQuestion)]
[PinnedId("991263dd-aeee-4751-ad20-bea6262af680")]
public sealed record LoopQuestion
{
    /// <summary>What the agent needs decided, in its own words.</summary>
    /// <remarks>
    /// Prose, so line breaks are the agent's own and survive: a question laid
    /// out over three lines is one somebody wrote to be read, and the field a
    /// person reads while deciding something is the wrong one to flatten.
    /// </remarks>
    public required string Question { get; init; }

    /// <summary>
    /// How long a question may be.
    /// </summary>
    /// <remarks>
    /// <b>Sized for what somebody reads while deciding something</b>, which is
    /// <see cref="HumanAccount.MaxStatement"/>'s disposition at a smaller
    /// number: an account is a person explaining themselves and a question is
    /// an agent asking one thing. Inline rather than a reference, because a
    /// question nobody can read without fetching an artifact is a question
    /// nobody answers.
    /// </remarks>
    public const int MaxQuestion = 2000;

    /// <summary>
    /// What is wrong with this question, or null when nothing is.
    /// </summary>
    /// <remarks>
    /// <b>Refused rather than truncated into safety.</b> A question cut in half
    /// is one a person cannot answer, and one that arrives looking answerable
    /// is worse than one that was rejected - the flight would then wait on a
    /// gate nobody could close. The ceiling is named in the refusal so an agent
    /// that hit it can say less rather than guess.
    /// </remarks>
    public static string? Validate(LoopQuestion question)
    {
        ArgumentNullException.ThrowIfNull(question);

        if (string.IsNullOrWhiteSpace(question.Question))
        {
            return "A question needs words in it. A blocked declaration with nothing in it is "
                 + "worse than none: it opens a gate a person has been given no way to close.";
        }

        return question.Question.Length > MaxQuestion
            ? $"A question is at most {MaxQuestion} characters and this one is "
            + $"{question.Question.Length}. Ask for the decision rather than describing the "
            + "whole situation - what is cut in half cannot be answered, and what is cut and "
            + "still reads as a question is worse."
            : null;
    }
}
