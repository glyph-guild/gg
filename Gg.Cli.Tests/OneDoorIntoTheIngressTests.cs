using System.Text.RegularExpressions;

namespace Gg.Cli.Tests;

/// <summary>
/// Every flag a person can type on <c>gg fly</c> reaches the ingress, whichever
/// door they came through.
/// </summary>
/// <remarks>
/// <para>
/// <b>There are two doors and one of them drifted.</b> The ordinary arm passes
/// text, uri, provider, id, repository, runner, attended, work kind and
/// environment. The <c>--hand</c> arm passes five of those nine, so
/// <c>gg fly --hand --work-kind X</c> parses, reports nothing, and opens a
/// flight with no work kind. The flag is not refused and not honoured; it is
/// dropped.
/// </para>
/// <para>
/// <b>The comment directly above those two arms already records this happening
/// once.</b> "`--hand` parsed for a whole slice and did nothing" — the same
/// defect, the same door, found the same way. Two call sites that must agree
/// are two call sites that will stop agreeing; the fix is one, and this asserts
/// there is one.
/// </para>
/// <para>
/// <b>It walks the members rather than naming them.</b> Nine assertions pass on
/// the day the list was last complete. A member added to <c>CliAction.Fly</c>
/// and left out of the ingress is the defect this exists to catch, and it can
/// only catch it by asking what the members ARE.
/// </para>
/// </remarks>
public class OneDoorIntoTheIngressTests
{
    /// <summary>
    /// Members that are deliberately not the ingress's business.
    /// </summary>
    /// <remarks>
    /// <b>Exemptions as data with a reason, which is this repository's shape for
    /// a list that must not quietly grow.</b> Neither of these describes the
    /// flight being opened: one says how to print the answer, the other says
    /// which door was used and is what chooses the arm in the first place.
    /// </remarks>
    private static readonly IReadOnlyDictionary<string, string> NotTheIngress =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Json"] = "how the answer is printed, decided after the flight is opened.",
            ["ByHand"] = "which door this came through, which is what picks the arm.",
        };

    private static string Program()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Gg.sln")))
        {
            dir = dir.Parent;
        }

        var root = (dir ?? throw new InvalidOperationException("Gg.sln not found")).FullName;

        return File.ReadAllText(Path.Combine(root, "Gg.Cli", "Program.cs"));
    }

    [Test]
    public async Task There_is_one_way_into_the_ingress()
    {
        var calls = Regex.Matches(Program(), @"\bFlyAsync\(").Count;

        await Assert.That(calls).IsEqualTo(1)
            .Because("two call sites that must agree are two call sites that will stop "
                   + "agreeing - which is exactly what happened, and the comment above "
                   + "them says it happened once before.");
    }

    [Test]
    public async Task And_it_carries_every_flag_a_person_can_type()
    {
        var program = Program();
        var opens = program.IndexOf("FlyAsync(", StringComparison.Ordinal);

        await Assert.That(opens).IsGreaterThan(-1);

        // THE CALL ITSELF, not the file. A member named anywhere in Program.cs
        // would pass a scan over the whole thing while the ingress never saw it.
        var call = program[opens..Math.Min(program.Length, opens + 600)];

        var members = typeof(CliAction.Fly)
            .GetProperties()
            .Select(p => p.Name)
            .Where(name => !NotTheIngress.ContainsKey(name)
                        && !string.Equals(name, "EqualityContract", StringComparison.Ordinal))
            .ToList();

        await Assert.That(members).IsNotEmpty();

        foreach (var member in members)
        {
            await Assert.That(call.Contains($".{member}", StringComparison.Ordinal)).IsTrue()
                .Because($"`{member}` is something a person typed, and a flag that is neither "
                       + "refused nor honoured is the worst of the three. Exempt it here with "
                       + "a reason if it is genuinely not the ingress's business.");
        }
    }

    [Test]
    public async Task Every_exemption_says_why()
    {
        foreach (var (member, why) in NotTheIngress)
        {
            await Assert.That(why).IsNotEmpty()
                .Because($"{member} is excused from crossing, and an excuse nobody wrote down "
                       + "is one nobody can disagree with later.");
        }
    }
}
