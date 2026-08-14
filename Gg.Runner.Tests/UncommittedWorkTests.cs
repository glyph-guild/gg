using Gg.Contracts;
using Gg.Runner.Facts;
using Gg.Runner.Vcs;

namespace Gg.Runner.Tests;

/// <summary>
/// A tree in the state the extractor really meets it in: the agent has edited it
/// and nothing has committed yet.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every other extraction fixture commits first, and that is the defect.</b>
/// <c>DiffFixture</c> and <c>AttemptFixture</c> both commit and push before they
/// extract, so the manifest they measure is a manifest of committed work - and the
/// runner's real order is materialize → invoke → EXTRACT → ship → land, with the
/// commit happening inside the destination adapter's push, after the facts have
/// already gone. This fixture leaves the tree dirty, which is what
/// <see cref="ChangeExtractor"/> is actually handed.
/// </para>
/// <para>
/// Real git, and real uncommitted edits of both shapes an agent produces: a
/// modification to a tracked file, and a new file git has never seen.
/// </para>
/// </remarks>
internal sealed class UncommittedWorkFixture : IDisposable
{
    /// <summary>What the agent adds. Untracked, which is the commonest shape of new work.</summary>
    internal const string NewMigration = "migrations/0002_add_discount_to_orders.sql";

    /// <summary>What the agent edits. Tracked, and unchanged between the two commits.</summary>
    internal const string EditedSource = "src/greet.py";

    /// <summary>What separates the base from the head BEFORE the agent touches anything.</summary>
    internal const string PreExisting = "src/somebody_elses_work.py";

    private readonly string _root;
    private readonly string _bare;
    private readonly string _trees;

