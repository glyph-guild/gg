using System.Text.RegularExpressions;
using Gg.Local;

namespace Gg.Cli.Tests;

/// <summary>
/// Where the estate's working copy is, and why the current directory could not
/// keep being the answer.
/// </summary>
/// <remarks>
/// <para>
/// <b>All three working-copy verbs took the process's current directory, and
/// nothing else could name one.</b> That is a fine answer for a verb somebody
/// types inside the tree and no answer at all for the console, which is
/// launched from wherever a person happened to be — so an estate pane could not
/// act on a working copy it had no way to find.
/// </para>
/// <para>
/// <b>The hazard it leaves behind is worse than the gap.</b>
/// <c>Git.Status</c> answers empty for a directory that is not a repository —
/// deliberately, because ADR-0016 holds that the repository is convenience and
/// a plain directory should get no git opinion rather than a refusal it cannot
/// act on. Pull's dirty-tree refusal is computed from that answer, so
/// <c>gg airspace pull</c> in the wrong directory skips the check entirely and
/// writes an <c>airspace/</c> tree there without a word. The doctor is where
/// that becomes visible before the first pull rather than after it.
/// </para>
/// <para>
/// <b>Not offerable, and that is a decision rather than an omission.</b> It
/// names a directory on this machine, and a control plane able to move where a
/// person's drafts live could move where their next edit lands. It keeps an
/// environment variable, unlike <c>accept-offered</c>, because a container or a
/// CI job has a legitimate reason to set one.
/// </para>
/// </remarks>
public class TheWorkingCopyHasAnAddressTests
{
    private const string Variable = "GG_AIRSPACE";

    private static string SourceOf(string project, string file)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Gg.sln")))
        {
            directory = directory.Parent;
        }

        return directory is null
            ? throw new InvalidOperationException(
                "The repository root was not found above the test binary, so the source this "
              + "guard reads cannot be found.")
            : File.ReadAllText(Path.Combine(directory.FullName, project, file));
    }

    [Test]
    public async Task The_file_can_carry_where_the_working_copy_is()
    {
        var written = Settings.With(new Configuration(), Variable, "/tmp/estate");

        var carried = Settings.Resolve(Variable, written, environment: _ => null);

        await Assert.That(carried.Value).IsEqualTo("/tmp/estate");
        await Assert.That(carried.Source).IsEqualTo(SettingSources.File)
            .Because("a path a person keeps is exactly what the configuration file is for - "
                   + "the console cannot be handed one on a command line it does not have.");
    }

    [Test]
    public async Task A_variable_in_front_of_one_command_still_wins()
    {
        var written = Settings.With(new Configuration(), Variable, "/tmp/from-the-file");

        var carried = Settings.Resolve(
            Variable, written, environment: v => v == Variable ? "/tmp/just-this-once" : null);

        await Assert.That(carried.Value).IsEqualTo("/tmp/just-this-once");
        await Assert.That(carried.Shadowed).IsEqualTo("/tmp/from-the-file")
            .Because("without the shadow a person edits the file, sees nothing change, and "
                   + "has nowhere to find out why.");
    }

    [Test]
    public async Task It_has_no_built_in_default()
    {
        // UNSET, NOT A DEFAULT. There is no path that is right for every
        // machine, and a default here would be a directory gg wrote an estate
        // into because nobody said otherwise.
        var carried = Settings.Resolve(Variable, new Configuration(), environment: _ => null);

        await Assert.That(carried.Source).IsEqualTo(SettingSources.Unset)
            .Because("the verbs fall back to the current directory, which is a fallback "
                   + "rather than a default - the difference is that a person can see the "
                   + "second one and cannot see the first.");
    }

    [Test]
    public async Task It_is_not_a_setting_a_control_plane_may_move()
    {
        await Assert.That(Gg.Contracts.OfferableKeys.All)
            .DoesNotContain("airspace", StringComparer.Ordinal)
            .Because("it names a directory on this machine, and a control plane able to move "
                   + "where a person's drafts live could move where their next edit lands. "
                   + "That is a different act from redirecting a fleet, and it is not one an "
                   + "offer may perform.");
    }

    [Test]
    public async Task No_working_copy_verb_reaches_for_the_current_directory_itself()
    {
        // THE POINT OF THE CHANGE, asserted where it can go wrong. Three verbs
        // each passed Directory.GetCurrentDirectory() at the call site, so
        // adding the setting to two of them and forgetting the third would be
        // invisible - each verb would work and they would disagree about which
        // tree they were talking about.
        var root = SourceOf("Gg.Cli", "Program.cs");

        var reaching = Regex.Matches(
                root, @"Airspace(Pull|Diff|Apply)Async\(Directory\.GetCurrentDirectory\(\)\)")
            .Select(m => m.Value)
            .ToList();

        await Assert.That(reaching).IsEmpty()
            .Because("a verb that resolves the working copy at its own call site is one that "
                   + "can disagree with its siblings about where the estate is. Found: "
                   + string.Join(", ", reaching));
    }

    [Test]
    public async Task The_doctor_says_where_the_working_copy_is_and_whether_it_is_one()
    {
        var doctor = SourceOf("Gg.Client", "Doctor.cs");

        await Assert.That(doctor).Contains(Variable, StringComparison.Ordinal)
            .Because("the doctor is the page a person reads when a verb did something they "
                   + "did not expect, and 'which tree did that write to' is exactly that "
                   + "question.");

        await Assert.That(doctor).Contains("IsRepository", StringComparison.Ordinal)
            .Because("Git.Status answers empty for a plain directory, so pull's dirty-tree "
                   + "refusal cannot fire there and an estate is written with no warning. "
                   + "Whether the path is a working tree is the one fact that makes that "
                   + "visible before the first pull rather than after it.");
    }
}
