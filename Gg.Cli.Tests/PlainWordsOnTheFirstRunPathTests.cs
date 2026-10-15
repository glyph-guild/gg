using System.Text.RegularExpressions;

namespace Gg.Cli.Tests;

/// <summary>
/// What a person meets on the first-run path, in the voice the owner chose.
/// </summary>
/// <remarks>
/// Neutral and formal: no "you", no internal words, and every refusal names what
/// was wrong and what to run next. These pin the wording that carries meaning,
/// not incidental phrasing.
/// </remarks>
public class PlainWordsOnTheFirstRunPathTests
{
    private static string Refusal(params string[] args) =>
        ((CliAction.Unknown)CliArgs.Parse(args)).Message;

    private static string FirstLine(string message) =>
        message.Split(Environment.NewLine)[0];

    [Test]
    public async Task An_unknown_word_is_named()
    {
        await Assert.That(FirstLine(Refusal("--repos")))
            .IsEqualTo("gg does not recognise `--repos`. The list below shows every command and its options.");
    }

    [Test]
    public async Task A_known_command_is_not_called_unknown()
    {
        var first = FirstLine(Refusal("runners", "--repos"));

        await Assert.That(first).StartsWith("gg runners does not recognise `--repos`.")
            .Because("gg runners is a command; the word it did not take is --repos.");
        await Assert.That(first).DoesNotContain("is not a gg command");
    }

    [Test]
    public async Task A_command_missing_its_last_word_says_so()
    {
        await Assert.That(FirstLine(Refusal("runner")))
            .StartsWith("gg runner is not a complete command.");
    }

    [Test]
    public async Task Two_intents_are_refused_with_what_to_do()
    {
        await Assert.That(FirstLine(Refusal("fly", "fix it", "--uri", "https://example.invalid/7")))
            .IsEqualTo("gg fly takes only one of: some text, --uri, --ticket or --file. More than "
                     + "one was given. Run it again with one.");
    }

    [Test]
    public async Task An_option_fly_lacks_says_no_flight_was_opened()
    {
        await Assert.That(FirstLine(Refusal("fly", "--help"))).Contains("no flight was opened");
    }

    [Test]
    public async Task No_help_summary_uses_words_the_voice_rules_out()
    {
        var ruledOut = new Regex(
            @"\b(you|your|yours|tenant|principal|nonce|mint|redeem|somebody)\b",
            RegexOptions.IgnoreCase);

        var reference = Reference.Build();
        var offending = reference.Commands
            .SelectMany(c => c.Options.Select(o => o.Summary).Prepend(c.Summary))
            .Where(summary => ruledOut.IsMatch(summary))
            .ToList();

        await Assert.That(offending).IsEmpty()
            .Because("help summaries say person and organisation, and never address the reader.");
    }

    [Test]
    public async Task Gates_and_runners_read_plainly()
    {
        var usage = Refusal("frobnicate");

        await Assert.That(usage).Contains("list this organisation's runners");
        await Assert.That(usage).Contains("create a link that adds another person to this organisation");
        await Assert.That(usage).Contains("list flights waiting for a person to decide (gates)");
    }
}
