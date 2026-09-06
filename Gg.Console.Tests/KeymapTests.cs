namespace Gg.Console.Tests;

/// <summary>
/// The keymap is pure, total, and the only place bindings live.
/// </summary>
public class KeymapTests
{
    /// <summary>
    /// Every keystroke the console could ever be handed.
    /// </summary>
    /// <remarks>
    /// Enumerated rather than sampled, because the hints check below is an
    /// EQUALITY and an equality needs both sides to be complete. A sampled
    /// universe would make "advertised keys equal live keys" mean "advertised
    /// keys equal the live keys I thought to try".
    /// </remarks>
    internal static IReadOnlyList<KeyStroke> Universe { get; } =
    [
        .. Enumerable.Range(32, 95).Select(c => KeyStroke.Char((char)c)),
        .. Enumerable.Range('a', 26).Select(c => KeyStroke.Control((char)c)),
        KeyStroke.Esc,
        KeyStroke.TabKey,
    ];

    /// <summary>Every context the console can be in.</summary>
    internal static IReadOnlyList<KeymapContext> EveryContext { get; } =
    [
        // EVERY TAB, not just whether the live pane was showing. The context
        // used to carry five booleans over one shared region and this crossed
        // exactly one of them; a view takes the whole screen now, so the tab
        // showing is the whole of that axis and the product is complete.
        .. from mode in Enum.GetValues<UiMode>()
           from showing in Enum.GetValues<TabId>()
           from frozen in (bool[])[false, true]
           select new KeymapContext(mode, showing, frozen),
    ];

    [Test]
    public async Task Advertised_keys_are_exactly_the_keys_that_do_something()
    {
        // Set equality, per context. "The hints look right" is a sampling, and
        // a sampling is how an advertised key that stopped working survives -
        // the person presses it, nothing happens, and they conclude the console
        // is broken rather than the hint.
        foreach (var context in EveryContext)
        {
            var advertised = Keymap.Bindings(context).Select(b => b.Key).ToHashSet();

            var live = Universe
                .Where(key => key != Keymap.Interrupt && Keymap.Resolve(key, context) is not null)
                .ToHashSet();

            await Assert.That(live.SetEquals(advertised)).IsTrue()
                .Because($"in {context}: advertised [{Names(advertised)}] but live [{Names(live)}].");
        }
    }

    [Test]
    public async Task Every_context_advertises_at_least_one_key()
    {
        // Guards the equality above: two empty sets are equal, and a keymap
        // that resolved nothing anywhere would pass it.
        foreach (var context in EveryContext)
        {
            await Assert.That(Keymap.Bindings(context)).IsNotEmpty().Because($"{context} advertises nothing.");
        }
    }

    [Test]
    public async Task The_hint_line_names_every_live_key_and_nothing_else()
    {
        // The rendered string, not just the binding list - because the string
        // is what a person actually reads.
        //
        // AMENDED WHEN j AND k STOPPED BEING TAUGHT, and amended by tightening:
        // it asserted presence only, so it would have passed just as well if
        // the line had lost a key nobody meant to hide. It now says the line
        // holds every live binding that is not hidden AND holds no hidden one.
        //
        // Hiding is about the page and never about the keyboard, which is why
        // Advertised_keys_are_exactly_the_keys_that_do_something above is
        // untouched: it reads Bindings, which still carries j and k, and it
        // still proves advertised keys and live keys are one set.
        foreach (var context in EveryContext)
        {
            var hints = Keymap.Hints(context);

            foreach (var binding in Keymap.Bindings(context))
            {
                if (binding.Hidden)
                {
                    await Assert.That(hints).DoesNotContain($"{binding.Key.Name} {binding.Description}")
                        .Because($"{context} teaches {binding.Key.Name}, which is bound and not "
                               + "advertised. Line: " + hints);
                    continue;
                }

                await Assert.That(hints).Contains(binding.Key.Name)
                    .Because($"{context} does not advertise {binding.Key.Name}.");
            }

            // THE ANCHOR FOR THE ARM ABOVE. A keymap that hid everything would
            // satisfy every DoesNotContain in this loop.
            await Assert.That(Keymap.Bindings(context).Any(b => !b.Hidden)).IsTrue()
                .Because($"{context} advertises nothing at all.");
        }
    }

    [Test]
    public async Task Ctrl_c_quits_from_every_context_including_a_modal()
    {
        // The last resort. A modal that could swallow it would be a modal that
        // can trap the terminal.
        foreach (var context in EveryContext)
        {
            await Assert.That(Keymap.Resolve(Keymap.Interrupt, context)).IsEqualTo(Command.Quit)
                .Because($"{context} swallowed ctrl+c.");
        }
    }

