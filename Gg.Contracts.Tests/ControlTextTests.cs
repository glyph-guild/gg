namespace Gg.Contracts.Tests;

/// <summary>
/// Control sequences are stripped at the boundary, not at render time.
/// </summary>
/// <remarks>
/// <para>
/// Stripping in the console would mean every other surface re-derives the
/// property: the web queue, a chat card, a PR comment, and a support bundle
/// would each need their own escape hatch, and the first one written without
/// it is the one that carries an escape sequence into a customer's terminal.
/// </para>
/// <para>
/// Stripping before storage means the property is inherited instead. It also
/// means the flight log cannot carry an escape sequence at all, which is what
/// makes a support bundle safe to open.
/// </para>
/// <para>
/// The rule lives in the contract for the same reason the flight-number
/// parser does: the control plane strips at ingress and gg strips what it
/// shows, and those must be the same rule rather than two that agree today.
/// </para>
/// <para>
/// Every control character in this file is written as a unicode escape. A
/// literal one is invisible in a diff, and a test whose subject cannot be seen
/// in review is a test nobody can check.
/// </para>
/// </remarks>
public class ControlTextTests
{
    private const string Esc = "\u001b";
    private const string Bel = "\u0007";

    [Test]
    public async Task An_escape_sequence_is_removed_whole_leaving_no_residue()
    {
        // The half-measure this guards against: dropping the ESC byte alone
        // and leaving "[31m" behind. That looks stripped in a diff and is
        // still wrong - the text now contains junk nobody wrote.
        await Assert.That(ControlText.Strip($"{Esc}[31mred{Esc}[0m")).IsEqualTo("red");
    }

    [Test]
    public async Task An_operating_system_command_is_removed_whole()
    {
        // The one that actually retitles a terminal window. It ends with BEL
        // or with ST rather than with a letter, so a stripper written only for
        // CSI leaves the entire payload behind.
        await Assert.That(ControlText.Strip($"a{Esc}]0;pwned{Bel}b")).IsEqualTo("ab");
        await Assert.That(ControlText.Strip($"a{Esc}]0;pwned{Esc}\\b")).IsEqualTo("ab");
    }

    [Test]
    public async Task Bare_control_characters_go_too()
    {
        await Assert.That(ControlText.Strip("a\u0001b\u0008c\u007fd")).IsEqualTo("abcd");

        // C1, the ones that arrive as single characters rather than ESC pairs.
        await Assert.That(ControlText.Strip("a\u009bb")).IsEqualTo("ab");
    }

    [Test]
    public async Task Ordinary_text_is_left_exactly_alone()
    {
        // A stripper that mangles ordinary input gets turned off. Names carry
        // accents, punctuation and emoji, and none of that is a control
        // sequence.
        foreach (var text in (string[])
                 ["fix the login bug", "Gruesse, Strasse", "日本語", "100% - done!", "á"])
        {
            await Assert.That(ControlText.Strip(text)).IsEqualTo(text);
        }
    }

    [Test]
    public async Task Line_breaks_survive_only_where_they_are_wanted()
    {
        // A flight name is one line: a newline in it breaks every list that
        // renders one row per flight. Free text is not, and flattening it
        // would silently destroy what somebody wrote.
        await Assert.That(ControlText.Strip("one\ntwo")).IsEqualTo("onetwo");
        await Assert.That(ControlText.Strip("one\ntwo", allowLineBreaks: true)).IsEqualTo("one\ntwo");

        // Even when line breaks are kept, escapes are not.
        await Assert.That(ControlText.Strip($"one\n{Esc}[2Jtwo", allowLineBreaks: true))
            .IsEqualTo("one\ntwo");
    }

    [Test]
    public async Task Stripping_twice_changes_nothing_the_first_pass_left()
    {
        // Text is stored stripped and may be stripped again on the way out. A
        // non-idempotent stripper would quietly eat real characters on the
        // second pass - here, the square brackets somebody actually typed.
        var once = ControlText.Strip($"{Esc}[31m[not an escape]{Esc}[0m");

        await Assert.That(once).IsEqualTo("[not an escape]");
        await Assert.That(ControlText.Strip(once)).IsEqualTo(once);
    }

    [Test]
    public async Task Null_and_empty_are_not_a_special_case_for_the_caller()
    {
        await Assert.That(ControlText.Strip(null)).IsEqualTo("");
        await Assert.That(ControlText.Strip("")).IsEqualTo("");
    }

    [Test]
    public async Task The_scan_fires_on_what_the_strip_removes()
    {
        // The poison twin, at contract level. The scan is written independently
        // of the stripper on purpose: a scan defined as "Strip(x) != x" proves
        // only that the stripper agrees with itself, and would report a clean
        // bill of health for a stripper that did nothing at all.
        var poisoned = $"{Esc}[31mred{Esc}[0m";

        await Assert.That(ControlText.ContainsControlSequence(poisoned)).IsTrue()
            .Because("a scan that cannot fire proves nothing about the text it passes.");
        await Assert.That(ControlText.ContainsControlSequence(ControlText.Strip(poisoned))).IsFalse();
        await Assert.That(ControlText.ContainsControlSequence("fix the login bug")).IsFalse();
    }

    [Test]
    public async Task Everything_the_stripper_produces_passes_the_scan()
    {
        // The two are meant to be exhaustive against each other. Anything the
        // stripper leaves behind that the scan would flag is a gap between the
        // rule applied at ingress and the rule that audits storage.
        foreach (var text in (string[])
                 [$"{Esc}[31mred{Esc}[0m", "a\u0001b", $"{Esc}]0;t{Bel}", "\u009b1m",
                  Esc, $"{Esc}[", "plain", "", "line\nbreak", "tab\there"])
        {
            await Assert.That(ControlText.ContainsControlSequence(ControlText.Strip(text))).IsFalse()
                .Because($"stripping '{Readable(text)}' must leave nothing the scan objects to.");
        }

        static string Readable(string text) => text.Replace(Esc, "<ESC>", StringComparison.Ordinal);
    }

    [Test]
    public async Task A_truncated_escape_takes_the_rest_of_the_string_with_it()
    {
        // An escape with no terminator is not text that happens to start with
        // ESC. Keeping the tail would let a crafted name smuggle characters
        // past the scan by never closing the sequence.
        await Assert.That(ControlText.Strip($"safe{Esc}[31")).IsEqualTo("safe");
        await Assert.That(ControlText.Strip($"safe{Esc}")).IsEqualTo("safe");
    }
}
