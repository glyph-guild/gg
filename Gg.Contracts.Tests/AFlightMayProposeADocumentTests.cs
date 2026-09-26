using Gg.Contracts;

namespace Gg.Contracts.Tests;

/// <summary>
/// A flight may hand back an airspace document it drafted, and that is a
/// request rather than a change.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why a fact and not a route.</b> A learning flight reads records and
/// concludes something a work kind should carry. It cannot use
/// <c>DocumentTool</c>: that writes into a local airspace working copy, where
/// "a person reads the change and decides whether to submit it", and a runner
/// has no working copy — the estate holds documents, not repositories. And it
/// cannot call an airspace door: admission reads a proposal already carried by
/// the flight, and nothing a runner may call puts one there.
/// </para>
/// <para>
/// <b>So it takes the one shape this vocabulary already has for an agent's
/// request.</b> <see cref="FlightNomination"/> is described as the only kind
/// that asks for something rather than measuring it — it asks that a flight
/// exist, and admission decides. This asks that a document change, and the gate
/// decides. Same door, same ledger, same attribution, and the runner needs no
/// route it does not already have.
/// </para>
/// <para>
/// <b>It applies nothing.</b> A proposal is held and a person opens the gate;
/// an envelope change is evaluated against the envelope in force and never
/// against the one it asks for, so a document cannot widen its own way in.
/// </para>
/// </remarks>
public class AFlightMayProposeADocumentTests
{
    private static DocumentProposal Drafted() => new()
    {
        Role = AirspaceRoles.WorkKind,
        Name = "ui-preview",
        Document = "based-on: ui-preview@v7\ncontext:\n  scope: \"**\"\n",
    };

    [Test]
    public async Task It_is_a_kind_that_crosses()
    {
        await Assert.That(FactKinds.All).Contains(FactKinds.DocumentProposal);
    }

    /// <summary>
    /// It names the role it claims to be, so a mismatch is refused rather than guessed.
    /// </summary>
    /// <remarks>
    /// <b>Explicit, though the document's own shape implies it.</b> The parser
    /// forks by role and would reach an answer on its own — but then a document
    /// that parses as the wrong thing lands as that wrong thing, silently. Naming
    /// it means the two can disagree, and a disagreement is something the control
    /// plane can refuse.
    /// </remarks>
    [Test]
    public async Task It_names_a_role_from_the_closed_list()
    {
        await Assert.That(AirspaceRoles.All).Contains(Drafted().Role);
    }

    /// <summary>
    /// The document travels whole, and nothing here is a locator.
    /// </summary>
    /// <remarks>
    /// <b>Unlike a transcript, and the boundary is why.</b> A transcript is
    /// customer content and crosses as a reference. An airspace document is the
    /// tenant's own governance text, which already crosses whole every time
    /// somebody applies one — so carrying it is the established disposition
    /// rather than a new one.
    /// </remarks>
    [Test]
    public async Task The_document_travels_whole()
    {
        await Assert.That(Drafted().Document).Contains("based-on:");
    }

    [Test]
    public async Task It_has_a_slot_of_its_own_on_the_envelope()
    {
        var envelope = new FactEnvelope
        {
            IdempotencyKey = "k",
            Kind = FactKinds.DocumentProposal,
            Digest = new string('a', 64),
            ObservedAt = DateTimeOffset.UnixEpoch,
            Document = Drafted(),
        };

        await Assert.That(envelope.Document).IsNotNull();
        await Assert.That(envelope.Document!.Name).IsEqualTo("ui-preview");
    }
}
