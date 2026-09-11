namespace Gg.Local;

/// <summary>
/// The first move a person can pick when a drafting session opens.
/// </summary>
/// <remarks>
/// <para>
/// <b>FOR THE PERSON, WHERE THE TOOLS ARE FOR THE AGENT.</b>
/// <see cref="AirspaceContextTool"/> and <see cref="AirspacePullTool"/> answer
/// an agent that has already been asked something. Whoever pressed the key
/// arrives at an empty prompt with nothing telling them what this session is
/// for, and <c>PtyDraftSession</c> sends no prompt on their behalf — rightly,
/// because a prompt gg sent would start an agent working before anybody said
/// what they wanted.
/// </para>
/// <para>
/// <b>A prompt is the one channel that splits that difference.</b> The client
/// offers it as a command; gg writes the wording; the person picks it, edits
/// it if they like, and sends it. Nothing is sent for them. It is the shape
/// the console already uses for <c>n</c>, which opens a question rather than
/// composing an answer.
/// </para>
/// <para>
/// <b>Orient, do not act — and that is most of why the wording exists.</b>
/// Somebody who has just opened a drafting session has not said what they
/// want changed yet, so the opening move reads and reports and says plainly
/// not to write. An agent that guessed would spend its first turn undoing the
/// guess, and the person would spend theirs reading it.
/// </para>
/// <para>
/// <b>It names <c>describe_airspace</c>, which is the whole reason it is worth
/// writing.</b> A person cannot know to ask for a tool they have never heard
/// of, and an agent reading its own tool list may or may not reach for it
/// first. One sentence in the opening turn settles both.
/// </para>
/// </remarks>
public static class DraftingPrompt
{
    /// <summary>The server key, and therefore the command's prefix.</summary>
    public const string Server = NominationTool.Server;

    /// <summary>The prompt, as the server declares it.</summary>
    public const string Name = "start_drafting";

    /// <summary>The command as a person types it.</summary>
    public const string Qualified = $"mcp__{Server}__{Name}";

    /// <summary>What the list a person picks from says about it.</summary>
    public const string Description =
        "Read this tenant's airspace and report what governs it, before changing anything.";

    /// <summary>
    /// The turn it sends, in the person's voice.
    /// </summary>
    /// <remarks>
    /// <b>Public so the wording can be asserted</b>, which is what the fleet
    /// path does with the prompt it builds, and for the same reason: a
    /// sentence that decides how a session opens is not a detail of how it is
    /// sent.
    /// </remarks>
    public static string Text { get; } =
        $"Start by calling {AirspaceContextTool.Name}. It tells you how this tenant's "
      + "envelope documents are read - which you cannot work out from the files - and "
      + "shows you what is already here.\n"
      + "\n"
      + "Then tell me, briefly: what the rules in force require, which documents exist, "
      + "and anything that looks wrong or duplicated.\n"
      + "\n"
      + "Do NOT write or submit any document yet. I will tell you what I want changed "
      + "once I have read what you found.";
}
