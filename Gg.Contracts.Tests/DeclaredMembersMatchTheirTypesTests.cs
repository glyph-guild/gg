using System.Reflection;
using System.Text.Json;
using Gg.Contracts.Description;

namespace Gg.Contracts.Tests;

/// <summary>
/// Every member name <see cref="ProtocolSurface.JsonMembers"/> declares is one
/// its type actually serializes, and every member its type serializes is
/// declared.
/// </summary>
/// <remarks>
/// <para>
/// <b>Written because this repository shipped a contract that contradicted
/// itself.</b> Contract 0.272.0 replaced <c>ConfigureCredentialAsk.Secret</c>
/// with an envelope and left the declared row saying <c>["locator",
/// "secret"]</c>. Every test here passed, the surface fingerprint moved as it
/// should, the package published — and the inconsistency was found by the
/// CONTROL PLANE's conformance suite, one repository away, when somebody moved
/// a pin.
/// </para>
/// <para>
/// <b>That is the wrong place to find it.</b> The two repositories cannot
/// reference each other, so the declaration is the only thing holding them
/// together; a declaration that disagrees with its own type is this side's
/// defect, and a consumer discovering it has already pinned a version that
/// cannot be fixed without another.
/// </para>
/// <para>
/// <b>Both directions, because they are different mistakes.</b> A declared name
/// the type does not have is a consumer told to expect something that never
/// arrives. A member the type has and the declaration omits is a member
/// travelling unannounced — which is how a field reaches a wire nobody agreed
/// to put it on.
/// </para>
/// </remarks>
public class DeclaredMembersMatchTheirTypesTests
{
    /// <summary>What a type's members are called on the wire.</summary>
    /// <remarks>
    /// camelCase, which is what the contract serializes with everywhere. Derived
    /// rather than listed, because a second list of names is the thing this test
    /// exists to catch.
    /// </remarks>
    private static IReadOnlyList<string> SerializedNamesOf(Type type) =>
        [.. type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.Name != "EqualityContract")
            .Select(p => JsonNamingPolicy.CamelCase.ConvertName(p.Name))
            .Order()];

    [Test]
    public async Task Every_declared_row_matches_the_type_it_names()
    {
        var wrong = new List<string>();

        foreach (var (type, declared) in ProtocolSurface.JsonMembers)
        {
            var actual = SerializedNamesOf(type);
            var said = declared.Order().ToList();

            if (!said.SequenceEqual(actual, StringComparer.Ordinal))
            {
                wrong.Add(
                    $"{type.Name}: declared [{string.Join(", ", said)}] "
                  + $"but serializes [{string.Join(", ", actual)}]");
            }
        }

        await Assert.That(wrong).IsEmpty()
            .Because("a declaration that disagrees with its own type is a consumer told to "
                   + "expect something that never arrives, and the two repositories cannot "
                   + "reference each other to find out. Found:"
                   + Environment.NewLine + string.Join(Environment.NewLine, wrong));
    }
}
