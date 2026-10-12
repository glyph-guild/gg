using System.Text.Json;
using Gg.Local;

namespace Gg.Cli.Tests;

/// <summary>
/// The tool a flight reads a repository's history with: <c>git log</c>,
/// <c>show</c>, <c>blame</c> and <c>diff</c>, run by this server where the
/// repository lives, and nothing else.
/// </summary>
/// <remarks>
/// <para>
/// <b>Read-only by construction, not by request.</b> The subcommand is one of
/// four, the repository is a name resolved under the tree this server was
/// handed, and the two flags that make a read write a file
/// (<c>--output</c>, <c>--ext-diff</c>) are refused by name. An agent that
/// asks for <c>push</c> is told what the tool is for, in a result it can read,
/// rather than handed a protocol error.
/// </para>
/// <para>
/// <b>A child, never a library.</b> git is run as the process a person would
/// run, in the repository's own directory, with no pager and no prompt. The
/// delegate is handed in - the server is a function of what it is given - so
/// these tests see the exact command line and directory without a repository
/// on disk, and one test runs the real child against a real repository so the
/// seam is proven on both sides.
/// </para>
/// </remarks>
public class AnAgentReadsTheRepositorysHistoryTests
{
    private sealed record Ran(IReadOnlyList<string> Arguments, string WorkingDirectory);

    private static async Task<(JsonDocument Answer, List<Ran> Ran)> CallAsync(
        string? treeRoot, object? arguments, PullReport? report = null)
    {
        var ran = new List<Ran>();
        var output = new StringWriter();

        await PlatformToolServer.RunAsync(
            new StringReader(Call(HistoryTool.Name, arguments)), output,
            intentPath: null, documentRoot: null,
            treeRoot: treeRoot,
            history: (args, directory) =>
            {
                ran.Add(new Ran(args, directory));
                return report ?? new PullReport { Started = true, ExitCode = 0, Said = "abc123 first" };
            });

        return (JsonDocument.Parse(output.ToString().Trim()), ran);
    }

    private static string Call(string tool, object? arguments) => JsonSerializer.Serialize(new
    {
        jsonrpc = "2.0",
        id = 7,
        method = "tools/call",
        @params = new { name = tool, arguments = arguments ?? new Dictionary<string, object>() },
    });

    private static string Said(JsonDocument answer) =>
        answer.RootElement.GetProperty("result").GetProperty("content")[0]
            .GetProperty("text").GetString() ?? "";

    private static bool Failed(JsonDocument answer) =>
        answer.RootElement.GetProperty("result").TryGetProperty("isError", out var flag)
        && flag.GetBoolean();

    private static string ATree(params string[] repositories)
    {
        var tree = Path.Combine(Path.GetTempPath(), "gg-history-" + Guid.NewGuid().ToString("N"));
        foreach (var repository in repositories)
        {
            // A REPOSITORY HAS A .git. A flight's tree also holds its scratch
            // directory, which is not one.
            Directory.CreateDirectory(Path.Combine(tree, repository, ".git"));
        }

        return tree;
    }

    [Test]
    public async Task A_directory_with_no_git_in_it_is_not_a_repository()
    {
        // GG-1084 WAS ASKED TO CHOOSE between its repository and its scratch
        // directory, and recovered by naming the repository. A directory with
        // no .git is not a choice, by default or by name.
        var tree = ATree("5b2c9211f718b10f");
        Directory.CreateDirectory(Path.Combine(tree, "scratch"));

        var (answer, ran) = await CallAsync(tree, new { command = "log", arguments = new[] { "-1" } });

        await Assert.That(Failed(answer)).IsFalse().Because(Said(answer));
        await Assert.That(ran[0].WorkingDirectory).IsEqualTo(Path.Combine(tree, "5b2c9211f718b10f"))
            .Because("the one repository is the default, and scratch is not a second one.");

        var (named, ranNamed) = await CallAsync(tree, new { repository = "scratch", command = "log" });

        await Assert.That(Failed(named)).IsTrue().Because("scratch holds no .git, so it is nothing to read.");
        await Assert.That(ranNamed).IsEmpty();
    }

