using Gg.Contracts;

namespace Gg.Client;

/// <summary>
/// Why this machine may not fly a flight by hand, and what to do about it.
/// </summary>
/// <remarks>
/// <b>One requirement, not a list.</b> A person brings up one environment at a
/// time, and a refusal naming three is three pieces of work presented as one
/// wall. <c>gg plan</c> is still there for the whole picture.
/// </remarks>
public sealed record HandRefusal
{
    /// <summary>The label this machine does not advertise, as the matcher spells it.</summary>
    /// <remarks>
    /// <b>Off the checklist, never composed here.</b> It came from the one
    /// compiler, control-plane-side. A client that turned an environment name
    /// into a label would be a second compiler one process further out, and the
    /// day the two disagreed a person would be refused for a label no runner
    /// was ever asked about.
    /// </remarks>
    public required string Requirement { get; init; }

    /// <summary>
    /// The refusal, as a kind rather than a sentence.
    /// </summary>
    /// <remarks>
    /// <see cref="Reason.Sentence"/> derives from the kind contract-side, one
    /// grammar — so this and the fleet's own waiting state cannot describe one
    /// fleet differently.
    /// </remarks>
    public required Reason Reason { get; init; }

    /// <summary>One of <see cref="ChecklistSatisfiers"/>: who could satisfy it, if anybody.</summary>
    public required string Satisfier { get; init; }

    /// <summary>One of <see cref="LabelDispositions"/>: how the requirement is known.</summary>
    public required string Disposition { get; init; }

    /// <summary>
    /// What the person can actually do, which is the difference between a
    /// refusal and a wall.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Derived from the satisfier, because the remedies genuinely differ.</b>
    /// Nobody having the environment means bring one up. A strategy declining it
    /// means the fleet WOULD serve this and something chose not to — and a
    /// person who brings a machine up against that has done work that changes
    /// nothing.
    /// </para>
    /// <para>
    /// <b>It never says "verified".</b> Nothing evaluates an environment's
    /// registered meaning: <c>measured</c> means somebody registered a sentence
    /// and <c>stated</c> is not even that. A remedy implying this machine had
    /// been checked against an environment would cite a promise nothing keeps.
    /// </para>
    /// </remarks>
    public string Remedy => Satisfier switch
    {
        ChecklistSatisfiers.DeclinedByBound =>
            $"The fleet can serve '{Requirement}' and a strategy is declining it, so bringing a "
          + "machine up here changes nothing. Widen the bound, or fly it on the fleet.",

        ChecklistSatisfiers.Withheld =>
            $"'{Requirement}' is withheld by declaration, so nothing this machine advertises "
          + "would make it eligible. Change the declaration, or fly it on the fleet.",

        ChecklistSatisfiers.Nobody =>
            $"Nothing in this fleet advertises '{Requirement}'. Bring that environment up here "
          + "and register this machine with the label, or fly it on the fleet once something can.",

        // A LABEL THE FLEET HAS AND THIS MACHINE DOES NOT, which is the ordinary
        // hand-flight refusal and the one the fleet answers with silence.
        _ => $"This machine does not advertise '{Requirement}'. Bring that environment up here, "
           + "or fly it on the fleet, which has a runner that can.",
    };

    /// <summary>
    /// The first requirement this machine cannot meet, or null when it can meet
    /// them all.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Containment, the same direction the matcher runs it.</b> A machine
    /// advertising MORE than the flight asks for is eligible — that is what lets
    /// a warm pool of differently-capable runners exist, and reversing it here
    /// would refuse every machine that had anything extra.
    /// </para>
    /// <para>
    /// <b>Ordinal, because a label is matched ordinally.</b> A case-insensitive
    /// comparison here would pass a machine the fleet would then not offer the
    /// flight to, which is the worst of both: created, and never claimed.
    /// </para>
    /// </remarks>
    public static HandRefusal? For(Checklist plan, IReadOnlyList<string> advertised)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(advertised);

        foreach (var item in plan.Items)
        {
            if (advertised.Contains(item.Requirement, StringComparer.Ordinal))
            {
                continue;
            }

            return new HandRefusal
            {
                Requirement = item.Requirement,
                Reason = Reason.For(ReasonKinds.NoRunnerAdvertises, [item.Requirement]),
                Satisfier = item.Satisfier,
                Disposition = item.Disposition,
            };
        }

        return null;
    }
}
