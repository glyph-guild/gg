using System.Text.RegularExpressions;
using Gg.Console;
using Gg.Contracts;

namespace Gg.Console.Tests;

/// <summary>
/// What the console observed about a decision. Never what it concluded.
/// </summary>
/// <remarks>
/// <para>
/// <b>gg says what it observed; the control plane decides what that means.</b> There is no
/// <c>attended</c> field anywhere and there must not be one - attendance is an
/// interpretation of these observations, made somewhere that can see policy, and a client
/// asserting it would be deciding the thing the observations exist to let somebody else
/// decide.
/// </para>
/// <para>
/// <b>Only rendering is observable.</b> <c>EvidenceRendered</c> means the pane displayed
/// it, which this process watched happen. <c>EvidenceReviewed</c> would be a claim about a
/// person's attention, and nothing in a terminal can know it - the same measured-versus-
/// stated distinction the payload draws, applied to a field name.
/// </para>
/// <para>
/// <b>Three surfaces, three distinguishable observations</b>: the console renders and is
/// interactive, the verb at a terminal is interactive and renders nothing, and the verb in
/// a script is neither. That is what makes "any gate cleared non-interactively is a
/// delegated gate" a thing somebody can look up rather than assert.
/// </para>
/// </remarks>
public class ConsoleObservationTests
{
    [Test]
    public async Task The_console_reports_that_it_rendered_the_evidence()
    {
        var observed = ConsoleObservation.Of(
            new AppState { Payload = APayload() }, TimeSpan.FromSeconds(12));

        await Assert.That(observed.EvidenceRendered).IsTrue()
            .Because("the pane had the case in it, which this process watched happen.");
        await Assert.That(observed.Interactive).IsTrue()
            .Because("somebody pressed a key in a terminal.");
        await Assert.That(observed.SecondsToDecide).IsEqualTo(12);
    }

    [Test]
    public async Task A_console_with_no_payload_reports_that_nothing_was_rendered()
    {
        // ASK WHY IT PASSES. If this always said true, the field would be a constant
        // dressed as a measurement - and a constant is exactly what nobody can act on.
        var observed = ConsoleObservation.Of(new AppState(), TimeSpan.FromSeconds(3));

        await Assert.That(observed.EvidenceRendered).IsFalse()
            .Because("there was no case in the pane, so saying it was shown would be a "
                   + "claim about something that did not happen.");
    }

    [Test]
    public async Task The_three_surfaces_are_distinguishable()
    {
        // THE EIGHTH THREE-SHAPES RESULT IN THIS SLICE, and the one that makes a policy
        // about delegated gates writable: a reader can tell a decision made in front of
        // the evidence from one made at a prompt from one made by a script.
        var console = ConsoleObservation.Of(
            new AppState { Payload = APayload() }, TimeSpan.FromSeconds(9));

        var atATerminal = new DecisionObservations
        {
            Interactive = true, EvidenceRendered = false, SecondsToDecide = null,
        };

        var scripted = new DecisionObservations
        {
            Interactive = false, EvidenceRendered = false, SecondsToDecide = null,
        };

        var shapes = new[]
        {
            (console.Interactive, console.EvidenceRendered),
            (atATerminal.Interactive, atATerminal.EvidenceRendered),
            (scripted.Interactive, scripted.EvidenceRendered),
        };

        await Assert.That(shapes.Distinct().Count()).IsEqualTo(3)
            .Because("three surfaces, three observations. Two that collided would make a "
                   + "rule about non-interactive gates unenforceable by inspection.");
    }

    [Test]
    public async Task Nothing_concludes_attendance_anywhere()
    {
        // The standing rule, re-asserted where it is newly tempting: the modal knows more
        // about the person than anything else in gg does, and it is still not allowed to
        // decide what that means.
        var naming = new Regex(
            @"\bAttended\b|\bAttendance\b|EvidenceReviewed|WasPresent|\bDiligent",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        var offenders = ConsoleSources()
            .Where(f => naming.IsMatch(CodeOf(f)))
            .Select(f => Path.GetFileName(f)!)
            .ToList();

        await Assert.That(offenders).IsEmpty()
            .Because("gg reports what it saw. What it meant is the control plane's, and a "
                   + "field name is where that boundary is most easily lost. Found: "
                   + string.Join(", ", offenders));

        await Assert.That(naming.IsMatch("var attended = true;")).IsTrue()
            .Because("the scan can see one, so the emptiness above means something.");
    }

    private static GateEvidencePayload APayload() => new()
    {
        Items =
        [
            new GateEvidenceItem
            {
                Item = EvidenceItems.ChangeManifest,
                Disposition = EvidenceDispositions.Inline,
                Voice = EvidenceVoices.Measured,
                Inline = "1 file(s)",
            },
        ],
        DeltaNote = "first ask",
    };

    /// <summary>
    /// One file's code, without its comments.
    /// </summary>
    /// <remarks>
    /// A rule about what the code DOES must not be tripped by prose explaining the rule -
    /// and the clearest way to write "nothing here concludes attendance" uses the word. The
    /// same helper the runner's absence scans use, for the same reason.
    /// </remarks>
    private static string CodeOf(string file) =>
        string.Join('\n', File.ReadAllLines(file)
            .Where(l => !l.TrimStart().StartsWith("//", StringComparison.Ordinal)
                     && !l.TrimStart().StartsWith("*", StringComparison.Ordinal)
                     && !l.TrimStart().StartsWith("///", StringComparison.Ordinal)));

    private static IEnumerable<string> ConsoleSources()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Gg.sln")))
        {
            dir = dir.Parent;
        }

        var root = dir ?? throw new InvalidOperationException("Gg.sln not found");

        return Directory.EnumerateFiles(
                Path.Combine(root.FullName, "Gg.Console"), "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
                            StringComparison.Ordinal)
                     && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}",
                            StringComparison.Ordinal));
    }
}
