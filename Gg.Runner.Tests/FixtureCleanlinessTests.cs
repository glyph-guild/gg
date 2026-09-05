using System.Text.Json;

namespace Gg.Runner.Tests;

/// <summary>
/// A committed transcript carries what the agent did, and nothing about the
/// machine that ran it.
/// </summary>
/// <remarks>
/// <para>
/// <b>This repository is public and these files are permanent.</b> A transcript
/// is captured from a real session on somebody's own machine, and the stream's
/// opening <c>system init</c> record describes that machine rather than the run:
/// where the operator's home directory is, which unix socket their session was
/// talking on, which plugins and skills they have installed, and where their
/// private notes live. None of it is under test. All of it is durable once
/// pushed.
/// </para>
/// <para>
/// <b>It is the product's own rule, applied to the product's own repository.</b>
/// The whole claim of the control plane is that a customer's environment does
/// not cross into it. A fixture that ships an engineer's home path is the same
/// mistake with the parties swapped, and it is the one nobody was looking at.
/// </para>
/// <para>
/// <b>A scan rather than a review.</b> Four fixtures were captured over four
/// slices and every one of them carried this; a rule that lives in somebody's
/// memory is a rule that lasts until the next capture. What is asserted is the
/// SHAPE of a leak - a home directory, a socket, an absolute path into a
/// developer's machine - because the specific values differ per capture and the
/// next one will be somebody else's.
/// </para>
/// </remarks>
public class FixtureCleanlinessTests
{
    private static IReadOnlyList<string> Fixtures()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "Fixtures")))
        {
            dir = dir.Parent;
        }

        return dir is null
            ? []
            : [.. Directory.EnumerateFiles(Path.Combine(dir.FullName, "Fixtures"), "*.ndjson")];
    }

    /// <summary>
    /// Shapes that mean "a real machine", each with the reason it may not ship.
    /// </summary>
    private static readonly (string Needle, string Why)[] Machines =
    [
        ("/Users/", "a macOS home directory names the person who captured this"),
        ("/home/", "a Linux home directory does the same"),
        ("C:\\Users\\", "and so does a Windows one"),
        ("/tmp/cc-socks/", "a session's own control socket, with the process id in the name"),
        ("/var/folders/", "a macOS per-user temporary directory, which is per-USER"),
        (".claude/plugins/", "which plugins this operator has installed, and where"),
        (".claude/projects/", "where this operator's private notes for a project live"),
    ];

    /// <summary>
    /// The person running these tests, which is the person a capture is made
    /// by.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A name can ship with no path around it, and one did.</b> An agent
    /// that runs <c>ls -la</c> puts the owning user in its tool result, where a
    /// scan for <c>/Users/</c> sees nothing - so the path rules above passed a
    /// file that still named somebody. Checked against the CURRENT user because
    /// that is who is capturing.
    /// </para>
    /// <para>
    /// <b>And only where a capture can be made, which is not a build machine.</b>
    /// This is scoping rather than an exemption: a transcript is captured by a
    /// person running an agent interactively, so the author this protects is
    /// only ever at a keyboard. On a build machine the value is a service
    /// account, and the common one is literally the word this repository uses
    /// about its own product on nearly every page - so the check would refuse a
    /// capture for containing a noun. It did: the first version of this scrub
    /// replaced a username with that very word, and the build refused the file.
    /// Both halves of this were found that way.
    /// </para>
    /// </remarks>
    private static string? Capturer =>
        Environment.GetEnvironmentVariable("CI") is { Length: > 0 }
            ? null
            : Environment.UserName;

    [Test]
    public async Task No_committed_transcript_names_the_machine_that_produced_it()
    {
        var fixtures = Fixtures();

        await Assert.That(fixtures).IsNotEmpty()
            .Because("the scan found no fixtures, so it asserted nothing.");

        var found = new List<string>();

        foreach (var path in fixtures)
        {
            var text = await File.ReadAllTextAsync(path);

            found.AddRange(Machines
                .Where(m => text.Contains(m.Needle, StringComparison.Ordinal))
                .Select(m => $"{Path.GetFileName(path)} carries '{m.Needle}' - {m.Why}"));

            if (Capturer is { Length: > 2 }
                && text.Contains(Capturer, StringComparison.OrdinalIgnoreCase))
            {
                found.Add($"{Path.GetFileName(path)} names the user running this build, which "
                        + "is how a capture ships somebody without shipping a path");
            }
        }

        await Assert.That(found).IsEmpty()
            .Because("this repository is public and these files are permanent. Rewrite the "
                   + "capture with neutral paths rather than adding an exemption - the value "
                   + "is not under test, and an exemption list is how the next four arrive. "
                   + "Found:\n  " + string.Join("\n  ", found));
    }

    [Test]
    public async Task Nor_the_operators_own_tooling()
    {
        // A DIFFERENT LEAK IN THE SAME LINE, and it survives a path scrub. The
        // init record lists every skill, slash command and plugin the capturing
        // machine had, which is an inventory of somebody's private work - the
        // names alone have said more than the paths did.
        var offenders = new List<string>();

        foreach (var path in Fixtures())
        {
            var first = (await File.ReadAllLinesAsync(path)).FirstOrDefault();

            if (first is not { Length: > 0 })
            {
                continue;
            }

            using var document = JsonDocument.Parse(first);

            // A PLUGIN'S SERVER IS THE OPERATOR'S; a configured one is the
            // run's. `gg` and `tracker` are why the nomination capture exists
            // and stay; `plugin:slack:slack` says which plugins somebody has
            // installed, which is the same leak the empty plugins array just
            // closed one field over.
            if (document.RootElement.TryGetProperty("mcp_servers", out var servers)
                && servers.ValueKind == JsonValueKind.Array
                && servers.EnumerateArray().Any(x =>
                    x.TryGetProperty("name", out var n)
                    && (n.GetString() ?? "").StartsWith("plugin:", StringComparison.Ordinal)))
            {
                offenders.Add($"{Path.GetFileName(path)}.mcp_servers names a plugin's server");
            }

            foreach (var member in (string[])["skills", "slash_commands", "plugins"])
            {
                if (document.RootElement.TryGetProperty(member, out var value)
                    && value.ValueKind == JsonValueKind.Array
                    && value.GetArrayLength() > 0)
                {
                    offenders.Add($"{Path.GetFileName(path)}.{member} lists "
                                + $"{value.GetArrayLength()} entries");
                }
            }
        }

        await Assert.That(offenders).IsEmpty()
            .Because("empty the list rather than curating it: nothing here reads these, and "
                   + "the run is no less genuine for a capture that does not enumerate "
                   + "somebody's toolbox. Found:\n  " + string.Join("\n  ", offenders));
    }
}
