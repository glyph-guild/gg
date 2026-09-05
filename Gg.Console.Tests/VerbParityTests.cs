using System.Text.RegularExpressions;

namespace Gg.Console.Tests;

/// <summary>
/// Every command line verb, and what the console does about it.
/// </summary>
/// <remarks>
/// <para>
/// <b>A declaration rather than a requirement.</b> Not every verb belongs in a
/// terminal user interface - a daemon does not, and neither does a write that
/// takes a file path - so this ratchet does not demand a pane for each. What it
/// demands is that somebody DECIDED, and wrote the decision down beside the
/// verb.
/// </para>
/// <para>
/// <b>Which is the whole difference between an absence and a gap.</b> Eleven
/// read verbs are absent or unreachable from the console today, and nothing in
/// the repository says whether that was chosen. A reader cannot tell
/// <c>runner serve</c> - correctly absent, it is a daemon - from
/// <c>why</c>, whose entire job is answering the question the console exists to
/// ask, and which is absent because nobody wired it.
/// </para>
/// <para>
/// <b>It fails when a verb is ADDED.</b> That is the point: the next person to
/// add one to the command line has to say, in one line, what the console does
/// about it. The list cannot silently fall behind, which is how this one got to
/// eleven.
/// </para>
/// </remarks>
public class VerbParityTests
{
    /// <summary>Every case of <c>CliAction</c>, read from where they are declared.</summary>
    private static IReadOnlyList<string> Verbs()
    {
        var text = ConsoleSource.Text("Gg.Cli", "CliArgs.cs");

        return [.. Regex.Matches(text, @"public sealed record ([A-Za-z]+)")
            .Select(m => m.Groups[1].Value)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)];
    }

    [Test]
    public async Task Every_verb_has_a_decision_written_beside_it()
    {
        var verbs = Verbs();

        await Assert.That(verbs).IsNotEmpty()
            .Because("no CliAction cases were found, so this ratchet asserted nothing.");

        var undecided = verbs.Where(v => !Decided.ContainsKey(v)).ToList();

        await Assert.That(undecided).IsEmpty()
            .Because("a verb the command line has and the console has not is either a "
                   + "deliberate absence or a gap, and a reader cannot tell which without "
                   + "being told. Add a line saying which. Found: "
                   + string.Join(", ", undecided));
    }

    [Test]
    public async Task The_declaration_names_no_verb_that_has_gone()
    {
        // THE OTHER DIRECTION. A decision about a verb nobody offers any more is
        // a sentence describing a product that does not exist, and it reads as
        // authoritative.
        var verbs = Verbs();
        var ghosts = Decided.Keys.Where(v => !verbs.Contains(v, StringComparer.Ordinal)).ToList();

        await Assert.That(ghosts).IsEmpty()
            .Because("these are not verbs any more. Delete their lines. Found: "
                   + string.Join(", ", ghosts));
    }

    /// <summary>
    /// What the console does about each verb, and why when it does nothing.
    /// </summary>
    /// <remarks>
    /// One line each, and the line is the decision. A bare name would make this
    /// a place to park things, which is what <c>ShellCommands.Handled</c>'s own
    /// comments exist to prevent.
    /// </remarks>
    internal static readonly IReadOnlyDictionary<string, string> Decided =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            // --- reachable from the console today ---
            ["Decide"] = "reachable: approve on a key, reject through $EDITOR.",
            ["Flights"] = "reachable, derived: fetched at boot and projected into the queue "
                        + "rather than listed. Whether a flight LIST belongs beside the queue "
                        + "is open question 2 of this slice.",
            ["Fly"] = "reachable: free text, a pasted uri, a provider#id ticket, and --repo. "
                    + "The uri, ticket and repo halves are slice twenty-nine's, not this "
                    + "slice's - see S28.5-01 and S28.5-02, cut for that reason.",
            ["Gates"] = "reachable at boot: fills the modal. Not refreshed after a decision "
                      + "until step 3.",
            ["Invite"] = "reachable: a key.",
            ["Take"] = "reachable: takeover and hand-back keys.",
            ["Log"] = "fetched at boot for the queue and discarded; the pane is step 2.",
            ["Runners"] = "fetched at boot into a local and never assigned to state; step 2.",

            // --- absent, and each of these is a gap this slice closes ---
            ["Show"] = "UNREACHABLE, and the one a person notices: the Flight pane exists, "
                     + "PaneText renders every line of it, ShowAsync is there to fetch it - "
                     + "and nothing calls ShowAsync, so the pane says loading… for ever. "
                     + "Step 2.",
            ["Plan"] = "absent, and a gap: the checklist is step 4. A dead wrapper today.",
            ["Why"] = "absent, and the sharpest gap: the verb whose whole job is answering "
                    + "why a flight is stopped, missing from the surface whose whole job is "
                    + "showing what needs somebody. Step 4.",
            ["RunnerLabels"] = "absent, and a gap: step 4, with dispositions beside the names.",
            ["EnvelopeShow"] = "absent, and a gap: step 4 adds the read. ConsoleData has no "
                             + "envelope method at all today.",
            ["AirspaceShow"] = "absent, and a gap: a dead wrapper today, resolved in step 6 "
                             + "by wiring or deleting.",
            ["CredentialList"] = "absent, and a gap: the field and the renderer exist and "
                               + "nothing fetches. Step 2.",
            ["CredentialAdd"] = "reachable but partial: scopes are hardcoded to read, and the "
                              + "command line takes a list. Step 5.",
            ["CredentialRemove"] = "absent, and a gap: a dead wrapper with no key. Step 5, "
                                 + "and it is the half of credential management that matters "
                                 + "when one leaks.",
            ["Bundle"] = "absent as a key, and built FROM the state by BundleFrom - so a "
                       + "bundle taken today redacts a model that is mostly empty. Step 2 "
                       + "changes what it contains, which S28.2-06 asserts.",

            // --- absent, and correctly so ---
            ["RunnerUp"] = "a daemon. Correctly not a console verb.",
            ["RunnerServe"] = "a daemon. Correctly not a console verb.",
            ["RunnerMaintain"] = "a daemon. Correctly not a console verb.",
            ["RunnerTools"] = "a tool server spoken over stdio by an agent, never by a person.",
            ["RunnerRead"] = "a tool server spoken over stdio by an agent, never by a person.",
            ["Login"] = "the console needs a session to start, so signing in from inside it "
                      + "is a bootstrap problem rather than a parity gap.",
            ["Logout"] = "the mirror of Login, and ending a session from inside the thing the "
                       + "session is running would leave the screen owned by nobody.",
            // REWORDED, because the declaration was true of the principal and
            // silently false of everything else the verb answers. WhoAmI also
            // carries Notices - the only carrier of a tenant degradation
            // anywhere in the contract - and the stored session carries no such
            // thing, so "answered without the verb" described a field rather
            // than a verb and hid a pane that was drawn and never filled.
            ["WhoAmI"] = "the boot calls it. Principal still comes from the stored session, "
                       + "because attribution is not a read; the NOTICES come from the verb "
                       + "and are drawn above the queue.",
            ["LaunchConsole"] = "this IS the console. A verb for entering the thing you are "
                              + "already in is not a pane.",
            ["PrintVersion"] = "a line in the help modal is the right shape, not a pane.",
            ["Doctor"] = "deliberately outside: it is the verb a person reaches for when the "
                       + "console looks broken, which makes inside the console the worst "
                       + "place to run it from. The help modal says to run it outside.",
            ["Unknown"] = "not a verb - the parse's own answer for something that is not one.",

            // --- absent, and out of scope for this slice, with the reason ---
            ["AirspacePull"] = "writes a working copy from a path argument; the console has no "
                             + "file argument and an $EDITOR round trip is a different "
                             + "interaction. Out of scope, stated.",
            ["AirspaceApply"] = "applies a governance document from a file. Same reason.",
            ["AirspaceDiff"] = "compares a working copy against the estate; needs the working "
                             + "copy the console does not have. Same reason.",
            ["EnvelopeApply"] = "applies a governance document from a file. Same reason - and "
                              + "the READ is in scope, in step 4, which is the distinction.",
            ["EnvelopeValidate"] = "validates a file. Same reason.",
            ["StrategyApply"] = "applies a document from a file. Same reason.",
        };
}
