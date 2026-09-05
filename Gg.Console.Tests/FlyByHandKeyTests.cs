using Gg.Console;

namespace Gg.Console.Tests;

/// <summary>
/// The key that hands this flight to the person sitting at the console.
/// </summary>
/// <remarks>
/// <para>
/// <b>Offered only where it means something, and the hint comes from the same
/// context dispatch uses.</b> A key advertised in a state it does not work in is
/// a key that does nothing, and a hint built from a second source is a hint that
/// drifts from what the key actually does.
/// </para>
/// <para>
/// <b>Its own key rather than a modifier on <c>fly this</c>.</b> Flying by hand
/// and flying on the fleet are the same act with different consequences for
/// where the work happens, and a person choosing between them is choosing before
/// they press, not after.
/// </para>
/// </remarks>
public class FlyByHandKeyTests
{
    private static IReadOnlyList<KeyBinding> Bindings(KeymapContext context) =>
        Keymap.Bindings(context);

    private static KeymapContext Normal() => new(UiMode.Normal);

    // ---- S26.5-04 ----

    [Test]
    public async Task The_key_is_offered_in_normal_mode()
    {
        var bound = Bindings(Normal()).Where(b => b.Command == Command.FlyByHand).ToList();

        await Assert.That(bound.Count).IsEqualTo(1)
            .Because("a command with two bindings resolves to whichever was written first.");

        await Assert.That(bound[0].Description).IsNotEmpty();
    }

    [Test]
    public async Task It_resolves_to_the_command_it_advertises()
    {
        // THE TWO HALVES ARE THE SAME TABLE, which is what stops a key that is
        // advertised and inert - four of those existed here until ShellCommands
        // was one declaration.
        var binding = Bindings(Normal()).Single(b => b.Command == Command.FlyByHand);

        await Assert.That(Keymap.Resolve(binding.Key, Normal())).IsEqualTo(Command.FlyByHand);
    }

    [Test]
    public async Task It_is_not_offered_while_a_modal_holds_the_keyboard()
    {
        // A TENANT-LEVEL WRITE, in Normal mode only. A modal holds the keyboard
        // while it is open, and a key reachable from a gate decision would be
        // doing something unrelated to the question on screen.
        foreach (var mode in Enum.GetValues<UiMode>().Where(m => m != UiMode.Normal))
        {
            await Assert.That(Bindings(new KeymapContext(mode)).Any(
                    b => b.Command == Command.FlyByHand))
                .IsFalse()
                .Because($"{mode} offers it, and a modal that answers a question about one "
                       + "flight must not also open another.");
        }
    }

    [Test]
    public async Task It_does_not_take_a_key_another_command_already_has()
    {
        // EVERY BINDING IN EVERY REACHABLE CONTEXT, because the table is a pure
        // function that can be handed any of them and a duplicate resolves to
        // whichever was written first - silently.
        foreach (var context in (KeymapContext[])
                 [
                     Normal(),
                     Normal() with { BrowseVisible = true },
                     Normal() with { LiveVisible = true },
                     Normal() with { Takeable = true, HandedBackable = true },
                     Normal() with { ChecklistVisible = true, EnvelopeVisible = true },
                     Normal() with { RepositoriesVisible = true },
                 ])
        {
            var duplicates = Bindings(context)
                .GroupBy(b => b.Key)
                .Where(g => g.Count() > 1)
                .Select(g => $"{g.Key}: {string.Join(", ", g.Select(b => b.Command))}")
                .ToList();

            await Assert.That(duplicates).IsEmpty()
                .Because("Found: " + string.Join(" | ", duplicates));
        }
    }
}
