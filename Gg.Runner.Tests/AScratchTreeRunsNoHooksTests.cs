using Gg.Runner.Vcs;

namespace Gg.Runner.Tests;

/// <summary>
/// A flight's working tree is scratch that gg materialized, and the hooks a
/// package install writes into it are not the tenant's instruction to gg.
/// </summary>
/// <remarks>
/// <para>
/// <b>Found by GG-268, the first <c>ui-preview</c> flight.</b> The agent fixed its
/// work item, built the app and published a preview; gg then could not commit, and
/// the flight ended holding work it could not propose:
/// </para>
/// <code>
/// src/JDX.Web/.husky/pre-commit: 19: npx: not found
/// husky - pre-commit script failed (code 1)
/// </code>
/// <para>
/// <b>Every step of how that happened is gg working as designed.</b>
/// <see cref="GitWorkingTree"/> materializes with a fresh <c>git init</c>, so the
/// tree begins with no hooks at all. <see cref="GitInvocation.RunAsync"/> then
/// points <c>GIT_CONFIG_GLOBAL</c> and <c>GIT_CONFIG_SYSTEM</c> at
/// <c>/dev/null</c> and sets <c>GIT_CONFIG_NOSYSTEM</c>, so the operator's own
/// config cannot reach a flight. None of that touches the tree's OWN
/// <c>.git/config</c> — and the agent's work required <c>npm install</c>, whose
/// <c>prepare</c> script wrote <c>core.hooksPath</c> into exactly that file.
/// </para>
/// <para>
/// <b>So gg isolated git from the machine and left one door open</b>, and a
/// package install walked through it. The hook then ran against a host that has
/// no <c>npx</c> and no <c>pwsh</c>, because a runner is not a developer
/// workstation and was never meant to be one.
/// </para>
/// <para>
/// <b>Why this is gg's to fix rather than the tenant's.</b> A repository's hooks
/// exist to stop a person committing something; they assume that person's laptop,
/// with that repository's toolchain installed. gg's commit is not that act. It is
/// the last step of carrying an agent's work to a branch nobody has reviewed yet,
/// where the gate is the pull request and the checks that run on it. A tenant who
/// wants its hooks honoured can say so; a tenant should not have to say anything
/// to stop a scratch tree reaching back into the machine.
/// </para>
/// <para>
/// <b>These assert on this disk rather than on the outcome</b>, for the reason
/// <see cref="DestinationCommitTests"/> gives: the remote is unreachable on
/// purpose, so the push fails, and the push is not the subject. Whether the work
/// was committed by the time the push was attempted is a fact about this disk.
/// </para>
/// </remarks>
public class AScratchTreeRunsNoHooksTests
{
    /// <summary>The file the agent is standing in for having written.</summary>
    private const string AgentsFile = "the-agent-wrote-this.txt";

    private const string Branch = "gg/flight-1";

    /// <summary>
    /// A tree with an uncommitted change and a refusing <c>pre-commit</c> hook
    /// reached through the tree's own <c>core.hooksPath</c> — the state
    /// <c>npm install</c> leaves a flight's tree in when the repository uses husky.
    /// </summary>
    private sealed class HookedTree : IDisposable
    {
        internal string Directory { get; }

        /// <summary>What the tree was sitting at before the agent touched it.</summary>
        internal string BaseCommit { get; }

        /// <summary>
        /// Written by the hook if the hook ever runs. Its absence is the assertion
        /// that matters: a commit that succeeds because the hook passed would look
        /// identical to one that succeeded because the hook never ran.
        /// </summary>
        private string MarkerPath { get; }

        internal bool HookRan => File.Exists(MarkerPath);

        internal HookedTree()
        {
            Directory = Path.Combine(
                Path.GetTempPath(), "gg-scratch-tree-hooks", Guid.NewGuid().ToString("n"));
            System.IO.Directory.CreateDirectory(Directory);
            MarkerPath = Path.Combine(Directory, "the-hook-ran");

            GitFixture.Run(Directory, "init", "--initial-branch=main", Directory);
            File.WriteAllText(Path.Combine(Directory, "README.md"), "widgets\n");
            GitFixture.Run(Directory, "add", ".");
            GitFixture.Run(Directory, "-c", "user.name=t", "-c", "user.email=t@t", "commit", "-m", "base");
            BaseCommit = Head();

            PlantTheHook();

            // THE AGENT'S WORK, left as an agent leaves it: written, not added and
            // not committed.
            File.WriteAllText(Path.Combine(Directory, AgentsFile), "the change\n");
        }

