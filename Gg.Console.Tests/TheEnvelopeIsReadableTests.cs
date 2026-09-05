using Gg.Client;
using Gg.Contracts;
using Gg.Contracts.Description;

namespace Gg.Console.Tests;

/// <summary>
/// The rules in force, readable from the console.
/// </summary>
/// <remarks>
/// <para>
/// <b>The console had no envelope method at all.</b> Every flight it shows is
/// governed by a document a person could read only by leaving the console, and
/// the flight pane names its version - so the console has been referring to
/// something it could not show since it was written.
/// </para>
/// <para>
/// <b>The read, never the apply.</b> Applying takes a document from a path and
/// this console has no file argument; that is out of scope by declaration, not
/// by omission. Reading is what a person does when a flight is stopped by
/// something they cannot see.
/// </para>
/// <para>
/// <b>Rendered by the CLI's own renderer</b>, so what the pane shows is what
/// `gg envelope show` prints. A second layout of one document is two views that
/// drift, and this is the document arguments are had about.
/// </para>
/// </remarks>
public class TheEnvelopeIsReadableTests
{
    private static EnvelopeState Applied() => new()
    {
        Version = "v3",
        UpdatedAt = new DateTimeOffset(2026, 9, 5, 8, 0, 0, TimeSpan.Zero),
        UpdatedBy = "a-person",
        Envelope = new Envelope
        {
            Context = new ContextBinding { Scope = "**", Constitution = "1.0.0" },
            Obligations =
            [
                new Obligation
                {
                    Id = "in-scope", Check = "machine", Rule = "no-file-outside-scope",
                },
            ],
            Loops = [],
            Destinations = [],
        },
    };

    [Test]
    public async Task The_projection_puts_the_envelope_where_the_pane_reads_it()
    {
        var state = ConsoleProjection.Apply(
            new AppState(), new VerbResult.EnvelopeShown(Applied()));

        await Assert.That(state.Envelope).IsNotNull();
        await Assert.That(state.Envelope!.Version).IsEqualTo("v3");
    }

    [Test]
    public async Task The_pane_says_which_version_and_who_applied_it()
    {
        var pane = PaneText.Envelope(new AppState { Envelope = Applied() });

        await Assert.That(pane).Contains("v3")
            .Because("the flight pane names a version, so the envelope pane has to be the "
                   + "thing that version identifies.");
        await Assert.That(pane).Contains("a-person")
            .Because("who put these rules in force is the first question a person asks "
                   + "about a rule they disagree with.");
        await Assert.That(pane).Contains("in-scope")
            .Because("and the document itself, or this is a header with nothing under it.");
    }

    [Test]
    public async Task An_unread_envelope_says_so_rather_than_saying_there_is_none()
    {
        // Rule 5, and the pair is dangerous here: `no envelope is in force`
        // means every flight is ungoverned, which is a sentence somebody would
        // act on immediately.
        var pane = PaneText.Envelope(new AppState());

        await Assert.That(pane).Contains("not read")
            .Because("`nothing has been read` and `no envelope is in force` are opposite "
                   + "facts, and the second one is an emergency.");
    }

    [Test]
    public async Task The_key_is_the_shell_s_work_because_showing_it_is_a_read()
    {
        var before = new AppState();

        await Assert.That(ShellCommands.Handled).Contains(Command.ToggleEnvelope);
        await Assert.That(Reducer.Reduce(before, Command.ToggleEnvelope)).IsEqualTo(before)
            .Because("a shell command with a reducer arm is a key that half works.");
    }

    [Test]
    public async Task One_region_one_pane()
    {
        // Five occupants now. Two visible flags over one region is two panes
        // drawn on top of each other, which is why each toggle turns the others
        // off rather than trusting the order the views were added in.
        var crowded = new AppState
        {
            EvidenceVisible = true, LiveVisible = true, BrowseVisible = true,
            ChecklistVisible = true,
        };

        var shown = Reducer.EnvelopeToggled(crowded);

        await Assert.That(shown.EnvelopeVisible).IsTrue();
        await Assert.That(shown.EvidenceVisible).IsFalse();
        await Assert.That(shown.LiveVisible).IsFalse();
        await Assert.That(shown.BrowseVisible).IsFalse();
        await Assert.That(shown.ChecklistVisible).IsFalse();
    }
}