    internal UncommittedWorkFixture()
    {
        _root = Path.Combine(Path.GetTempPath(), "gg-uncommitted-fixture", Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(_root);

        _bare = Path.Combine(_root, "widgets.git");
        _trees = Path.Combine(_root, "trees");
        var work = Path.Combine(_root, "work");

        GitFixture.Run(_root, "init", "--bare", "--initial-branch=main", _bare);
        GitFixture.Run(_root, "clone", _bare, work);

        Write(work, EditedSource, "def greet(name):\n    return \"Hello \" + name\n");
        Write(work, "migrations/0001_init.sql", "create table orders (id int);\n");
        GitFixture.Run(work, "add", ".");
        GitFixture.Run(work, "commit", "-m", "base");
        GitFixture.Run(work, "push", "origin", "main");

        // A second commit on a branch, so a flight pinned to it has a base that
        // is genuinely behind - somebody else's work, which the agent did not do.
        GitFixture.Run(work, "checkout", "-b", "feature");
        Write(work, PreExisting, "def unrelated():\n    return 1\n");
        GitFixture.Run(work, "add", ".");
        GitFixture.Run(work, "commit", "-m", "somebody else's work");
        GitFixture.Run(work, "push", "origin", "feature");
    }

    /// <summary>The tree as the runner materializes it, before anybody has worked in it.</summary>
    internal Materialized Materialize(string pinnedRef, string baseRef) =>
        new Materializer(new LocalVcsAdapter(_root), new WorkingTreeRoot(_trees))
            .MaterializeAsync(
                "flight-1",
                new RepoTarget
                {
                    Provider = LocalVcsAdapter.ProviderKey,
                    Slug = _bare,
                    PinnedRef = pinnedRef,
                    BaseRef = baseRef,
                },
                secret: null)
            .GetAwaiter().GetResult();

    /// <summary>
    /// What an agent does to a tree, and stops there.
    /// </summary>
    /// <remarks>
    /// No <c>git add</c> and no commit, because the agent does neither. The
    /// destination adapter commits during the push, which happens after the facts
    /// have shipped.
    /// </remarks>
    internal static void TheAgentWorks(Materialized tree)
    {
        Write(tree.Path, NewMigration,
            "alter table orders add column discount numeric;\n"
          + "-- down: alter table orders drop column discount;\n");

        Write(tree.Path, EditedSource,
            "def greet(name):\n    \"\"\"Greet somebody.\"\"\"\n    return \"Hello \" + name\n");
    }

    /// <summary>What git says the agent did, in the plainest form there is.</summary>
    internal static string GitStatus(Materialized tree) =>
        GitFixture.Run(tree.Path, "status", "--porcelain");

    private static void Write(string root, string relative, string content)
    {
        var path = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}

/// <summary>
/// The manifest must describe the agent's work, and it is the agent's work that is
/// uncommitted.
/// </summary>
/// <remarks>
/// <b>Everything slice three reads off a manifest rests on this.</b> The
/// <c>in-scope</c> verdict, <c>when:</c> self-attachment, the evidence-manifest hash
/// that scopes an approval and the payload a person decides from are all statements
/// about what the agent changed. A manifest measured between two commits that
/// predate the agent's edits is a true statement about a different question.
/// </remarks>
public class UncommittedWorkTests
{
    [Test]
    public async Task An_implement_flights_manifest_names_what_the_agent_edited()
    {
        // THE SHAPE A REAL FLIGHT HAS. `tree/main` pins refs/heads/main, so the
        // head and the base are the same commit and the only difference in that
        // tree is what the agent did.
        using var fixture = new UncommittedWorkFixture();
        var tree = fixture.Materialize("refs/heads/main", "refs/heads/main");

        UncommittedWorkFixture.TheAgentWorks(tree);

        var manifest = ChangeExtractor.Extract(tree, ClassificationRules.Default);

        await Assert.That(manifest).IsNotNull()
            .Because("a flight with a base produced work, so there is a change to report.");
        await Assert.That(manifest!.Paths.Select(p => p.Path))
            .Contains(UncommittedWorkFixture.EditedSource);
        await Assert.That(manifest.Paths.Select(p => p.Path))
            .Contains(UncommittedWorkFixture.NewMigration)
            .Because("a new file is the agent's work, and `when: touches migrations/**` "
                   + "is the gate this whole slice exists for.");
    }

    [Test]
    public async Task A_manifest_reports_the_agents_work_and_not_the_difference_it_was_handed()
    {
        // The same defect where it is visible rather than empty: a flight pinned
        // to a branch that is ahead of its base. The commit-to-commit diff has
        // something in it, and none of it is the agent's.
        using var fixture = new UncommittedWorkFixture();
        var tree = fixture.Materialize("refs/heads/feature", "refs/heads/main");

        UncommittedWorkFixture.TheAgentWorks(tree);

        var manifest = ChangeExtractor.Extract(tree, ClassificationRules.Default)!;

        await Assert.That(manifest.Paths.Select(p => p.Path))
            .Contains(UncommittedWorkFixture.NewMigration);
        await Assert.That(manifest.Paths.Select(p => p.Path))
            .Contains(UncommittedWorkFixture.EditedSource);
    }

    [Test]
    public async Task What_the_manifest_reports_instead_is_recorded_here()
    {
        // Not an assertion about correctness - a record of the reading the broken
        // instrument gives, so the repair can be compared against something.
        using var fixture = new UncommittedWorkFixture();

        foreach (var (pinned, @base) in ((string, string)[])
                 [("refs/heads/main", "refs/heads/main"), ("refs/heads/feature", "refs/heads/main")])
        {
            var shape = fixture.Materialize(pinned, @base);
            UncommittedWorkFixture.TheAgentWorks(shape);
            var reading = ChangeExtractor.Extract(shape, ClassificationRules.Default)!;

            Console.WriteLine($"--- pinned {pinned}, base {@base}");
            Console.WriteLine("git status --porcelain:");
            Console.WriteLine(UncommittedWorkFixture.GitStatus(shape));
            Console.WriteLine($"manifest.filesChanged: {reading.FilesChanged}");
            Console.WriteLine("manifest.paths: "
                + string.Join(", ", reading.Paths.Select(p => $"{p.Change} {p.Path}")));
        }

        var tree = fixture.Materialize("refs/heads/feature", "refs/heads/main");
        UncommittedWorkFixture.TheAgentWorks(tree);
        var manifest = ChangeExtractor.Extract(tree, ClassificationRules.Default)!;

        await Assert.That(manifest.Paths.Select(p => p.Path))
            .DoesNotContain(UncommittedWorkFixture.PreExisting)
            .Because("work the agent did not do must not be reported as work it did.");
    }
}
