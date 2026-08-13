using System.Text.RegularExpressions;
using Gg.Client;
using Gg.Contracts;

namespace Gg.Client.Tests;

/// <summary>
/// `gg decide` posts a claim. It does not decide anything.
/// </summary>
/// <remarks>
/// <para>
/// <b>Article IX in its softest clothing, which is the dangerous kind.</b> Moving an
/// obligation check into the runner for latency is an obvious violation anybody would
/// catch in review. Marking the obligation satisfied locally when the user presses a
/// key feels like ordinary client state - and it produces a demo that works, a record
/// that disagrees with it, and no error anywhere.
/// </para>
/// <para>
/// So: gg posts a decision, the control plane records it, the Engine re-evaluates
/// admission, and the client renders whatever came back. Cites ADR-0011 - a decision
/// is an input to evaluation, never a substitute for admission.
/// </para>
/// <para>
/// <b>This supersedes a step-3 assertion of mine.</b> `GatesVerbTests` asserted that
/// no decision path existed anywhere in the client, which was true when a gate could
/// only be listed. The claim that survives is narrower and still load-bearing: exactly
/// one path posts a decision, and it computes nothing.
/// </para>
/// </remarks>
public class DecideVerbTests
{
    private static DecisionRecorded Approved(DestinationAdmission? admission = null) => new()
    {
        FlightNumber = "GG-42",
        ObligationId = "reversibility-plan",
        Outcome = DecisionOutcomes.Approved,
        DecidedBy = "someone@example.test",
        DecidedAt = new DateTimeOffset(2026, 8, 13, 14, 0, 0, TimeSpan.Zero),
        Admission = admission,
    };

    // ---- the client computes nothing ----

    [Test]
    public async Task Nothing_in_the_client_marks_an_obligation_satisfied()
    {
        // STRUCTURAL. The words that would do it, anywhere in the client: an obligation
        // set to satisfied, a gate closed, an admission constructed. A client holding
        // any of those is one keystroke from a demo that lies.
        var deciding = new Regex(
            @"Attachments\.Attached\s*=|Outcome\s*=\s*""satisfied""|ObligationOutcomes"
          + @"|new DestinationAdmission|resolved_at|CloseGate|MarkSatisfied",
            RegexOptions.Compiled);

        var offenders = Sources()
            .Where(f => deciding.IsMatch(Code(f)))
            .Select(f => Path.GetFileName(f)!)
            .Order(StringComparer.Ordinal)
            .ToList();

        await Assert.That(offenders).IsEmpty()
            .Because("the obligation's state and the landing decision both arrive from the "
                   + "control plane. Found: " + string.Join(", ", offenders));

        // The scan can see one, so the emptiness above means something.
        await Assert.That(deciding.IsMatch("var a = new DestinationAdmission { };")).IsTrue();
    }

    [Test]
    public async Task Exactly_one_path_posts_a_decision()
    {
        // The liveness twin for the assertion above, and the replacement for step 3's
        // "no decision path exists": one transport method, one verb that calls it, and
        // two callers of that verb - the CLI dispatch and the console's data path. A
        // second poster would be a second place to get the scoping wrong, which is what
        // this counts rather than forbidding callers.
        var posting = new Regex(@"DecideAsync", RegexOptions.Compiled);

        var files = Sources()
            .Where(f => posting.IsMatch(Code(f)))
            .Select(f => Path.GetFileName(f)!)
            .Order(StringComparer.Ordinal)
            .ToList();

        await Assert.That(files).IsEquivalentTo((string[])
            [
                // The console's data path, wired to no key. Present for parity so the
                // modal step 6 adds cannot introduce a SECOND way to decide - which is
                // the failure this count exists to catch.
                "ConsoleData.cs",

                // The transport.
                "ControlPlaneClient.cs",

                // The verb, which fetches the gate and posts what it says.
                "FlightCommands.cs",

                // The dispatch.
                "Program.cs",
            ])
            .Because("found: " + string.Join(", ", files));
    }

    [Test]
    public async Task The_decision_carries_the_hash_it_was_made_against_rather_than_computing_one()
    {
        // The scoping, and where it comes from. A hash the client computed would be a
        // hash of what the client happens to hold; the gate's hash is what the person
        // was shown.
        var verb = Code(Sources().Single(f => Path.GetFileName(f) == "FlightCommands.cs"));

        await Assert.That(verb).Contains("ManifestHash = gate.ManifestHash")
            .Because("the gate says which fact set this decision is about.");
        await Assert.That(verb).DoesNotContain("SHA256")
            .Because("and the client hashes nothing, because a second computation of the same "
                   + "answer is a second answer.");
    }

    // ---- reject is absent, not stubbed ----

    [Test]
    public async Task An_outcome_outside_the_closed_list_is_refused_by_name()
    {
        // SUPERSEDED IN 4b, and narrowed rather than deleted. This asserted that
        // `rejected` was absent, which was true while reject was not built. What survives
        // is the property that still matters: the list is closed, and a word outside it
        // is refused by name rather than recorded as though it had been acted on.
        await Assert.That(DecisionOutcomes.All)
            .IsEquivalentTo(new[] { DecisionOutcomes.Approved, DecisionOutcomes.Rejected });
        await Assert.That(DecisionOutcomes.All.Contains("amended")).IsFalse()
            .Because("amend has nothing to mean at cardinality two, and a verb that accepted it "
                   + "would record a decision nobody acted on.");

        var verb = Code(Sources().Single(f => Path.GetFileName(f) == "FlightCommands.cs"));

        await Assert.That(verb).Contains("is not a decision this version of gg can record")
            .Because("an outcome outside the closed list is refused by name.");
    }

