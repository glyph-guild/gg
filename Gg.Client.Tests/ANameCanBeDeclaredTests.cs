using Gg.Client;
using Gg.Contracts;

namespace Gg.Client.Tests;

/// <summary>
/// Declaring a name: the act with an endpoint, a contract type, and no caller.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nothing in gg could create an envelope document.</b> A name has to exist
/// in the topology before a document can be applied to it — the control plane
/// answers <i>"Declare the name - POST /v1/airspace/names - and apply again"</i>
/// — and <c>DeclareNameRequest</c> has been a registered, serializable contract
/// type with no method calling it. A person could edit every document they
/// already had and could not make a new one.
/// </para>
/// <para>
/// <b>Two success shapes, and the 202 is the ordinary one.</b> ADR-0016 § 6
/// makes a registration a widening unconditionally — <i>"reach that did not
/// exist a moment ago"</i> — so a new name rides the gate the widened document
/// declares, and the answer names the flight and who decides. The 200 is the
/// narrow case: the name is already there, and a topology entry has no
/// updatable body, so present is identical.
/// </para>
/// <para>
/// <b>Reported the way apply already reports a divert.</b> <c>AppliedDocument</c>
/// folds a gated apply into the same record as a landed one, with the flight,
/// the approver and the widened field nullable beside it — so this reuses that
/// shape rather than inventing a second way to say the same thing.
/// </para>
/// </remarks>
public class ANameCanBeDeclaredTests
{
    private static StoredSession SignedIn { get; } = new()
    {
        SessionToken = "stub-session",
        ExpiresAt = new DateTimeOffset(2027, 1, 1, 0, 0, 0, TimeSpan.Zero),
        TenantId = "stub-tenant",
        PrincipalDisplay = "someone@example.test",
    };

    private static FlightCommands Against(StubControlPlane stub, HttpClient http)
    {
        http.BaseAddress = new Uri(stub.BaseAddress);
        return new FlightCommands(new ControlPlaneClient(http), new HeldSessionStore(SignedIn));
    }

    [Test]
    public async Task A_name_that_rides_a_flight_reports_the_flight_and_who_decides()
    {
        await using var stub = new StubControlPlane
        {
            NamePending = new RegistrationPending
            {
                Flight = "GG-58",
                Awaiting = "an-architect",
                Widens = "topology",
            },
        };

        using var http = new HttpClient();
        var declared = ((VerbResult.NameDeclared)await Against(stub, http)
            .DeclareNameAsync("narrowing", "pci", "root")).Value;

        await Assert.That(declared.Flight).IsEqualTo("GG-58");
        await Assert.That(declared.Awaiting).IsEqualTo("an-architect")
            .Because("a gate nobody is named for is a gate a person cannot go and ask about, "
                   + "which is the same defect the apply divert already answers for.");
        await Assert.That(declared.Widens).IsEqualTo("topology");

        await Assert.That(declared.Name).IsEqualTo("pci");
        await Assert.That(declared.Role).IsEqualTo("narrowing");
    }

    [Test]
    public async Task A_name_already_declared_is_reported_live_rather_than_pending()
    {
        // THE POSITIVE CONTROL, and it is not decoration: a verb that reported
        // every declaration as pending would satisfy the test above and tell a
        // person to wait for a gate that will never open, because the name is
        // already theirs.
        await using var stub = new StubControlPlane
        {
            NameLive = new TopologyName
            {
                Name = "pci",
                Role = "narrowing",
                Parent = "root",
                DeclaredBy = "an-architect",
                DeclaredAt = DateTimeOffset.UnixEpoch,
            },
        };

        using var http = new HttpClient();
        var declared = ((VerbResult.NameDeclared)await Against(stub, http)
            .DeclareNameAsync("narrowing", "pci", "root")).Value;

        await Assert.That(declared.Flight).IsNull()
            .Because("nothing rode a flight, so naming one would send somebody to a gate "
                   + "list that does not hold it.");
        await Assert.That(declared.DeclaredBy).IsEqualTo("an-architect");
    }

    [Test]
    public async Task A_declaration_the_door_refuses_carries_the_door_s_own_sentence()
    {
        // THE REFUSALS ARE THE USEFUL PART OF THIS DOOR - reserved, malformed,
        // an unknown role, an orphaned parent - and each one is composed at the
        // control plane naming the value. Rewording them here would be a second
        // opinion about what is wrong with a name.
        await using var stub = new StubControlPlane
        {
            NameRefusal = "'root' is reserved: it is in every tenant's topology by synthesis "
                        + "and cannot be declared, so nothing rode a flight.",
        };

        using var http = new HttpClient();
        var commands = Against(stub, http);

        var refused = await Assert.That(
                async () => await commands.DeclareNameAsync("work-kind", "root", "root"))
            .Throws<EnvelopeRefusedException>();

        await Assert.That(refused!.Message).Contains("reserved", StringComparison.Ordinal)
            .Because("the sentence a person acts on is the one the door composed, and it "
                   + "names the value.");
    }

    [Test]
    public async Task The_role_the_name_and_the_parent_all_cross_the_wire()
    {
        // WHAT ACTUALLY LEAVES, asserted against the body rather than against
        // the answer. A verb that dropped the parent would still get a 202 from
        // a door whose parent check treats blank as "no parent", and the name
        // would be unreachable afterwards.
        await using var stub = new StubControlPlane
        {
            NamePending = new RegistrationPending
            {
                Flight = "GG-58",
                Awaiting = "an-architect",
                Widens = "topology",
            },
        };

        using var http = new HttpClient();
        _ = await Against(stub, http).DeclareNameAsync("narrowing", "pci", "migrate-data");

        await Assert.That(stub.DeclaredName).IsNotNull();
        await Assert.That(stub.DeclaredName!.Name).IsEqualTo("pci");
        await Assert.That(stub.DeclaredName.Role).IsEqualTo("narrowing");
        await Assert.That(stub.DeclaredName.Parent).IsEqualTo("migrate-data");
    }
}
