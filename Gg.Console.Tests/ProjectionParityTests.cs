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

            // THE CHECKLIST TAB WAS WITHDRAWN AND ITS ARM WENT WITH IT. Plan
            // is still a verb - `gg plan` renders it - and the console still
            // ASKS for one: ConsoleHandFlight reads a checklist to refuse a
            // hand-flown flight whose labels the fleet cannot serve. What it no
            // longer does is put one in the model, because nothing draws it.
            //
            // The pane was withdrawn for answering two questions at once. A
            // checklist's requirements are PINNED to what the flight compiled
            // at creation; its satisfiers are recomputed LIVE against today's
            // fleet. Before a lease those agree; after one the satisfier column
            // is a reading of the estate rather than a fact about the flight,
            // and nothing gated the tab, so it said so on flights that had long
            // since flown.
            //
            // REMOVE THIS ENTRY when a pane draws a checklist again - and if
            // one does, it should say which of the two questions it answers.
            ["Plan"] = "not projected: the checklist tab was withdrawn. The verb and the "
                     + "contract stay, and the hand-flight refusal still reads one directly "
                     + "rather than through the model.",

            // THE LOCAL ALLOWANCE READING, and it must never GET an arm. This
            // result is one machine's own transcripts; what a console shows is
            // the fleet's allowances, over the read surface, including
            // machines this one cannot see. Projecting this into the model
            // would put a person's own laptop on a pane labelled with the
            // fleet - which is the two-cursor defect this console has already
            // met once, where a pane and its title answered "which flight"
            // from different places.
            ["Allowance"] = "not projected, and not pending an arm: this is one machine's "
                          + "local reading and the console's pane is the fleet's, over the "
                          + "read surface. An arm here would draw a laptop and label it a "
                          + "fleet.",

            // THE CONFIGURATION VERBS, and the console's answer to them is not
            // a projection. What a person reads in the console is the
            // Environment page, which is built from ConsoleEnvironment.Read -
            // the same resolution these verbs render - rather than from a
            // VerbResult travelling back. So there is nothing to project, and
            // an arm would be a second path to the same values.
            //
            // Editing is step 5: a key on that page hands the file to $EDITOR
            // between sessions, because nothing in this console is written by
            // typing into a widget.
            ["ConfigShown"] = "not projected: the console reads the same resolution directly "
                            + "for its Environment page, so a projection would be a second "
                            + "path to one answer.",
            ["ConfigValidated"] = "not projected: validating is a command-line answer. The "
                                + "console's equivalent is step 5, where a bad edit is "
                                + "refused before it is written.",

            // THIS ENTRY USED TO SAY THE CONSOLE COULD NOT REACH AN OFFER AT
            // ALL, "because a UI session may read a local file and nothing
            // else". That rule is about a SESSION. The console fetches at boot
            // and on refresh, between sessions, which is where this now
            // happens - so the console does see an offer, and the reason it is
            // still not PROJECTED is the ordinary one the two entries above
            // give: it arrives as its own summary rather than as a VerbResult.
            ["ConfigOffered"] = "not projected: the root reads it at boot and hands the "
                              + "Environment page a summary, so a projection would be a "
                              + "second path to one answer - ConfigShown's reason exactly. "
                              + "A summary rather than the document, because this model is "
                              + "dumped and bundled.",

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
            ["RunnerRepinned"] = "no arm because no console verb, and nothing to project "
                               + "either: forgetting a pin changes a file this machine "
                               + "keeps, not anything a pane draws. The reason is at "
                               + "VerbParityTests' entry for RunnerRepin.",
            ["RunnerRetired"] = "no arm because no console verb: retiring is one-way "
                              + "and the modal has no confirmation to hang it on. The "
                              + "reason is written out at ConsoleDataTests' exemption "
                              + "for RetireRunnerAsync, and both go together or "
                              + "neither does.",
            ["Decided"] = "step 3: a decision's own result, so the queue and the gates "
                        + "reflect it without a second boot.",
            ["Taken"] = "step 2: the seed is fetched at boot and assigned directly; an arm "
                      + "makes it a projection like everything else.",
            ["Invited"] = "step 6: the verb is reachable and its RESULT is not projected, so "
                        + "the console shows a sentence and not a state. Wired or the "
                        + "sentence is declared to be the whole of it.",
            ["Launched"] = "step 6, on the same footing as Invited.",
            // AirspaceTopology WAS EXEMPT HERE and is projected now. The entry
            // said it was "read by ConsoleEstate and unwrapped there", which was
            // true and made the names reachable only by pressing `v` - so a
            // question asked at fly time could offer nothing on a console that
            // had not visited the Envelope tab. It has an arm, so it has no
            // entry: this list names nothing that is handled.

            // --- arms that would be wrong, and the reason is the same one ---
            // Each of these is the RESULT OF A WRITE THAT TAKES A FILE, which
            // this slice puts out of scope: the console has no file argument.
            // A projection arm for one would be a model field for a thing the
            // console cannot do.
            ["EnvelopeApplied"] = "a write from a file. Out of scope, stated in the slice.",
            ["EnvelopeValidated"] = "a validation of a file. Same.",
            ["AirspacePulled"] = "writes a working copy. Same.",
            ["AirspaceApplied"] = "applies a document from a file. Same.",
            // WAS "compares against a working copy the console has not", which
            // stopped being true the moment the console got one. It is read now,
            // by ConsoleEstate, and unwrapped there rather than projected: a
            // projection arm maps one result onto one field, and the estate
            // record joins two results plus two facts about the machine, so a
            // pair of arms would leave it half-built between them.
            ["AirspaceDiffed"] = "read by ConsoleEstate and unwrapped there, because the "
                               + "estate record joins two reads and an arm fills one field.",
            ["NameDeclared"] = "the answer to declaring a topology name. Not a working-copy "
                             + "result like the three above - it needs no tree - so it is "
                             + "absent only until the estate pane it belongs on exists.",

            ["NameRetired"] = "the answer to retiring a topology name, and it is never a "
                            + "field on the model: it always rides a gate, so there is "
                            + "nothing to project onto a pane - what it produces is a "
                            + "FLIGHT, which the queue already shows. ConsoleRetire renders "
                            + "it into the outcome modal, per name, saying that nothing is "
                            + "gone until the gate opens.",

            ["AirspaceDocuments"] = "every applied document, whole, read in one request "
                                  + "and unwrapped by ConsoleEstate rather than projected "
                                  + "here - AirspaceDiffed's reason: the estate record "
                                  + "joins two reads and a walk of the disk, and an arm "
                                  + "fills one field. It is what the airspace tab's "
                                  + "right-hand pane draws for whatever row the cursor is "
                                  + "on, without asking again.",

            ["RulesInForce"] = "the floor composed with one work kind - what actually "
                             + "governs a flight of that kind. A command-line answer today "
                             + "(gg envelope show <work-kind>): the console shows the floor "
                             + "and SAYS it is the floor, naming both routes to the "
                             + "composed view rather than titling a partial answer as a "
                             + "whole one. Composing it in the modal is the next increment "
                             + "and wants a read of its own.",

            ["NamedEnvelopeShown"] = "one document read back by name, unwrapped by "
                                   + "ConsoleDocument rather than projected here - "
                                   + "AirspaceDiffed's reason: what lands on the model is "
                                   + "one field chosen from the answer, and every failure "
                                   + "is a sentence rather than state. It is what `v' over "
                                   + "a document row shows.",

            ["StrategyShown"] = "a strategy read back by name. It reaches the console "
                              + "through the same read and is SAID rather than rendered: a "
                              + "strategy is not an envelope, and a modal that drew one as "
                              + "the other would be inventing a shape. The command line "
                              + "renders it in full.",

            // --- and one that is correct as it stands ---
            ["Bundle"] = "built FROM the state rather than into it, so there is nothing to "
                       + "project. S28.2-06 asserts what a bundle contains once the model is "
                       + "no longer mostly empty; an arm here would be backwards.",
            ["Diagnosis"] = "not a result kind the projection receives - it is the field every "
                          + "other arm clears, which is the shape a failure takes here.",
        };
}
