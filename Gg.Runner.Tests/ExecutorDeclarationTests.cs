using System.Text.RegularExpressions;
using Gg.Local;

namespace Gg.Runner.Tests;

/// <summary>
/// <c>GG_EXECUTOR_BINARY</c> declares which agent a machine has, not only where
/// its binary is — and one parser reads it for everybody.
/// </summary>
/// <remarks>
/// <para>
/// <b>The variable was a bare path, and a bare path names no agent.</b> Four
/// readers took it as one: the executor, the allowance meter's refresh, the
/// hand-flight's refusal and the doctor. Every one of them assumed
/// <c>claude</c>, and nothing said so — so an adapter for a second CLI tool
/// had nowhere to be chosen, and a login adapter for the first had nowhere to
/// learn which tool it was for.
/// </para>
/// <para>
/// <b><c>HostDeclaration</c> is the shape, down to the refusal.</b> A
/// <c>key=value</c> entry, refused with a diagnosis naming the variable when it
/// is malformed — and the bare form kept, because every unit on every host
/// today is written that way and <i>"a deploy silently unbounding every
/// envelope that exists"</i> is this repository's own name for a compatibility
/// break.
/// </para>
/// <para>
/// <b>One parser, in <c>Gg.Local</c>, because two projects that cannot see
/// each other both read it.</b> <c>Gg.Client</c>'s doctor and <c>Gg.Runner</c>'s
/// executor both see <c>Gg.Local</c> and neither sees the other; a copy in each
/// is <i>"the two-computations problem this file's own history records"</i>
/// (<c>HostDeclaration</c>), one variable over.
/// </para>
/// </remarks>
public partial class ExecutorDeclarationTests
{
    [Test]
    public async Task A_bare_path_is_the_claude_agent_at_that_path()
    {
        // COMPATIBILITY, and it is load-bearing: vmlinux001's three units and
        // deploy/member-browser/Dockerfile all say GG_EXECUTOR_BINARY=/a/path.
        var declared = ExecutorDeclaration.Parse(
            "/home/gg/.local/bin/claude", ExecutorDeclaration.Variable);

        await Assert.That(declared.Agent).IsEqualTo("claude");
        await Assert.That(declared.Binary).IsEqualTo("/home/gg/.local/bin/claude");
    }

    [Test]
    public async Task A_keyed_entry_names_the_agent_and_its_binary()
    {
        var declared = ExecutorDeclaration.Parse(
            "claude=/usr/local/bin/claude", ExecutorDeclaration.Variable);

        await Assert.That(declared.Agent).IsEqualTo("claude");
        await Assert.That(declared.Binary).IsEqualTo("/usr/local/bin/claude");
    }

    [Test]
    public async Task An_agent_nobody_has_an_adapter_for_is_refused_naming_the_entry()
    {
        // NOT ACCEPTED AND NOT ASSUMED. A key this build cannot dispatch would
        // otherwise become claude by fallback, which is the assumption this
        // type exists to stop being silent.
        var refused = Assert.Throws<InvalidOperationException>(() =>
            ExecutorDeclaration.Parse("codex=/usr/bin/codex", ExecutorDeclaration.Variable));

        await Assert.That(refused.Message).Contains(ExecutorDeclaration.Variable)
            .Because("the refusal names the variable, or a person reads it as a "
                   + "statement about the machine rather than about one setting.");
        await Assert.That(refused.Message).Contains("codex")
            .Because("and names the entry, so they know which word to change.");
        await Assert.That(refused.Message).Contains("claude")
            .Because("and says what IS known, which is the whole of the fix.");
    }

    [Test]
    public async Task An_entry_with_an_empty_half_is_refused()
    {
        foreach (var entry in new[] { "claude=", "=/usr/bin/claude", "" })
        {
            var refused = Assert.Throws<InvalidOperationException>(() =>
                ExecutorDeclaration.Parse(entry, ExecutorDeclaration.Variable));

            await Assert.That(refused.Message).Contains(ExecutorDeclaration.Variable)
                .Because($"'{entry}' names no agent or no binary, and the refusal says which "
                       + "variable to fix.");
        }
    }

    [Test]
    public async Task The_variable_is_spelled_in_one_place()
    {
        // Both readers cite it from here, so the doctor and the runner cannot
        // spell it two ways.
        await Assert.That(ExecutorDeclaration.Variable).IsEqualTo("GG_EXECUTOR_BINARY");
        await Assert.That(Execution.ExecutorConfiguration.BinaryVariable)
            .IsEqualTo(ExecutorDeclaration.Variable);
    }

    // ---- and every reader goes through it ----

    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Gg.sln")))
        {
            directory = directory.Parent;
        }

        return (directory ?? throw new InvalidOperationException("Gg.sln not found")).FullName;
    }

    /// <summary>The files that read the variable today, by name.</summary>
    private static readonly string[] Readers =
    [
        "Gg.Runner/Execution/ExecutorConfiguration.cs",
        "Gg.Runner/AllowanceReporter.cs",
        "Gg.Cli/Program.cs",
    ];

    [Test]
    public async Task The_walk_finds_the_readers_it_is_about_to_judge()
    {
        // THE LIVENESS ANCHOR. A regex matching nothing would pass a codebase
        // where every reader took the bare path.
        var reads = Readers
            .Select(file => (file, hits: BinaryVariableReads().Matches(
                File.ReadAllText(Path.Combine(Root(), file))).Count))
            .ToList();

        await Assert.That(reads.Sum(r => r.hits)).IsGreaterThanOrEqualTo(3)
            .Because("three files read GG_EXECUTOR_BINARY, so a walk that finds fewer has "
                   + "stopped reading: " + string.Join(", ", reads.Select(r => $"{r.file}={r.hits}")));
    }

    [Test]
    public async Task Every_reader_of_the_variable_parses_it_as_a_declaration()
    {
        // A READER THAT TAKES THE BARE STRING assumes claude in silence, which
        // is the defect the declaration exists to end. Every file that reads
        // the variable has to hand it to the one parser.
        var offenders = Readers
            .Where(file =>
            {
                var source = File.ReadAllText(Path.Combine(Root(), file));
                return BinaryVariableReads().IsMatch(source)
                    && !source.Contains("ExecutorDeclaration.Parse", StringComparison.Ordinal);
            })
            .ToList();

        await Assert.That(offenders).IsEmpty()
            .Because("these read GG_EXECUTOR_BINARY and never parse it, so a keyed entry would "
                   + "reach them as a path named 'claude=/…': " + string.Join(", ", offenders));
    }

    /// <summary>A read of the variable, by constant or by its spelling.</summary>
    [GeneratedRegex(@"ExecutorConfiguration\.BinaryVariable|ExecutorDeclaration\.Variable|""GG_EXECUTOR_BINARY""")]
    private static partial Regex BinaryVariableReads();
}
