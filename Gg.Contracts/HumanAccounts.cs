namespace Gg.Contracts;

/// <summary>How a person dealt with the account that was proposed to them.</summary>
/// <remarks>
/// <para>
/// <b>A number that says whether a design premise held.</b> The premise is that
/// nobody should have to write a summary, so an agent proposes one and a person
/// corrects it. If people take the reject escape often, the inference is bad and
/// everybody is writing the summary after all - which is the exact failure this
/// was built to avoid.
/// </para>
/// <para>
/// Instrumented from the first flight, like the attach rate, and for the same
/// reason: a rate measured after somebody has been impressed by the feature
/// measures the wrong thing.
/// </para>
/// </remarks>
[VocabularyOf(VocabularyFingerprints.Fact)]
public static class AccountConfirmations
{
    /// <summary>Taken as proposed.</summary>
    public const string Accepted = "accepted";

    /// <summary>Corrected. The premise holding looks like this.</summary>
    public const string Edited = "edited";

    /// <summary>
    /// Thrown away and written from scratch.
    /// </summary>
    /// <remarks>
    /// The escape exists because a bad proposal is worse to edit than to
    /// replace. It is also the signal that the feature is not working.
    /// </remarks>
    public const string Replaced = "replaced";

    /// <summary>
    /// Written from nothing, because nothing was proposed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>NOT <see cref="Replaced"/>, and the difference is a signal rather than
    /// a shade of meaning.</b> Replaced says a proposal was made and was bad
    /// enough to throw away - it is how this feature reports that it is not
    /// working. An account with no proposal behind it has nothing to report
    /// about the inference, and recording `replaced` for it would fire that
    /// signal wherever the inference had simply never run.
    /// </para>
    /// <para>
    /// <b>The state <see cref="HumanAccount.WasProposed"/> already named.</b> Its
    /// remark distinguishes "a replaced account had a proposal that was
    /// discarded" from "one written with no inference at all", and until this
    /// value existed the second could not be said.
    /// </para>
    /// <para>
    /// <b>Where it comes from today: a hand-flight.</b> A person handed a
    /// terminal writes what they did in the return file, and nothing proposed
    /// anything to them - <c>HandSession</c> runs on the takeover path and not
    /// that one. Their words are the only account such a flight has.
    /// </para>
    /// </remarks>
    public const string Unaided = "unaided";

    public static IReadOnlyList<string> All { get; } =
        [Accepted, Edited, Replaced, Unaided];
}

/// <summary>
/// What a person says they did, in their own name.
/// </summary>
/// <remarks>
/// <para>
/// <b>A human assertion, and that is a different kind of thing from everything
/// else in the vocabulary.</b> Measurements are ours and computed. The agent's
/// account is the agent's and decides nothing. An agent's PROPOSAL about a
/// human's work is a guess. This is the only artifact in the system a person has
/// put their name to.
/// </para>
/// <para>
/// <b>The confirmation is what makes it one.</b> Nothing is recorded until a
/// person accepts or edits, because an unconfirmed proposal stored as somebody's
/// account is putting words in their mouth and attributing them under Article
/// XII. A person who walks away leaves no human account at all, and the flight
/// says so rather than showing a guess as theirs.
/// </para>
/// <para>
/// <b>Never in the digest.</b> Same rule as the agent's account: Article XIII
/// compares accumulated flights, which needs records computed identically every
/// time, and prose differs every time. This one has a second reason - a digest
/// is a machine record and this is a person's statement.
/// </para>
/// </remarks>
[FactKind(FactKinds.HumanAccount)]
[PinnedId("9c47f6b2-1e58-4d03-a76f-24b8e05c19da")]
public sealed record HumanAccount
{
    /// <summary>Who is asserting this. The session's principal, never typed in.</summary>
    public required string By { get; init; }

    /// <summary>
    /// What they say they did.
    /// </summary>
    /// <remarks>
    /// Named for what it is rather than for what it holds. A member called
    /// <c>Text</c> trips the scan that keeps file contents off the wire - and
    /// rightly, because the scan cannot tell prose a person wrote from prose a
    /// file contained. This is an assertion, and saying so is the more accurate
    /// name anyway.
    /// </remarks>
    public required string Statement { get; init; }

    /// <summary>One of <see cref="AccountConfirmations"/>.</summary>
    /// <remarks>
    /// Carried WITH the account rather than beside it. "This is what I did" and
    /// "and I wrote every word of it myself" are two facts a reader wants
    /// together: an accepted account is one a person read and agreed with, and an
    /// edited one is a sentence they changed.
    /// </remarks>
    public required string Confirmation { get; init; }

    /// <summary>When they confirmed it.</summary>
    public required DateTimeOffset ConfirmedAt { get; init; }

    /// <summary>How much of the proposal survived, when it was edited.</summary>
    /// <remarks>
    /// Null when nothing was proposed - a replaced account had a proposal that
    /// was discarded, and one written with no inference at all is a different
    /// thing again.
    /// </remarks>
    public bool? WasProposed { get; init; }

    /// <summary>The most a person's account may be.</summary>
    /// <remarks>
    /// Inline evidence, and the inline budget is sized for what somebody READS
    /// while deciding something. A statement longer than this is a document, and
    /// documents are what the reference disposition is for.
    /// </remarks>
    public const int MaxStatement = 8000;

    /// <summary>The diagnosis, or null when there is nothing wrong.</summary>
    public static string? Validate(HumanAccount account)
    {
        ArgumentNullException.ThrowIfNull(account);

        if (string.IsNullOrWhiteSpace(account.By))
        {
            return "A human account names who asserted it. An assertion nobody can be attributed "
                 + "to is not one.";
        }

        if (string.IsNullOrWhiteSpace(account.Statement))
        {
            return "A human account says something. An empty one is a person who walked away, and "
                 + "that is recorded by there being no account rather than by an empty one.";
        }

        if (account.Statement.Length > MaxStatement)
        {
            return $"A human account is at most {MaxStatement} characters and this one is "
                 + $"{account.Statement.Length}. It is inline evidence, sized for what somebody reads "
                 + "while deciding something.";
        }

        return AccountConfirmations.All.Contains(account.Confirmation, StringComparer.Ordinal)
            ? null
            : $"Unknown confirmation '{account.Confirmation}'. Expected one of: "
            + string.Join(", ", AccountConfirmations.All) + ".";
    }
}
