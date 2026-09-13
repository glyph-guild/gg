using System.Reflection;
using Gg.Contracts;
using Gg.Contracts.Description;

namespace Gg.Contracts.Tests;

/// <summary>
/// A capability is for one console, one runner, one purpose, and it expires.
/// </summary>
/// <remarks>
/// <para>
/// <b>ADR-0013's phrase made into a shape.</b> The ADR says the control plane
/// mints "a short-lived capability for one pair, one purpose —
/// <c>(this console, that runner, tail-your-own-log, 60s)</c>". Four narrowings
/// in a sentence are four things a later implementation can quietly drop; four
/// required members are not.
/// </para>
/// <para>
/// <b>Each is asserted separately, because a capability broad in one dimension
/// is broad.</b> A test that only counted members would pass on a claims type
/// that named a TENANT rather than a principal.
/// </para>
/// </remarks>
public class AnIntroductionNarrowsFourWaysTests
{
    private static Gg.Contracts.Description.Endpoint Introduction =>
        ProtocolSurface.Endpoints.Single(
            e => e.Path == "/v1/runners/{id}/introduction" && e.Method == "POST");

    [Test]
    public async Task The_capability_names_one_console_and_one_runner()
    {
        var members = typeof(RunnerCapabilityClaims).GetProperties()
            .Select(p => p.Name).ToList();

        await Assert.That(members).Contains("PrincipalId")
            .Because("one PERSON. A tenant here would let anybody in it answer as anybody.");
        await Assert.That(members).Contains("RunnerId")
            .Because("one MACHINE. A label would be several, since a label is what a fleet "
                   + "shares and an id is what names one box.");
    }

    [Test]
    public async Task The_purpose_is_a_closed_vocabulary_a_value_cannot_slip_into()
    {
        // A FREE STRING WOULD MAKE "one purpose" A DESCRIPTION OF TODAY. The
        // second purpose would arrive as a typo nobody noticed, and widening
        // what a capability authorises is exactly the change that should cost a
        // version.
        //
        // IT COST ONE, WHICH IS THIS ASSERTION WORKING RATHER THAN BEING
        // WEAKENED. `configure-this-runner` moved the fingerprint, spent
        // 0.153.0, and made somebody come here and edit this number on purpose -
        // which is the whole of what the count was ever for. The list is what is
        // asserted, so a THIRD value still has to be argued for here.
        await Assert.That(RunnerCapabilityPurposes.All).IsEquivalentTo(
            (string[])
            [
                RunnerCapabilityPurposes.TailYourOwnLog,
                RunnerCapabilityPurposes.ConfigureThisRunner,
            ]);

        var membership = typeof(RunnerCapabilityPurposes)
            .GetCustomAttribute<VocabularyOfAttribute>();

        await Assert.That(membership).IsNotNull()
            .Because("a closed vocabulary outside both ledgers is one a person can widen "
                   + "without anything moving.");
    }

    [Test]
    public async Task The_capability_expires_and_says_when()
    {
        var members = typeof(RunnerCapabilityClaims).GetProperties().Select(p => p.Name);

        await Assert.That(members).Contains("ExpiresAt")
            .Because("short-lived is the fourth narrowing, and a capability with no expiry "
                   + "is a standing grant with a nicer name.");

        // AND ON THE ANSWER TOO, because the console has to be able to tell a
        // person how long they have without parsing a capability it is not
        // supposed to understand.
        await Assert.That(typeof(RunnerIntroduction).GetProperties().Select(p => p.Name))
            .Contains("ExpiresAt");
    }

    [Test]
    public async Task The_capability_binds_the_key_the_console_generated()
    {
        // WITHOUT THIS, ANYTHING HOLDING THE CAPABILITY COULD ANSWER AS THEM.
        // The runner checks that whoever it is talking to holds the key the
        // capability names - and the control plane still cannot read either half.
        await Assert.That(typeof(RunnerCapabilityClaims).GetProperties().Select(p => p.Name))
            .Contains("EphemeralKeyHash");
        await Assert.That(typeof(RunnerIntroductionRequest).GetProperties().Select(p => p.Name))
            .Contains("EphemeralPublicKey")
            .Because("the console has to send the key for the capability to bind it.");
    }

    [Test]
    public async Task Introducing_is_a_persons_act()
    {
        // A RUNNER MUST NOT INTRODUCE ITSELF, for parking's reason and one more:
        // a runner that could mint its own introduction could reach any console
        // that would answer.
        await Assert.That(Introduction.Audience).IsEqualTo(Audience.Developer);
        await Assert.That(Introduction.RequiredHeaders).Contains(ProtocolSurface.SessionHeader);
        await Assert.That(Introduction.RequiredHeaders)
            .DoesNotContain(ProtocolSurface.RunnerHeader);
    }

    [Test]
    public async Task A_runner_with_no_key_is_refused_as_such()
    {
        // 409 RATHER THAN 404. "There is nothing to seal to" and "there is no
        // such runner" are different facts, and this system's own vocabulary
        // calls collapsing two silences its most dangerous failure. A runner
        // registered before keys existed still takes work; it cannot be
        // introduced, and that has to read as CANNOT.
        await Assert.That(Introduction.Statuses).Contains(409);
        await Assert.That(Introduction.Statuses).Contains(404);
        await Assert.That(Introduction.Statuses).Contains(403)
            .Because("within the tenant, a runner somebody else registered is refused by a "
                   + "rule the caller can be told - they can already see the row.");
    }

    [Test]
    public async Task The_registration_key_is_optional_so_an_old_runner_still_works()
    {
        var key = typeof(RunnerRegistrationRequest).GetProperty("PublicKey");

        await Assert.That(key).IsNotNull();

        // NULLABLE, and that is the two-repo rule rather than politeness: the
        // repositories are not upgraded in step, so a runner registered before
        // this member existed has to keep taking work.
        await Assert.That(new NullabilityInfoContext().Create(key!).WriteState)
            .IsEqualTo(NullabilityState.Nullable);
    }
}
