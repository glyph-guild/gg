namespace Gg.Contracts.Tests;

using Gg.Contracts.Description;

/// <summary>
/// Flight creation declares what the flight is FOR, and where it will run.
/// </summary>
/// <remarks>
/// <para>
/// <b>A work kind is knowable at the start</b> - "am I researching or
/// implementing" cannot change mid-flight, which is the test that
/// reconciled selection-by-kind with the classification rejection
/// (ADR-0014). What the work TOUCHES stays a narrowing, attached from
/// facts; what it is FOR is declared here, once, before anything runs.
/// </para>
/// <para>
/// <b>All three members are optional, and absent means what it always
/// meant.</b> No work kind is <c>implement</c> - every flight before kinds
/// existed was one - and no selection inherits the composed envelope's,
/// which is where the bound lives. A client that predates these members
/// launches exactly the flight it launched yesterday.
/// </para>
/// </remarks>
public class WorkKindSurfaceTests
{
    [Test]
    public async Task Flight_creation_can_declare_a_kind_and_a_place()
    {
        var declared = new FlightLaunchRequest
        {
            Name = "fix the rounding",
            Intent = new FlightIntent { Kind = FlightIntentKinds.Text, Text = "fix it" },
            WorkKind = "implement",
            Environment = "aspire-payments",
            Repository = "acme/payments",
        };

        await Assert.That(declared.WorkKind).IsEqualTo("implement");
        await Assert.That(declared.Environment).IsEqualTo("aspire-payments");
        await Assert.That(declared.Repository).IsEqualTo("acme/payments");
    }

    [Test]
    public async Task The_wire_declares_the_four_members_beside_name_and_intent()
    {
        // Declared, not derived - if each side derived these from its own
        // serializer they would agree with themselves and prove nothing.
        await Assert.That(ProtocolSurface.JsonMembers[typeof(FlightLaunchRequest)])
            .IsEquivalentTo((string[])["name", "intent", "workKind", "environment", "repository", "runner"]);
    }

    [Test]
    public async Task Absent_stays_absent()
    {
        var bare = new FlightLaunchRequest
        {
            Name = "yesterday's flight",
            Intent = new FlightIntent { Kind = FlightIntentKinds.Text, Text = "as before" },
        };

        await Assert.That(bare.WorkKind).IsNull()
            .Because("null is 'not declared', which the control plane reads as implement - "
                   + "the kind every flight before kinds existed was.");
        await Assert.That(bare.Environment).IsNull();
        await Assert.That(bare.Repository).IsNull();
    }
}
