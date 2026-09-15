using System.Text.Json;

namespace Gg.Local;

/// <summary>One thing a tracker records about a work item.</summary>
/// <remarks>
/// <b>The tracker's own name, not a translation.</b> A person who wants to know
/// what <c>Microsoft.VSTS.Scheduling.StoryPoints</c> is looks it up in the
/// tracker, and a console that renamed it to "points" would have taught them a
/// word only this console uses.
/// </remarks>
public sealed record WorkItemField(string Name, string Value);

/// <summary>
/// What a tracker's field value says, whatever JSON shape it arrived in.
/// </summary>
/// <remarks>
/// <para>
/// <b>Because a work item is not a bag of strings.</b> Story points is a
/// number, "is this a bug" is a boolean, and a person is an object. The seven
/// named fields happen to be strings, so the reader that reads them only reads
/// strings — which is why adding a story point by name alone would have
/// produced an empty cell rather than a five.
/// </para>
/// <para>
/// <b>An object answers with the part a person would read.</b> ADO writes an
/// identity as displayName / uniqueName / imageUrl / descriptor; rendered whole
/// it is a wall of urls with the one useful word buried in it.
/// </para>
/// </remarks>
public static class WorkItemFields
{
    /// <summary>The names an object-shaped value hides a readable word behind.</summary>
    /// <remarks>
    /// In preference order, and every one of them is something a tracker calls
    /// the human-facing half of a record. Anything else is rendered as the JSON
    /// it arrived as, because guessing further would be this console inventing
    /// a reading the tracker did not offer.
    /// </remarks>
    private static readonly string[] Readable =
        ["displayName", "name", "title", "text", "value", "uniqueName"];

    /// <summary>What this value says, as a person would read it.</summary>
    public static string Text(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => value.GetString() ?? "",

        // RAW, so a number renders as the tracker wrote it. GetRawText keeps 5
        // as "5" and 1.5 as "1.5" rather than going through a double and
        // arriving as "1.5000000000000002" on some other machine.
        JsonValueKind.Number => value.GetRawText(),

        JsonValueKind.True => "true",
        JsonValueKind.False => "false",

        // ABSENT AND EMPTY ARE THE SAME HERE. A field the tracker sent as null
        // is one it holds nothing for, which is what an empty cell says.
        JsonValueKind.Null or JsonValueKind.Undefined => "",

        JsonValueKind.Object => Inside(value),

        // A LIST, JOINED. Tags and links arrive this way and read perfectly
        // well as one line; the alternative is a row per element under one
        // name, which is a table inside a cell.
        JsonValueKind.Array => string.Join(
            ", ", value.EnumerateArray().Select(Text).Where(t => t.Length > 0)),

        _ => value.GetRawText(),
    };

    /// <summary>The readable half of an object, or the whole thing.</summary>
    private static string Inside(JsonElement value)
    {
        foreach (var name in Readable)
        {
            if (value.TryGetProperty(name, out var said) && Text(said) is { Length: > 0 } text)
            {
                return text;
            }
        }

        return value.GetRawText();
    }
}
