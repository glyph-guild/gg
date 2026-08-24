using Gg.Contracts.Description;

namespace Gg.Contracts.Tests;

/// <summary>
/// A label has a disposition: <c>measured</c> or <c>stated</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>The lie hazard was never the claim - it is a claim wearing measurement's
/// clothes,</b> and the disposition is what strips the costume. A label whose
/// name has a registered meaning is <c>measured</c>: the control plane
/// evaluated it from produced facts. A label with no registered meaning is
/// <c>stated</c>: an advertised claim, admitted rather than refused, and
/// visibly so. It renders everywhere the label does - the runner listing, the
/// checklist, the refusal text - three surfaces, one word.
/// </para>
/// <para>
/// <b>Its own vocabulary, deliberately not <see cref="EvidenceVoices"/>,</b>
/// even though today the words coincide. One says what a gate's evidence is;
/// the other says what a capability claim is. Shared, a value added for either
/// concept would be a break for readers of the other - coupling two closed
/// enumerations is how an addition becomes a halt somewhere unrelated.
/// </para>
/// </remarks>
public class LabelDispositionTests
{
    [Test]
    public async Task The_dispositions_are_measured_and_stated()
    {
        await Assert.That(LabelDispositions.All)
            .IsEquivalentTo((string[])[LabelDispositions.Measured, LabelDispositions.Stated]);
        await Assert.That(LabelDispositions.Measured).IsEqualTo("measured");
        await Assert.That(LabelDispositions.Stated).IsEqualTo("stated");
    }

    [Test]
    public async Task The_disposition_vocabulary_is_its_own()
    {
        // Same words, different concept, separate types. If somebody merges
        // them this fails, and the remarks above are the argument to reread.
        await Assert.That(typeof(LabelDispositions)).IsNotEqualTo(typeof(EvidenceVoices));
    }

    [Test]
    public async Task An_advertised_label_carries_its_disposition()
    {
        var label = new AdvertisedLabel
        {
            Name = "environment=aspire-payments",
            Disposition = LabelDispositions.Stated,
        };

        await Assert.That(label.Disposition).IsEqualTo("stated");
        await Assert.That(ProtocolSurface.JsonMembers[typeof(AdvertisedLabel)])
            .IsEquivalentTo((string[])["name", "disposition"]);
        await Assert.That(Vocabulary.Types).Contains(typeof(AdvertisedLabel));
    }

    [Test]
    public async Task A_runner_summary_lists_what_it_advertises()
    {
        // Empty for a runner that advertises nothing, which every runner
        // registered before this member existed does.
        var summary = new RunnerSummary
        {
            RunnerId = Guid.NewGuid().ToString(),
            Label = "runner-a",
            State = RunnerStates.Idle,
        };

        await Assert.That(summary.Labels).IsEmpty();
        await Assert.That(ProtocolSurface.JsonMembers[typeof(RunnerSummary)]).Contains("labels");
    }
}
