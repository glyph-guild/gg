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
    /// <summary>Every vocabulary and its values, in a stable order.</summary>
    internal static IReadOnlyList<string> Lines()
    {
        var lines = new List<string>();

        foreach (var type in typeof(FactKinds).Assembly.GetExportedTypes()
                     .Where(t => t.IsAbstract && t.IsSealed)
                     .OrderBy(t => t.FullName, StringComparer.Ordinal))
        {
            if (type.GetProperty("All", BindingFlags.Public | BindingFlags.Static) is not
                { } all || all.PropertyType != typeof(IReadOnlyList<string>))
            {
                continue;
            }

            if (all.GetValue(null) is not IReadOnlyList<string> values)
            {
                continue;
            }

            // ORDERED, because the fingerprint is of what the vocabulary IS rather than
            // of how somebody happened to type it. Reordering the values is not a wire
            // change and must not read as one.
            lines.Add($"vocabulary {type.Name} "
                    + string.Join(",", values.OrderBy(v => v, StringComparer.Ordinal)));
        }

        return lines;
    }
}
