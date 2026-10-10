namespace Gg.Console;

/// <summary>The title a Claude Code session sets for itself, as a row's name.</summary>
/// <remarks>
/// Claude Code writes its title with OSC 0: a status glyph, a space, then a summary of the
/// conversation - <c>✳ Basic arithmetic question</c> idle, a spinner (<c>◐</c>, <c>◑</c>, ...) in
/// that place while it works. Until it has something to summarise the title is
/// <c>Claude Code</c>, which says nothing the row's own label does not.
/// </remarks>
public static class MuxTitle
{
    private const string Default = "Claude Code";

    public static string? Of(string? title)
    {
        var text = (title ?? "").Trim();

        // THE GLYPH, AND ONLY IT: one or two non-letters (a surrogate pair is two) before the first
        // space. A title that merely starts with punctuation further in keeps it.
        var space = text.IndexOf(' ', StringComparison.Ordinal);
        if (space is > 0 and <= 2 && !text[..space].Any(char.IsLetterOrDigit))
        {
            text = text[(space + 1)..].Trim();
        }
        else if (text.Length <= 2 && !text.Any(char.IsLetterOrDigit))
        {
            text = "";
        }

        return text.Length == 0 || text == Default ? null : text;
    }
}
