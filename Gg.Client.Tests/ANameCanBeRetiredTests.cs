using Gg.Client;
using Gg.Contracts;

namespace Gg.Client.Tests;

/// <summary>
/// A name can be retired, and retiring one always rides a gate.
/// </summary>
/// <remarks>
/// <para>
/// <b>THE DOOR SHIPPED AND NOTHING KNOCKED ON IT.</b>
/// <c>POST /v1/airspace/envelopes/{name}/retirement</c> has been in
/// <c>ProtocolSurface</c> with a response type and a status list, and no
/// method on <c>ControlPlaneClient</c> ever called it.
/// <c>Changeset.Retirement</c> was declared and ranked, and nothing produced a
/// change with that direction. So the estate could grow names and never lose
/// one — which made a typo in a filename permanent.
/// </para>
/// <para>
/// <b>A VERSION, NOT A DELETION</b> (ADR-0014): <i>"the only way to retire a
/// name is to apply a terminal version of it, because retiring by deleting a
/// topology entry is a governance-critical change wearing bookkeeping's
/// clothes — the constraint stops attaching and no version records that it
/// did."</i>
/// </para>
/// <para>
/// <b>And there is NO 200, by contract.</b> <i>"A document that stops applying
/// removes every constraint in it at once, so this is a widening by
/// construction and always rides the gate — registration's rule in the other
/// direction."</i> So the only success is 202, and a client that reported a
/// retirement as done would be reporting something that has not happened. That
/// is the assertion this file exists for.
/// </para>
/// </remarks>
public class ANameCanBeRetiredTests
{
    private static StoredSession ASession() => new()
    {
        SessionToken = StubControlPlane.IssuedSessionToken,
        ExpiresAt = DateTimeOffset.UtcNow.AddHours(12),
        TenantId = "019fe062-d000-730c-a37d-7247342cd810",
        PrincipalDisplay = "stub-principal",
    };

    private static FlightCommands Build(StubControlPlane stub) =>
        new(new ControlPlaneClient(new HttpClient { BaseAddress = new Uri(stub.BaseAddress) }),
            new HeldSessionStore(ASession()));

    [Test]
    public async Task Retiring_opens_a_flight_and_says_who_decides()
    {
        await using var stub = new StubControlPlane();

        var result = await Build(stub).RetireNameAsync("score-hall");

        await Assert.That(stub.RetiredName).IsEqualTo("score-hall")
            .Because("the name is in the path, and it is the whole of the request - this "
                   + "door takes no body.");

        var retired = (VerbResult.NameRetired)result;

        await Assert.That(retired.Value.Flight).IsNotNull()
            .Because("there is no 200 on this door: a retirement removes every constraint "
                   + "in its document at once, so it is a widening by construction and "
                   + "always rides a gate. A client reporting one as done would be "
                   + "reporting something that has not happened.");

        await Assert.That(retired.Value.Awaiting).IsNotNull()
            .Because("a gate with nobody named is a gate a person cannot go and ask about.");
    }

    [Test]
    public async Task The_text_says_the_name_is_still_in_force_until_the_gate_opens()
    {
        await using var stub = new StubControlPlane();

        var said = VerbOutput.ToText(await Build(stub).RetireNameAsync("score-hall"));

        await Assert.That(said).Contains("score-hall", StringComparison.Ordinal);

        await Assert.That(said).Contains("gate", StringComparison.OrdinalIgnoreCase)
            .Because("somebody who ran this and walked away would otherwise believe the "
                   + "name was gone. It is not: it governs until a person decides. Said: "
                   + said);
    }

    [Test]
    public async Task A_refusal_is_the_control_planes_own_sentence()
    {
        await using var stub = new StubControlPlane();

        stub.RetirementRefusal =
            "'root' cannot be retired: the floor is synthesized by the read and is "
          + "undeclarable by rule.";

        var refused = await Assert.ThrowsAsync<EnvelopeRefusedException>(
            async () => await Build(stub).RetireNameAsync("root"));

        await Assert.That(refused!.Message)
            .Contains("cannot be retired", StringComparison.Ordinal)
            .Because("carried through unchanged, like every other refusal on this client - "
                   + "the door knows why and gg does not paraphrase it.");
    }
}
