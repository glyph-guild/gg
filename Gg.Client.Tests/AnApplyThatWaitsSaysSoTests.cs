using Gg.Contracts;

namespace Gg.Client.Tests;

/// <summary>
/// An apply the control plane diverted to a gate says so, naming the field,
/// the flight and who it waits on.
/// </summary>
/// <remarks>
/// <b>Found applying root's <c>models:</c> (slice seventy-six).</b> The
/// control plane answered <c>changed: false</c> with <c>widens: models</c>, a
/// flight and <c>awaiting: platform-owner</c>, and gg printed only "Nothing
/// changed" - so the person sent it again and opened a second gate for one
/// change. "Nothing changed" is true of the version and false of the request.
/// </remarks>
public class AnApplyThatWaitsSaysSoTests
{
    [Test]
    public async Task A_widening_names_the_field_the_gate_and_who_it_waits_on()
    {
        var text = VerbOutput.ToText(new VerbResult.EnvelopeApplied(new EnvelopeApplied
        {
            Version = "v20",
            AppliedAt = DateTimeOffset.UnixEpoch,
            Changed = false,
            Widens = "models",
            Flight = "01a12f1c-fda6-7201-a870-535884451661",
            Awaiting = "platform-owner",
        }, []));

        await Assert.That(text.Contains("Nothing changed", StringComparison.Ordinal)).IsFalse()
            .Because("a change waiting at a gate is not nothing. Text:\n" + text);
        await Assert.That(text).Contains("models")
            .And.Contains("01a12f1c-fda6-7201-a870-535884451661")
            .And.Contains("platform-owner")
            .And.Contains("v20");
    }

    [Test]
    public async Task An_identical_apply_still_says_nothing_changed()
    {
        var text = VerbOutput.ToText(new VerbResult.EnvelopeApplied(new EnvelopeApplied
        {
            Version = "v20",
            AppliedAt = DateTimeOffset.UnixEpoch,
            Changed = false,
        }, []));

        await Assert.That(text).Contains("Nothing changed. The envelope is still v20.");
    }
}
