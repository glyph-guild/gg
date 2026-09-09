using Gg.Local;

namespace Gg.Console.Tests;

/// <summary>
/// Changing configuration from the console, with the terminal free.
/// </summary>
/// <remarks>
/// <para>
/// <b>A handoff, not a field.</b> The one <c>TextField</c> in this console is
/// read-only and says why: <i>"Nothing in this console is written by typing into
/// a widget — a write happens between sessions with the terminal provably free —
/// so a field that accepted a keystroke would be a promise it cannot keep."</i>
/// So the session ends, <c>ConsoleLoop</c> hands the document to
/// <c>$EDITOR</c>, and what comes back is validated before anything is written.
/// </para>
/// <para>
/// <b>Which buys the property a field could not.</b> An edit that does not
/// validate is refused and the file on disk is untouched, so a person cannot
/// leave their machine holding a document the next run declines to read.
/// </para>
/// <para>
/// <b>The key is in help, where the settings already are.</b> Not a Normal-mode
/// letter: the Environment page is where somebody reads what is configured, and
/// the place you read a thing is the place to change it. It costs no letter from
/// the console's own alphabet.
/// </para>
/// </remarks>
public class ConfigurationIsEditedBetweenSessionsTests
{
    [Test]
    public async Task Editing_is_the_shell_s_work_and_never_a_session_s()
    {
        // It opens a child process and writes a file. Both are things a UI
        // session may not do, so being in this set is the whole design.
        await Assert.That(ShellCommands.Handled).Contains(Command.EditConfiguration)
            .Because("it spawns an editor and writes a file, and a session may do neither.");
    }

    [Test]
    public async Task The_key_is_offered_where_the_settings_are_read()
    {
        var inHelp = Keymap.Resolve(KeyStroke.Char('e'), new KeymapContext(UiMode.Help));

        await Assert.That(inHelp).IsEqualTo(Command.EditConfiguration)
            .Because("the Environment page is where a person reads what is configured.");
    }

    [Test]
    public async Task The_key_is_not_taken_from_normal_mode()
    {
        // `e` is the envelope pane in Normal mode, and this must not shadow it.
        // The help modal owns the keyboard while it is open, which is what makes
        // the same letter safe in both.
        var inNormal = Keymap.Resolve(KeyStroke.Char('e'), new KeymapContext(UiMode.Normal));

        await Assert.That(inNormal).IsEqualTo(Command.ToggleEnvelope)
            .Because("Normal mode's `e` is the envelope and stays the envelope.");
    }

    [Test]
    public async Task An_edit_that_does_not_validate_is_not_written()
    {
        // THE PROPERTY THE HANDOFF BUYS. The file on disk stays readable,
        // always, because what came back is parsed before it is written.
        var path = Path.Combine(Path.GetTempPath(), $"gg-edit-{Guid.NewGuid():N}.json");

        try
        {
            ConfigurationFile.Write(new Configuration { Editor = "vi" }, path);

            var said = ConsoleConfiguration.Edited(
                path, _ => "{ \"runner-hold-seconds\": 0 }");

            await Assert.That(said).IsNotNull();
            await Assert.That(said!).Contains("0", StringComparison.Ordinal)
                .Because($"the console has to say what was wrong, not just that something "
                       + $"was. Said: {said}");

            await Assert.That(ConfigurationFile.Read(path).Configuration!.Editor)
                .IsEqualTo("vi")
                .Because("the document on disk is what it was before the edit.");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public async Task An_edit_that_validates_is_written_and_said()
    {
        var path = Path.Combine(Path.GetTempPath(), $"gg-edit-{Guid.NewGuid():N}.json");

        try
        {
            ConfigurationFile.Write(new Configuration { Editor = "vi" }, path);

            var said = ConsoleConfiguration.Edited(path, _ => "{ \"editor\": \"hx\" }");

            await Assert.That(ConfigurationFile.Read(path).Configuration!.Editor).IsEqualTo("hx");
            await Assert.That(said).IsNotNull()
                .Because("a write nobody is told about is a key that looks broken.");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public async Task The_editor_is_handed_what_is_there_now()
    {
        // Opening on a blank page would make every edit a rewrite, and the
        // first thing anybody does is delete a setting they meant to keep.
        var path = Path.Combine(Path.GetTempPath(), $"gg-edit-{Guid.NewGuid():N}.json");

        try
        {
            ConfigurationFile.Write(new Configuration { Editor = "vi" }, path);

            string? handed = null;
            ConsoleConfiguration.Edited(path, given => { handed = given; return given; });

            await Assert.That(handed).IsNotNull();
            await Assert.That(handed!).Contains("\"editor\": \"vi\"", StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public async Task A_machine_with_no_file_is_handed_something_to_start_from()
    {
        // Not an empty buffer. A person who opens this and sees nothing has to
        // know the key names before they can write one.
        var path = Path.Combine(Path.GetTempPath(), $"gg-edit-{Guid.NewGuid():N}.json");

        string? handed = null;
        ConsoleConfiguration.Edited(path, given => { handed = given; return given; });

        try
        {
            await Assert.That(handed).IsNotNull();
            await Assert.That(handed!).Contains("control-plane", StringComparison.Ordinal)
                .Because("an empty buffer asks somebody to know the spelling of every key "
                       + "before they can set one.");
        }
        finally
        {
            File.Delete(path);
        }
    }
}
