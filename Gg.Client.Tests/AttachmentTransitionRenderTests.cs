using Gg.Client;
using Gg.Contracts;

namespace Gg.Client.Tests;

/// <summary>
/// `gg why` shows when an obligation attached and when it detached - both
/// directions, with their times - because a gate that appeared and vanished
/// is exactly what a reviewer needs to see.
/// </summary>
/// <remarks>
/// The history is DERIVED server-side from the attribution stream and crosses
/// as data; this render never recomputes anything. One entry is the common
/// case (it attached and stayed); the interesting flight is
/// attached-detached-attached, and hiding the middle would file the question
/// the estate asked and withdrew under never-asked.
/// </remarks>
public class AttachmentTransitionRenderTests
{
    private static readonly DateTimeOffset T0 = new(2026, 8, 25, 14, 2, 0, TimeSpan.Zero);

    private static FlightAttribution WithHistory(params AttachmentTransition[] transitions) => new()
    {
        FlightNumber = "GG-42",
        EnvelopeVersion = "v3",
        Obligations =
        [
            new ObligationAttribution
            {
                ObligationId = "reversibility-plan",
                Attachment = transitions.Length == 0
                    ? Attachments.Attached
                    : transitions[^1].To,
                Condition = "change.manifest touches migrations/**",
                Because = "change.manifest names 1 path(s) under 'migrations/**'.",
                Transitions = transitions,
            },
        ],
    };

    [Test]
    public async Task Both_directions_render_with_their_times()
    {
        var text = VerbOutput.ToText(new VerbResult.Why(WithHistory(
            new AttachmentTransition { To = Attachments.Attached, At = T0 },
            new AttachmentTransition
            {
                To = Attachments.NotAttached,
                At = T0.AddMinutes(9),
                Because = "no path in change.manifest is under 'migrations/**', and the "
                        + "manifest names 1 path(s).",
            },
            new AttachmentTransition { To = Attachments.Attached, At = T0.AddMinutes(18) })));

        await Assert.That(text).Contains("attached 14:02")
            .Because("when the question was first asked is part of the answer.");
        await Assert.That(text).Contains("not-attached 14:11")
            .Because("a gate that vanished is exactly what a reviewer needs to see - "
                   + "hiding the middle files a withdrawn question under never-asked.");
        await Assert.That(text).Contains("attached 14:20");
    }

    [Test]
    public async Task A_history_of_one_renders_without_ceremony()
    {
        var text = VerbOutput.ToText(new VerbResult.Why(WithHistory(
            new AttachmentTransition { To = Attachments.Attached, At = T0 })));

        await Assert.That(text).Contains("attached 14:02");
        await Assert.That(text.Split('\n').Count(l => l.Contains("history:")))
            .IsEqualTo(1)
            .Because("the common case is one entry, and it reads as one line.");
    }

    [Test]
    public async Task An_empty_history_renders_no_history_line()
    {
        // Older control planes serve no transitions; the member defaults empty
        // and the render says nothing rather than inventing a time.
        var text = VerbOutput.ToText(new VerbResult.Why(WithHistory()));

        await Assert.That(text).DoesNotContain("history:");
    }

    [Test]
    public async Task A_transition_to_a_state_this_build_does_not_know_is_refused_by_validate()
    {
        // The poison twin, at the contract's door rather than the render's: a
        // transition to a fourth state would make the history unreadable, and
        // Validate names it instead of letting a reader shrug.
        var refused = ObligationAttribution.Validate(new ObligationAttribution
        {
            ObligationId = "reversibility-plan",
            Attachment = Attachments.Attached,
            Because = "attached, and the history disagrees",
            Transitions =
            [
                new AttachmentTransition { To = "reconsidering", At = T0 },
            ],
        });

        await Assert.That(refused).IsNotNull();
        await Assert.That(refused!).Contains("reconsidering");
    }
}