    [Test]
    public async Task A_modal_answers_only_its_own_keys()
    {
        // What "modals own the keyboard" means concretely: NOTHING UNDERNEATH is
        // reachable, so no key can act on a flight the person cannot currently see.
        //
        // Narrowed once, deliberately. This used to require that a modal answer only its
        // escape hatch, which was true while every modal was a menu or a help screen and
        // stopped being true when one of them had to offer a choice: the gate modal exists
        // to be answered, and a modal that could only be closed could not be.
        //
        // What is protected is unchanged and is the whole of it - a modal answers ONLY
        // what it declares, so nothing leaks through to the pane behind it. The old
        // assertion was a special case of this one for modals that declare nothing.
        foreach (var context in EveryContext.Where(c => c.Mode != UiMode.Normal))
        {
            var declared = Keymap.Bindings(context).Select(b => b.Command).ToHashSet();

            var reachable = Universe
                .Where(key => key != Keymap.Interrupt && Keymap.Resolve(key, context) is not null)
                .Select(key => Keymap.Resolve(key, context)!.Value)
                .ToHashSet();

            await Assert.That(reachable.Except(declared).ToList()).IsEmpty()
                .Because($"{context.Mode} let something through that it does not declare, "
                       + "which means a key reached the flight behind it.");

            await Assert.That(reachable).Contains(Command.CloseModal)
                .Because($"{context.Mode} is escapable, which the hatch test also checks and "
                       + "which is worth failing twice rather than never.");
        }
    }

    [Test]
    public async Task Every_modal_has_exactly_one_escape_hatch()
    {
        foreach (var context in EveryContext.Where(c => c.Mode != UiMode.Normal))
        {
            var hatches = Universe
                .Where(key => key != Keymap.Interrupt && Keymap.Resolve(key, context) == Command.CloseModal)
                .ToList();

            await Assert.That(hatches).Count().IsEqualTo(1)
                .Because($"{context.Mode} has {hatches.Count} ways out; exactly one is the discipline.");
            await Assert.That(hatches[0]).IsEqualTo(Keymap.EscapeHatch(context)!.Value);
        }
    }

    [Test]
    public async Task Normal_mode_has_no_escape_hatch_because_it_is_not_a_modal()
    {
        await Assert.That(Keymap.EscapeHatch(new KeymapContext(UiMode.Normal))).IsNull();
    }

    [Test]
    public async Task Freeze_is_only_offered_where_there_is_something_to_freeze()
    {
        // An advertised key that does nothing teaches people to distrust the
        // hint line, which is the one thing telling them what works.
        var hidden = new KeymapContext(UiMode.Normal, TabId.Queue);
        var shown = new KeymapContext(UiMode.Normal, TabId.Live);

        await Assert.That(Keymap.Resolve(KeyStroke.Char('f'), hidden)).IsNull();
        await Assert.That(Keymap.Resolve(KeyStroke.Char('f'), shown)).IsEqualTo(Command.ToggleFreeze);
    }

    [Test]
    public async Task The_hint_for_a_toggle_says_what_pressing_it_will_do()
    {
        // "close" rather than "hide", and only from the tab itself: from
        // anywhere else the key brings it forward, and advertising a close that
        // does not happen is how a person learns to stop trusting the line.
        await Assert.That(Keymap.Hints(new(UiMode.Normal, TabId.Queue))).Contains("l live");
        await Assert.That(Keymap.Hints(new(UiMode.Normal, TabId.Live))).Contains("l close live");
        await Assert.That(Keymap.Hints(new(UiMode.Normal, TabId.Evidence))).Contains("l live")
            .Because("the live tab may be open behind this one, and l goes to it.");
        await Assert.That(Keymap.Hints(new(UiMode.Normal, TabId.Live, Frozen: true))).Contains("f unfreeze");
    }

    [Test]
    public async Task The_keymap_is_total_over_every_key_in_every_context()
    {
        // Pure and total: no input throws, whatever arrives. A keymap that
        // threw on an unexpected key would take the terminal down with it.
        foreach (var context in EveryContext)
        {
            foreach (var key in Universe)
            {
                _ = Keymap.Resolve(key, context);
            }
        }

        await Assert.That(Universe.Count).IsGreaterThan(100)
            .Because("a universe this small would make the loop above prove very little.");
    }

    private static string Names(IEnumerable<KeyStroke> keys) => string.Join(", ", keys.Select(k => k.Name));
}
