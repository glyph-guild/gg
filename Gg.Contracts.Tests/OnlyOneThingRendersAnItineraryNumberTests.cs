using Gg.Contracts.Description;

namespace Gg.Contracts.Tests;

/// <summary>
/// S53.1-03. One place renders an itinerary number, and a literal anywhere else
/// fails the build.
/// </summary>
/// <remarks>
/// <para>
/// <b><c>FlightRefTests.Nothing_else_in_the_repository_renders_a_flight_number</c>'s
/// guard, on the second prefix.</b> Two implementations that agree today is the
/// arrangement this replaces, and it comes back as a lone <c>$"ITN-{n}"</c> in
/// a renderer - which reads as harmless and is how the flight number's own
/// parser came to exist twice.
/// </para>
/// <para>
/// <b>Shipped code only.</b> A test spelling the format is pinning it from the
/// outside, which is worth having and is the opposite of a second
/// implementation. Narrowing the rule to what ships keeps it aimed at the thing
/// that can actually drift.
/// </para>
/// </remarks>
public class OnlyOneThingRendersAnItineraryNumberTests
{
    [Test]
    public async Task A_number_renders_the_way_a_person_types_it()
    {
        await Assert.That(ItineraryRef.Format(7)).IsEqualTo("ITN-7");
        await Assert.That(ItineraryRef.Format(1042)).IsEqualTo("ITN-1042");
    }

    [Test]
    public async Task Nothing_else_in_the_repository_renders_an_itinerary_number()
    {
        var root = RepoRoot();
        var offenders = new List<string>();

        foreach (var file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                || file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                || file.Contains(".Tests", StringComparison.Ordinal)
                // The one place allowed to know the prefix.
                || Path.GetFileName(file) == "ItineraryRef.cs")
            {
                continue;
            }

            foreach (var (line, number) in File.ReadAllLines(file).Select((l, i) => (l, i + 1)))
            {
                var code = line.TrimStart();
                if (code.StartsWith("//", StringComparison.Ordinal))
                {
                    continue;   // Prose may name the format; only code may not build it.
                }

                // A literal "ITN-" followed by a digit or an interpolation is a
                // rendered itinerary number.
                for (var at = code.IndexOf("\"ITN-", StringComparison.OrdinalIgnoreCase);
                     at >= 0;
                     at = code.IndexOf("\"ITN-", at + 1, StringComparison.OrdinalIgnoreCase))
                {
                    var next = at + 5 < code.Length ? code[at + 5] : '\0';
                    if (char.IsAsciiDigit(next) || next == '{')
                    {
                        offenders.Add($"{Path.GetRelativePath(root, file)}:{number}");
                    }
                }
            }
        }

        await Assert.That(offenders).IsEmpty()
            .Because("ItineraryRef.Format is the only thing that renders an itinerary number. "
                   + "Found: " + string.Join(", ", offenders));
    }

    [Test]
    public async Task The_two_prefixes_are_not_each_others_prefix()
    {
        // The structural half of "nothing reads an itinerary as a flight". If
        // one prefix ever started with the other, both guards above would
        // still pass and every reference would resolve twice.
        await Assert.That(ItineraryRef.Prefix.StartsWith(FlightRef.Prefix, StringComparison.OrdinalIgnoreCase))
            .IsFalse();
        await Assert.That(FlightRef.Prefix.StartsWith(ItineraryRef.Prefix, StringComparison.OrdinalIgnoreCase))
            .IsFalse();
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Gg.sln")))
        {
            dir = dir.Parent;
        }
        return (dir ?? throw new InvalidOperationException("Gg.sln not found")).FullName;
    }
}
