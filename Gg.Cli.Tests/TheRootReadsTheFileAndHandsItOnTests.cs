using System.Text.RegularExpressions;
using Gg.Local;

namespace Gg.Cli.Tests;

/// <summary>
/// One reader, and the composition root is what feeds it.
/// </summary>
/// <remarks>
/// <para>
/// <b>A page that says a value came from the file, over a consumer that read
/// the variable, is worse than no file at all.</b> So the rule is not "the root
/// may read the file" but "the root may not read a configurable variable any
/// other way" — which is checkable, and which the walk below checks.
/// </para>
/// <para>
/// <b>Only the twelve the file can carry.</b> The path roots decide where the
/// file lives and the per-invocation signals are gg writing into its own
/// children, so those are read straight from the environment and always will be.
/// </para>
/// </remarks>
public partial class TheRootReadsTheFileAndHandsItOnTests
{
    /// <summary>Constants the root reads a variable through, and what they spell.</summary>
    /// <remarks>
    /// Named here because the walk reads source rather than running it, and a
    /// constant is the shape it would otherwise miss —
    /// <c>ExecutorConfiguration.BinaryVariable</c> is a configurable setting
    /// wearing an identifier.
    /// </remarks>
    private static readonly Dictionary<string, string> Spells = new(StringComparer.Ordinal)
    {
        ["IntentTool.PathVariable"] = "GG_INTENT_PATH",
        ["ExecutorConfiguration.BinaryVariable"] = "GG_EXECUTOR_BINARY",
        ["MemberBootstrap.NonceVariable"] = "GG_MEMBER_NONCE",
        ["MemberBootstrap.ReachableAsVariable"] = "GG_MEMBER_CONTROL_PLANE",
        ["VcsConfiguration.HostsVariable"] = "GG_VCS_HOSTS",
        ["DestinationConfiguration.ApisVariable"] = "GG_DESTINATION_APIS",
        ["StunConfiguration.Variable"] = "GG_STUN_SERVERS",
        ["IntentConfiguration.ServedVariable"] = "GG_INTENT_HOSTS",
        ["IntentConfiguration.ReadersVariable"] = "GG_INTENT_READERS",
    };

    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Gg.sln")))
        {
            directory = directory.Parent;
        }

        return (directory ?? throw new InvalidOperationException("Gg.sln not found")).FullName;
    }

    private static string ProgramText() =>
        File.ReadAllText(Path.Combine(Root(), "Gg.Cli", "Program.cs"));

    /// <summary>Every variable the root reads straight from the environment.</summary>
    private static List<string> ReadDirectly(string source)
    {
        var read = new List<string>();

        foreach (Match match in Reads().Matches(source))
        {
            var argument = match.Groups[1].Value.Trim();

            read.Add(argument.StartsWith('"')
                ? argument.Trim('"')
                : Spells.TryGetValue(argument, out var spelled) ? spelled : argument);
        }

        return read;
    }

    [Test]
    public async Task The_walk_finds_the_reads_it_is_about_to_judge()
    {
        // THE LIVENESS ANCHOR. A regex that matched nothing would make the test
        // below pass for a root that read every variable by hand.
        var read = ReadDirectly(ProgramText());

        await Assert.That(read).IsNotEmpty();
        await Assert.That(read).Contains("GG_STATE_DUMP")
            .Because("the root still reads this one directly, and always will - it is a "
                   + "thing a person turns on for one run rather than a setting.");
    }

    [Test]
    public async Task The_root_reads_no_configurable_variable_straight_from_the_environment()
    {
        // RULE 6, MADE CHECKABLE. Every one of these has somewhere in the file
        // to live, so a direct read here is a value the file cannot reach - and
        // the page would say `file` over a consumer that never looked.
        var configurable = Configuration.Members
            .Select(m => m.Variable)
            .ToHashSet(StringComparer.Ordinal);

        var offenders = ReadDirectly(ProgramText())
            .Where(configurable.Contains)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        await Assert.That(offenders).IsEmpty()
            .Because("the root reads these straight from the environment, so nothing put "
                   + "in the file would reach them: " + string.Join(", ", offenders));
    }

    [Test]
    public async Task The_root_hands_the_console_the_commands_it_resolved()
    {
        // The four sessions each take an optional command and fall back to the
        // environment when they are handed nothing - which is the fallback that
        // must not fire in production, because it does not know about the file.
        var source = ProgramText();

        foreach (var construction in (string[])
                 ["new PtyEditorSession(", "new PtyAgentSession(", "new TakeSession("])
        {
            var at = source.IndexOf(construction, StringComparison.Ordinal);

            await Assert.That(at).IsGreaterThan(-1)
                .Because($"'{construction}' is not in the root, so this scan judges nothing.");
        }

        await Assert.That(source).Contains("Settings.Value(", StringComparison.Ordinal)
            .Because("the root resolves through the one reader, or the file reaches "
                   + "nothing it constructs.");
    }

    [Test]
    public async Task A_value_in_the_file_reaches_the_page()
    {
        // The end of the thread, asserted where it can be: the page is built
        // from the same resolution the root hands to everything else.
        var page = ConsoleEnvironment.Read(new Configuration { Editor = "hx" });
        var editor = page.Single(s => s.Name == "EDITOR");

        await Assert.That(editor.Source).IsEqualTo(SettingSources.File);
        await Assert.That(editor.Value).IsEqualTo("hx");
    }

    [GeneratedRegex(@"Environment\.GetEnvironmentVariable\(\s*([^),]+?)\s*\)")]
    private static partial Regex Reads();
}
