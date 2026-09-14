using Gg.Contracts;
using Gg.Runner.Execution;

namespace Gg.Runner.Tests;

/// <summary>
/// A flight's working directory is the flight's own, and the prompt says which
/// repository is in which subdirectory.
/// </summary>
/// <remarks>
/// <para>
/// <b>The agent was dropped inside one tree and told to stay there.</b>
/// <c>WorkingDirectory</c> was <c>Trees[0].Path</c> and the task ended <i>"Stay
/// in this working tree"</i>, so a flight that cloned two repositories handed an
/// agent one and never mentioned the other. GG-102 is what that costs:
/// <c>score-hal</c>'s rubric lives in one repository and the work item's code in
/// another, and the agent stopped because <i>"this flight is pinned to the
/// JDNext working tree"</i>.
/// </para>
/// <para>
/// <b>A DIRECTORY OF HASHES IS NOT SELF-DESCRIBING, which is why naming them is
/// the whole of this.</b> <c>WorkingTreeRoot</c> puts each tree at
/// <c>&lt;flight&gt;/&lt;fingerprint(slug)&gt;</c> — sixteen hex characters,
/// deliberately, because <i>"a slug … never as a directory name"</i>. An agent
/// started at the flight root sees <c>3f2a9c1d4e5b6a78/</c> and
/// <c>9c8b7a6d5e4f3210/</c> and cannot tell which is the one it was asked
/// about. The prompt has to map them or the move makes things worse.
/// </para>
/// <para>
/// <b>This moves the ground under every flight that exists</b>, which is the
/// cost that was accepted when the shape was chosen: an agent that used to
/// start inside a checkout now starts one level above it. Saying where the
/// checkout is, in the same breath, is what keeps that from being a regression.
/// </para>
/// </remarks>
public class AnAgentIsToldWhereEveryTreeIsTests
{
    private static ExecutorRequest Request(params (string Slug, string Path)[] trees) => new()
    {
        WorkingDirectory = "/tmp/gg-tree/flight",
        LoopId = "score",
        IntentProvider = "a-tracker",
        IntentId = "18001",
        Moves = [LoopMoves.Read],
        WallClock = TimeSpan.FromMinutes(30),
        TranscriptPath = "/tmp/gg-transcript.ndjson",
        Trees = [.. trees.Select(t => new RequestedTree(t.Slug, t.Path))],
    };

    [Test]
    public async Task Every_tree_is_named_with_the_slug_a_person_would_recognise()
    {
        var prompt = ClaudeCodeExecutor.PromptFor(Request(
            ("JDX/JDNext", "/tmp/gg-tree/flight/3f2a9c1d4e5b6a78"),
            ("JDX/agile-cortex", "/tmp/gg-tree/flight/9c8b7a6d5e4f3210")));

        await Assert.That(prompt).Contains("JDX/JDNext");
        await Assert.That(prompt).Contains("JDX/agile-cortex");
    }

    [Test]
    public async Task Every_tree_is_named_with_the_directory_it_is_actually_in()
    {
        // THE PATH, NOT ONLY THE NAME. A slug an agent cannot turn into a
        // directory is a slug it has to guess at, and the directory is a hash
        // of that very slug - so guessing is not available to it.
        var prompt = ClaudeCodeExecutor.PromptFor(Request(
            ("JDX/JDNext", "/tmp/gg-tree/flight/3f2a9c1d4e5b6a78"),
            ("JDX/agile-cortex", "/tmp/gg-tree/flight/9c8b7a6d5e4f3210")));

        await Assert.That(prompt).Contains("3f2a9c1d4e5b6a78");
        await Assert.That(prompt).Contains("9c8b7a6d5e4f3210");
    }

    [Test]
    public async Task A_single_tree_is_named_too_rather_than_assumed()
    {
        // THE COMMONEST FLIGHT, and the one this change is most able to break.
        // It used to START inside its checkout; now it starts above it, so the
        // one repository has to be named as explicitly as two would be.
        var prompt = ClaudeCodeExecutor.PromptFor(
            Request(("JDX/JDNext", "/tmp/gg-tree/flight/3f2a9c1d4e5b6a78")));

        await Assert.That(prompt).Contains("JDX/JDNext");
        await Assert.That(prompt).Contains("3f2a9c1d4e5b6a78");
    }

    [Test]
    public async Task A_flight_with_no_repository_says_nothing_about_trees()
    {
        // A TICKET, A LINK AND A TYPED SENTENCE ALL RESOLVE TO NO REPOSITORY,
        // correctly - WorkspaceResult.Root exists for exactly that case. A
        // prompt listing an empty set would tell an agent to look somewhere
        // that is not there.
        var prompt = ClaudeCodeExecutor.PromptFor(Request());

        await Assert.That(prompt).DoesNotContain("is checked out at");
    }

    [Test]
    public async Task The_task_no_longer_claims_there_is_one_working_tree()
    {
        // "Stay in this working tree" WAS TRUE AND IS NOT. It named a place the
        // agent was standing in; the agent now stands above several, and a
        // sentence that still said it would be telling it not to open the only
        // repository it was given.
        var prompt = ClaudeCodeExecutor.PromptFor(Request(
            ("JDX/JDNext", "/tmp/gg-tree/flight/3f2a9c1d4e5b6a78"),
            ("JDX/agile-cortex", "/tmp/gg-tree/flight/9c8b7a6d5e4f3210")));

        await Assert.That(prompt).DoesNotContain("Stay in this working tree");
    }
}
