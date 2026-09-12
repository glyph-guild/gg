namespace Gg.Console.Tests;

/// <summary>
/// The buttons a modal shows, and the labels on them.
/// </summary>
/// <remarks>
/// <para>
/// <b>Derived, so a button cannot say something the key does not do.</b> Which
/// answers a modal has is <see cref="Keymap"/>'s already; a hand-kept list
/// beside it would be a second place the same thing is written, and the one that
/// drifts is the one nobody is looking at. A button is a second way to REACH a
/// decision, never a second decision.
/// </para>
/// <para>
/// <b>Pure, so it is answerable without a screen.</b> What the view does with
/// these is render them; that they are the right ones, labelled sensibly, is a
/// question about values — the same reason <c>PtyScreen</c> returns a frame
/// rather than writing one.
/// </para>
/// </remarks>
public class ModalButtonTests
{
    private static KeymapContext In(UiMode mode) => new(mode);

    [Test]
    public async Task A_modal_that_asks_something_offers_its_answers_as_buttons()
    {
        // THE ONE THIS WAS BUILT FOR. Two answers, and until now the only place
        // they appeared was the hint line at the foot of the screen - the
        // furthest point from where somebody who just pressed `n` is looking.
        var buttons = Keymap.Buttons(In(UiMode.ComposeChoice));

        await Assert.That(buttons.Select(b => b.Command))
            .IsEquivalentTo((Command[])[Command.ComposeInEditor, Command.ComposeWithAgent]);
    }

    [Test]
    public async Task The_navigational_modals_offer_buttons_and_the_deciding_ones_do_not()
    {
        // WHICH MODALS HAVE THEM IS A DECISION, so it is written here rather
        // than left as whatever somebody happened to label. The rule it follows:
        // a button is easier to hit than a key, so the modals that join in are
        // the ones where a stray click turns a page or opens a browser, and the
        // ones that stay keys-only are the ones where it ends something or is
        // recorded against the person who did it.
        //
        // Rolling one of those in later is a row in this table and a label on
        // each answer, and this is the sentence that has to be argued with
        // first.
        var offers = new (KeymapContext Context, bool Offers, string Why)[]
        {
            // GETTING SOMEWHERE. Turning the help page over, starting a
            // sign-in, opening a browser, putting a code on the clipboard,
            // restarting a runner that is meant to be up anyway. Every one of
            // these is undone by doing it again or by doing nothing.
            (new(UiMode.Help), true, "turns the help page over"),
            (new(UiMode.SignIn), true, "starts a sign-in"),
            (new(UiMode.SignIn) { SignInStarted = true }, true, "reaches the browser"),

            // DECIDING. Approving a gate is attributed to whoever approved it,
            // grounding a flight ends work that is running, and confirming a
            // second flight opens one. These keep the keystroke, which is a
            // thing you have to aim at.
            (new(UiMode.GateDecision), false, "approves or rejects, attributed"),
            (new(UiMode.ConfirmFlight), false, "opens a second flight"),
            (new(UiMode.FlightDetail), false, "grounds a flight"),

            // NOTHING TO OFFER, and this falls out of the rule rather than being
            // decided here: both have only a way out, and escape is never a
            // button. If either grows an answer, this row is what says whether
            // it gets one.
            (new(UiMode.FlightActions), false, "has only a way out"),
            (new(UiMode.HandFlight), false, "has only a way out"),

            // NOT OURS TO STOP. Over somebody else's runner the two keys are not
            // bound at all, so there is nothing to put on a button - the same
            // guard, reached through a different door.
            (new(UiMode.Runner) { RunnerIsOurs = false }, false, "is somebody else's runner"),

            // AND OURS JOINED IT, WHICH MOVED A ROW ACROSS THIS TABLE. It read
            // "restarts or stops our runner" on the clickable side, on the
            // argument that both are undone by doing them again or by doing
            // nothing. THAT ARGUMENT IS UNTOUCHED - what changed is the room.
            //
            // The runner modal's foot is a TAB BAR now, and a button row under
            // a bar somebody can already click is two clickable things in one
            // place; `Next view' was a button that did exactly what clicking a
            // tab header does. Removing them also ends a difference nobody
            // asked for: the modal looked one way over our runner and another
            // over somebody else's.
            //
            // A ROW MOVED RATHER THAN DELETED, because the sentence that has to
            // be argued with first is the one that says what this modal is -
            // and "it used to have buttons and here is why it stopped" is more
            // of that sentence than silence would be.
            (new(UiMode.Runner) { RunnerIsOurs = true }, false, "restarts or stops our runner, "
                                                             + "under a bar you can click"),
        };

        foreach (var (context, wanted, why) in offers)
        {
            var got = Keymap.Buttons(context).Count > 0;

            await Assert.That(got).IsEqualTo(wanted)
                .Because(wanted
                    ? $"{context.Mode} {why}, which is safe to click, and offers no buttons."
                    : $"{context.Mode} {why}, so it should stay keys-only, and offers buttons.");
        }
    }

