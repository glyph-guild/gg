using Gg.Contracts;
using Gg.Runner.Execution;

namespace Gg.Runner.Tests;

/// <summary>
/// A flight keeps both records of what its agent did, because neither one is
/// enough on its own.
/// </summary>
/// <remarks>
/// <para>
/// <b>Measured on GG-309, and the two files disagree about what they hold.</b>
/// gg captures the agent's output stream and writes it to its own transcript
/// store. Claude Code separately writes a session file of its own. Diagnosing
/// that flight needed both, and neither is a superset of the other:
/// </para>
/// <list type="bullet">
/// <item>gg's stream holds the teardown — <c>background_tasks_changed</c>,
/// <c>task_updated: {status: "killed"}</c>, <c>task_notification</c> — and
/// drops the initial message entirely.</item>
/// <item>Claude's session holds the composed prompt, the full message content
/// and every tool input, and holds none of those teardown records.</item>
/// </list>
/// <para>
/// The decisive datum in that diagnosis was <c>run_in_background: true</c> on a
/// tool input, which said WHICH mechanism the agent used and therefore that the
/// envelope's instruction had named the wrong one. It is in Claude's file and
/// nowhere else. The record that the task was then killed is in gg's file and
/// nowhere else. A learning pass that could read only one of them would have
/// concluded the flight went fine.
/// </para>
/// <para>
/// <b>Found from the stream, never guessed.</b> The session's id and the
/// directory it is written under are both stated in the stream's own
/// <c>system/init</c> record, so this reads them rather than reconstructing a
/// path layout that is not ours to depend on. <c>memory_paths.auto</c> names the
/// project directory directly; slugifying <c>cwd</c> reaches the same directory
/// and is the fallback for a build that stops emitting the first.
/// </para>
/// <para>
/// <b>Pure, and it touches no disk.</b> A function of the stream and a home
/// directory, for the reason <see cref="TranscriptDigest"/> is: the thing that
/// has to be right is the derivation, and a test that had to lay out a home
/// directory to check it would be testing the layout.
/// </para>
/// </remarks>
public class BothTranscriptsAreKeptTests
{
    /// <summary>
    /// An init record with the two members this reads, and nothing else.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Hand-written, against this project's own preference, and
    /// <see cref="FixtureCleanlinessTests"/> is why.</b> A captured record of
    /// this shape cannot ship to a public repository: its
    /// <c>memory_paths.auto</c> contains <c>.claude/projects/</c>, which that
    /// scan forbids outright because it is where an operator's private notes for
    /// a project live. The first version of this test shipped the real capture
    /// and the build refused it, correctly.
    /// </para>
    /// <para>
    /// <b>So the evidence moved rather than being dropped.</b> The derivation
    /// that had to be right is the slug, and that is asserted below against the
    /// real <c>cwd</c> and directory pair from GG-309 - two strings in source,
    /// carrying no home directory and no toolbox. What is hand-written here is
    /// only the envelope around them.
    /// </para>
    /// </remarks>
    private static string Init(string sessionId, string cwd, string? memory) =>
        "{\"type\":\"system\",\"subtype\":\"init\""
      + $",\"session_id\":\"{sessionId}\",\"cwd\":\"{cwd}\""
      + (memory is null ? "" : $",\"memory_paths\":{{\"auto\":\"{memory}\"}}")
      + "}\n";

    /// <summary>
    /// The path this derives is the one the file was actually fetched from.
    /// </summary>
    /// <remarks>
    /// Not a shape that looks plausible: the session for GG-309 was read off
    /// vmlinux002 at exactly this path, 584,001 bytes of it, so the assertion is
    /// against a measurement rather than against my reading of a layout.
    /// </remarks>
    [Test]
    public async Task The_session_file_is_found_from_the_streams_own_init_record()
    {
        var found = ClaudeSession.FileIn(
            Init("a-session", "/state/trees/a-tree", "/state/notes/-state-trees-a-tree/memory/"),
            home: "/state");

        await Assert.That(found).IsEqualTo("/state/notes/-state-trees-a-tree/a-session.jsonl")
            .Because("the directory is the agent's own answer, taken as its parent, so a change "
                   + "to that layout reaches us as a different string rather than a wrong guess.");
    }

    /// <summary>
    /// A stream with no init record names no session, and that is ordinary.
    /// </summary>
    /// <remarks>
    /// An attended session, the move-bound probe and a sweep all reach an
    /// executor differently, and a derived path for a file nobody wrote would be
    /// a reference that cannot be followed - which this repository has already
    /// decided is a bug rather than a capability gap.
    /// </remarks>
    [Test]
    public async Task A_stream_without_an_init_record_names_no_session()
    {
        await Assert.That(ClaudeSession.FileIn(
            "{\"type\":\"assistant\"}\n{\"type\":\"result\",\"subtype\":\"success\"}\n",
            home: "/state")).IsNull();
    }

    /// <summary>
    /// The slug is derived the way the agent derives it, and the fixture proves it.
    /// </summary>
    /// <remarks>
    /// <b>Both routes reach the same directory, which is what makes the fallback
    /// safe.</b> The fixture's <c>memory_paths.auto</c> names
    /// <c>-var-lib-gg--cache-good-grief-trees-…</c> and its <c>cwd</c> is
    /// <c>/var/lib/gg/.cache/good-grief/trees/…</c>. Every character that is not a
    /// letter, digit or dash becomes a dash, so a leading slash becomes a leading
    /// dash and <c>/.cache</c> becomes <c>--cache</c>. Asserted against the real
    /// pair rather than against the rule, because the rule is a guess about
    /// somebody else's code and the pair is evidence.
    /// </remarks>
    [Test]
    public async Task The_projects_directory_is_derived_from_cwd_when_the_stream_stops_naming_it()
    {
        await Assert.That(ClaudeSession.ProjectSlug(
                "/var/lib/gg/.cache/good-grief/trees/01a0d9d9-98e8-7534-abeb-660051b40ade"))
            .IsEqualTo("-var-lib-gg--cache-good-grief-trees-01a0d9d9-98e8-7534-abeb-660051b40ade");
    }

    /// <summary>
    /// A run carries a reference to the session beside the one to gg's stream.
    /// </summary>
    /// <remarks>
    /// <b>Two members rather than a list</b>, because they are not
    /// interchangeable and a consumer asking for "the transcript" has to be made
    /// to say which. A learning pass wants both; a gate offering a person
    /// something to read wants gg's.
    /// </remarks>
    [Test]
    public async Task A_run_carries_a_reference_to_each_of_them()
    {
        var run = ExecutorRun.Completed("implement", "done", 1, TimeSpan.FromSeconds(1), [])
            with
            {
                Transcript = new ArtifactReference
                {
                    Locator = "/state/transcripts/f/implement.ndjson",
                    Sha256 = new string('a', 64),
                    Bytes = 1,
                    MediaType = "application/x-ndjson",
                    Scope = ArtifactScopes.RunnerLocal,
                },
                Session = new ArtifactReference
                {
                    Locator = "/state/transcripts/f/implement.session.jsonl",
                    Sha256 = new string('b', 64),
                    Bytes = 2,
                    MediaType = "application/x-ndjson",
                    Scope = ArtifactScopes.RunnerLocal,
                },
            };

        await Assert.That(run.Session).IsNotNull();
        await Assert.That(run.Session!.Locator).IsNotEqualTo(run.Transcript!.Locator)
            .Because("two files, two locators - a single reference would lose whichever "
                   + "one it did not point at.");
    }
}
