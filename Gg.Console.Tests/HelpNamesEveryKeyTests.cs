namespace Gg.Console.Tests;

/// <summary>
/// The help page is where a person goes to find a key they do not know. It has
/// to hold every one.
/// </summary>
/// <remarks>
/// <para>
/// <b>IT WAS SHOWING THE KEYS THAT HAPPEN TO BE LIVE.</b> <c>HelpKeys</c> asked
/// the keymap for the bindings of one context — Normal mode, with whatever the
/// live and freeze flags were — so <c>f</c> was missing from the page whenever
/// neither the live pane nor browse was showing, which is how a console starts.
/// So were the gate modal's <c>a</c> and <c>r</c>, the confirmation's <c>y</c>,
/// and <c>t</c> and <c>h</c> unless the selected flight happened to qualify.
/// The hint line is right to show only what is live; the help page is the
/// opposite question.
/// </para>
/// <para>
/// <b>And two keys are deliberately not taught.</b> <c>j</c> and <c>k</c> stay
/// bound — a person whose focus is on a pane rather than the list still needs
/// them — but the arrow keys move the queue through the list widget itself, so
/// advertising a second pair spends two of the fourteen slots on the hint line
/// teaching a vim habit to somebody who does not have one.
/// </para>
/// </remarks>
public class HelpNamesEveryKeyTests
{
    private static AppState Helping() => new() { Mode = UiMode.Help, HelpPage = HelpPage.Keys };

    [Test]
    public async Task Every_key_the_console_has_is_on_the_help_page()
    {
        var page = PaneText.Modal(Helping());

        foreach (var binding in Keymap.Catalogue().Where(b => !b.Binding.Untaught))
        {
            await Assert.That(page).Contains(binding.Binding.Key.Name, StringComparison.Ordinal)
                .Because($"{binding.Binding.Key.Name} ({binding.Binding.Description}) is a key this "
                       + "console answers, and help is where somebody looks for a key they do not "
                       + "know. Page:\n" + page);
        }
    }

    /// <summary>
    /// Every context there is, as the product of every member the keymap has.
    /// </summary>
    /// <remarks>
    /// Complete rather than reachable. This is a pure function over a struct,
    /// so a shape the console cannot get into still has to answer, and the
    /// union is only a union if nothing is left out of it.
    /// </remarks>
    private static IEnumerable<KeymapContext> Everywhere() =>
        from mode in Enum.GetValues<UiMode>()
        from showing in Enum.GetValues<TabId>()
        from frozen in (bool[])[false, true]
        from takeable in (bool[])[false, true]
        from handedBack in (bool[])[false, true]
        from started in (bool[])[false, true]
        select new KeymapContext(mode, showing, frozen, takeable, handedBack)
        {
            SignInStarted = started,
        };

    [Test]
    public async Task The_catalogue_holds_every_key_the_keymap_can_resolve()
    {
        // THE HALF THE PAGE TEST CANNOT SEE. Every_key_the_console_has_is_on_the
        // _help_page walks the CATALOGUE and checks the page renders it, so a
        // key the catalogue never learned about is invisible to it - the page
        // and the catalogue agree, and both are missing the same key.
        //
        // Catalogue builds itself by enumerating shapes of context, and its own
        // remarks say a flag left out of that enumeration "would show up as a
        // key missing from the page, which is what HelpNamesEveryKeyTests
        // asserts". This is that assertion. Until it existed the claim was
        // about a test that did not check it.
        var catalogued = Keymap.Catalogue()
            .Select(entry => (entry.Mode, entry.Binding.Key, entry.Binding.Command))
            .ToHashSet();

        var missing = (from context in Everywhere()
                       from binding in Keymap.Bindings(context)
                       select (context.Mode, binding.Key, binding.Command))
            .Distinct()
            .Where(live => !catalogued.Contains(live))
            .Select(live => $"{live.Mode}/{live.Key.Name} {live.Command}")
            .ToList();

        await Assert.That(missing).IsEmpty()
            .Because("a key that resolves somewhere and is in no catalogue entry cannot reach "
                   + "the help page, and the page is where somebody looks for a key they do "
                   + "not know. Found: " + string.Join(", ", missing));
    }

    [Test]
    public async Task The_product_above_is_over_every_flag_the_keymap_has()
    {
        // The ratchet on the ratchet. Everywhere() is a written-out product, so
        // a seventh member on KeymapContext would leave it enumerating six.
        var members = typeof(KeymapContext)
            .GetProperties()
            .Select(p => p.Name)
            .ToList();

        await Assert.That(members.Count).IsEqualTo(6)
            .Because("Everywhere() crosses every one of these, and a member left out of it "
                   + "would leave the completeness check above quietly incomplete - which is "
                   + "exactly how the shapes it audits came to be missing one. Found: "
                   + string.Join(", ", members));
    }

