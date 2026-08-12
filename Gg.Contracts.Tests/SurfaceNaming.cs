using System.Reflection;
using System.Runtime.CompilerServices;

namespace Gg.Contracts.Tests;

/// <summary>
/// How a type is named inside a fingerprint.
/// </summary>
/// <remarks>
/// <para>
/// <b>Not <c>Type.FullName</c>, and that was a real defect.</b> For a
/// constructed generic like <c>IReadOnlyList&lt;LockHash&gt;</c>, FullName
/// renders the argument assembly-qualified - including
/// <c>Version=0.10.0.0</c>. So the assembly version was inside every recorded
/// surface, and bumping the package version made both ledgers report that the
/// surface had changed when nothing had.
/// </para>
/// <para>
/// That is the worst failure a check like this can have. It fires on exactly
/// the action a genuine change also requires, so whoever sees it learns to
/// re-record the hash without reading the diff - and the check keeps passing
/// forever while meaning nothing.
/// </para>
/// <para>
/// This renders a name that depends on the CONTRACT and nothing else: the
/// namespace, the type, and its arguments, recursively. No version, no
/// culture, no public key token.
/// </para>
/// </remarks>
internal static class SurfaceNaming
{
    /// <summary>A stable name for a type, with nothing about its assembly in it.</summary>
    internal static string StableTypeName(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);

        if (type.IsArray)
        {
            return StableTypeName(type.GetElementType()!) + "[]";
        }

        if (!type.IsGenericType)
        {
            // A non-generic type's FullName carries no assembly identity, so
            // it is already stable. Namespace-qualified, because two contract
            // types could share a short name.
            return type.FullName ?? type.Name;
        }

        // The arity suffix (`1) is an implementation detail of the CLR name
        // and says nothing a consumer could break on.
        var name = type.GetGenericTypeDefinition().FullName ?? type.Name;
        var tick = name.IndexOf('`', StringComparison.Ordinal);
        if (tick >= 0)
        {
            name = name[..tick];
        }

        return $"{name}<{string.Join(',', type.GetGenericArguments().Select(StableTypeName))}>";
    }

    /// <summary>Every property of a type, in a fixed order, named stably.</summary>
    /// <remarks>
    /// Sorted by name. JSON is not positional, so the order members happen to
    /// be declared in is not a wire change and must not read as one.
    /// </remarks>
    internal static IEnumerable<string> PropertyLines(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);

        return type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .OrderBy(p => p.Name, StringComparer.Ordinal)
            .Select(p =>
            {
                var required = p.GetCustomAttributes().Any(a => a is RequiredMemberAttribute)
                    ? "required" : "optional";
                return $"  {p.Name} {StableTypeName(p.PropertyType)} {required}";
            });
    }
}
