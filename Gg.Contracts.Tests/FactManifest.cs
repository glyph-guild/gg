using System.Reflection;
using Gg.Contracts.Description;

namespace Gg.Contracts.Tests;

/// <summary>
/// The rule that a fact type is registered everywhere it has to be.
/// </summary>
/// <remarks>
/// <para>
/// A fact crosses the boundary only if four things are true at once: it carries
/// a pinned id, its kind is named in <see cref="FactKinds"/>, its JSON members
/// are declared in <see cref="ProtocolSurface"/>, and
/// <see cref="FactEnvelope"/> has a slot for it to arrive in. Three of the four
/// produces a fact that serializes to a digest and nothing, or one the other
/// side cannot name.
/// </para>
/// <para>
/// A rule over a SET of types rather than a scan of one assembly, so a test can
/// point it at the type somebody would add wrongly without adding one. A scan
/// of the wrong assembly returns no offenders and looks diligent.
/// </para>
/// <para>
/// It lives in the TEST assembly rather than in the contract, and that is a
/// trimming decision rather than a design one: the contract is AOT-compatible
/// and this walks types by reflection. The rule it enforces is about the
/// contract's own build, which is where it runs.
/// </para>
/// </remarks>
internal static class FactManifest
{
    /// <summary>Every type in an assembly that claims to be a fact payload.</summary>
    internal static IReadOnlyList<Type> FactTypesIn(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        return [.. assembly.GetExportedTypes()
            .Where(t => t.GetCustomAttribute<FactKindAttribute>() is not null)
            .OrderBy(t => t.Name, StringComparer.Ordinal)];
    }

    /// <summary>
    /// The nullable payload properties on the envelope.
    /// </summary>
    /// <remarks>
    /// Identified by shape - a nullable reference to a contract type - rather
    /// than by a list of names, so a slot added for a new fact is found without
    /// anybody remembering to add it here as well.
    /// </remarks>
    internal static IReadOnlyList<PropertyInfo> PayloadSlots(Type envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        return [.. envelope.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.PropertyType.IsClass
                     && p.PropertyType != typeof(string)
                     && p.PropertyType.Namespace == typeof(FactEnvelope).Namespace)];
    }

    /// <summary>
    /// Which of these fact types is not registered everywhere it must be.
    /// </summary>
    /// <returns>
    /// One sentence per offence, naming the type and which registration is
    /// missing. A list of type names alone would send somebody hunting through
    /// four files to find out which.
    /// </returns>
    internal static IReadOnlyList<string> Unregistered(IEnumerable<Type> factTypes)
    {
        ArgumentNullException.ThrowIfNull(factTypes);

        var offenders = new List<string>();
        var slots = PayloadSlots(typeof(FactEnvelope)).Select(p => p.PropertyType).ToHashSet();

        foreach (var type in factTypes)
        {
            var kind = type.GetCustomAttribute<FactKindAttribute>()?.Kind;

            if (kind is null || !FactKinds.All.Contains(kind))
            {
                offenders.Add($"{type.Name} claims kind '{kind}', which FactKinds does not name.");
            }

            if (type.GetCustomAttribute<PinnedIdAttribute>() is null)
            {
                offenders.Add($"{type.Name} has no pinned id, so a rename would change its wire identity.");
            }

            if (!Vocabulary.Types.Contains(type))
            {
                offenders.Add($"{type.Name} is not in the vocabulary manifest.");
            }

            if (!ProtocolSurface.JsonMembers.ContainsKey(type))
            {
                offenders.Add($"{type.Name} has no declared JSON members, so the two sides cannot "
                            + "agree on how it is spelled.");
            }

            if (!slots.Contains(type))
            {
                offenders.Add($"{type.Name} has no slot on the envelope to arrive in, so it would "
                            + "serialize to a digest and nothing.");
            }
        }

        return offenders;
    }
}
