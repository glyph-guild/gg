using Gg.Contracts;

namespace Gg.Contracts.Tests;

/// <summary>
/// A preview's address is a fact, so the person asked to look at one can be told
/// where to look.
/// </summary>
/// <remarks>
/// <para>
/// <b>GG-268 made this unavoidable.</b> The first <c>ui-preview</c> flight served
/// its app, published a tunnel, and reached a gate asking a person to review the
/// preview — with no way to tell them the URL. The work kind's own instructions
/// admit the gap in writing: <i>"a fact kind for it is not built, so the summary
/// is the only place it can go."</i>
/// </para>
/// <para>
/// <b>And the summary is not a place it can go.</b> Every view truncates it —
/// <c>gg show</c>, <c>gg facts</c> and <c>gg log</c> alike — so on that flight the
/// only way to read the deliverable was to open a transcript on the runner's own
/// disk over ssh. A gate that says "review the preview" without saying where is
/// not a gate somebody can answer.
/// </para>
/// <para>
/// <b>Why a fact rather than a line of prose.</b> Facts cross the boundary,
/// carry a pinned identity, and are what every reader already reads. Prose in a
/// summary is a claim a model wrote; this is the address the platform arranged,
/// and the difference matters when the two disagree.
/// </para>
/// <para>
/// <b>It names no credential and no slot secret</b>, deliberately. What reaches
/// the control plane is the address a person opens. Which credential dialled the
/// tunnel stays on the machine that holds it, as every other secret does.
/// </para>
/// </remarks>
public class APreviewUrlIsAFactTests
{
    private static PreviewUrl Served() => new()
    {
        Url = "https://jdapp-03.example.dev",
        Exposure = "jdapp",
        Slot = "03",
    };

    [Test]
    public async Task The_vocabulary_knows_it()
    {
        await Assert.That(FactKinds.All).Contains(FactKinds.PreviewUrl);

        await Assert.That(FactKinds.PreviewUrl).IsEqualTo("preview.url")
            .Because("the kinds are dotted nouns naming what was measured, and this one is the "
                   + "address a preview was served at.");
    }

    [Test]
    public async Task A_url_that_is_not_one_is_refused()
    {
        await Assert.That(PreviewUrl.Validate(Served() with { Url = "" })).IsNotNull();

        await Assert.That(PreviewUrl.Validate(Served() with { Url = "jdapp-03.example.dev" }))
            .IsNotNull()
            .Because("a person is going to open this. An address with no scheme is one a "
                   + "browser may read as a search, and the gate would have sent them nowhere.");
    }

    [Test]
    public async Task An_address_that_is_not_https_is_refused()
    {
        await Assert.That(PreviewUrl.Validate(Served() with { Url = "http://jdapp-03.example.dev" }))
            .IsNotNull()
            .Because("a preview carries the tenant's unreleased interface and whatever data is "
                   + "on its screen, and plain http would carry it in the clear to whoever is "
                   + "between. Every provider gg can arrange serves https.");
    }

    [Test]
    public async Task It_names_the_exposure_and_the_slot_it_was_granted()
    {
        await Assert.That(PreviewUrl.Validate(Served() with { Exposure = " " })).IsNotNull();
        await Assert.That(PreviewUrl.Validate(Served() with { Slot = "" })).IsNotNull()
            .Because("an address with no slot behind it cannot be reconciled against an "
                   + "inventory, so nothing could tell a leaked grant from a released one.");
    }

    [Test]
    public async Task A_served_preview_is_not_refused()
    {
        // LIVENESS. Every assertion above asks for a refusal, and a validator
        // that refused everything would satisfy all of them.
        await Assert.That(PreviewUrl.Validate(Served())).IsNull();
    }
}
