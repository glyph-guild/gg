using System.Reflection;
using Gg.Client;

namespace Gg.Console.Tests;

/// <summary>
/// A verb result the projection cannot turn into state.
/// </summary>
/// <remarks>
/// <para>
/// <b>The read half's answer to <c>ShellHandledTests</c>.</b> The console has
/// one parity ratchet and it is a good one: every command bound to a key needs
/// an arm in the loop, and a test holds it - so the WRITE half cannot drift
/// silently. Nothing held the read half, and it drained: twenty-five verb result
/// kinds, five arms in <c>ConsoleProjection.Apply</c>, and a projection with no
/// caller outside its own tests.
/// </para>
/// <para>
/// <b>An arm, not a field.</b> A result with no arm cannot reach the model at
/// all, whatever <c>ConsoleData</c> offers and whatever <c>PaneText</c> is ready
/// to render - which is why a wrapper, a renderer and a pane can all exist and
/// show nothing.
/// </para>
/// <para>
/// <b>Read from the source, because a switch has no arms at runtime.</b> The
/// types are found by reflection - that is what exists - and whether each is
/// handled is a question about code.
/// </para>
/// </remarks>
public class ProjectionParityTests
{
    private static IReadOnlyList<string> Kinds() =>
        [.. typeof(VerbResult)
            .GetNestedTypes(BindingFlags.Public)
            .Where(t => t.IsSubclassOf(typeof(VerbResult)))
            .Select(t => t.Name)
            .Order(StringComparer.Ordinal)];

    [Test]
    public async Task Every_verb_result_has_an_arm_or_a_reason()
    {
        var kinds = Kinds();

        await Assert.That(kinds).IsNotEmpty()
            .Because("no VerbResult kinds were found, so this ratchet asserted nothing.");

        var apply = ConsoleSource.Text("Gg.Console", "ConsoleData.cs");

        var unhandled = kinds
            .Where(k => !apply.Contains($"VerbResult.{k} ", StringComparison.Ordinal)
                     && !apply.Contains($"VerbResult.{k}\n", StringComparison.Ordinal)
                     && !apply.Contains($"VerbResult.{k} =>", StringComparison.Ordinal))
            .Where(k => !Exempt.ContainsKey(k))
            .ToList();

        await Assert.That(unhandled).IsEmpty()
            .Because("a result with no arm cannot reach the model, so every wrapper that "
                   + "returns one and every renderer that would draw it are unreachable "
                   + "together. Add an arm, or put it on the list with a reason. Found: "
                   + string.Join(", ", unhandled));
    }

    [Test]
    public async Task The_exemption_list_names_nothing_that_is_handled()
    {
        var apply = ConsoleSource.Text("Gg.Console", "ConsoleData.cs");

        var stale = Exempt.Keys
            .Where(k => apply.Contains($"VerbResult.{k} ", StringComparison.Ordinal))
            .ToList();

        await Assert.That(stale).IsEmpty()
            .Because("these have arms now and their exemptions describe a past. Delete the "
                   + "entries. Found: " + string.Join(", ", stale));
    }

    /// <summary>What the projection cannot yet turn into state, and why.</summary>
    internal static readonly IReadOnlyDictionary<string, string> Exempt =
        new Dictionary<string, string>(StringComparer.Ordinal);
}
