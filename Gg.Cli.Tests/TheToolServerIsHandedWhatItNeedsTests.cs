using System.Text.RegularExpressions;
using Gg.Local;

namespace Gg.Cli.Tests;

/// <summary>
/// What the console writes into the tool server's environment, the tool server
/// reads.
/// </summary>
/// <remarks>
/// <para>
/// <b>THE DEFECT THIS IS WRITTEN AGAINST, AND IT SHIPPED.</b>
/// <c>PtyDraftSession</c> writes <c>GG_DOCUMENT_ROOT</c> into the tool server's
/// own <c>env</c> block, and <c>Program.cs</c> passes
/// <c>PlatformToolServer.RunAsync</c> only <c>intentPath</c> — so
/// <c>documentRoot</c> was null on every launch and <c>submit_document</c>
/// answered every call with <i>"this session has no working copy, so there is
/// nowhere for a document to go"</i>. The whole drafting feature could not
/// write a file.
/// </para>
/// <para>
/// <b>Everything around it was green.</b> The tool is built, described,
/// declared in <c>tools/list</c>, granted by the launch, and tested — and
/// <c>ADocumentArrivesByToolCallTests</c> passes <c>documentRoot</c> to
/// <c>RunAsync</c> itself, so it proves the tool works when it is handed a
/// root and says nothing about whether anything hands it one.
/// <c>Sources.NotOnThePage</c> even carries an exemption for the variable
/// explaining the mechanism, which did not exist. Two correct halves and no
/// wire between them: the shape this repository keeps finding, and the reason
/// <c>EveryPortIsPassedTests</c> exists one project over.
/// </para>
/// <para>
/// <b>So the assertion is about the wire, not the ends.</b> A variable gg
/// writes into a child it starts has exactly two ends, and either alone is
/// silent: a write nobody reads is a value thrown away, and a read nobody
/// writes is a feature that is always off. Both are named here, per variable,
/// with the projects they must appear in.
/// </para>
/// </remarks>
public partial class TheToolServerIsHandedWhatItNeedsTests
{
    /// <summary>
    /// The variables gg hands to its own tool server, and who must say them.
    /// </summary>
    /// <remarks>
    /// The console starts the server and writes the value; the CLI is the
    /// server and reads it. Naming the projects rather than the files keeps
    /// this from failing on a rename, and a walk that finds neither end fails
    /// on the constant's own name.
    /// </remarks>
    private static readonly (string Constant, string Purpose)[] Wires =
    [
        (nameof(IntentTool.PathVariable), "where a composed intent is recorded"),
        (nameof(DocumentTool.RootVariable), "the working copy a drafted document lands in"),
        (nameof(AirspaceContextTool.EnvelopeVariable), "the rules in force for this tenant"),
    ];

    [Test]
    public async Task Both_ends_of_every_wire_to_the_tool_server_exist()
    {
        foreach (var (constant, purpose) in Wires)
        {
            var written = Mentions("Gg.Console", constant);
            var read = Mentions("Gg.Cli", constant);

            await Assert.That(written).IsGreaterThan(0)
                .Because($"nothing writes {constant} ({purpose}), so the server is started "
                       + "without it and the tool it belongs to refuses every call.");

            await Assert.That(read).IsGreaterThan(0)
                .Because($"nothing in the server reads {constant} ({purpose}). The console "
                       + "writes it into the child's environment and the child never looks, "
                       + "so the value is thrown away and the tool answers as though the "
                       + "session had none - which is what shipped for submit_document.");
        }
    }

    [Test]
    public async Task Every_argument_the_server_takes_is_supplied_where_it_is_started()
    {
        // THE RATCHET, not the one fix. A parameter with a default is a
        // parameter the compiler will never ask about, so the next one added
        // to RunAsync can be forgotten exactly as documentRoot was - silently,
        // and only where it matters. This reads the parameter list and holds
        // the composition root to naming each one.
        var wanted = Parameters();

        await Assert.That(wanted).IsNotEmpty()
            .Because("RunAsync's signature was not found, so this walk reads nothing and "
                   + "would pass over any wiring at all.");

        var arm = ToolServerArm();

        foreach (var parameter in wanted)
        {
            await Assert.That(arm).Contains(parameter, StringComparison.Ordinal)
                .Because($"'{parameter}' is a value the tool server needs and the one place "
                       + "that starts it does not supply it, so it takes its default and "
                       + "whatever depends on it is off. Arm:\n" + arm);
        }
    }

    /// <summary>
    /// The optional parameters of <c>PlatformToolServer.RunAsync</c>, read off
    /// its own signature.
    /// </summary>
    /// <remarks>
    /// The two streams and the cancellation token are excluded: the first two
    /// are what a server IS, and a token is passed by a caller that has one.
    /// What is left is the state the server is handed, which is the whole
    /// question here.
    /// </remarks>
    private static IReadOnlyList<string> Parameters()
    {
        var source = Read("Gg.Cli", "PlatformToolServer.cs");
        var at = source.IndexOf("Task<int> RunAsync(", StringComparison.Ordinal);

        if (at < 0)
        {
            return [];
        }

        var open = source.IndexOf('(', at);
        var close = source.IndexOf(')', open);

        // COMMENTS OUT FIRST. A parameter carrying a reason above it is the
        // house style, and a comma inside that prose splits into something
        // that reads like a parameter name - this walk reported `RUN`, out of
        // "HOW THE VERB IS RUN, injected so...". A scan that invents its own
        // subject fails on writing rather than on wiring, which is the one
        // way a guard like this wastes somebody's afternoon.
        var list = string.Join('\n', source[(open + 1)..close]
            .Split('\n')
            .Select(line => line.IndexOf("//", StringComparison.Ordinal) is var slashes
                            && slashes >= 0
                ? line[..slashes]
                : line));

        return [.. list
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(one => one.Split('=', 2)[0].Trim().Split(' ').Last())
            .Where(name => name.Length > 0)
            .Where(name => name is not ("input" or "output" or "cancellationToken"))];
    }

    /// <summary>The one arm of the root that starts the tool server.</summary>
    private static string ToolServerArm()
    {
        var source = Read("Gg.Cli", "Program.cs");
        var at = source.IndexOf("CliAction.RunnerTools", StringComparison.Ordinal);

        if (at < 0)
        {
            throw new InvalidOperationException(
                "the RunnerTools arm was renamed, so this scan reads nothing.");
        }

        var next = NextArm().Match(source, at + 1);

        return next.Success ? source[at..next.Index] : source[at..];
    }

    private static int Mentions(string project, string constant)
    {
        var at = Path.Combine(Root(), project);
        var found = 0;

        foreach (var file in Directory.EnumerateFiles(at, "*.cs", SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
                    StringComparison.Ordinal)
             || file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}",
                    StringComparison.Ordinal))
            {
                continue;
            }

            found += File.ReadAllText(file).Contains(constant, StringComparison.Ordinal) ? 1 : 0;
        }

        return found;
    }

    private static string Read(params string[] parts) =>
        File.ReadAllText(Path.Combine([Root(), .. parts]));

    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Gg.sln")))
        {
            directory = directory.Parent;
        }

        return (directory ?? throw new InvalidOperationException("Gg.sln not found")).FullName;
    }

    [GeneratedRegex(@"\n    CliAction\.")]
    private static partial Regex NextArm();
}
