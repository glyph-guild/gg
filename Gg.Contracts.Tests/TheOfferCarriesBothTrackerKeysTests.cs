namespace Gg.Contracts.Tests;

/// <summary>
/// A profile's trackers can be offered, and neither key applies unattended
/// (slice forty-seven, step 2).
/// </summary>
/// <remarks>
/// <b>Directed, not unwatched, and the definition decides it.</b>
/// <c>Unwatched</c> is for values that are data and grant nothing - relay
/// addresses, and the labels a machine advertises work for. A tracker changes
/// where something is read from or sent to AND hands over a credential to do it
/// with, which is what <c>vcs-hosts</c> and <c>destination-apis</c> do, and both
/// of those are directed.
/// </remarks>
public class TheOfferCarriesBothTrackerKeysTests
{
    [Test]
    public async Task Both_tracker_keys_are_offerable()
    {
        await Assert.That(OfferableKeys.All).Contains(OfferableKeys.IntentHosts);
        await Assert.That(OfferableKeys.All).Contains(OfferableKeys.TrackerApis);
    }

    [Test]
    public async Task And_neither_applies_with_nobody_watching()
    {
        await Assert.That(OfferableKeys.Unwatched).DoesNotContain(OfferableKeys.IntentHosts)
            .Because("it points a machine at a host and hands it a credential, which is what a "
                   + "forge host does - and a wrong one reads a tracker nobody chose.");

        await Assert.That(OfferableKeys.Unwatched).DoesNotContain(OfferableKeys.TrackerApis)
            .Because("a wrong one WRITES to a tracker nobody chose, which is the same argument "
                   + "with less of a way back.");
    }

    [Test]
    public async Task The_keys_are_the_settings_they_become()
    {
        // NAMED AS THE SETTING, because an offer's key IS the setting a machine
        // writes, and a second spelling here would be a mapping table nobody
        // reads until it is wrong.
        await Assert.That(OfferableKeys.IntentHosts).IsEqualTo("intent-hosts");
        await Assert.That(OfferableKeys.TrackerApis).IsEqualTo("tracker-apis");
    }
}
