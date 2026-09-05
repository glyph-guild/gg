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
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            // --- arms this slice adds, each with the step that adds it ---
            ["Gates"] = "step 3: answering a gate has to refetch them, and today the boot's "
                      + "copy is the only one there will ever be.",
            // A DECISION NOW, NOT A GAP - and the difference is worth the words.
            // The console CANNOT receive this kind: step 6 deleted
            // ConsoleData.RunnerLabelsAsync, because it and RunnersAsync both
            // call ListRunnersAsync and `gg runners` and `gg runner labels` are
            // one answer rendered two ways. The labels reach the pane, with
            // their dispositions, out of the RunnerList that VerbResult.Runners
            // already puts in the model. An arm here would be an arm for a
            // result nothing in this project can hand it.
            ["RunnerLabels"] = "one request under two names. The console receives Runners, "
                             + "renders its labels, and no longer offers the second wrapper.",
            ["CredentialAdded"] = "step 5: the console can register one and cannot see the "
                                + "result reach the model.",
            ["CredentialRemoved"] = "step 5, the mirror of it.",
            ["Decided"] = "step 3: a decision's own result, so the queue and the gates "
                        + "reflect it without a second boot.",
            ["Taken"] = "step 2: the seed is fetched at boot and assigned directly; an arm "
                      + "makes it a projection like everything else.",
            ["Invited"] = "step 6: the verb is reachable and its RESULT is not projected, so "
                        + "the console shows a sentence and not a state. Wired or the "
                        + "sentence is declared to be the whole of it.",
            ["Launched"] = "step 6, on the same footing as Invited.",
            ["AirspaceRepositories"] = "slice twenty-nine's browse pane, not this slice's.",
            ["AirspaceTopology"] = "the registered repositories as a tree. Resolved in step "
                                 + "6 with AirspaceAsync, which returns it: wired to a pane "
                                 + "or deleted together, because a projection arm for a read "
                                 + "nothing calls is half a feature twice.",

            // --- arms that would be wrong, and the reason is the same one ---
            // Each of these is the RESULT OF A WRITE THAT TAKES A FILE, which
            // this slice puts out of scope: the console has no file argument.
            // A projection arm for one would be a model field for a thing the
            // console cannot do.
            ["EnvelopeApplied"] = "a write from a file. Out of scope, stated in the slice.",
            ["EnvelopeValidated"] = "a validation of a file. Same.",
            ["AirspacePulled"] = "writes a working copy. Same.",
            ["AirspaceApplied"] = "applies a document from a file. Same.",
            ["AirspaceDiffed"] = "compares against a working copy the console has not. Same.",

            // --- and one that is correct as it stands ---
            ["Bundle"] = "built FROM the state rather than into it, so there is nothing to "
                       + "project. S28.2-06 asserts what a bundle contains once the model is "
                       + "no longer mostly empty; an arm here would be backwards.",
            ["Diagnosis"] = "not a result kind the projection receives - it is the field every "
                          + "other arm clears, which is the shape a failure takes here.",
        };
}
