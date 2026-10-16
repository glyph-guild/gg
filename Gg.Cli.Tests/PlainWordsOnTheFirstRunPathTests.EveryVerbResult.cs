using System.Text.RegularExpressions;
using Gg.Client;

namespace Gg.Cli.Tests;

/// <summary>
/// Every verb's text output, rendered from a sample of each result kind and
/// held to the voice rules.
/// </summary>
/// <remarks>
/// <para>
/// The samples live in <see cref="VerbResultSamples"/>. A result kind with no
/// sample fails here, so a new verb's output is read as soon as it exists.
/// </para>
/// <para>
/// Lines are of two kinds. Column headings, table cells and labelled fields are
/// fragments, and only their words are checked. A line that reads as a message
/// (it ends with a full stop, or holds two sentences) is a full sentence: a
/// capital letter at the start and a full stop at the end. A label at the start
/// of a line ("because:", "note:") is set aside before that check, and a line
/// may open with a name or an id the reader typed.
/// </para>
/// </remarks>
public partial class PlainWordsOnTheFirstRunPathTests
{
    private static readonly Regex RuledOutInOutput = new(
        @"\b(you|your|yours|tenant|tenants|principal|principals|nonce|mint|mints|minted|redeem|redeems|redeemed|somebody|nobody)\b"
      + @"|\b\w+(n't|'re|'ve|'ll)\b",
        RegexOptions.IgnoreCase);

    // Kinds whose body is a document Gg.Contracts renders for an agent as well as
    // a person (EnvelopeText, TakeSeedComposer), and which the contract's own
    // voice check exempts for that reason. Only the lines this renderer adds -
    // the headings that start with '#' - are read here.
    private static readonly IReadOnlySet<Type> DocumentBodies = new HashSet<Type>
    {
        typeof(VerbResult.EnvelopeShown),
        typeof(VerbResult.RulesInForce),
        typeof(VerbResult.NamedEnvelopeShown),
        typeof(VerbResult.StrategyShown),
        typeof(VerbResult.Taken),
    };

    // A value the person types is quoted in backticks, and is shown as spelled.
    private static string Spoken(string line) => Regex.Replace(line, "`[^`]*`", "``");

    private static IEnumerable<(Type Kind, string Line)> RenderedLines()
    {
        foreach (var (kind, samples) in VerbResultSamples.All)
        {
            foreach (var sample in samples)
            {
                foreach (var raw in VerbOutput.ToText(sample).Split('\n'))
                {
                    var line = raw.Trim();
                    if (line.Length == 0)
                    {
                        continue;
                    }

                    if (DocumentBodies.Contains(kind) && !line.StartsWith('#'))
                    {
                        continue;
                    }

                    yield return (kind, line);
                }
            }
        }
    }

    [Test]
    public async Task Every_verb_result_kind_has_a_sample()
    {
        var kinds = typeof(VerbResult).GetNestedTypes()
            .Where(t => t.IsSubclassOf(typeof(VerbResult)) && !t.IsAbstract)
            .Select(t => t.Name)
            .Except(VerbResultSamples.All.Keys.Select(t => t.Name))
            .ToList();

        await Assert.That(kinds).IsEmpty()
            .Because("a result kind with no sample is output no voice check reads.");
    }

    [Test]
    public async Task Every_verb_result_kind_renders_as_text()
    {
        var failed = new List<string>();

        foreach (var (kind, samples) in VerbResultSamples.All)
        {
            foreach (var sample in samples)
            {
                try
                {
                    _ = VerbOutput.ToText(sample);
                    _ = VerbOutput.ToJson(sample);
                }
                catch (InvalidOperationException)
                {
                    failed.Add(kind.Name);
                }
            }
        }

        await Assert.That(failed.Distinct().ToList()).IsEmpty()
            .Because("a verb whose result cannot be printed fails after the control plane has acted.");
    }

    [Test]
    public async Task No_verb_output_uses_a_word_the_voice_rules_out()
    {
        var offending = RenderedLines()
            .Where(r => RuledOutInOutput.IsMatch(Spoken(r.Line)))
            .Select(r => $"{r.Kind.Name}: {r.Line}")
            .Distinct()
            .ToList();

        await Assert.That(string.Join(Environment.NewLine, offending)).IsEmpty()
            .Because("output says person, organisation, someone and no one, never you, and spells words out.");
    }

    [Test]
    public async Task Every_message_in_verb_output_is_a_full_sentence()
    {
        var label = new Regex(@"^[a-z]+( [a-z]+){0,2}(:\s+|\s{2,})");
        var named = new Regex(@"^\S*[-:/._@#0-9]\S*");

        var offending = RenderedLines()
            .Select(r => (r.Kind, Line: label.Replace(r.Line, "")))
            .Where(r => r.Line.Length > 0)
            .Where(r => r.Line.EndsWith('.') || Regex.IsMatch(r.Line, @"[a-z)]\. [A-Z]"))
            .Where(r => !(char.IsUpper(r.Line[0])
                          || r.Line[0] is '\'' or '"' or '`' or '('
                          || r.Line.StartsWith("gg ", StringComparison.Ordinal)
                          || named.IsMatch(r.Line))
                        || !(r.Line.EndsWith('.') || r.Line.EndsWith(':')))
            .Select(r => $"{r.Kind.Name}: {r.Line}")
            .Distinct()
            .ToList();

        await Assert.That(string.Join(Environment.NewLine, offending)).IsEmpty()
            .Because("a message is a full sentence: a capital letter and a full stop.");
    }
}
