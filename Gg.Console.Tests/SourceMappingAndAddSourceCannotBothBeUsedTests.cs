using System.Text.RegularExpressions;

namespace Gg.Console.Tests;

/// <summary>
/// Package source mapping and <c>--add-source</c> cannot both be used, and only
/// CI finds out.
/// </summary>
/// <remarks>
/// <para>
/// <b>Written after it happened.</b> Adding a <c>packageSourceMapping</c> to
/// <c>nuget.config</c> - so the local folder holding a patched SIPSorcery could
/// not answer for anything else - made the tool job fail with <i>"The
/// --add-source option cannot be combined with package source mapping"</i>.
/// Two of three jobs passed; the one that broke is the only place a package is
/// INSTALLED rather than restored, and it does not run on a laptop.
/// </para>
/// <para>
/// <b>So the pairing is asserted where it can be seen.</b> A mapping is worth
/// having - without one a local directory is a source for every id in it - and
/// the way to keep it is to declare the source rather than pass it.
/// </para>
/// </remarks>
public class SourceMappingAndAddSourceCannotBothBeUsedTests
{
    private static string Config() => Sources.Read("nuget.config");

    private static string Workflow() =>
        Sources.Read(".github", "workflows", "ci.yml");

    [Test]
    public async Task No_workflow_passes_add_source_while_a_mapping_is_declared()
    {
        if (!Config().Contains("<packageSourceMapping>", StringComparison.Ordinal))
        {
            return;
        }

        await Assert.That(Workflow()).DoesNotContain("--add-source")
            .Because("NuGet refuses the flag outright once mapping is on, and the job that "
                   + "uses it is the one place a package is installed rather than restored - "
                   + "so nothing local reproduces it.");
    }

    [Test]
    public async Task Every_source_the_config_declares_is_mapped()
    {
        var config = Config();

        if (!config.Contains("<packageSourceMapping>", StringComparison.Ordinal))
        {
            return;
        }

        var declared = Regex.Matches(config, @"<add key=""([^""]+)"" value=")
            .Select(m => m.Groups[1].Value)
            .ToList();

        var mapped = Regex.Matches(config, @"<packageSource key=""([^""]+)""")
            .Select(m => m.Groups[1].Value)
            .ToList();

        await Assert.That(declared).IsNotEmpty()
            .Because("liveness: a config this could not parse would pass forever.");

        foreach (var source in declared)
        {
            await Assert.That(mapped).Contains(source)
                .Because($"'{source}' is declared and unmapped, so it answers for nothing - "
                       + "which is a source that looks configured and silently is not.");
        }
    }

    [Test]
    public async Task The_folder_sources_answer_for_one_package_each()
    {
        var config = Config();

        if (!config.Contains("<packageSourceMapping>", StringComparison.Ordinal))
        {
            return;
        }

        // THE POINT OF THE MAPPING. An unmapped local directory is a source for
        // every id in it, so a stray .nupkg shadows nuget.org - which is the
        // 512-byte incident's shape, arriving by accident rather than by a
        // deliberate pin.
        await Assert.That(config).Contains("<package pattern=\"*\" />")
            .Because("nuget.org has to keep answering for everything else.");

        foreach (var local in (string[])["sipsorcery-fork", "artifacts"])
        {
            await Assert.That(config).Contains($"<packageSource key=\"{local}\">");
        }
    }
}
