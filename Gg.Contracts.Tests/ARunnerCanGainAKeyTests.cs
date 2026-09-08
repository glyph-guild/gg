using System.Reflection;
using Gg.Contracts;
using Gg.Contracts.Description;

namespace Gg.Contracts.Tests;

/// <summary>
/// A runner that predates keys can offer one without being re-registered.
/// </summary>
/// <remarks>
/// <para>
/// <b>Found by walking the live fleet, and no test could have found it.</b>
/// Registration is read-or-register: a runner with a stored credential reuses it
/// and never calls the registration route again, so the key
/// <c>RunnerIdentityKey.LoadOrCreate</c> makes is never presented to anybody.
/// Every runner registered before 0.125.0 was therefore permanently
/// unintroducible — holding a key on its own disk that the control plane had
/// never seen — and re-running <c>gg runner up</c> did not fix it, because there
/// was nothing to fix it with. Catching that needs a machine with a registration
/// older than the feature, which is exactly what a test fixture never has.
/// </para>
/// <para>
/// <b>Set once, and a different key is refused.</b> Consoles pin the first key
/// they see. A route that replaced one silently would be the substitution the
/// pin exists to catch, wearing the clothes of a migration.
/// </para>
/// </remarks>
public class ARunnerCanGainAKeyTests
{
    private static Endpoint TheRoute() =>
        ProtocolSurface.Endpoints.Single(
            e => e.Path == "/v1/runners/{id}/key" && e.Method == "POST");

    [Test]
    public async Task The_runner_offers_it_and_authenticates_as_itself()
    {
        // NOT A PERSON'S ROUTE. It is the runner's key and it holds the private
        // half; a route where somebody else supplied a runner's public key would
        // be a route for supplying a key that is not that runner's at all.
        var route = TheRoute();

        await Assert.That(route.Audience).IsEqualTo(Audience.Runner);
        await Assert.That(route.RequiredHeaders).Contains(ProtocolSurface.RunnerHeader);
        await Assert.That(route.Request).IsEqualTo(typeof(RunnerKeyOffer));
    }

    [Test]
    public async Task A_different_key_is_a_conflict_rather_than_an_overwrite()
    {
        // THE WHOLE SAFETY OF THIS ROUTE. Without 409 it is a way to replace the
        // key a console pinned, which is the attack the pin was added to notice.
        var route = TheRoute();

        await Assert.That(route.Statuses).Contains(409);
        await Assert.That(route.Statuses).Contains(204)
            .Because("there is nothing to say back when it worked, and a body would be a "
                   + "second place for the key to travel.");
        await Assert.That(route.Statuses).DoesNotContain(200)
            .Because("200 with no content is a body somebody will later put something in.");
    }

    [Test]
    public async Task It_carries_the_key_and_nothing_else()
    {
        // ONE MEMBER. A route that also took a label or a lifetime would be a
        // second registration path, and two ways to say what a runner is is how
        // the two come to disagree.
        await Assert.That(ProtocolSurface.JsonMembers[typeof(RunnerKeyOffer)])
            .IsEquivalentTo(new[] { "publicKey" });

        await Assert.That(typeof(RunnerKeyOffer).GetProperties().Select(p => p.Name))
            .IsEquivalentTo(new[] { "PublicKey" });
    }

    [Test]
    public async Task It_is_in_the_closed_vocabulary_with_a_pinned_id()
    {
        // The four-way registration, which is what makes a rename cost a version
        // rather than silently changing what crosses the wire.
        await Assert.That(Vocabulary.Types).Contains(typeof(RunnerKeyOffer));

        await Assert.That(typeof(RunnerKeyOffer).GetCustomAttribute<PinnedIdAttribute>())
            .IsNotNull()
            .Because("a wire type without one has its identity in its NAME, so renaming it "
                   + "would change what a consumer sees with nothing failing.");
    }

    [Test]
    public async Task The_route_it_repairs_is_still_the_one_that_refuses_a_keyless_runner()
    {
        // The liveness half, and the pair is the point: 409 there is the symptom
        // this route exists to cure, so a change that removed it would leave this
        // one solving nothing.
        var introduce = ProtocolSurface.Endpoints.Single(
            e => e.Path == "/v1/runners/{id}/introduction" && e.Method == "POST");

        await Assert.That(introduce.Statuses).Contains(409)
            .Because("'this runner registered before it could offer a key' is what a person "
                   + "sees, and it is what POST /key is for.");
    }
}
