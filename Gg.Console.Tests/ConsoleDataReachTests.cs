using System.Reflection;

namespace Gg.Console.Tests;

/// <summary>
/// A read the console cannot reach is a pane somebody thinks exists.
/// </summary>
/// <remarks>
/// <para>
/// <b>Seven wrappers had no caller when this slice was planned, and there are
/// nine.</b> They were built for panes that were never written - and could not
/// have been, because <c>ConsoleProjection.Apply</c> has no arm for what they
/// return and <c>AppState</c> has no field to put it in. The next reader sees a
/// method named <c>WhyAsync</c> and concludes the console can answer <i>why is
/// this stopped</i>.
/// </para>
/// <para>
/// <b>The exemption list is the measurement.</b> Every entry carries the reason
/// it is still here and what removes it, on <c>ShellCommands.Handled</c>'s model
/// - a list of bare names becomes a place to park things. Step 6 of this slice
/// is not done while the list is not empty.
/// </para>
/// <para>
/// <b>A caller in <c>Gg.Console</c>, not in a test.</b> Every one of these is
/// exercised by <c>ConsoleDataTests</c>, which is exactly the shape that passes
/// for ever while the product shows nothing.
/// </para>
/// </remarks>
public class ConsoleDataReachTests
{
    [Test]
    public async Task Every_read_the_console_offers_is_one_the_console_makes()
    {
        var declared = typeof(ConsoleData)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => !m.IsSpecialName)
            .Select(m => m.Name)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();

        await Assert.That(declared).IsNotEmpty()
            .Because("ConsoleData has no public methods, so this ratchet asserted nothing.");

        var callers = ConsoleSource.In("Gg.Console")
            .Where(f => !f.EndsWith("ConsoleData.cs", StringComparison.Ordinal))
            .Select(File.ReadAllText)
            .ToList();

        var unreachable = declared
            .Where(name => !callers.Any(t => t.Contains($".{name}(", StringComparison.Ordinal)))
            .Where(name => !Exempt.ContainsKey(name))
            .ToList();

        await Assert.That(unreachable).IsEmpty()
            .Because("a read nothing calls is a pane that does not exist, and the method's "
                   + "name is what makes the next reader believe it does. Wire it, delete it, "
                   + "or put it on the list with a reason. Found: "
                   + string.Join(", ", unreachable));
    }

    [Test]
    public async Task The_exemption_list_names_nothing_that_is_already_wired()
    {
        // A LIST THAT OUTLIVES ITS REASON IS A LIST NOBODY READS. An entry for a
        // method somebody has since wired reads as though the gap were still
        // open, and the next person to look believes the console cannot do
        // something it can.
        var callers = ConsoleSource.In("Gg.Console")
            .Where(f => !f.EndsWith("ConsoleData.cs", StringComparison.Ordinal))
            .Select(File.ReadAllText)
            .ToList();

        var stale = Exempt.Keys
            .Where(name => callers.Any(t => t.Contains($".{name}(", StringComparison.Ordinal)))
            .ToList();

        await Assert.That(stale).IsEmpty()
            .Because("these have callers now, so their exemptions are describing a past. "
                   + "Delete the entries. Found: " + string.Join(", ", stale));
    }

    /// <summary>What is not reached yet, why, and what removes the entry.</summary>
    internal static readonly IReadOnlyDictionary<string, string> Exempt =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            // THREE ENTRIES LEFT HERE AND ALL THREE WERE DELETED, which was not
            // what any of them predicted. Each said a pane would arrive and call
            // it; each turned out to be a SECOND WAY TO ASK a question the model
            // already holds the answer to.
            //
            //   ShowAsync         - one flight's summary. The boot fetches every
            //                       flight's in one list to derive the queue and
            //                       keeps it, so the detail under the cursor is a
            //                       lookup. That is what makes an arrow key free.
            //   RunnerLabelsAsync - the same ListRunnersAsync call RunnersAsync
            //                       makes. `gg runners` and `gg runner labels`
            //                       are one request rendered two ways, and the
            //                       console renders the labels from what boot
            //                       already fetched.
            //   AirspaceAsync     - the topology. Nobody asked for it, and
            //                       `wired to a pane or deleted` has no third
            //                       option.
            //
            // A wrapper whose data is already in the model is not a missing
            // pane. It is a way for a console to make a request it has already
            // made, and the name is what makes the next reader believe otherwise.

            // NOT THIS SLICE'S, and here by agreement rather than by oversight.
            // Slice twenty-nine landed both deliberately ahead of the pane that
            // uses them, because that pane needs this slice's read plane and
            // these reads did not. Its author asked for this ratchet knowing it
            // would fire on them, and is fixing what fires: these two leave the
            // list when its step 4 lands, not when this slice's does.
            // REWORDED at slice twenty-nine's author's request: its step 4
            // landed without this one, so "its browse pane is its step 4" had
            // stopped being true. The tracker browser and "what can I fly
            // against" are different panes; this read belongs to the second,
            // which nobody has built.
            ["RepositoriesAsync"] = "slice twenty-nine, tier B: what this tenant can fly "
                                  + "against - a different pane from the tracker browser its "
                                  + "step 4 shipped, and one nobody has built yet.",
        };
}