    [Test]
    public async Task A_key_that_only_works_sometimes_says_when()
    {
        // The catalogue is a union over every context, so a page built from it
        // would otherwise list `f` twice with two meanings and no way to tell
        // which applies - which is worse than leaving it out, because it reads
        // as a contradiction rather than a condition.
        var conditional = Keymap.Catalogue()
            .Where(entry => entry.Binding.When is null or { Length: 0 })
            .Where(entry => Keymap.Resolve(entry.Binding.Key, new KeymapContext(entry.Mode)) is null)
            .Select(entry => $"{entry.Mode}/{entry.Binding.Key.Name} {entry.Binding.Description}")
            .ToList();

        await Assert.That(conditional).IsEmpty()
            .Because("a key that is not live in the plainest form of its own mode is one whose "
                   + "condition a person cannot see. Say when it applies. Found: "
                   + string.Join(", ", conditional));
    }

    [Test]
    public async Task The_help_page_does_not_teach_j_and_k()
    {
        var page = PaneText.Modal(Helping());

        foreach (var hidden in (string[])["j", "k"])
        {
            await Assert.That(page).DoesNotContain($"  {hidden,-8}", StringComparison.Ordinal)
                .Because($"'{hidden}' is bound and not taught: the arrows do this through the "
                       + "list widget, and a second pair spends a row on a habit. Page:\n" + page);
        }
    }

    [Test]
    public async Task The_hint_line_does_not_teach_j_and_k_either()
    {
        var hints = Keymap.Hints(new KeymapContext(UiMode.Normal));

        await Assert.That(hints).DoesNotContain("j down", StringComparison.Ordinal);
        await Assert.That(hints).DoesNotContain("k up", StringComparison.Ordinal);

        // THE ANCHOR. A hint line that lost everything would pass the two
        // above, and the line is the only place most people ever read a key.
        await Assert.That(hints).Contains("q quit", StringComparison.Ordinal);
        await Assert.That(hints).Contains("? help", StringComparison.Ordinal);
    }

    [Test]
    public async Task A_key_off_the_line_is_still_in_help_unless_something_else_does_its_job()
    {
        // WHAT THE ONE FLAG COST, ASSERTED. `Hidden` meant both "not on the
        // line" and "not on the page", so moving the six tab keys off the line
        // took them out of help - the page that exists to name every key - and
        // the test above was iterating the same flag, so it passed while the
        // page was wrong. Two claims, two properties, and this is the sentence
        // that connects them.
        var page = PaneText.Modal(Helping());

        foreach (var entry in Keymap.Catalogue().Where(e => e.Binding.OffTheHintLine))
        {
            if (entry.Binding.Untaught)
            {
                continue;
            }

            await Assert.That(page).Contains(entry.Binding.Key.Name, StringComparison.Ordinal)
                .Because($"{entry.Binding.Key.Name} ({entry.Binding.Description}) is off the "
                       + "hint line, so help is where a person finds it. Page:\n" + page);
        }

        // AND THE ONLY TWO THAT ARE IN NEITHER PLACE ARE THE TWO THE ARROWS DO.
        var untaught = Keymap.Catalogue()
            .Where(e => e.Binding.Untaught)
            .Select(e => e.Binding.Key.Name)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();

        await Assert.That(untaught).IsEquivalentTo((string[])["j", "k"])
            .Because("a key in neither place is one a person can only find by reading the "
                   + "source. Found: " + string.Join(", ", untaught));
    }

    [Test]
    public async Task The_line_keeps_what_a_person_uses_and_drops_what_they_do_not()
    {
        var hints = Keymap.Hints(new KeymapContext(UiMode.Normal));

        foreach (var gone in (string[])["add credential", "forget credential", "evidence", "browse"])
        {
            await Assert.That(hints).DoesNotContain(gone, StringComparison.Ordinal)
                .Because("this is on a tab or in help. The line is one line. Line: " + hints);
        }

        foreach (var kept in (string[])["q quit", "d decide", "n new flight", "y fly by hand"])
        {
            await Assert.That(hints).Contains(kept, StringComparison.Ordinal)
                .Because("this has nowhere else to be advertised. Line: " + hints);
        }
    }

    [Test]
    public async Task The_keys_that_are_still_bound_are_still_bound()
    {
        // Hidden is about the page, not about the keyboard. A person reading
        // this file should not be able to conclude that j stopped working.
        await Assert.That(Keymap.Resolve(KeyStroke.Char('j'), new KeymapContext(UiMode.Normal)))
            .IsEqualTo(Command.SelectNext);
        await Assert.That(Keymap.Resolve(KeyStroke.Char('k'), new KeymapContext(UiMode.Normal)))
            .IsEqualTo(Command.SelectPrevious);
    }
}
