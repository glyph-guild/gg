using System.Text.RegularExpressions;

namespace Gg.Cli.Tests;

/// <summary>
/// The refusals, prompts and notices in Gg.Cli and Gg.Client that no verb
/// result carries, read from source.
/// </summary>
/// <remarks>
/// <para>
/// Exceptions, progress lines and refusals are raised from many places with
/// their own inputs, so they are checked by scanning every string literal in
/// the two projects rather than by rendering them. Comments, attributes,
/// constants and single wire tokens are not prose and are skipped.
/// </para>
/// <para>
/// <b>A scan cannot tell a person's message from text addressed to an AI
/// agent</b>, where "you" is correct. Those files are listed in
/// <see cref="AgentFacing"/>, each with its reason.
/// </para>
/// </remarks>
public partial class PlainWordsOnTheFirstRunPathTests
{
    // Text addressed to an AI agent rather than to a person.
    private static readonly IReadOnlyDictionary<string, string> AgentFacing =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Gg.Cli/PlatformToolServer.cs"] = "MCP tool descriptions and tool answers an agent reads.",
            ["Gg.Cli/ItineraryToolServer.cs"] = "MCP tool descriptions and answers for an agent's plan session.",
            ["Gg.Cli/ManageToolServer.cs"] = "MCP tool descriptions and answers for an agent managing the airspace.",
            ["Gg.Cli/WorkItemToolServer.cs"] = "MCP tool descriptions and answers for an agent reading work items.",
            ["Gg.Client/HandProposal.cs"] = "the prompt passed to claude to draft a person's account of a takeover.",
        };

    // Words the person types, shown as spelled: the `gg tenant name` verb, its
    // `--tenant` option, and the ownership values `tenant` and `open`.
    private static readonly Regex Typed = new(
        @"gg tenant name|--tenant|tenant\|open|/v1/[^\s""]*",
        RegexOptions.Compiled);

    private static string RepoRoot()
    {
        var here = new DirectoryInfo(AppContext.BaseDirectory);
        while (here is not null && !File.Exists(Path.Combine(here.FullName, "Gg.sln")))
        {
            here = here.Parent;
        }

        return here?.FullName ?? throw new InvalidOperationException("Gg.sln not found");
    }

    [Test]
    public async Task Every_agent_facing_exemption_names_a_file_that_exists()
    {
        var missing = AgentFacing.Keys
            .Where(file => !File.Exists(Path.Combine(RepoRoot(), file)))
            .ToList();

        await Assert.That(missing).IsEmpty()
            .Because("an exemption for a file that is gone exempts whatever takes its name next.");
    }

    [Test]
    public async Task No_message_in_the_cli_or_client_uses_a_word_the_voice_rules_out()
    {
        var literal = new Regex("\"(?:[^\"\\\\]|\\\\.)*\"");
        var offending = new List<string>();

        foreach (var project in new[] { "Gg.Cli", "Gg.Client" })
        {
            foreach (var path in Directory.EnumerateFiles(
                         Path.Combine(RepoRoot(), project), "*.cs", SearchOption.TopDirectoryOnly))
            {
                var file = $"{project}/{Path.GetFileName(path)}";
                if (AgentFacing.ContainsKey(file))
                {
                    continue;
                }

                var number = 0;
                foreach (var line in File.ReadLines(path))
                {
                    number++;
                    var code = line.TrimStart();
                    if (code.StartsWith("//", StringComparison.Ordinal)
                        || code.StartsWith('[')
                        || code.Contains("const string", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    foreach (Match m in literal.Matches(line))
                    {
                        var text = m.Value[1..^1];

                        // A wire value - a single lower-case token - is not prose.
                        if (Regex.IsMatch(text, "^[a-z0-9:._/-]*$"))
                        {
                            continue;
                        }

                        var prose = Typed.Replace(Spoken(text), "");
                        prose = Regex.Replace(prose, @"\{[^}]*\}", "{}");

                        if (RuledOutInOutput.IsMatch(prose))
                        {
                            offending.Add($"{file}:{number}: {text}");
                        }
                    }
                }
            }
        }

        await Assert.That(string.Join(Environment.NewLine, offending)).IsEmpty()
            .Because("a refusal or notice is read by a person: person, organisation, someone, no one, never you.");
    }

    // Exceptions a person never reads: argument checks and parsing failures
    // that a caller catches and words again.
    private static readonly IReadOnlySet<string> ProgrammerExceptions = new HashSet<string>(StringComparer.Ordinal)
    {
        "ArgumentException", "ArgumentNullException", "ArgumentOutOfRangeException",
        "JsonException", "NotSupportedException", "FormatException",
    };

    [Test]
    public async Task Every_refusal_in_the_cli_or_client_starts_like_a_sentence()
    {
        var raised = new Regex(@"\b(Fail|Refused|new\s+(\w+Exception))\(\s*\$?""([^""]{0,60})");
        var offending = new List<string>();

        foreach (var project in new[] { "Gg.Cli", "Gg.Client" })
        {
            foreach (var path in Directory.EnumerateFiles(
                         Path.Combine(RepoRoot(), project), "*.cs", SearchOption.TopDirectoryOnly))
            {
                var file = $"{project}/{Path.GetFileName(path)}";
                if (AgentFacing.ContainsKey(file))
                {
                    continue;
                }

                var source = File.ReadAllText(path);
                foreach (Match m in raised.Matches(source))
                {
                    var text = m.Groups[3].Value;
                    if (text.Length == 0 || ProgrammerExceptions.Contains(m.Groups[2].Value))
                    {
                        continue;
                    }

                    // A capital, a quoted or interpolated name, an option, or a command.
                    if (char.IsUpper(text[0])
                        || text[0] is '{' or '\'' or '`' or '(' or '-'
                        || text.StartsWith("gg ", StringComparison.Ordinal)
                        || text.StartsWith("git ", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    var line = source[..m.Index].Count(c => c == '\n') + 1;
                    offending.Add($"{file}:{line}: {text}");
                }
            }
        }

        await Assert.That(string.Join(Environment.NewLine, offending)).IsEmpty()
            .Because("a refusal is a message, and a message starts with a capital letter.");
    }
}
