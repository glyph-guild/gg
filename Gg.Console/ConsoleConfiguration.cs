using Gg.Local;

namespace Gg.Console;

/// <summary>
/// Hands the configuration to an editor and takes back what was written.
/// </summary>
/// <remarks>
/// <para>
/// <b>Called with the terminal free, never from a session.</b> It opens a child
/// and then writes a file, and a UI session may do neither — which is why
/// <c>EditConfiguration</c> is in <c>ShellCommands.Handled</c> and this is
/// reached from <c>ConsoleLoop</c>.
/// </para>
/// <para>
/// <b>Parsed before it is written, which is the property the handoff buys.</b>
/// A field in a widget could accept anything and leave a person's machine
/// holding a document the next run declines to read. Here what came back is
/// checked, and a document that does not validate leaves the file exactly as it
/// was — with a sentence saying which value was wrong.
/// </para>
/// <para>
/// <b>Text in, text out</b>, the shape <c>IEditorSession</c> already has. The
/// real file is never handed to the editor: what a person edits is a rendering,
/// so a crash halfway through cannot leave a half-written document where the
/// configuration used to be.
/// </para>
/// </remarks>
public static class ConsoleConfiguration
{
    /// <summary>
    /// Opens the configuration in an editor and writes back what validates.
    /// </summary>
    /// <param name="path">The file, or null for the one this machine uses.</param>
    /// <param name="ask">
    /// Given the document as it stands, returns what a person wrote.
    /// </param>
    /// <returns>What happened, in one line, for the console to say.</returns>
    public static string? Edited(string? path, Func<string, string> ask)
    {
        ArgumentNullException.ThrowIfNull(ask);

        var at = path ?? ConfigurationFile.DefaultPath();
        var read = ConfigurationFile.Read(at);

        if (read.Diagnosis is { } unreadable)
        {
            // A BROKEN FILE IS STILL EDITABLE, and it is the case where editing
            // helps most - somebody has to be able to fix it. What is handed
            // over is the bytes as they are, so nothing is lost on the way in.
            return Written(at, ask(SafeToRead(at)), unreadable);
        }

        return Written(at, ask(Shown(read.Configuration)), was: null);
    }

    /// <summary>What to put in front of a person, given what is there now.</summary>
    /// <remarks>
    /// <b>Never an empty buffer.</b> A machine with no file gets a rendering of
    /// what is in force, so the keys are in front of somebody who has not
    /// learned their spelling — which is every person the first time.
    /// </remarks>
    private static string Shown(Configuration? configuration)
    {
        var rendered = ConfigurationFile.Render(configuration ?? Settings.Seed());

        // `{}` IS AN EMPTY BUFFER WEARING BRACES, and it is what a machine
        // nobody has configured renders to - the seed records only what
        // somebody actually set, and on most machines that is nothing.
        return rendered.TrimEnd().Length > 2 ? rendered : Template();
    }

    /// <summary>Every key, set to nothing, for a person who has none.</summary>
    /// <remarks>
    /// <para>
    /// <b>Null rather than the value in force.</b> Rendering the defaults would
    /// put a document in front of somebody that PINS them the moment they save:
    /// the machine would keep those values after the built-in ones changed, and
    /// nothing on the page would say why it differed from a fresh machine. The
    /// same argument <c>Settings.Seed</c> makes, one surface out.
    /// </para>
    /// <para>
    /// <b>Saving it unchanged does nothing</b>, which is what makes it safe to
    /// hand over: a null member is an absent one, and the next render drops the
    /// line. So the template teaches the spellings and commits to nothing.
    /// </para>
    /// </remarks>
    private static string Template() =>
        "{\n"
      + string.Join(",\n", Configuration.Members.Select(m => $"  \"{m.Key}\": null"))
      + "\n}\n";

    private static string SafeToRead(string at)
    {
        try
        {
            return File.ReadAllText(at);
        }
        catch (IOException)
        {
            return ConfigurationFile.Render(Settings.Seed());
        }
        catch (UnauthorizedAccessException)
        {
            return ConfigurationFile.Render(Settings.Seed());
        }
    }

    private static string Written(string at, string edited, string? was)
    {
        var parsed = ConfigurationFile.Parse(edited);

        if (parsed.Diagnosis is { } refused)
        {
            // NOTHING IS WRITTEN, and the sentence is the parser's rather than
            // one composed here. It names the value, which is what somebody
            // about to open the editor again needs.
            return $"Nothing was written: {refused}";
        }

        try
        {
            ConfigurationFile.Write(parsed.Configuration!, at);
        }
        catch (IOException unwritable)
        {
            return $"Nothing was written: {unwritable.Message}";
        }
        catch (UnauthorizedAccessException unwritable)
        {
            return $"Nothing was written: {unwritable.Message}";
        }

        // WHAT CHANGED IS NOT SAID, deliberately. A diff belongs on the
        // Environment page, which the next session renders from the file this
        // just wrote - and a sentence claiming a change it computed separately
        // would be a second answer to a question the page already answers.
        return was is null
            ? $"Configuration written to {at}."
            : $"Configuration written to {at}, replacing one that could not be read.";
    }
}