        /// <summary>
        /// Plants a refusing hook the way husky does: a directory of hooks, named
        /// by <c>core.hooksPath</c> in the tree's own config. Husky writes
        /// <c>.husky/_</c>; the name is not the point and a neutral one is used so
        /// this test is about any repository's hooks rather than about one tool.
        /// </summary>
        private void PlantTheHook()
        {
            var hooks = Path.Combine(Directory, "tree-hooks");
            System.IO.Directory.CreateDirectory(hooks);

            var hook = Path.Combine(hooks, "pre-commit");
            File.WriteAllText(
                hook,
                "#!/bin/sh\n"
                + $"echo ran > '{MarkerPath}'\n"
                + "echo 'pre-commit: npx: not found' >&2\n"
                + "exit 1\n");

            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(
                    hook,
                    UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
                    | UnixFileMode.GroupRead | UnixFileMode.GroupExecute
                    | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
            }

            // IN THE TREE'S OWN CONFIG, which is the whole point: the fixture and
            // the runner both neutralize global and system config, and neither
            // reaches this.
            GitFixture.Run(Directory, "config", "core.hooksPath", hooks);
        }

        internal string Head() =>
            GitFixture.Run(Directory, "rev-parse", "HEAD").Trim();

        /// <summary>Everything the current commit actually contains.</summary>
        internal string CommittedFiles() =>
            GitFixture.Run(Directory, "ls-tree", "--name-only", "-r", "HEAD");

        public void Dispose()
        {
            try
            {
                System.IO.Directory.Delete(Directory, recursive: true);
            }
            catch (IOException)
            {
                // A temp directory that outlives the run is not a failed test.
            }
        }
    }

    private static LandingRequest Request(HookedTree tree) => new()
    {
        WorkingDirectory = tree.Directory,
        Slug = "acme/widgets",
        Branch = Branch,
        BaseRef = "main",
        Title = "A change",
        Secret = "not-a-real-credential",
    };

    /// <summary>
    /// Unreachable on purpose. <c>.invalid</c> is reserved by RFC 2606 and resolves
    /// nowhere, so this cannot accidentally reach a host somebody owns.
    /// </summary>
    private static RefNamedDestinationAdapter PathScoped() =>
        new("forge", "unreachable.invalid", new HttpClient());

    /// <summary>Attempts the push and discards how it went; see the remarks.</summary>
    private static async Task AttemptAsync(IDestinationAdapter destination, LandingRequest request)
    {
        try
        {
            _ = await destination.PushAsync(request, CancellationToken.None);
        }
        catch (InvalidOperationException)
        {
            // The remote is unreachable by construction.
        }
    }

    [Test]
    public async Task A_refusing_hook_in_the_trees_own_config_does_not_stop_the_commit()
    {
        // THE DEFECT, as GG-268 met it. The hook exits 1, git exits 1, and
        // GitCommit.ForPushAsync reports "could not be committed before pushing" -
        // so the agent's work never reaches a branch and the flight ends holding
        // it.
        using var tree = new HookedTree();

        await AttemptAsync(PathScoped(), Request(tree));

        await Assert.That(tree.Head()).IsNotEqualTo(tree.BaseCommit)
            .Because("a repository's pre-commit hook is written for a person at a workstation; "
                   + "letting it decide whether an agent's work can be carried to a branch puts "
                   + "the flight at the mercy of a toolchain the runner was never meant to have.");
        await Assert.That(tree.CommittedFiles()).Contains(AgentsFile)
            .Because("carrying the agent's work is the entire point of a destination.");
    }

    [Test]
    public async Task The_hook_is_not_run_at_all()
    {
        // The assertion that says WHY the one above passes. A commit that succeeded
        // because the hook happened to pass would be indistinguishable from one
        // that succeeded because gg never ran it - and only the second is the
        // decision. The marker is written by the hook's first line, before it
        // refuses, so it records that the hook was entered rather than how it ended.
        using var tree = new HookedTree();

        await AttemptAsync(PathScoped(), Request(tree));

        await Assert.That(tree.HookRan).IsFalse()
            .Because("gg already points global and system git config at nothing so that a "
                   + "developer's machine cannot change what a flight does; a tree's own hooks "
                   + "are the same class of thing, reaching the same flight from one door over.");
    }

    [Test]
    public async Task The_slug_shaped_destination_runs_no_hooks_either()
    {
        // THE TWIN. Both adapters commit, so both have to answer this the same way;
        // a fix on one of them is how the commit step itself came to differ by forge
        // once already - see DestinationCommitTests.
        using var tree = new HookedTree();

        await AttemptAsync(
            new HttpsDestinationAdapter("forge", "unreachable.invalid", new HttpClient()), Request(tree));

        await Assert.That(tree.HookRan).IsFalse();
        await Assert.That(tree.CommittedFiles()).Contains(AgentsFile);
    }
}
