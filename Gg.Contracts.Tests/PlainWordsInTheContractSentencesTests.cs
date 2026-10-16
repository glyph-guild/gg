using System.Text.RegularExpressions;

namespace Gg.Contracts.Tests;

/// <summary>
/// The sentences Gg.Contracts composes for people, held to the voice rules.
/// </summary>
/// <remarks>
/// <para>
/// Reason and story sentences are rendered here from every kind the contract
/// declares, so a new kind is checked as soon as it exists. Reasons are full
/// sentences: a capital letter (or an opening quote around a name) and a full
/// stop. Story entries are rows in a log, so they are fragments and only the
/// words are checked.
/// </para>
/// <para>
/// The other diagnoses (envelope, watch and credential refusals) are checked by
/// scanning the string literals in Gg.Contracts' source, because each is reached
/// through a different validator with its own inputs.
/// </para>
/// </remarks>
public class PlainWordsInTheContractSentencesTests
{
    private static readonly Regex RuledOut = new(
        @"\b(you|your|yours|tenant|tenants|principal|principals|nonce|mint|mints|minted|redeem|redeems|redeemed|somebody|nobody)\b"
      + @"|\b\w+(n't|'re|'ve|'ll)\b",
        RegexOptions.IgnoreCase);

    // A value the person types is quoted in backticks, and a wire value such as
    // `for: tenant` has to be shown as it is spelled.
    private static string Prose(string text) => Regex.Replace(text, "`[^`]*`", "``");

    private static readonly IReadOnlyDictionary<string, string[][]> ReasonSamples =
        new Dictionary<string, string[][]>(StringComparer.Ordinal)
        {
            [ReasonKinds.NoRunnerAdvertises] = [["environment=aurora"], ["a=1", "b=2"]],
            [ReasonKinds.CannotBeShownToTighten] = [["obligations.root.approver"]],
            [ReasonKinds.FlightsInTheAir] = [["implement", "GG-1, GG-2"]],
            [ReasonKinds.StaleWorkingCopy] = [["v3", "v4"]],
            [ReasonKinds.WideningRequiresAGate] = [["context.scope"]],
            [ReasonKinds.DeclaredAndAbsent] = [["acme/widgets", ".gg/narrowings"]],
            [ReasonKinds.ForgeUnreachable] = [["acme/widgets"]],
            [ReasonKinds.Uncharted] = [["staging"]],
            [ReasonKinds.RegistrationIsAWidening] = [["airspace.repositories"]],
            [ReasonKinds.BlockedByBound] =
                [["pool-maximum", BoundClearings.Capacity], ["active-hours", BoundClearings.Schedule, "08:00Z"]],
            [ReasonKinds.PoolWarming] = [["payments-pool"]],
            [ReasonKinds.RunnerReserved] = [["linux-x64", "acme:alice"], ["linux-x64"]],
            [ReasonKinds.RunnerParked] = [["linux-x64", "disk replacement"], ["linux-x64"]],
            [ReasonKinds.DirectedRunnerAbsent] = [["laptop-7"]],
            [ReasonKinds.DirectedRunnerBusy] = [["laptop-7"]],
            [ReasonKinds.PersonalRunnerAbsent] = [["acme:alice"], []],
            [ReasonKinds.NoCredentialRegistered] = [["acme/widgets"]],
            [ReasonKinds.CredentialUnreadable] = [["acme-token", "laptop-7"]],
        };

    private static IEnumerable<string> ReasonSentences() =>
        ReasonSamples.SelectMany(kind => kind.Value.Select(p => Reason.Sentence(kind.Key, p)));

    [Test]
    public async Task Every_reason_kind_is_rendered_here()
    {
        await Assert.That(ReasonKinds.All.Except(ReasonSamples.Keys).ToList()).IsEmpty()
            .Because("a reason kind with no sample here is a sentence no voice check reads.");
    }

    [Test]
    public async Task No_reason_sentence_uses_a_word_the_voice_rules_out()
    {
        var offending = ReasonSentences().Where(s => RuledOut.IsMatch(Prose(s))).ToList();

        await Assert.That(offending).IsEmpty()
            .Because("a reason is read by a person: someone, no one, organisation, never you.");
    }

    [Test]
    public async Task Every_reason_sentence_starts_with_a_capital_and_ends_with_a_full_stop()
    {
        var offending = ReasonSentences()
            .Where(s => !(char.IsUpper(s[0]) || s[0] is '\'' or '"' or '`') || !s.EndsWith('.'))
            .ToList();

        await Assert.That(offending).IsEmpty()
            .Because("a reason is a message, and a message is a full sentence.");
    }

    [Test]
    public async Task No_story_entry_uses_a_word_the_voice_rules_out()
    {
        string[] full = ["acme:alice", "approved", "leg 2"];

        var offending = StoryKinds.All
            .SelectMany(kind => new[] { FlightStory.Sentence(kind, full), FlightStory.Sentence(kind, []) })
            .Where(s => RuledOut.IsMatch(Prose(s)))
            .ToList();

        await Assert.That(offending).IsEmpty()
            .Because("an entry with a param missing falls back to a word, and that word is read too.");
    }

    // Text addressed to an AI agent, where "you" is correct, and tool answers an
    // agent reads. The scan cannot tell these from a person's message.
    private static readonly IReadOnlySet<string> AgentFacing = new HashSet<string>(StringComparer.Ordinal)
    {
        // The envelope rendered into an agent's prompt, and the seed a taking agent reads.
        "EnvelopeText.cs",
        "TakeSeed.cs",
        // Refusals returned to an agent by the itinerary and platform tool servers.
        "FlightNominations.cs",
        "LandingProposals.cs",
        "WorkItemProposals.cs",
    };

    [Test]
    public async Task No_diagnosis_in_the_contract_uses_a_word_the_voice_rules_out()
    {
        var literal = new Regex("\"(?:[^\"\\\\]|\\\\.)*\"");
        var offending = new List<string>();

        foreach (var path in Directory.EnumerateFiles(
                     Path.Combine(RepoRoot(), "Gg.Contracts"), "*.cs", SearchOption.TopDirectoryOnly))
        {
            var file = Path.GetFileName(path);
            if (AgentFacing.Contains(file))
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
                    if (Regex.IsMatch(text, "^[a-z0-9:._-]*$"))
                    {
                        continue;
                    }

                    if (RuledOut.IsMatch(Prose(text)))
                    {
                        offending.Add($"{file}:{number}: {text}");
                    }
                }
            }
        }

        await Assert.That(offending).IsEmpty()
            .Because("every diagnosis here reaches a person through gg or the control plane.");
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Gg.sln")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("Gg.sln not found above " + AppContext.BaseDirectory);
    }
}
