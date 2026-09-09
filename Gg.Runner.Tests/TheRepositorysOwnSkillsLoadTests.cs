using Gg.Contracts;
using Gg.Runner.Execution;

namespace Gg.Runner.Tests;

/// <summary>
/// A repository's <c>.claude</c> reaches the agent, and the bound survives it.
/// </summary>
/// <remarks>
/// <para>
/// <b>A skill in the repository is the point.</b> It travels with the code the
/// flight materializes, is versioned with it, and is the same on every runner —
/// which is everything the machine's own skills are not. Those are an ambient
/// property of a host and <c>ClaudeCodeExecutor</c> calls them "a declared gap
/// rather than a solved problem".
/// </para>
/// <para>
/// <b>They did not load, and it looked as though they did.</b> Measured against
/// the exact flags this executor passes: with <c>--setting-sources ""</c> the
/// <c>Skill</c> tool answers "Unknown skill", and the agent reaches the right
/// answer anyway by opening <c>.claude/skills/…/SKILL.md</c> as a file. That is
/// the agent going looking, not a skill running, and it degrades where a skill
/// has scripts or several files.
/// </para>
/// <para>
/// <b>The mode is pinned because loading settings hands the file a lever.</b>
/// Also measured: a repository declaring
/// <c>permissions.defaultMode: acceptEdits</c> wrote a file under
/// <c>--allowedTools Read</c> — an envelope's <c>moves</c> defeated by the
/// repository it was about. Passing <c>--permission-mode</c> explicitly beats
/// the file and the bound holds. On an ATTENDED session this matters more, not
/// less: nothing is in the allowlist there, so the mode is the whole of it.
/// </para>
/// <para>
/// <b>What is knowingly accepted is hooks.</b> A repository's
/// <c>SessionStart</c> hook runs a command on the runner, measured, and no
/// probe fires before it. The owner's call, made with that in front of them.
/// </para>
/// </remarks>
public class TheRepositorysOwnSkillsLoadTests
{
    private static readonly Gg.Local.SelfInvocation Self =
        Gg.Local.SelfInvocation.For("/bin/gg", null)!;

    private static ExecutorRequest Request() => new()
    {
        WorkingDirectory = "/work/flight",
        LoopId = "implement",
        Moves = [LoopMoves.Read],
        IntentUri = "https://example.invalid/work/1",
        WallClock = TimeSpan.FromMinutes(30),
        TranscriptPath = "/work/flight/transcript.ndjson",
    };

    /// <summary>
    /// The bound as the ATTENDED executor passes it, which is the same list.
    /// </summary>
    /// <remarks>
    /// <b>Read through this one on purpose.</b> The two executors share
    /// <c>BoundingArguments</c> because "two hand-maintained copies of this list
    /// would drift, and the drift would be a flight running under a bound
    /// somebody thought it had" - and the attended session is where the mode
    /// matters most, since nothing is in its allowlist at all.
    /// </remarks>
    private static IReadOnlyList<string> Arguments() =>
        AttendedExecutor.StartInfoFor(Request(), [], null, Self).ArgumentList!;

    [Test]
    public async Task The_repositorys_settings_are_the_ones_that_load()
    {
        var arguments = Arguments().ToList();
        var at = arguments.IndexOf("--setting-sources");

        await Assert.That(at).IsGreaterThanOrEqualTo(0);

        await Assert.That(arguments[at + 1]).IsEqualTo("project")
            .Because("a skill in the repository is the whole reason, and `project` is the "
                   + "source that carries it. Not `user`: the machine's own skills and "
                   + "settings are the operator's, and clearing those is what this flag was "
                   + "for. Not `local`: that file is a person's, gitignored, and a fleet "
                   + "runner has no person.");

        foreach (var forbidden in new[] { "user", "local", "" })
        {
            await Assert.That(arguments[at + 1]).IsNotEqualTo(forbidden);
        }
    }

    [Test]
    public async Task The_permission_mode_is_pinned_because_the_file_can_move_it()
    {
        // THE LEVER THAT REPLACES THE ONE BEING GIVEN UP. `--setting-sources ""`
        // stopped a settings file deciding what a session may do; loading the
        // repository's settings hands that decision back unless something takes
        // it. Measured: `defaultMode: acceptEdits` in a repository wrote a file
        // under `--allowedTools Read`, and passing the mode explicitly stopped
        // it.
        var arguments = Arguments().ToList();
        var at = arguments.IndexOf("--permission-mode");

        await Assert.That(at).IsGreaterThanOrEqualTo(0)
            .Because("without it a repository sets its own permission mode, and an envelope's "
                   + "moves stop meaning anything the moment the repository says otherwise. "
                   + "Passed: " + string.Join(" ", arguments));

        await Assert.That(arguments[at + 1]).IsEqualTo("default");
    }

    [Test]
    public async Task The_allowlist_is_still_derived_from_the_moves()
    {
        // THE OTHER HALF, UNMOVED. The mode stops the file widening things; the
        // allowlist is what the envelope actually said. Neither replaces the
        // other, and MoveBoundProbe proves the pair every session rather than
        // either being assumed.
        var arguments = Arguments().ToList();
        var at = arguments.IndexOf("--allowedTools");

        await Assert.That(at).IsGreaterThanOrEqualTo(0);
        await Assert.That(arguments.Skip(at + 1).Any(a => a.Contains("Read", StringComparison.Ordinal)))
            .IsTrue()
            .Because("read was the only move declared. Passed: " + string.Join(" ", arguments));
    }
}
