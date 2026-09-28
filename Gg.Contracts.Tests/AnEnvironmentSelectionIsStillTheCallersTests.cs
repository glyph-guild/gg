using Gg.Contracts;
using Gg.Contracts.Description;

namespace Gg.Contracts.Tests;

/// <summary>
/// Where the LOOP runs stays the caller's to name, and where the STACK runs
/// stays the kind's — so a flight request carries the first and never the
/// second.
/// </summary>
/// <remarks>
/// <para>
/// <b>The obvious next commit is the one this exists to stop.</b>
/// <see cref="Envelope.Hosts"/> has just arrived, and the shortest way to make a
/// flight use it is to let a caller pass it — <c>gg fly --hosts ui</c>, beside
/// <c>--environment</c>, which already works exactly that way. That would be
/// wrong in a way no other test in this repository would notice: the member is
/// <c>work-kind-only</c> precisely so that what a kind needs is a property of
/// the KIND, and a caller who can override it per flight has taken the
/// declaration back apart.
/// </para>
/// <para>
/// <b>And it is the defect this slice exists for, inverted.</b> <c>ui-preview</c>
/// failed because the environment was only ever a <c>gg fly</c> argument and a
/// kind could not say what it needed — GG-309 asked for <c>environment=dev</c>
/// and got a worker with no browser. Answering that by adding a second argument
/// would leave the kind exactly as unable to say anything as it was.
/// </para>
/// <para>
/// <b>Asserted against the DECLARED surface rather than the type.</b>
/// <c>ProtocolSurface.JsonMembers</c> is what the two repositories agree on and
/// what <c>ProtocolConformanceTests</c> holds both sides to, so a member that
/// never reaches it cannot reach a control plane either — whatever a C# property
/// happens to exist.
/// </para>
/// </remarks>
public class AnEnvironmentSelectionIsStillTheCallersTests
{
    private static IReadOnlyList<string> RequestMembers() =>
        ProtocolSurface.JsonMembers[typeof(FlightLaunchRequest)];

    [Test]
    public async Task A_caller_still_names_the_environment_its_loop_runs_in()
    {
        await Assert.That(RequestMembers()).Contains("environment")
            .Because("gg fly --environment is how a flight picks from the tenant's charted "
                   + "bound, and nothing about a work kind saying where its stack runs changes "
                   + "who chooses that.");
    }

    [Test]
    public async Task And_cannot_name_where_the_stack_runs()
    {
        await Assert.That(RequestMembers()).DoesNotContain("hosts")
            .Because("hosts: is work-kind-only so that what a kind needs is a property of the "
                   + "kind. A caller able to override it per flight has taken that declaration "
                   + "back apart - which is the defect this slice exists for, inverted: "
                   + "ui-preview failed because the environment was ONLY ever a gg fly "
                   + "argument.");
    }

    [Test]
    public async Task The_two_are_not_the_same_word_wearing_two_spellings()
    {
        // A READABLE ANCHOR for the confusion this pair invites. `environments:`
        // is the tenant's root-only BOUND, `environment` on a request is the
        // caller's pick from it, and `hosts:` is the kind's statement about
        // something else entirely. Three things, and the first two are the only
        // ones a caller touches.
        var envelope = ProtocolSurface.JsonMembers[typeof(Envelope)];

        await Assert.That(envelope).Contains("environments");
        await Assert.That(envelope).Contains("hosts");
        await Assert.That(RequestMembers()).Contains("environment");
        await Assert.That(RequestMembers()).DoesNotContain("environments");
    }
}
