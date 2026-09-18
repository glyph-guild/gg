namespace Gg.Console;

/// <summary>
/// What somebody typed into the browse tab's field, read as what they meant.
/// </summary>
/// <remarks>
/// <para>
/// <b>Two acts were asked for and one field serves both</b>, because from a
/// person's side they are the same act: you know what you are after and you
/// type it. What was typed says which it was, so nothing has to be chosen
/// before the typing starts.
/// </para>
/// <para>
/// <b>Digits, and nothing else, are an id.</b> This console does not know which
/// trackers number their items and which key them, so the rule is the narrow
/// one - and the cost of guessing wrong in each direction is not symmetric. A
/// title read as an id fetches one wrong item; an id read as a title finds it
/// anyway, at the top of a short list.
/// </para>
/// <para>
/// <b>Nothing typed is nothing asked.</b> A search for spaces is the listing
/// already on the screen, and a page fetched for it is a page nobody asked for.
/// </para>
/// </remarks>
public static class BrowseFind
{
    /// <summary>What the field is asking for. Closed at two.</summary>
    public abstract record Wish
    {
        private Wish()
        {
        }

        /// <summary>One item, by the id the tracker gives it.</summary>
        public sealed record AnItem(string Id) : Wish;

        /// <summary>Items whose title carries these words.</summary>
        public sealed record SomeWords(string Text) : Wish;
    }

    /// <summary>What was typed, or null when nothing was.</summary>
    public static Wish? Wanted(string? typed)
    {
        if (typed is null || string.IsNullOrWhiteSpace(typed))
        {
            return null;
        }

        var said = typed.Trim();

        return said.All(char.IsAsciiDigit)
            ? new Wish.AnItem(said)
            : new Wish.SomeWords(said);
    }
}
