namespace Gg.Contracts;

/// <summary>
/// Why an obligation could never have applied, in the one sentence every
/// surface says it with.
/// </summary>
/// <remarks>
/// <para>
/// <b>One fact, one sentence, however many surfaces render it.</b> The plan
/// composed one wording and <c>gg why</c> composed another — <i>"this work
/// kind"</i> against <i>"this kind of work"</i> — in two repositories, agreeing
/// because somebody typed carefully twice. A person reading a plan and a person
/// reading <c>gg why</c> are checking the same claim, and the only thing they
/// can compare is the words.
/// </para>
/// <para>
/// <b>It lives here because both sides already reference here</b>, and beside
/// the vocabulary it names. Two spellings of one sentence is the manifest hazard
/// wearing a different coat: two computations that agree until the first fix to
/// either.
/// </para>
/// </remarks>
public static class Inapplicability
{
    /// <summary>
    /// The sentence for a rule this kind of work could never answer.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The family is not one this contract declares. A reason naming a family
    /// nobody can look up is a reason nobody can check — the same refusal
    /// <see cref="ObligationAttribution"/> already makes on the wire, made
    /// before the sentence is composed rather than after it is rendered.
    /// </exception>
    public static string Because(string family) =>
        family is { Length: > 0 } && FactKinds.All.Contains(family, StringComparer.Ordinal)
            ? $"this kind of work cannot produce {family}, so this rule can never apply to it"
            : throw new ArgumentOutOfRangeException(
                nameof(family),
                family,
                "An obligation marked inapplicable has to name a fact family somebody can look "
              + "up, or the reason is one nobody can check. Expected one of: "
              + string.Join(", ", FactKinds.All) + ".");
}
