using Gg.Console;

namespace Gg.Console.Tests;

/// <summary>
/// The help page's keys, as a tree a person can collapse.
/// </summary>
/// <remarks>
/// <para>
/// <b>The grouping already existed and could not be used.</b>
/// <c>HelpKeys</c> groups <c>Keymap.Catalogue()</c> by mode and renders each
/// group as a heading and a run of lines — so the structure is real and then
/// flattened into a string, where nothing can fold it, walk it, or ask how
/// many groups there are. This makes the same grouping a value.
/// </para>
/// <para>
/// <b>A model, not a widget.</b> Terminal.Gui's TreeView renders this; it does
/// not decide it. Which groups exist and which are open is <c>AppState</c>'s,
/// for the reason every other pane's state is: the console tears its whole UI
/// down to hand the terminal to an editor and rebuilds from the model, so
/// anything only a widget knows does not survive the trip.
/// </para>
/// <para>
/// <b>Untaught bindings stay out</b>, exactly as the flat render has them out.
/// A key deliberately not advertised does not become discoverable by changing
/// how the page is drawn.
/// </para>
/// </remarks>
public class TheKeysAreATreeTests
{
    [Test]
    public async Task Every_taught_binding_reaches_a_leaf()
    {
        var tree = HelpTree.Keys();

        var leaves = tree.SelectMany(g => g.Keys).Count();
        var taught = Keymap.Catalogue().Count(e => !e.Binding.Untaught);

        await Assert.That(leaves).IsEqualTo(taught)
            .Because("the tree is the same catalogue the flat page renders. A key that "
                   + "reaches one and not the other is a page that teaches two different "
                   + "consoles.");
    }

    [Test]
    public async Task An_untaught_binding_reaches_neither()
    {
        var tree = HelpTree.Keys();

        await Assert.That(tree.SelectMany(g => g.Keys).Any(k => k.Untaught)).IsFalse()
            .Because("a key deliberately not advertised does not become discoverable "
                   + "because somebody changed how the page is drawn.");
    }

    [Test]
    public async Task The_keys_a_person_can_always_press_come_first()
    {
        var tree = HelpTree.Keys();

        await Assert.That(tree).IsNotEmpty();
        await Assert.That(tree[0].Mode).IsEqualTo(UiMode.Normal)
            .Because("the flat page leads with them and prints no heading, because they "
                   + "are the answer to `what can I do', and a tree that buried them "
                   + "under a fold would make the common case the hidden one.");
    }

    [Test]
    public async Task Every_group_says_when_its_keys_apply()
    {
        foreach (var group in HelpTree.Keys())
        {
            await Assert.That(group.Heading).IsNotNull();
            await Assert.That(group.Heading.Length).IsGreaterThan(0)
                .Because($"{group.Mode} would otherwise be a fold with no name on it, and "
                       + "a person deciding whether to open it has only the name.");
        }
    }

    [Test]
    public async Task A_group_with_no_taught_keys_is_not_a_fold()
    {
        await Assert.That(HelpTree.Keys().Any(g => g.Keys.Count == 0)).IsFalse()
            .Because("an empty fold is a promise of something behind it. Modes whose keys "
                   + "are all untaught have nothing to show and must not appear.");
    }

    [Test]
    public async Task What_is_open_is_the_models_to_say()
    {
        var tree = HelpTree.Keys();

        await Assert.That(HelpTree.Opens(tree, UiMode.Normal)).IsTrue()
            .Because("the always-available keys open with the page: a tree whose useful "
                   + "half needs a keystroke to reveal is a worse flat page.");

        await Assert.That(HelpTree.Opens(tree, UiMode.Help)).IsFalse()
            .Because("everything else starts folded, which is the point of grouping a list "
                   + "that had grown past a screen.");
    }
}
