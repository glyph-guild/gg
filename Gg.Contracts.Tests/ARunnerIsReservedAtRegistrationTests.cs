using Gg.Contracts;

namespace Gg.Contracts.Tests;

/// <summary>
/// A runner can be registered reserved to the person registering it.
/// </summary>
/// <remarks>
/// <para>
/// <b>Labels only ever say yes.</b> A flight's requirements are matched by JSONB
/// containment against what a runner advertises, which is monotone: adding a
/// label can only ever add work. An untargeted flight requires nothing, and
/// nothing is contained by every label set — so <b>there is no value a runner
/// can advertise that makes it take less.</b> A laptop is a candidate for every
/// untargeted flight in its tenant.
/// </para>
/// <para>
/// <b>Reserved is not a label, and needs no charted environment.</b> A reserved
/// runner may advertise nothing at all and still be reachable by its holder's
/// flights. Making this a label would require charting an environment to reserve
/// a laptop, and would inherit containment's one direction.
/// </para>
/// <para>
/// <b>Set by the REGISTERING session, so there is no unreserved window.</b> A
/// runner reserved by a later call would take public work until that call
/// landed — which on a busy tenant is every flight in the queue.
/// </para>
/// <para>
/// <b>A boolean, not a principal.</b> The request says <i>reserve this to me</i>
/// and the control plane knows who "me" is; a request naming a principal would
/// be one person reserving another's runner, which is a different act with a
/// different approver and is deliberately not in this version.
/// </para>
/// </remarks>
public class ARunnerIsReservedAtRegistrationTests
{
    private static RunnerRegistrationRequest ARequest(bool reserved) => new()
    {
        Label = "a-laptop",
        ProtocolVersion = 1,
        Reserved = reserved,
    };

    [Test]
    public async Task A_registration_can_ask_for_the_runner_to_be_reserved()
    {
        await Assert.That(ARequest(reserved: true).Reserved).IsTrue();
    }

    [Test]
    public async Task Registering_without_asking_is_unreserved()
    {
        // THE ANCHOR, and it is every runner in every deployment. The default
        // has to be what the fleet does today: take public work. Reversing it
        // is a breaking change that has to be announced, and this member is
        // opt-in precisely so that decision stays open.
        await Assert.That(ARequest(reserved: false).Reserved).IsFalse();
        await Assert.That(new RunnerRegistrationRequest
        {
            Label = "a-laptop",
            ProtocolVersion = 1,
        }.Reserved).IsFalse()
            .Because("a registration written before this member existed means what it meant, "
                   + "and it did not mean reserved.");
    }

    [Test]
    public async Task The_request_carries_no_principal_of_any_kind()
    {
        // Reserving to SOMEBODY ELSE is a different act - it routes work at a
        // person who did not ask for it - and it is not this version. A member
        // here that could name one would make that act reachable by accident,
        // through a request the runner itself composes.
        var members = typeof(RunnerRegistrationRequest).GetProperties().Select(p => p.Name);

        await Assert.That(members).DoesNotContain("ReservedTo");
        await Assert.That(members).DoesNotContain("PrincipalId")
            .Because("v0 reserves to the caller on every path, and the control plane is what "
                   + "knows who that is.");
    }
}
