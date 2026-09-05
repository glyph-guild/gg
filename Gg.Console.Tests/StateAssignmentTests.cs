using System.Text.RegularExpressions;

namespace Gg.Console.Tests;

/// <summary>
/// A field the panes render is a field production assigns.
/// </summary>
/// <remarks>
/// <para>
/// <b>Rule 1 of this slice, and the rule the whole thing exists to
/// establish.</b> <c>AppState</c> grew fields, <c>PaneText</c> grew renderers to
/// draw them, and nine of those fields are at their default in the running
/// product for ever - so the panes are correct, the tests that build the state
/// by hand pass, and a person sees <c>loading…</c>.
/// </para>
/// <para>
/// <b>Assigned in PRODUCTION, not in a test.</b> Every one of these is populated
/// by <c>StateGenerator</c> and friends, which is exactly the shape that passes
/// while the product shows nothing. The scan reads <c>Gg.Console</c> and
/// <c>Gg.Cli</c> and no test project.
/// </para>
/// <para>
/// <b>Its own declaration does not count.</b> <c>AppState.cs</c> gives every
/// field an initialiser; that is what makes them default rather than what makes
/// them filled, so the file is excluded from the assignment scan.
/// </para>
/// </remarks>
public class StateAssignmentTests
{
    /// <summary>
    /// Fields PaneText reads that something could assign.
    /// </summary>
    /// <remarks>
    /// <b>A derived property is excluded, and not as a convenience.</b>
    /// <c>Selected</c> is <c>Queue[SelectedRow]</c> and <c>SelectedGate</c> is a
    /// lookup - neither has a setter, so "does production assign this" has one
    /// answer for ever and an exemption for it could never be deleted. A list
    /// step 6 must empty cannot contain an entry that can never leave it.
    /// Reflection is what tells them apart.
    /// </remarks>
    private static IReadOnlyList<string> Rendered()
    {
        var text = ConsoleSource.Text("Gg.Console", Path.Combine("State", "PaneText.cs"));

        var settable = typeof(AppState)
            .GetProperties()
            .Where(p => p.CanWrite)
            .Select(p => p.Name)
            .ToHashSet(StringComparer.Ordinal);

        return [.. Regex.Matches(text, @"state\.([A-Z][A-Za-z0-9]*)")
            .Select(m => m.Groups[1].Value)
            .Where(settable.Contains)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)];
    }

    /// <summary>
    /// Whether the one path from a verb result into the model is walked by
    /// anything.
    /// </summary>
    private static bool ApplyIsReached() =>
        ConsoleSource.In("Gg.Console", "Gg.Cli")
            .Where(f => !f.EndsWith("ConsoleData.cs", StringComparison.Ordinal))
            .Select(File.ReadAllText)
            .Any(t => t.Contains("ConsoleProjection.Apply(", StringComparison.Ordinal)
                   || t.Contains(".Apply(", StringComparison.Ordinal));

    [Test]
    public async Task Every_field_a_pane_renders_is_one_production_fills()
    {
        var rendered = Rendered();

        await Assert.That(rendered).IsNotEmpty()
            .Because("PaneText reads nothing off the state, so this ratchet asserted nothing.");

        var production = Assigning();

        var never = rendered
            .Where(field => !production.Any(t =>
                Regex.IsMatch(t, $@"\b{Regex.Escape(field)}\s*=\s*[^=]")))
            .Where(field => !Exempt.ContainsKey(field))
            .ToList();

        await Assert.That(never).IsEmpty()
            .Because("a field nothing assigns renders its default for ever, and the renderer "
                   + "above it is unreachable code that looks like a feature. Assign it, stop "
                   + "rendering it, or put it on the list with a reason. Found: "
                   + string.Join(", ", never));
    }

    /// <summary>
    /// The source that can actually fill a field, which excludes a projection
    /// nothing calls.
    /// </summary>
    private static IReadOnlyList<string> Assigning()
    {
        var files = ConsoleSource.In("Gg.Console", "Gg.Cli")
            .Where(f => !f.EndsWith("AppState.cs", StringComparison.Ordinal));

        // ConsoleData.cs holds ConsoleProjection.Apply. While nothing calls it,
        // what it assigns is not assigned.
        if (!ApplyIsReached())
        {
            files = files.Where(f => !f.EndsWith("ConsoleData.cs", StringComparison.Ordinal));
        }

        return [.. files.Select(File.ReadAllText)];
    }

    [Test]
    public async Task The_exemption_list_names_nothing_that_is_filled()
    {
        var production = Assigning();

        var stale = Exempt.Keys
            .Where(field => production.Any(t =>
                Regex.IsMatch(t, $@"\b{Regex.Escape(field)}\s*=\s*[^=]")))
            .ToList();

        await Assert.That(stale).IsEmpty()
            .Because("these are assigned now and their exemptions describe a past. Delete the "
                   + "entries. Found: " + string.Join(", ", stale));
    }

    /// <summary>What renders its default in the running product, and why.</summary>
    internal static readonly IReadOnlyDictionary<string, string> Exempt =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            // MOVED FROM STEP 2 TO STEP 6, with the reason it could not be step
            // 2's: NO VERB PRODUCES A GateEvidencePayload. ConsoleData offers
            // eighteen reads and none returns one, and `why` answers a
            // FlightAttribution instead - so this is not a fetch somebody
            // forgot to call, it is a field with no possible source. Wired to a
            // read that does not exist yet, or deleted with the renderer above
            // it.
            ["Payload"] = "step 6: no verb produces a GateEvidencePayload at all, so this "
                        + "cannot be filled by calling something - it needs a read to exist "
                        + "first, or the field and its renderer both go.",
        };
}
