using Gg.Client;
using Gg.Console;

namespace Gg.Console.Tests;

/// <summary>
/// `p` on the Envelope tab renders the estate into the working copy.
/// </summary>
/// <remarks>
/// <para>
/// <b>The pane could show an estate and not bring one into being.</b> Until a
/// pull happens there is no working copy at all, so the documents column said
/// <i>nothing is pulled here yet</i> and named a verb a person had to leave the
/// console to run. That is the one act without which none of the others have a
/// subject.
/// </para>
/// <para>
/// <b>A write, so it releases the terminal.</b> Pull overwrites files with
/// canonical renderings, which puts it in <c>ShellCommands.Handled</c> rather
/// than <c>Reads</c> — the exception that lets a read run on a background task
/// is for a read, and does not stretch to a write. Every write in this console
/// already has that shape.
/// </para>
/// <para>
/// <b>The refusal is the feature.</b> Pull refuses a dirty tree and names the
/// files, because overwriting formatting and overwriting somebody's unfinished
/// edit are different acts and only one is intended. A console that swallowed
/// that into "pull failed" would take away the only thing that tells a person
/// what to do next — commit, or discard.
/// </para>
/// <para>
/// <b>Scoped to the tab, the way `f` already is.</b> `f` freezes on the Live
/// tab and flies on Browse; a key that meant pull everywhere would be a key
/// that writes a directory from a screen with nothing to do with it. `p` is
/// free because the checklist tab was removed, and it is the letter in the word.
/// </para>
/// </remarks>
public class PullingFromTheConsoleTests
{
    private static KeymapContext On(TabId tab) => new(UiMode.Normal, tab);

    [Test]
    public async Task The_key_pulls_while_the_envelope_tab_is_showing()
    {
        var command = Keymap.Resolve(KeyStroke.Char('p'), On(TabId.Envelope));

        await Assert.That(command).IsEqualTo(Command.PullEstate);
    }

    [Test]
    public async Task It_does_nothing_on_any_other_tab()
    {
        // THE SCOPE IS THE POINT. This writes a directory, so a key that meant
        // it from the queue would overwrite a working copy from a screen that
        // has nothing to do with one.
        foreach (var tab in Tabs.All.Where(t => t != TabId.Envelope))
        {
            await Assert.That(Keymap.Resolve(KeyStroke.Char('p'), On(tab))).IsNull()
                .Because($"`p' is the envelope tab's, and it resolved on {Tabs.Name(tab)}.");
        }
    }

    [Test]
    public async Task It_is_advertised_where_it_works_and_nowhere_else()
    {
        await Assert.That(Keymap.Hints(On(TabId.Envelope))).Contains("p ", StringComparison.Ordinal)
            .Because("the hint line is where a person finds a key that has nowhere else to "
                   + "live, and this one is live only here.");

        await Assert.That(Keymap.Hints(On(TabId.Queue)))
            .DoesNotContain("p pull", StringComparison.Ordinal)
            .Because("advertising a key that does nothing is worse than one nobody was "
                   + "shown.");
    }

    [Test]
    public async Task Pulling_is_the_shell_s_work_and_never_a_session_s()
    {
        await Assert.That(ShellCommands.Handled).Contains(Command.PullEstate)
            .Because("it overwrites files, and a UI session may not. The read exception "
                   + "AutoRefresh carries is for a read and does not stretch to a write.");

        await Assert.That(ShellCommands.Reads).DoesNotContain(Command.PullEstate)
            .Because("a Reads command is patched back into the model without tearing the "
                   + "session down, which is right for a read and wrong for something that "
                   + "writes a directory.");
    }

    [Test]
    public async Task The_reducer_does_not_pull_anything_itself()
    {
        // A shell command the reducer also acted on would do the thing twice,
        // or do a different thing in the session than the shell does after it.
        var before = new AppState { ActiveTab = TabId.Envelope };

        await Assert.That(Reducer.Reduce(before, Command.PullEstate)).IsEqualTo(before)
            .Because("the shell handles this one, so the reducer's answer must be the state "
                   + "it was given.");
    }

    [Test]
    public async Task A_dirty_tree_is_refused_by_naming_the_files()
    {
        var lines = ConsolePull.Pulled(
            () => throw new DirtyWorkingCopyException(
                ["airspace/root.yaml", "airspace/narrowings/pci.yaml"]));

        var said = string.Join('\n', lines);

        await Assert.That(said).Contains("pci.yaml", StringComparison.Ordinal)
            .Because("the files are what a person acts on - commit them or discard them - "
                   + "and a refusal that only says the tree is dirty leaves them hunting.");
        await Assert.That(said).DoesNotContain("Exception", StringComparison.Ordinal)
            .Because("a refusal is an answer, not a fault, and it reads as one.");
    }

    [Test]
    public async Task What_a_pull_wrote_is_said_in_counts_rather_than_a_file_list()
    {
        // A WHOLE ESTATE IS DOZENS OF FILES. The activity line is one line, so
        // it says how many and what changed rather than listing a screenful
        // nobody asked for - the pane below already shows the documents.
        var lines = ConsolePull.Pulled(() => new VerbResult.AirspacePulled(new TreeWritten
        {
            Written = ["airspace/root.yaml", "airspace/narrowings/pci.yaml"],
            Removed = ["airspace/work-kinds/gone.yaml"],
            Unrepresentable = [],
        }));

        var said = string.Join('\n', lines);

        await Assert.That(said).Contains("2", StringComparison.Ordinal);
        await Assert.That(said).Contains("1", StringComparison.Ordinal)
            .Because("a removal is a document whose stream ended, and somebody who does not "
                   + "know one went is somebody with a file they will look for later.");
    }

    [Test]
    public async Task A_name_no_path_can_carry_is_named_rather_than_skipped()
    {
        var lines = ConsolePull.Pulled(() => new VerbResult.AirspacePulled(new TreeWritten
        {
            Written = [],
            Removed = [],
            Unrepresentable = ["Payments/EU"],
        }));

        var said = string.Join('\n', lines);

        await Assert.That(said).Contains("Payments/EU", StringComparison.Ordinal)
            .Because("a name declared before the name rule existed cannot be written back, "
                   + "and a file that is silently absent is worse than a name that is "
                   + "named.");
    }
}
