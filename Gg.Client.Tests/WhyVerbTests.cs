using System.Text.RegularExpressions;
using Gg.Client;
using Gg.Contracts;

namespace Gg.Client.Tests;

/// <summary>
/// `gg why` renders an attribution it did not compute.
/// </summary>
/// <remarks>
/// <para>
/// <b>The client is not an authority.</b> A client that re-evaluated a predicate
/// in order to explain it could explain a verdict it did not produce, and the two
/// would drift - Article IX wearing the costume of a rendering concern, which is
/// the version of the mistake that gets waved through.
/// </para>
/// <para>
/// <b>And every obligation is shown, including the ones that did not attach.</b> A
/// verb that listed only what applied would make non-attachment invisible, which
/// is the failure the whole three-state design exists to prevent.
/// </para>
/// </remarks>
public class WhyVerbTests
{
    private static FlightAttribution AllThreeStates() => new()
    {
        FlightNumber = "GG-42",
        EnvelopeVersion = "v3",
        Obligations =
        [
            new ObligationAttribution
            {
                ObligationId = "in-scope",
                Attachment = Attachments.Attached,
                Condition = null,
                Because = "this obligation declares no condition, so it always applies",
                Outcome = "satisfied",
                Diagnosis = "Every path this flight touched is inside 'src/**'.",
            },
            new ObligationAttribution
            {
                ObligationId = "reversibility-plan",
                Attachment = Attachments.NotAttached,
                Condition = "change.manifest touches migrations/**",
                Because = "no path in change.manifest is under 'migrations/**'",
            },
            new ObligationAttribution
            {
                ObligationId = "not-exhausted",
                Attachment = Attachments.Unevaluable,
                Condition = "change.manifest touches migrations/**",
                Diagnosis = "This obligation reads a loop.outcome fact and this flight has none.",
            },
        ],
    };

    // ---- the three states are three readings ----

    [Test]
    public async Task All_three_states_render_differently()
    {
        // THE POINT OF THE VERB. If these read alike, an obligation that never
        // attached is indistinguishable from one nobody wrote.
        var text = VerbOutput.ToText(new VerbResult.Why(AllThreeStates()));

        await Assert.That(text).Contains("in-scope: attached");
        await Assert.That(text).Contains("reversibility-plan: not-attached");
        await Assert.That(text).Contains("not-exhausted: unevaluable");

        await Assert.That(Attachments.All.Distinct().Count()).IsEqualTo(3)
            .Because("three states, spelled three ways, or the rendering above is decoration.");
    }

    [Test]
    public async Task An_obligation_that_did_not_attach_names_the_condition_and_why_it_did_not_hold()
    {
        // "It did not attach" is a shrug. "Its condition was
        // change.manifest touches migrations/** and no path is under it" is
        // something somebody can check.
        var text = VerbOutput.ToText(new VerbResult.Why(AllThreeStates()));

        await Assert.That(text).Contains("change.manifest touches migrations/**");
        await Assert.That(text).Contains("no path in change.manifest is under");
    }

    [Test]
    public async Task An_always_attaching_obligation_says_so_rather_than_leaving_a_gap()
    {
        // A missing 'when' line and a condition nobody could read must not look
        // the same. This is the state absence would otherwise impersonate.
        var text = VerbOutput.ToText(new VerbResult.Why(AllThreeStates()));

        await Assert.That(text).Contains("always (this obligation declares no condition)");
    }

    [Test]
    public async Task An_unevaluable_obligation_carries_the_diagnosis()
    {
        var text = VerbOutput.ToText(new VerbResult.Why(AllThreeStates()));

        await Assert.That(text).Contains("loop.outcome fact and this flight has none");
    }

    [Test]
    public async Task A_halted_flight_says_it_halted_rather_than_looking_like_it_passed()
    {
        var text = VerbOutput.ToText(new VerbResult.Why(
            AllThreeStates() with { Halt = "Obligation 'not-exhausted' could not be evaluated." }));

        await Assert.That(text).Contains("HALTED");
    }

    [Test]
    public async Task An_envelope_governing_nothing_says_that_rather_than_printing_a_heading()
    {
        // The empty case, which would otherwise render as a flight with a title
        // and no content - and read as a flight that satisfied everything.
        var text = VerbOutput.ToText(new VerbResult.Why(
            AllThreeStates() with { Obligations = [] }));

        await Assert.That(text).Contains("declares no obligation");
    }

    // ---- --json from the first version ----

    [Test]
    public async Task The_verb_has_json_from_its_first_version()
    {
        var json = VerbOutput.ToJson(new VerbResult.Why(AllThreeStates()));

        await Assert.That(json).Contains("\"not-attached\"")
            .Because("the machine-readable surface carries the three states too, or a script "
                   + "cannot see a non-attachment either.");
        await Assert.That(json).Contains("reversibility-plan");
        await Assert.That(json).Contains("condition");
    }

    [Test]
    public async Task The_rendered_and_json_surfaces_agree_about_the_states()
    {
        // Two surfaces, one answer. A state visible in one and not the other is a
        // state somebody's tooling cannot see.
        var attribution = AllThreeStates();
        var json = VerbOutput.ToJson(new VerbResult.Why(attribution));
        var text = VerbOutput.ToText(new VerbResult.Why(attribution));

        foreach (var state in Attachments.All)
        {
            await Assert.That(json).Contains(state);
            await Assert.That(text).Contains(state);
        }
    }

    // ---- the client computes nothing ----

    [Test]
    public async Task No_predicate_evaluation_exists_client_side()
    {
        // STRUCTURAL, and it enforces a sentence in ADR-0009: policy is evaluated
        // in the control plane and nowhere else. A client that could match a glob
        // could answer a question it has no authority over.
        var evaluating = new Regex(
            @"\bGlob\b|Matches\(|IsMatch\(.*glob|ObligationEngine|VerdictSetEngine|AttachmentConditions\.GlobOf",
            RegexOptions.Compiled);

        var offenders = Sources()
            .Where(f => evaluating.IsMatch(Code(f)))
            .Select(f => Path.GetFileName(f)!)
            .Order(StringComparer.Ordinal)
            .ToList();

        await Assert.That(offenders).IsEmpty()
            .Because("the attribution arrives decided. A client that worked out why an obligation "
                   + "attached could explain a verdict it did not produce. Found: "
                   + string.Join(", ", offenders));

        // The scan can see one, so the emptiness above means something.
        await Assert.That(evaluating.IsMatch("if (Glob.Matches(scope, path))")).IsTrue();
    }

    [Test]
    public async Task The_client_carries_no_copy_of_the_attachment_decision()
    {
        // The other half: not just "does not evaluate" but "holds nothing it
        // could evaluate with". A client that stored the glob would be one commit
        // from matching against it.
        var why = Sources().Single(f => Path.GetFileName(f) == "FlightCommands.cs");

        await Assert.That(Code(why)).DoesNotContain("migrations")
            .Because("no condition is compiled in here; conditions live in envelopes.");
        await Assert.That(Code(why)).Contains("WhyAsync")
            .Because("and the scan is looking at the file that serves the verb.");
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
