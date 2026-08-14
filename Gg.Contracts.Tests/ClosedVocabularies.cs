using Gg.Contracts;
using System.Reflection;

namespace Gg.Contracts.Tests;

/// <summary>
/// Every closed vocabulary that crosses, and its values.
/// </summary>
/// <remarks>
/// <para>
/// <b>Written because the rule had no mechanism behind it.</b> Contracts says a member
/// may be added freely and a value may not - because the only safe response to an
/// unknown value is to halt, so an added value breaks every prior reader by design.
/// Both fingerprints hashed pinned types and their PROPERTY names, so a third
/// <c>DiffBasis</c> value moved neither of them: the guard that exists to force this
/// conversation could not see the change that most needs one.
/// </para>
/// <para>
/// <b>Found by predicting the guard would fire and watching it not.</b> A recorded
/// prediction is falsifiable and an abstention is not.
/// </para>
/// <para>
/// <b>Discovered rather than listed.</b> A static class exposing
/// <c>public static IReadOnlyList&lt;string&gt; All</c> is this codebase's shape for a
/// closed vocabulary, so a new one is covered the day it is written - which a
/// hand-maintained list would not be, and the hand-maintained schema sentinel is why
/// that is not a hypothetical.
/// </para>
/// </remarks>
internal static class ClosedVocabularies
{
    /// <summary>
    /// Every type in the contract that is a closed vocabulary, found by shape.
    /// </summary>
    /// <remarks>
    /// <b>By shape, because shape answers this correctly.</b> What shape cannot answer is
    /// which fingerprint each belongs to - nothing about a static list of strings says
    /// whether its values reach a fact - so that is declared and this is what verifies
    /// everybody declared.
    /// <para>
    /// Any public static <c>IReadOnlyList&lt;string&gt;</c>, not only one named
    /// <c>All</c>. Requiring the name meant <see cref="Gg.Contracts.Classifications"/> -
    /// whose list is called <c>Ordered</c> - was invisible to a mechanism built to make
    /// closed vocabularies visible, while its values sit inside every change manifest.
    /// </para>
    /// </remarks>
    internal static IReadOnlyList<Type> Discovered() =>
        [.. typeof(FactKinds).Assembly.GetExportedTypes()
            .Where(t => t.IsAbstract && t.IsSealed)
            .Where(t => Lists(t).Count > 0)
            .OrderBy(t => t.FullName, StringComparer.Ordinal)];

    /// <summary>The vocabularies belonging to one fingerprint, and their values.</summary>
    internal static IReadOnlyList<string> Lines(string fingerprint)
    {
        var lines = new List<string>();

        foreach (var type in Discovered())
        {
            if (type.GetCustomAttribute<VocabularyOfAttribute>() is not { } membership
                || !string.Equals(membership.Fingerprint, fingerprint, StringComparison.Ordinal))
            {
                continue;
            }

            foreach (var list in Lists(type))
            {
                if (list.GetValue(null) is not IReadOnlyList<string> values)
                {
                    continue;
                }

                // NORMALISED TO WHAT THE VOCABULARY MEANS. A set is sorted, because
                // reordering how somebody typed a list is not a wire change and must not
                // read as one. A RANKING is not, because there the order IS the content -
                // reordering the classification levels changes what may leave a
                // customer's network, and a sorted hash would not move.
                //
                // A normalisation that discards something meaningful misses in a way that
                // looks like coverage, which is worse than a domain that is too narrow.
                IEnumerable<string> normalised = membership.Ordered
                    ? values
                    : values.OrderBy(v => v, StringComparer.Ordinal);

                lines.Add($"vocabulary {type.Name} "
                        + (membership.Ordered ? "ordered " : "")
                        + string.Join(",", normalised));
            }
        }

        return lines;
    }

    private static IReadOnlyList<PropertyInfo> Lists(Type type) =>
        [.. type.GetProperties(BindingFlags.Public | BindingFlags.Static)
            .Where(p => p.PropertyType == typeof(IReadOnlyList<string>))
            .OrderBy(p => p.Name, StringComparer.Ordinal)];
}