    // ---- observations, interpreted by nothing ----

    [Test]
    public async Task The_observations_are_facts_about_the_process_and_not_a_conclusion()
    {
        // NO `attended` FIELD, and that is the assertion. Connection is a transport fact
        // and attendance is a decision record; gg cannot tell them apart, because a
        // person can pipe input and a script can allocate a terminal.
        var members = typeof(DecisionObservations).GetProperties().Select(p => p.Name).ToList();

        await Assert.That(members).IsEquivalentTo(new[]
        {
            nameof(DecisionObservations.Interactive),
            nameof(DecisionObservations.EvidenceRendered),
            nameof(DecisionObservations.SecondsToDecide),
        });

        foreach (var forbidden in (string[])["Attended", "Delegated", "Automated", "Trusted"])
        {
            await Assert.That(members.Contains(forbidden)).IsFalse()
                .Because($"'{forbidden}' is a judgement, and gg is not the one making it.");
        }
    }

    [Test]
    public async Task Nothing_in_the_client_interprets_the_observations()
    {
        // The other half: the fields exist and nothing reads them to reach a verdict
        // about the decision. A client branching on `Interactive` would be deciding what
        // attendance means, which is the policy question this defers.
        var reading = new Regex(
            @"if\s*\([^)]*Interactive|Interactive\s*\?|EvidenceRendered\s*\?|SecondsToDecide\s*[<>]",
            RegexOptions.Compiled);

        var offenders = Sources()
            .Where(f => reading.IsMatch(Code(f)))
            .Select(f => Path.GetFileName(f)!)
            .ToList();

        await Assert.That(offenders).IsEmpty()
            .Because("they are recorded and read by nobody in this version. Found: "
                   + string.Join(", ", offenders));

        await Assert.That(reading.IsMatch("if (observations.Interactive) { }")).IsTrue();
    }

    [Test]
    public async Task A_scripted_decision_is_distinguishable_from_one_at_a_terminal()
    {
        // The record has to be able to tell them apart, because the first thing anybody
        // does with `gg decide --json` is script it. Asserted on the shape rather than by
        // running two processes: what matters is that the field exists and carries
        // opposite values, and the value comes from Console redirection at the one place
        // that can see it.
        var scripted = new DecisionObservations
        {
            Interactive = false,
            EvidenceRendered = false,
            SecondsToDecide = null,
        };

        var attended = scripted with { Interactive = true, EvidenceRendered = true, SecondsToDecide = 42 };

        await Assert.That(scripted.Interactive).IsNotEqualTo(attended.Interactive);
        await Assert.That(scripted).IsNotEqualTo(attended)
            .Because("two decisions made in different ways are two different records.");
    }

    // ---- what the verb renders ----

    [Test]
    public async Task An_approval_that_lets_the_work_land_says_where()
    {
        var text = VerbOutput.ToText(new VerbResult.Decided(Approved(new DestinationAdmission
        {
            DestinationId = "pull-request",
            Branch = "gg/GG-42",
            BaseRef = "main",
            Slug = "acme/widgets",
            Reason = "every obligation the destination requires holds",
        })));

        await Assert.That(text).Contains("approved");
        await Assert.That(text).Contains("pull-request");
        await Assert.That(text).Contains("someone@example.test")
            .Because("attributed, or the record does not say who decided.");
    }

    [Test]
    public async Task An_approval_that_does_not_let_the_work_land_says_so_rather_than_leaving_a_gap()
    {
        // A decision that changed nothing about landing is a normal outcome, and a blank
        // line would read as one that did.
        var text = VerbOutput.ToText(new VerbResult.Decided(Approved()));

        await Assert.That(text).Contains("not yet");
        await Assert.That(text).Contains("outstanding");
    }

    [Test]
    public async Task The_verb_has_json_from_its_first_version()
    {
        var json = VerbOutput.ToJson(new VerbResult.Decided(Approved()));

        await Assert.That(json).Contains("\"outcome\"");
        await Assert.That(json).Contains("approved");
        await Assert.That(json).Contains("someone@example.test");
    }

    private static string Code(string file) =>
        string.Join('\n', File.ReadAllLines(file)
            .Where(l => !l.TrimStart().StartsWith("//", StringComparison.Ordinal)
                     && !l.TrimStart().StartsWith("///", StringComparison.Ordinal)
                     && !l.TrimStart().StartsWith("*", StringComparison.Ordinal)));

    private static IEnumerable<string> Sources()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Gg.sln")))
        {
            dir = dir.Parent;
        }

        var root = dir ?? throw new InvalidOperationException("Gg.sln not found");

        return new[] { "Gg.Client", "Gg.Cli", "Gg.Console" }
            .SelectMany(project => Directory.EnumerateFiles(
                Path.Combine(root.FullName, project), "*.cs", SearchOption.AllDirectories))
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
                            StringComparison.Ordinal)
                     && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}",
                            StringComparison.Ordinal));
    }
}