    [Test]
    public async Task The_escape_hatch_never_becomes_a_button()
    {
        // It is on every modal, and the frame around the box already means
        // "there is a way out of this". A button for it would be the one
        // affordance a person did not need help finding.
        foreach (var mode in Enum.GetValues<UiMode>())
        {
            await Assert.That(Keymap.Buttons(In(mode)).Any(b => b.Key == KeyStroke.Esc))
                .IsFalse()
                .Because($"{mode} offers escape as a button.");
        }
    }

    [Test]
    public async Task Normal_mode_has_none_because_it_is_not_asking_anything()
    {
        // A row of buttons under the whole console would be furniture. These
        // belong to a question, and Normal mode is not one.
        await Assert.That(Keymap.Buttons(In(UiMode.Normal))).IsEmpty();
    }

    [Test]
    public async Task A_mode_labels_all_of_its_answers_or_none_of_them()
    {
        // ALL OR NOTHING, and that is the rule that makes rolling this out
        // safe. A modal showing a button for `approve` and nothing for `reject`
        // is worse than one showing neither: it reads as though approving is
        // the only thing on offer. So a mode joins in when somebody has written
        // every label, and until then it looks exactly as it does today.
        foreach (var mode in Enum.GetValues<UiMode>().Where(m => m != UiMode.Normal))
        {
            var answers = Keymap.Bindings(In(mode))
                .Where(b => b.Key != KeyStroke.Esc)
                .ToList();

            var buttons = Keymap.Buttons(In(mode));

            if (buttons.Count == 0)
            {
                continue;
            }

            await Assert.That(buttons.Count).IsEqualTo(answers.Count)
                .Because($"{mode} shows {buttons.Count} buttons for {answers.Count} answers, "
                       + "so at least one answer is reachable by key and not by mouse - which "
                       + "is a menu that hides one of its options.");
        }
    }

    [Test]
    public async Task The_answer_focus_lands_on_is_never_one_that_stops_something()
    {
        // FOCUS STARTS ON THE FIRST BUTTON, and the marks now go with it, so the
        // first answer is the one a person gets for pressing enter without
        // reading. That makes the DECLARATION ORDER in Keymap load-bearing:
        // swapping the runner's two answers would put `Shut down' under a
        // reflex keypress, and nothing else in the suite would notice.
        //
        // Named rather than inferred, because "consequential" is not a property
        // a command has. These two end something that is running.
        var ending = (Command[])[Command.StopRunner, Command.GroundFlight];

        foreach (var context in (KeymapContext[])
                 [new(UiMode.Help), new(UiMode.SignIn),
                  new(UiMode.SignIn) { SignInStarted = true },
                  new(UiMode.Runner) { RunnerIsOurs = true },
                  new(UiMode.ComposeChoice)])
        {
            var buttons = Keymap.Buttons(context);
            if (buttons.Count == 0)
            {
                continue;
            }

            await Assert.That(ending).DoesNotContain(buttons[0].Command)
                .Because($"{context.Mode} declares {buttons[0].Command} first, so it is what "
                       + "focus and the default marks land on - and enter is pressed by "
                       + "reflex.");
        }
    }

    [Test]
    public async Task A_label_is_a_word_or_two_rather_than_the_hint_line_sentence()
    {
        // The description is written for a line that explains; a button is a
        // thing you point at. "write it in your editor" is right in one place
        // and too long for the other, and using one for both is what made the
        // first attempt render two half-labels.
        foreach (var mode in Enum.GetValues<UiMode>())
        {
            foreach (var b in Keymap.Buttons(In(mode)))
            {
                await Assert.That(b.Label).IsNotNull();
                await Assert.That(b.Label!.Length).IsLessThanOrEqualTo(14)
                    .Because($"'{b.Label}' is a sentence on a button. Two of these sit side "
                           + "by side in a box a person's eye takes in at once.");
            }
        }
    }

    [Test]
    public async Task Every_button_is_a_key_that_really_resolves()
    {
        // THE HAZARD THIS WHOLE SHAPE EXISTS TO AVOID: a button that sends a
        // command the keymap would not, which is a second decision wearing the
        // first one's clothes.
        foreach (var mode in Enum.GetValues<UiMode>())
        {
            foreach (var b in Keymap.Buttons(In(mode)))
            {
                await Assert.That(Keymap.Resolve(b.Key, In(mode))).IsEqualTo(b.Command)
                    .Because($"the button labelled '{b.Label}' claims to be {b.Key}.");
            }
        }
    }

