using System.Text.RegularExpressions;

namespace Gg.Cli.Tests;

/// <summary>
/// A refusal type nothing catches is a sentence that reaches nobody.
/// </summary>
/// <remarks>
/// <para>
/// <b>This estate has fixed this defect three times by naming the instance.</b>
/// <c>EmitAsync</c>'s first catch says <i>"AN ANSWER, AND IT USED TO BE A
/// CRASH. Every refusal on this path left as an unhandled
/// InvalidOperationException - a stack trace and exit 134."</i> Then the two
/// airspace refusals were routed through that same emitter and not added to
/// it, which <c>AnAirspaceRefusalIsASentenceTests</c> records in those words.
/// Then <c>AdminRefusedException</c> did it again, and it was found by running
/// the program against a real control plane rather than by anything here.
/// </para>
/// <para>
/// <b>So it is a walk, not three assertions.</b> Three assertions would pass on
/// the day the list was last correct - the argument
/// <c>EveryVerbIsDiscoverableTests</c> makes about usage, applied to refusals.
/// The subject is the source of both files: every refusal type Gg.Client
/// declares, against every type the entry point names in a catch.
/// </para>
/// <para>
/// <b>Caught SOMEWHERE, not caught by one function.</b> There are several
/// emitters on purpose - a takeover refusal is the ordinary case for one verb
/// and a fault for another - so the rule is that a person can be told, not
/// which clause tells them.
/// </para>
/// </remarks>
public class EveryRefusalReachesAPersonTests
{
    /// <summary>
    /// Refusals no emitter catches, each with the reason it is not a gap.
    /// </summary>
    /// <remarks>
    /// Kept as data with the reason attached, so an addition has to be argued
    /// rather than quietly appended - the shape <c>NotVerbs</c> uses one file
    /// over.
    /// </remarks>
    private static readonly Dictionary<string, string> Unhandled =
        new(StringComparer.Ordinal)
        {
            ["PinnedKeysUnreadableException"] =
                "raised on the gg runner watch path, and WatchAsync has no try at all - so "
              + "catching this one type in an emitter it does not run through would be false "
              + "comfort. That function growing a handler is its own change, and this line "
              + "comes off the list when it does.",
        };

    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Gg.sln")))
        {
            directory = directory.Parent;
        }

        return (directory ?? throw new InvalidOperationException(
            "The repository root was not found above the test binary. This guard reads the "
          + "source deliberately; see the remarks.")).FullName;
    }

    /// <summary>Every exception type Gg.Client raises at a caller.</summary>
    private static IReadOnlyList<string> Refusals()
    {
        var declared = new List<string>();

        foreach (var file in Directory.EnumerateFiles(
            Path.Combine(Root(), "Gg.Client"), "*.cs", SearchOption.AllDirectories))
        {
            declared.AddRange(Regex
                .Matches(File.ReadAllText(file),
                    @"public (?:sealed )?class (?<name>\w+)\s*\(?[^)]*\)?\s*:\s*(?:Exception|InvalidOperationException)")
                .Select(m => m.Groups["name"].Value));
        }

        return [.. declared.Distinct(StringComparer.Ordinal).OrderBy(n => n, StringComparer.Ordinal)];
    }

    [Test]
    public async Task Every_refusal_the_client_raises_is_caught_somewhere_in_the_entry_point()
    {
        var entry = File.ReadAllText(Path.Combine(Root(), "Gg.Cli", "Program.cs"));

        var refusals = Refusals();

        await Assert.That(refusals).IsNotEmpty()
            .Because("no refusal type was found, so this walk asked nothing - the pattern "
                   + "matched no declaration and the guard is vacuous.");

        // BOTH SPELLINGS. Several clauses are written as a filtered catch over
        // a union - `catch (Exception refused) when (refused is A or B)' - so a
        // walk that only read `catch (T' would report handled types as gaps.
        var uncaught = refusals
            .Where(type =>
                !Regex.IsMatch(entry, @"catch \((?:Gg\.Client\.)?" + type + @"\b")
                && !Regex.IsMatch(entry, @"\b(?:is|or) " + type + @"\b"))
            .Where(type => !Unhandled.ContainsKey(type))
            .ToList();

        await Assert.That(uncaught).IsEmpty()
            .Because("these reach a person as a stack trace and exit 134, which is what gg "
                   + "looks like when it breaks - and every one of them carries a sentence "
                   + "somebody wrote to be acted on. Catch it, or add a line to Unhandled "
                   + "saying why it is not a gap. Found: " + string.Join(", ", uncaught));
    }

    [Test]
    public async Task The_exemption_list_names_no_type_that_has_gone()
    {
        // THE OTHER DIRECTION. A reason written about a type nobody raises any
        // more is a sentence describing a product that does not exist, and it
        // reads as authoritative.
        var refusals = Refusals();

        var ghosts = Unhandled.Keys
            .Where(type => !refusals.Contains(type, StringComparer.Ordinal))
            .ToList();

        await Assert.That(ghosts).IsEmpty()
            .Because("these are not refusal types any more. Delete their lines. Found: "
                   + string.Join(", ", ghosts));
    }
}
