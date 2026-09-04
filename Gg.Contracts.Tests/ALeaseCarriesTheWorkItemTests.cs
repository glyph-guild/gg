using Gg.Contracts;

namespace Gg.Contracts.Tests;

/// <summary>
/// A lease says which work item a flight is about, when a work item is what it
/// is about.
/// </summary>
/// <remarks>
/// <para>
/// <b>A ticket flight is leased, cloned, and never worked.</b>
/// <c>LeaseGranted</c> carries the intent as a URI and nothing else, and a
/// ticket is a provider and an id — contract 0.86.0 added the kind and declared
/// both fields on the intent, but the LEASE never learned to carry them. The
/// runner's invocation gate requires a non-empty intent uri, so every ticket
/// flight reaches a runner, materializes a tree, and returns without invoking
/// anything. Silent: no refusal, no fact, no reason.
/// </para>
/// <para>
/// <b>Beside the uri rather than instead of it.</b> A link flight still carries
/// a link and nothing about it changes. These are two ways of naming external
/// work, and the runner needs to be able to tell which one it was handed —
/// collapsing them would mean composing a URL out of a provider and an id,
/// which is the derivation slice nine retired and which this repository cannot
/// do at all, because it names no forge.
/// </para>
/// <para>
/// <b>The id is declared and never parsed</b>, which is 0.86.0's own rule for
/// the same two fields one layer up. Nothing here reads structure out of it.
/// </para>
/// </remarks>
public class ALeaseCarriesTheWorkItemTests
{
    private static LeaseGranted ALease(
        string? uri = null, string? provider = null, string? id = null) => new()
    {
        FlightId = "01a06962-0000-7000-8000-000000000000",
        LeaseId = "a-lease",
        Generation = 1,
        FlightNumber = "GG-26",
        Repos = [],
        Credentials = [],
        ClassificationCeiling = Classifications.Internal,
        ClassificationRules = ClassificationRules.Default,
        ExpiresAt = DateTimeOffset.UnixEpoch,
        RenewWithinSeconds = 30,
        IntentUri = uri,
        IntentProvider = provider,
        IntentId = id,
    };

    [Test]
    public async Task A_lease_carries_the_provider_and_the_id_a_work_item_is()
    {
        // THE DEFECT. Without these the runner is handed a flight it cannot
        // name, so it declines to invoke and says nothing about why.
        var lease = ALease(provider: "a-tracker", id: "26");

        await Assert.That(lease.IntentProvider).IsEqualTo("a-tracker");
        await Assert.That(lease.IntentId).IsEqualTo("26");
    }

    [Test]
    public async Task A_lease_over_a_link_carries_a_link_and_no_work_item()
    {
        // THE ANCHOR. Every flight in the air today is one of these, and the
        // two ways of naming external work stay distinguishable - a runner has
        // to know which it was handed.
        var lease = ALease(uri: "https://forge.invalid/acme/widgets/issues/7");

        await Assert.That(lease.IntentUri).IsEqualTo("https://forge.invalid/acme/widgets/issues/7");
        await Assert.That(lease.IntentProvider).IsNull();
        await Assert.That(lease.IntentId).IsNull();
    }

    [Test]
    public async Task A_lease_over_a_sentence_names_no_external_work_at_all()
    {
        // Also shipped and also intended. All three members absent is a real
        // state and not a half-filled one: the flight is about what somebody
        // typed, and there is nothing for a runner to resolve.
        var lease = ALease();

        await Assert.That(lease.IntentUri).IsNull();
        await Assert.That(lease.IntentProvider).IsNull();
        await Assert.That(lease.IntentId).IsNull();
    }
}