    [Test]
    public async Task A_fleet_flight_is_offered_it_and_a_drafting_session_is_not()
    {
        var flight = await OfferedAsync(documentRoot: null);
        var drafting = await OfferedAsync(documentRoot: "/tmp/airspace");

        await Assert.That(flight).Contains(HistoryTool.Name)
            .Because("the envelope decides whether a loop is granted it; the server's job is "
                   + "to offer it to every fleet flight, as it offers propose_landing.");
        await Assert.That(drafting.Contains(HistoryTool.Name)).IsFalse()
            .Because("a drafting session has an airspace working copy and no repository tree.");
    }

    [Test]
    public async Task Log_runs_git_in_the_named_repository_with_no_pager()
    {
        var tree = ATree("5b2c9211f718b10f");

        var (answer, ran) = await CallAsync(tree, new
        {
            repository = "5b2c9211f718b10f",
            command = "log",
            arguments = new[] { "--oneline", "--grep", "AB#17860" },
        });

        await Assert.That(Failed(answer)).IsFalse().Because(Said(answer));
        await Assert.That(Said(answer)).Contains("abc123 first");
        await Assert.That(ran).Count().IsEqualTo(1);
        await Assert.That(ran[0].WorkingDirectory).IsEqualTo(Path.Combine(tree, "5b2c9211f718b10f"))
            .Because("git runs where the repository lives, so a Bash rule's inability to "
                   + "reach below the working directory never arises.");
        await Assert.That(ran[0].Arguments).IsEquivalentTo(
            new[] { "--no-pager", "log", "--no-ext-diff", "--oneline", "--grep", "AB#17860" })
            .Because("no pager, because there is no terminal; no external diff, because a "
                   + "repository's configuration must not choose what runs.");
    }

    [Test]
    public async Task Blame_takes_no_diff_flag()
    {
        var tree = ATree("repo");

        var (_, ran) = await CallAsync(tree, new
        {
            repository = "repo", command = "blame", arguments = new[] { "-L", "41,47", "a.html" },
        });

        await Assert.That(ran[0].Arguments).IsEquivalentTo(
            new[] { "--no-pager", "blame", "-L", "41,47", "a.html" })
            .Because("--no-ext-diff is a diff option and blame refuses it.");
    }

    [Test]
    public async Task The_one_repository_in_the_tree_is_the_default()
    {
        var tree = ATree("only");

        var (answer, ran) = await CallAsync(tree, new { command = "log", arguments = new[] { "-1" } });

        await Assert.That(Failed(answer)).IsFalse().Because(Said(answer));
        await Assert.That(ran[0].WorkingDirectory).IsEqualTo(Path.Combine(tree, "only"))
            .Because("almost every flight has one repository, and naming it is noise.");
    }

    [Test]
    public async Task Two_repositories_and_no_name_is_refused_naming_both()
    {
        var tree = ATree("alpha", "beta");

        var (answer, ran) = await CallAsync(tree, new { command = "log" });

        await Assert.That(Failed(answer)).IsTrue();
        await Assert.That(Said(answer)).Contains("alpha").And.Contains("beta")
            .Because("the refusal names what it could not choose between.");
        await Assert.That(ran).IsEmpty();
    }

    [Test]
    public async Task A_repository_outside_the_tree_is_refused()
    {
        var tree = ATree("repo");

        foreach (var outside in new[] { "../elsewhere", "/etc", "repo/../..", "nope" })
        {
            var (answer, ran) = await CallAsync(tree, new { repository = outside, command = "log" });

            await Assert.That(Failed(answer)).IsTrue()
                .Because($"'{outside}' is not a repository in this tree. Said: {Said(answer)}");
            await Assert.That(ran).IsEmpty()
                .Because("nothing runs until the repository has been resolved under the tree.");
        }
    }

    [Test]
    public async Task Only_the_four_reading_subcommands_are_accepted()
    {
        var tree = ATree("repo");

        foreach (var command in new[] { "push", "commit", "checkout", "reset", "status", "" })
        {
            var (answer, ran) = await CallAsync(tree, new { repository = "repo", command });

            await Assert.That(Failed(answer)).IsTrue().Because($"'{command}' is not a read.");
            await Assert.That(Said(answer)).Contains("log").And.Contains("show")
                .And.Contains("blame").And.Contains("diff")
                .Because("the refusal says what the tool is for.");
            await Assert.That(ran).IsEmpty();
        }
    }

