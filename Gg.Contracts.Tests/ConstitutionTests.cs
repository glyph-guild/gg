namespace Gg.Contracts.Tests;

/// <summary>
/// The constitution is vendored into this repo at .goodgrief/constitution.md.
/// This test exists to catch the realistic failure: the file being deleted.
/// </summary>
/// <remarks>
/// File IO here is deliberate and test-only. Nothing at run time may read this
/// path - gg AOT-publishes to a single binary and must not depend on the
/// repository. The recorded-version constant lives in the control plane, which
/// is what writes it onto flights; this repo only vendors the text.
/// </remarks>
public class ConstitutionTests
{
    private static string VendoredConstitutionPath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Gg.sln")))
        {
            dir = dir.Parent;
        }
        return dir is null
            ? throw new InvalidOperationException("Gg.sln not found above " + AppContext.BaseDirectory)
            : Path.Combine(dir.FullName, ".goodgrief", "constitution.md");
    }

    [Test]
    public async Task VendoredConstitutionExistsAndDeclaresAVersion()
    {
        var path = VendoredConstitutionPath();

        await Assert.That(File.Exists(path)).IsTrue()
            .Because(".goodgrief/constitution.md is the vendored copy this repo is governed by; deleting it must fail the build.");

        var markdown = await File.ReadAllTextAsync(path);
        var lines = markdown.Split('\n');

        await Assert.That(lines[0].TrimEnd('\r')).IsEqualTo("---")
            .Because("the vendored copy keeps its YAML frontmatter.");

        string? version = null;
        for (var i = 1; i < lines.Length; i++)
        {
            var line = lines[i].TrimEnd('\r');
            if (line == "---")
            {
                break;
            }
            if (line.StartsWith("version:", StringComparison.Ordinal))
            {
                version = line["version:".Length..].Trim().Trim('"');
                break;
            }
        }

        await Assert.That(version).IsNotNull()
            .Because("the frontmatter version is load-bearing - the control plane records it onto every flight.");
        await Assert.That(version).IsNotEmpty();
    }
}
