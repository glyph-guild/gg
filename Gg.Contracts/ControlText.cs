using System.Text;

namespace Gg.Contracts;

/// <summary>
/// Removes terminal control sequences from externally-sourced text.
/// </summary>
/// <remarks>
/// <para>
/// Applied at INGRESS, before storage - not at render time. Stripping in the
/// console would mean every other surface re-derives the property: the web
/// queue, a chat card, a PR comment and a support bundle would each need their
/// own escape hatch, and the first one written without it is the one that
/// carries an escape sequence into somebody's terminal.
/// </para>
/// <para>
/// Stored-clean means those surfaces inherit the property instead, and it
/// means the flight log cannot carry an escape sequence at all - which is what
/// makes a support bundle safe to open.
/// </para>
/// <para>
/// The rule lives in the contract, like the flight-number parser, because the
/// control plane strips at ingress and gg strips what it renders. Those must
/// be one rule rather than two that agree today.
/// </para>
/// <para>
/// Deliberately not a regex: this ships in a Native AOT binary, and a
/// hand-written scan of a short string is both faster and readable by whoever
/// has to audit it.
/// </para>
/// </remarks>
public static class ControlText
{
    private const char Escape = '\u001b';
    private const char Bell = '\u0007';

    /// <summary>
    /// Text with every control sequence removed.
    /// </summary>
    /// <param name="text">Whatever arrived from outside. Null is empty.</param>
    /// <param name="allowLineBreaks">
    /// Whether newlines survive. A flight name is one line - a newline in it
    /// breaks every list that renders one row per flight - while free text is
    /// not, and flattening that would silently destroy what somebody wrote.
    /// </param>
    public static string Strip(string? text, bool allowLineBreaks = false)
    {
        if (string.IsNullOrEmpty(text))
        {
            return "";
        }

        var kept = new StringBuilder(text.Length);

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];

            if (c == Escape)
            {
                // An escape with no terminator takes the rest of the string
                // with it. Keeping the tail would let a crafted name smuggle
                // characters past by never closing the sequence.
                i = SkipEscape(text, i);
                continue;
            }

            if (IsKeepable(c, allowLineBreaks))
            {
                kept.Append(c);
            }
        }

        return kept.ToString();
    }

    /// <summary>
    /// Whether text still carries anything a terminal would act on.
    /// </summary>
    /// <remarks>
    /// Written independently of <see cref="Strip"/>, on purpose. A scan defined
    /// as <c>Strip(x) != x</c> proves only that the stripper agrees with
    /// itself, and would give a clean bill of health to a stripper that did
    /// nothing at all. This one asks a question about the characters actually
    /// present, which is the question an audit of storage wants answered.
    /// </remarks>
    public static bool ContainsControlSequence(string? text, bool allowLineBreaks = false)
    {
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        foreach (var c in text)
        {
            if (!IsKeepable(c, allowLineBreaks))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Whether a character survives stripping.</summary>
    /// <remarks>
    /// One predicate, used by both the stripper and the scan, so "what counts
    /// as a control character" cannot be answered two ways.
    /// </remarks>
    private static bool IsKeepable(char c, bool allowLineBreaks)
    {
        if (allowLineBreaks && c is '\n' or '\r' or '\t')
        {
            return true;
        }

        // C0 and DEL, then C1 - the ones that arrive as single characters
        // rather than as ESC pairs.
        //
        // C1 is removed as a CHARACTER and not treated as a sequence
        // introducer, so U+009B followed by "1m" leaves "1m" behind. That is
        // deliberate: a terminal in UTF-8 mode does not read U+009B as CSI, so
        // the residue is inert text, whereas treating it as an introducer
        // would silently eat real characters after a stray one.
        return c is not (>= '\u0000' and <= '\u001f')
            && c != '\u007f'
            && c is not (>= '\u0080' and <= '\u009f');
    }

    /// <summary>
    /// Index of the last character belonging to the escape sequence starting
    /// at <paramref name="start"/>.
    /// </summary>
    /// <remarks>
    /// Three shapes matter. CSI (<c>ESC [</c>) runs through its parameters to a
    /// final byte in 0x40..0x7E. OSC (<c>ESC ]</c>) - the one that actually
    /// retitles a window - runs to BEL or to ST, so a stripper written only for
    /// CSI would leave the whole payload behind. Anything else is a two
    /// character escape.
    /// </remarks>
    private static int SkipEscape(string text, int start)
    {
        var i = start + 1;
        if (i >= text.Length)
        {
            return text.Length;     // A trailing ESC: nothing after it survives.
        }

        switch (text[i])
        {
            case '[':
                for (i++; i < text.Length; i++)
                {
                    if (text[i] is >= '@' and <= '~')
                    {
                        return i;
                    }
                }
                return text.Length;  // Unterminated.

            case ']':
                for (i++; i < text.Length; i++)
                {
                    if (text[i] == Bell)
                    {
                        return i;
                    }
                    // ST, written as ESC \.
                    if (text[i] == Escape && i + 1 < text.Length && text[i + 1] == '\\')
                    {
                        return i + 1;
                    }
                }
                return text.Length;  // Unterminated.

            default:
                return i;
        }
    }
}