    [Test]
    public async Task A_flag_that_writes_a_file_is_refused()
    {
        var tree = ATree("repo");

        foreach (var flag in new[] { "--output=/tmp/x", "--output", "--ext-diff" })
        {
            var (answer, ran) = await CallAsync(tree, new
            {
                repository = "repo", command = "diff", arguments = new[] { flag, "HEAD~1" },
            });

            await Assert.That(Failed(answer)).IsTrue().Because($"'{flag}' makes a read write.");
            await Assert.That(ran).IsEmpty();
        }
    }

    [Test]
    public async Task A_failed_git_is_an_error_result_in_gits_own_words()
    {
        var tree = ATree("repo");

        var (answer, _) = await CallAsync(tree, new { repository = "repo", command = "show", arguments = new[] { "nope" } },
            new PullReport { Started = true, ExitCode = 128, Said = "fatal: bad revision 'nope'" });

        await Assert.That(Failed(answer)).IsTrue();
        await Assert.That(Said(answer)).Contains("fatal: bad revision 'nope'")
            .Because("an agent can act on git's refusal; it cannot act on a blank.");
    }

    [Test]
    public async Task Long_output_is_cut_and_says_so()
    {
        var tree = ATree("repo");
        var long_ = new string('x', HistoryTool.MaxOutput + 500);

        var (answer, _) = await CallAsync(tree, new { repository = "repo", command = "log" },
            new PullReport { Started = true, ExitCode = 0, Said = long_ });

        await Assert.That(Said(answer).Length).IsLessThan(HistoryTool.MaxOutput + 200);
        await Assert.That(Said(answer)).Contains("cut")
            .Because("an agent told its output was truncated narrows the query; one not told "
                   + "reasons from a log it believes is whole.");
    }

    [Test]
    public async Task Without_a_tree_it_says_the_wiring_is_missing()
    {
        var (answer, ran) = await CallAsync(treeRoot: null, new { repository = "repo", command = "log" });

        await Assert.That(Failed(answer)).IsTrue();
        await Assert.That(Said(answer)).Contains("gg")
            .Because("a server started without a tree is a wiring fault in gg, and the agent "
                   + "is told so rather than left to retry.");
        await Assert.That(ran).IsEmpty();
    }

    [Test]
    public async Task The_real_child_reads_a_real_repository()
    {
        // THE SEAM, PROVEN FROM THE OTHER SIDE. Every test above hands in a
        // fake; this one runs the git this machine has, in a repository it
        // just made, through the delegate Program.cs wires.
        var tree = ATree("repo");
        var repository = Path.Combine(tree, "repo");

        foreach (var arguments in new[]
                 {
                     new[] { "init", "-q" },
                     new[] { "-c", "user.email=t@example.invalid", "-c", "user.name=t", "commit", "-q", "--allow-empty", "-m", "AB#17860 deferred two sites" },
                 })
        {
            var started = GitHistoryChild.Run(arguments, repository);
            await Assert.That(started.ExitCode).IsEqualTo(0).Because(started.Said);
        }

        var output = new StringWriter();
        await PlatformToolServer.RunAsync(
            new StringReader(Call(HistoryTool.Name, new
            {
                repository = "repo", command = "log", arguments = new[] { "--oneline", "--grep", "AB#17860" },
            })),
            output, intentPath: null, documentRoot: null, treeRoot: tree, history: GitHistoryChild.Run);

        using var answer = JsonDocument.Parse(output.ToString().Trim());
        await Assert.That(Failed(answer)).IsFalse().Because(Said(answer));
        await Assert.That(Said(answer)).Contains("AB#17860 deferred two sites");
    }

    private static async Task<IReadOnlyList<string>> OfferedAsync(string? documentRoot)
    {
        var output = new StringWriter();
        await PlatformToolServer.RunAsync(
            new StringReader("""{"jsonrpc":"2.0","id":1,"method":"tools/list","params":{}}"""),
            output, intentPath: null, documentRoot: documentRoot);

        using var answer = JsonDocument.Parse(output.ToString().Trim());
        return [.. answer.RootElement.GetProperty("result").GetProperty("tools")
            .EnumerateArray().Select(tool => tool.GetProperty("name").GetString()!)];
    }
}
