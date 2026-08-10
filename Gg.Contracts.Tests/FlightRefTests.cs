using Gg.Contracts.Description;

namespace Gg.Contracts.Tests;

/// <summary>
/// One parser, in the contract, used by both sides.
/// </summary>
/// <remarks>
/// <para>
/// A flight number exists because <c>gg show &lt;flight&gt;</c> needs something a
/// person can type. A read surface that only accepted uuids would make the
/// number decorative - so both the number and the id resolve to the same
/// flight, and the rule for turning text into one is written HERE rather than
/// once in gg and once in the control plane.
/// </para>
/// <para>
/// Two implementations that agree today is the failure being avoided. They
/// agree until one of them accepts lowercase, or stops rejecting a negative,
/// and then <c>gg show gg-42</c> works against one deployment and 404s against
/// the next.
/// </para>
/// </remarks>
public class FlightRefTests
{
    [Test]
    public async Task A_number_renders_the_way_a_person_types_it()
    {
        await Assert.That(FlightRef.Format(42)).IsEqualTo("GG-42");
        await Assert.That(FlightRef.Format(1042)).IsEqualTo("GG-1042");
    }

    [Test]
    public async Task What_it_renders_it_parses_back()
    {
        // The property that makes the number usable at all: whatever a person
        // reads off the screen can be typed into the next command.
        foreach (var number in (int[])[0, 1, 7, 42, 1042, int.MaxValue])
        {
            await Assert.That(FlightRef.TryParse(FlightRef.Format(number), out var parsed)).IsTrue();
            await Assert.That(parsed!.Number).IsEqualTo(number);
            await Assert.That(parsed.Id).IsNull();
        }
    }

    [Test]
    public async Task A_uuid_is_a_reference_too()
    {
        var id = Guid.NewGuid();

        await Assert.That(FlightRef.TryParse(id.ToString(), out var parsed)).IsTrue();
        await Assert.That(parsed!.Id).IsEqualTo(id);
        await Assert.That(parsed.Number).IsNull();
    }

    [Test]
    public async Task The_prefix_is_read_in_any_case_and_written_in_one()
    {
        // People type what is quickest. The canonical rendering stays uppercase
        // so a flight number looks the same everywhere it is printed.
        foreach (var typed in (string[])["GG-42", "gg-42", "Gg-42", "gG-42"])
        {
            await Assert.That(FlightRef.TryParse(typed, out var parsed)).IsTrue()
                .Because($"'{typed}' is a flight number somebody typed.");
            await Assert.That(parsed!.ToString()).IsEqualTo("GG-42");
        }
    }

    [Test]
    public async Task Surrounding_whitespace_does_not_make_it_a_different_flight()
    {
        // Copied out of a terminal, a number arrives with whatever came with it.
        await Assert.That(FlightRef.TryParse("  GG-42\n", out var parsed)).IsTrue();
        await Assert.That(parsed!.Number).IsEqualTo(42);
    }

    [Test]
    public async Task Things_that_are_not_a_reference_are_refused()
    {
        // A bare integer is deliberately NOT accepted. It reads as a flight
        // number today and as an index or an offset the moment a list gains
        // paging, and accepting it now would be permanent.
        foreach (var text in (string?[])[null, "", "   ", "GG-", "GG-x", "GG--1", "42", "gg42",
                                         "GG-42-", "not-a-uuid", "GG-4 2"])
        {
            await Assert.That(FlightRef.TryParse(text, out var parsed)).IsFalse()
                .Because($"'{text}' is not a flight reference.");
            await Assert.That(parsed).IsNull()
                .Because("a refused parse must not hand back half a reference.");
        }
    }

    [Test]
    public async Task A_reference_is_a_number_or_an_id_and_never_both()
    {
        // The whole point of the type: downstream code branches on which one it
        // got, and a reference that claimed to be both would make that branch
        // arbitrary.
        await Assert.That(FlightRef.TryParse("GG-42", out var byNumber)).IsTrue();
        await Assert.That(byNumber!.Id is null && byNumber.Number is not null).IsTrue();

        await Assert.That(FlightRef.TryParse(Guid.NewGuid().ToString(), out var byId)).IsTrue();
        await Assert.That(byId!.Id is not null && byId.Number is null).IsTrue();
    }

    [Test]
    public async Task An_id_reference_renders_as_the_id()
    {
        var id = Guid.NewGuid();
        FlightRef.TryParse(id.ToString(), out var parsed);

        await Assert.That(parsed!.ToString()).IsEqualTo(id.ToString());
    }

    [Test]
    public async Task Nothing_else_in_the_repository_renders_a_flight_number()
    {
        // The structural half of "one parser, in the contract". Two
        // implementations that agree today is exactly the arrangement this
        // step replaced, and it would come back as a lone $"GG-{n}" in a
        // renderer.
        var root = RepoRoot();
        var offenders = new List<string>();

        foreach (var file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
        {
            var name = Path.GetFileName(file);
            if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                || file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                // The one place allowed to know the prefix, and the tests that
                // hold it to account.
                || name is "FlightRef.cs" or "FlightRefTests.cs")
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

                // A literal "GG-" followed by a digit or an interpolation is a
                // rendered flight number. The version headers are also spelled
                // GG-something and are not - they are followed by a letter.
                for (var at = code.IndexOf("\"GG-", StringComparison.OrdinalIgnoreCase);
                     at >= 0;
                     at = code.IndexOf("\"GG-", at + 1, StringComparison.OrdinalIgnoreCase))
                {
                    var next = at + 4 < code.Length ? code[at + 4] : '\0';
                    if (char.IsAsciiDigit(next) || next == '{')
                    {
                        offenders.Add($"{Path.GetRelativePath(root, file)}:{number}");
                    }
                }
            }
        }

        await Assert.That(offenders).IsEmpty()
            .Because("FlightRef.Format is the only thing that renders a flight number. Found: "
                   + string.Join(", ", offenders));
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