    [Test]
    public async Task The_compose_modal_says_what_the_two_answers_mean()
    {
        // THE DEFECT A SPIKE FOUND ON MAIN. PaneText.Modal had no arm for this
        // mode, so the box was drawn with a title and NOTHING IN IT. The test
        // that was supposed to cover it asserted the HINTS name both ways, which
        // they did - the hints are a different surface, at the other end of the
        // screen.
        var body = PaneText.Modal(new AppState { Mode = UiMode.ComposeChoice });

        await Assert.That(body).IsNotEmpty()
            .Because("a titled empty box is what this rendered as.");

        foreach (var word in (string[])["editor", "agent"])
        {
            await Assert.That(body).Contains(word, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Test]
    public async Task The_gate_modal_says_what_is_being_decided()
    {
        // WHAT THE RATCHET FOUND, covered properly rather than left at "not
        // empty". This is the one modal I could not screenshot - a gate needs a
        // control plane to exist - so the text is asserted directly instead.
        //
        // It matters more than the compose modal it was found beside: this asks
        // somebody to approve or reject, and the answer is attributed to them.
        // "Waiting on you" over blank space is the wrong amount to say about
        // that.
        var state = new AppState
        {
            Mode = UiMode.GateDecision,
            Queue =
            [
                new QueueRow
                {
                    FlightId = "01a0776a-cacb-76dc-b444-2b7031e840d8",
                    FlightNumber = "GG-52",
                    Name = "create a PR for a python script",
                    Reason = QueueReason.AwaitingDecision,
                    Since = DateTimeOffset.UnixEpoch,
                },
            ],
            Gates = new Gg.Contracts.GateList
            {
                Gates =
                [
                    new Gg.Contracts.PendingGate
                    {
                        FlightNumber = "GG-52",
                        ObligationId = "human-approves-the-diff",
                        Approver = "kevin",
                        Branch = "gg/GG-52",

                        // A BRANCH WITH NO COMMIT IS NOT A GATE ANYBODY HAS.
                        // The renderer prints the branch only alongside the
                        // commit on it, on purpose - "the two absences render
                        // as one sentence because they are one fact: there is
                        // no code here" - so a fixture carrying one without the
                        // other asks for output that would be wrong to produce.
                        Commit = "3f9a1c2d4e5b6a7c8d9e0f1a2b3c4d5e6f7a8b9c",
                        ManifestHash = "sha256:9f2c",
                        Because = "the loop asked for a decision",
                        AwaitingSince = DateTimeOffset.UnixEpoch,
                        Attempt = 1,
                    },
                ],
            },
        };

        var body = PaneText.Modal(state);

        foreach (var said in (string[])
                 ["GG-52", "human-approves-the-diff", "kevin", "gg/GG-52"])
        {
            await Assert.That(body).Contains(said, StringComparison.Ordinal)
                .Because($"a person deciding this is owed '{said}'. Body:\n{body}");
        }

        // THE FIELD A HAND-WRITTEN VERSION OF THIS DROPPED. `because` is the
        // Engine's own words for why the obligation attached, and when the
        // condition is "the loop asked" it IS the decision - everything else on
        // screen is bookkeeping around it. It is here because this renders
        // through the same function `gg gates` uses rather than a second one
        // somebody assembled from the fields they happened to remember.
        await Assert.That(body).Contains("the loop asked for a decision", StringComparison.Ordinal)
            .Because("rendering a gate without its reason is a decision asked in the dark. "
                   + $"Body:\n{body}");
    }

    [Test]
    public async Task A_gate_that_has_gone_says_so_rather_than_blanking()
    {
        // The list is re-read underneath a person, and somebody else may have
        // answered it. An empty box for that is indistinguishable from the bug
        // this whole file exists because of.
        var body = PaneText.Modal(new AppState { Mode = UiMode.GateDecision });

        await Assert.That(body).IsNotEmpty();

        // "DECISION" RATHER THAN "GATE", which is what this used to say. A gate
        // is what the code calls it; a decision is what the person was waiting
        // to make, and the queue already calls it that on the row they came
        // from. The test follows the words a reader sees rather than pinning
        // the ones the implementation happens to use.
        await Assert.That(body).Contains("decision", StringComparison.OrdinalIgnoreCase);
    }

    [Test]
    public async Task No_modal_that_asks_something_is_drawn_empty()
    {
        // THE RATCHET FOR IT, and it asks about the SOURCE because that is the
        // only place the question has an answer. Two versions of this failed
        // first, and both failed the same way: asked with `new AppState()`, and
        // then with four hundred generated ones, a mode with no arm looks
        // exactly like a mode whose arm has nothing to say yet - a flight's
        // detail about no flight, a gate decision about no gate. What is wrong
        // is a mode PaneText.Modal never mentions, and mentioning is a fact
        // about code rather than about any state.
        var source = ConsoleSource.Text("Gg.Console", "State/PaneText.cs");

        var from = source.IndexOf("public static string Modal(", StringComparison.Ordinal);
        await Assert.That(from).IsGreaterThan(0)
            .Because("this scans one method, and a scan that found nothing would pass "
                   + "silently for every mode at once.");

        var arms = source[from..source.IndexOf("};", from, StringComparison.Ordinal)];

        var unmentioned = Modals.Drawn
            .Where(m => !arms.Contains($"UiMode.{m} =>", StringComparison.Ordinal))
            .ToList();

        await Assert.That(unmentioned).IsEmpty()
            .Because("these fall through to the empty default, so the modal draws a title "
                   + "over nothing: " + string.Join(", ", unmentioned));
    }
}
