namespace Gg.Console.Tests;

/// <summary>
/// The console advertises no key whose effect nobody can see.
/// </summary>
/// <remarks>
/// <para>
/// <b><c>e</c> — "edit notes" — was one.</b> It ended the UI session, handed the
/// terminal to <c>$EDITOR</c>, read the text back, and put it in
/// <c>AppState.Notes</c>. Nothing rendered that field, nothing sent it anywhere,
/// and it was discarded on quit. Two references in the whole codebase: the
/// assignment and the declaration.
/// </para>
/// <para>
/// <b>It was not dead code, which is why it survived.</b> It was the vehicle for
/// the terminal-release property — <i>the model is the only survivor</i> — and a
/// scratchpad is the easiest payload to round-trip. That property is now carried
/// by <c>new flight</c>, which hands the terminal to the same editor for a reason
/// somebody asked for.
/// </para>
/// <para>
/// <b><see cref="ShellHandledTests"/> is the neighbour this belongs beside.</b>
/// That guard was written after four keys were found bound, advertised in the
/// hint line, and inert — and it holds the letter of the rule: every bound
/// command is declared as the shell's and reaches the loop. <c>e</c> passed it.
/// It reached the loop, the loop did something, and the something was invisible.
/// This holds the other half.
/// </para>
/// </remarks>
public class NoScratchpadKeyTests
{
    [Test]
    public async Task No_key_opens_an_editor_on_a_scratchpad()
    {
        var bound = Keymap.Bindings(new KeymapContext(UiMode.Normal))
            .Select(binding => binding.Description)
            .ToList();

        await Assert.That(bound).DoesNotContain("edit notes")
            .Because("a key advertised in the hint line whose result nothing displays is the "
                   + "exact failure ShellHandledTests was written for, one layer up.");
    }

    [Test]
    public async Task The_model_carries_no_field_that_only_a_removed_key_wrote()
    {
        // The other half of the removal. Leaving `Notes` behind would leave a
        // field on the model that nothing writes and nothing reads - which is
        // how it comes back.
        await Assert.That(typeof(AppState).GetProperties().Select(p => p.Name))
            .DoesNotContain("Notes");
    }

    [Test]
    public async Task The_keys_that_remain_are_still_advertised()
    {
        // THE LIVENESS ANCHOR. A keymap that returned nothing would pass both
        // assertions above while removing the whole console.
        var hints = Keymap.Bindings(new KeymapContext(UiMode.Normal))
            .Select(binding => binding.Description)
            .ToList();

        await Assert.That(hints).Contains("new flight");
        await Assert.That(hints.Count).IsGreaterThan(3);
    }
}
