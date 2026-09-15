namespace Gg.Contracts;

/// <summary>
/// What an agent asks its own proposal be called, and what it should say.
/// </summary>
/// <remarks>
/// <para>
/// <b>Because a title cut out of prose is not one the agent chose.</b> A
/// landing composed one from the loop's closing account - first sentence,
/// truncation marker removed, cut on a word - and that is a fallback with every
/// fallback's failure mode: it is whatever the agent happened to write first.
/// The rung below it was worse. Pull request 8629 was called
/// <i>"GG-118: Destination 'pull-request' requires 'in-scope', and it holds."</i>,
/// an obligation verdict written for an audit trail, used as a title because it
/// was the nearest string in scope.
/// </para>
/// <para>
/// <b>Two members, and only one of them is required.</b> A title is the thing a
/// reviewer reads in a list of thirty; a description is a courtesy, and the one
/// a destination writes already carries the branch and the work
/// item. An agent with nothing to add leaves it out rather than padding it,
/// which is the same distinction <c>WorkItemProposal.Target</c> draws one type
/// over: absent is a real answer.
/// </para>
/// <para>
/// <b>Bounded, and REFUSED rather than trimmed.</b> A silent truncation is a
/// title the agent did not write appearing under its name - and the agent can
/// read a refusal and shorten it, which it cannot do about a cut it never sees.
/// <c>FlightNomination</c> makes the same choice about its note, for the same
/// reason.
/// </para>
/// <para>
/// <b>It decides nothing and grants nothing.</b> Whether the flight lands at
/// all is admission's answer, arrived at without reading this; the runner uses
/// the wording if a landing happens and drops it if one does not. That is what
/// makes the move behind it record-only, and what makes it safe to grant to an
/// agent that also holds the edit move.
/// </para>
/// </remarks>
[FactKind(FactKinds.LandingProposal)]
[PinnedId("8a4c1f07-52d9-4be3-9c60-1d7e3b85a2f4")]
public sealed record LandingProposal
{
    /// <summary>The most a title may be.</summary>
    /// <remarks>
    /// <b>Chosen for the person rather than for the api.</b> Both providers
    /// this platform speaks to accept far more; a list of proposals is read at a
    /// glance, and a title that wraps twice is one nobody finishes. The envelope
    /// may ask for less - a team wanting seventy characters says so in its
    /// instruction - and this is the bound past which nothing is a title at all.
    /// </remarks>
    public const int MaxTitle = 120;

    /// <summary>The most a description may be.</summary>
    /// <remarks>
    /// Generous, because this one really is prose and a reviewer asked for it.
    /// Still bounded: one that ran to the length of a transcript would make
    /// the fact that carries it the most expensive thing in the record to read,
    /// which is the argument <c>ExecutorPort.MaxReasonLength</c> makes about the
    /// row beneath it.
    /// </remarks>
    public const int MaxDescription = 4000;

    /// <summary>What the proposal should be called.</summary>
    public required string Title { get; init; }

    /// <summary>What it should say beyond its title, or null.</summary>
    public string? Description { get; init; }

    /// <summary>Why this may not be used, or null when it may.</summary>
    public static string? Validate(LandingProposal proposal)
    {
        ArgumentNullException.ThrowIfNull(proposal);

        if (string.IsNullOrWhiteSpace(proposal.Title))
        {
            return "A landing proposal with no title proposes nothing. Leave the call out "
                 + "rather than making one that says nothing.";
        }

        if (proposal.Title.Length > MaxTitle)
        {
            return $"A title is at most {MaxTitle} characters and this one is "
                 + $"{proposal.Title.Length}. Shorten it and call again - it is what somebody "
                 + "reads in a list of thirty, not the account.";
        }

        // ONE LINE, because a title is one line on every forge that has one. An
        // agent that sent a paragraph would have it rendered as a title in one
        // place and as a title-with-newlines in another.
        if (proposal.Title.AsSpan().ContainsAny('\n', '\r'))
        {
            return "A title is one line. What runs past the first line belongs in the description.";
        }

        if (proposal.Description is { Length: > MaxDescription })
        {
            return $"A description is at most {MaxDescription} characters and this one is "
                 + $"{proposal.Description.Length}.";
        }

        return null;
    }
}

/// <summary>
/// What a flight's destination asks of the agent about its landing.
/// </summary>
/// <remarks>
/// <b>Carried to the agent, because a document nothing renders into a lease is
/// a policy nobody is told about.</b> That omission has shipped twice in this
/// product, one member apart, in the same method: <c>instructions:</c> was
/// green end to end with nothing reaching an agent, and <c>brief:</c> after it.
/// Both members exist on the lease for that reason and this one joins them.
/// <para>
/// Null where the envelope asks nothing, which is every envelope written before
/// this. Not an empty string: "no instruction" and "an instruction that says
/// nothing" must not read the same.
/// </para>
/// </remarks>
[PinnedId("b16e9d38-7a02-4c5f-91ad-6e08c4f27b31")]
public sealed record LeaseLanding
{
    /// <summary>How the destination wants what it opens named, or null.</summary>
    public string? Title { get; init; }

    /// <summary>What it wants the description to say, or null.</summary>
    public string? Description { get; init; }
}
