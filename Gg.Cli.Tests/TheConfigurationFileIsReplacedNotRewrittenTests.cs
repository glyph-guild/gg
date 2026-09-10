using Gg.Local;

namespace Gg.Cli.Tests;

/// <summary>
/// A reader never sees half a configuration, because writing one is a replace.
/// </summary>
/// <remarks>
/// <para>
/// <b>There are two writers now, and there did not use to be.</b> A person runs
/// <c>gg config set</c> or edits through <c>$EDITOR</c>; a runner writes what
/// its control plane offered, at startup. <c>File.WriteAllText</c> truncates
/// the target and then fills it, so anything reading in between gets a document
/// that is not one — and the reader here is <c>InForce</c>, which answers by
/// warning once and running the whole process on defaults.
/// </para>
/// <para>
/// <b>A rename cannot be caught halfway.</b> The bytes go to a temporary file
/// beside the target and the target is replaced in one step, so a reader sees
/// the old document or the new one and never a mixture. The same reason applies
/// to a crash: a machine that loses power mid-write keeps the document it had
/// rather than a truncated one it will refuse to read on the way back up.
/// </para>
/// <para>
/// <b>What this does NOT fix, said plainly.</b> Two writers that each read,
/// change one value and write can still lose one another's change — the last
/// one wins, whole. Atomicity is about a reader never seeing a torn file; it is
/// not a lock. The window is small (a runner writes once, at startup) and the
/// failure is a setting quietly reverting rather than a machine refusing its
/// own configuration, which is why this is the half worth having first.
/// </para>
/// </remarks>
public class TheConfigurationFileIsReplacedNotRewrittenTests
{
    /// <summary>A document big enough that writing it is not one syscall.</summary>
    /// <remarks>
    /// <b>The size is the test.</b> A real configuration is a few hundred bytes
    /// and <c>File.WriteAllText</c> would very likely put it down in one go —
    /// so a race over one would pass against the very implementation this
    /// exists to refuse. Two megabytes cannot be written atomically by
    /// accident.
    /// </remarks>
    private static Configuration Large() => new()
    {
        ControlPlane = "https://control.invalid",
        IntentHosts = new string('t', 2 * 1024 * 1024),
    };

    private static string ADirectory()
    {
        var at = Path.Combine(Path.GetTempPath(), $"gg-atomic-{Guid.NewGuid():N}");
        Directory.CreateDirectory(at);
        return at;
    }

    [Test]
    public async Task A_reader_alongside_a_writer_never_sees_a_document_that_is_not_one()
    {
        var directory = ADirectory();
        var path = Path.Combine(directory, "config.json");

        try
        {
            ConfigurationFile.Write(Large(), path);

            var torn = new List<string>();
            var writing = true;

            // NO SLEEPS, and no waiting on a clock: a bounded number of real
            // writes with a real reader beside them. The carve-out this
            // repository allows is a client polling real state with bounded
            // patience, and that is what this is.
            var reader = Task.Run(() =>
            {
                while (Volatile.Read(ref writing))
                {
                    var parsed = ConfigurationFile.Read(path);

                    if (parsed.Diagnosis is { } refused)
                    {
                        torn.Add(refused);
                        return;
                    }
                }
            });

            for (var i = 0; i < 40; i++)
            {
                ConfigurationFile.Write(Large(), path);
            }

            Volatile.Write(ref writing, false);
            await reader;

            await Assert.That(torn).IsEmpty()
                .Because("a truncate-then-fill write is readable in the middle, and the "
                       + "reader is InForce - which answers a document it cannot parse by "
                       + "warning once and running the whole process on defaults. Saw: "
                       + string.Join(" | ", torn));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public async Task Nothing_is_left_beside_the_file_afterwards()
    {
        var directory = ADirectory();
        var path = Path.Combine(directory, "config.json");

        try
        {
            ConfigurationFile.Write(new Configuration { Editor = "hx" }, path);
            ConfigurationFile.Write(new Configuration { Editor = "vi" }, path);

            await Assert.That(Directory.EnumerateFileSystemEntries(directory).Select(e => Path.GetFileName(e)!))
                .IsEquivalentTo((string[])["config.json"])
                .Because("a temporary that outlives the write is a file in a person's "
                       + "config directory that nothing will ever clean up, and one they "
                       + "may well open by mistake.");

            await Assert.That(ConfigurationFile.Read(path).Configuration!.Editor).IsEqualTo("vi");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public async Task A_write_into_a_directory_that_does_not_exist_still_makes_it()
    {
        // The behaviour that was there before, kept: `gg config init` on a
        // machine with no config directory has always created one, and a
        // rename into a directory nothing made would fail where the old write
        // succeeded.
        var directory = ADirectory();
        var path = Path.Combine(directory, "nested", "deeper", "config.json");

        try
        {
            ConfigurationFile.Write(new Configuration { Editor = "hx" }, path);

            await Assert.That(ConfigurationFile.Read(path).Configuration!.Editor).IsEqualTo("hx");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
