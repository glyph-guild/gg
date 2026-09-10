using Gg.Local;

namespace Gg.Console;

/// <summary>
/// Says where this machine's airspace working copy is, into the configuration
/// file.
/// </summary>
/// <remarks>
/// <para>
/// <b>One value, not the whole document.</b>
/// <c>ConsoleConfiguration.Edited</c> hands a person every setting at once,
/// which is right for the Environment page and wrong here: somebody on the
/// airspace tab is answering one question, and a whole JSON document is a
/// larger thing to get wrong than the answer is worth.
/// </para>
/// <para>
/// <b>Into the file the verbs read, and nowhere else.</b> The doctor, the three
/// airspace verbs and the console all resolve through
/// <c>Settings</c> — so a path the console held privately would be a second
/// place this lives, and the two would disagree the first time either was
/// edited.
/// </para>
/// <para>
/// <b>An empty answer is an escape, not a clear.</b> Somebody who opened the
/// editor and thought better of it must not end up with no path: an empty
/// <c>airspace</c> makes every verb on that tab refuse, and the person who
/// caused it would have been trying to leave.
/// </para>
/// </remarks>
public static class ConsoleAirspacePath
{
    /// <summary>Takes a path and writes it, or says why it did not.</summary>
    /// <param name="path">
    /// The configuration file to write, or null for this machine's own.
    /// </param>
    /// <param name="typed">
    /// What somebody typed into the field on the airspace tab. The field
    /// collects it during a session and this runs after one, with the terminal
    /// free — which is what keeps <c>ConsoleScreen</c>'s rule true: nothing is
    /// written by typing into a widget, only collected by one.
    /// </param>
    public static string Set(string? path, string? typed)
    {
        var at = path ?? ConfigurationFile.DefaultPath();

        var answered = (typed ?? "").Trim();

        if (answered.Length == 0)
        {
            return "The airspace is unchanged: nothing was written.";
        }

        var read = ConfigurationFile.Read(at);

        if (read.Diagnosis is { } unreadable)
        {
            // NOT OVERWRITTEN. A broken document is somebody's work, and
            // replacing it with one key's worth of settings would take the rest
            // of it with them. The Environment page's editor is where a broken
            // file gets fixed, because that one hands over the bytes as they
            // are.
            return $"Nothing was written: {unreadable} Fix the file from the Environment "
                 + "page, then set this again.";
        }

        var configuration = Settings.With(
            read.Configuration ?? new Configuration(), "GG_AIRSPACE", answered);

        if (Configuration.Validate(configuration) is { } refused)
        {
            return $"Nothing was written: {refused}";
        }

        try
        {
            ConfigurationFile.Write(configuration, at);
        }
        catch (Exception unwritable) when (
            unwritable is IOException or UnauthorizedAccessException)
        {
            return $"Nothing was written: {unwritable.Message}";
        }

        // NOT THERE YET IS A CLAUSE, NOT A REFUSAL. The order a person works in
        // is set-then-pull, and AirspaceTree.Write creates the tree it renders
        // into - so refusing an absent directory would make this unusable every
        // first time. Saying so matters because the next key press creates it.
        var missing = Directory.Exists(answered)
            ? ""
            : " It is not there yet; pull will create it.";

        // AND WHETHER GIT CAN SEE IT, for the reason the doctor says it: pull's
        // dirty-tree refusal is computed from a git answer that is empty for a
        // plain directory, so somewhere that is not a working tree is somewhere
        // pull will overwrite without being able to warn.
        var untracked = Directory.Exists(answered) && !Gg.Client.Git.IsRepository(answered)
            ? " It is not a git working tree, so pull cannot refuse to overwrite an "
            + "uncommitted edit."
            : "";

        return $"The airspace is {answered}.{missing}{untracked}";
    }
}
