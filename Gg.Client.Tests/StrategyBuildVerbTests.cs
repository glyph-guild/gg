using System.Net;
using System.Text;
using Gg.Contracts;

namespace Gg.Client.Tests;

/// <summary>
/// <c>gg strategy build &lt;name&gt;</c> asks the control plane for a build of the
/// strategy's recipe, and says what was decided - or, in the control plane's
/// own words, why nothing was.
/// </summary>
/// <remarks>
/// <para>
/// <b>Slice forty-one, S41.2-05, rule 4.</b> A build is decided, never polled:
/// a Dockerfile change merged into the recipe's branch builds nothing until a
/// person asks, and this is how a person asks. The build itself runs at the
/// pool's pull point, so the answer is what was DECIDED - the action, the
/// strategy version it answers to, and the recipe it will fetch.
/// </para>
/// <para>
/// <b>A refusal is the control plane's sentence, not ours.</b> A strategy that
/// names no recipe (404) and one with a build already standing (409) are both
/// facts only the control plane holds, so the verb says what it was told.
/// </para>
/// </remarks>
public class StrategyBuildVerbTests
{
    private sealed class Answering(HttpStatusCode status, string body) : HttpMessageHandler
    {
        internal HttpRequestMessage? Seen { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Seen = request;

            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            });
        }
    }

    private static StoredSession SignedIn { get; } = new()
    {
        SessionToken = "stub-session",
        ExpiresAt = new DateTimeOffset(2027, 1, 1, 0, 0, 0, TimeSpan.Zero),
        TenantId = "stub-tenant",
        PrincipalDisplay = "someone@example.test",
    };

    private static PoolAction ADecidedBuild() => new()
    {
        ActionId = Guid.Parse("01a0c000-0000-7000-8000-000000000041"),
        Pool = "gg-pool-dev",
        Action = PoolActions.Build,
        Image = "127.0.0.1:5000/gg-member@sha256:"
              + "7249a4ca005782263b53b7d560c1178bd7127ee0a03307dd3d03b3c3e21c6e2c",
        StrategyVersion = "dev@v7",
        DecidedAt = new DateTimeOffset(2026, 9, 18, 22, 0, 0, TimeSpan.Zero),
        Recipe = new PoolRecipe
        {
            Repository = new LeaseRepoRef
            {
                Provider = "ado",
                Slug = "JDX/gg-airspace",
                PinnedRef = "main",
            },
            Path = "images/gg-member",
        },
    };

    private static FlightCommands Commands(Answering handler) =>
        new(new ControlPlaneClient(new HttpClient(handler) { BaseAddress = new Uri("https://cp.invalid/") }),
            new HeldSessionStore(SignedIn));

    [Test]
    public async Task It_asks_the_strategys_build_door_and_says_what_was_decided()
    {
        var handler = new Answering(
            HttpStatusCode.Accepted,
            System.Text.Json.JsonSerializer.Serialize(
                ADecidedBuild(), ProtocolJsonContext.Default.PoolAction));

        var result = await Commands(handler).BuildStrategyAsync("dev");

        await Assert.That(handler.Seen!.Method).IsEqualTo(HttpMethod.Post);
        await Assert.That(handler.Seen.RequestUri!.AbsolutePath)
            .IsEqualTo("/v1/airspace/strategies/dev/builds");
        await Assert.That(result.Kind).IsEqualTo(VerbResultKinds.StrategyBuild);

        var text = VerbOutput.ToText(result);

        await Assert.That(text).Contains("gg-pool-dev");
        await Assert.That(text).Contains("dev@v7")
            .Because("the version it answers to is what says which recipe it will build.");
        await Assert.That(text).Contains("JDX/gg-airspace");
        await Assert.That(text).Contains("images/gg-member");
        await Assert.That(text).Contains("main")
            .Because("the ref, which the runner resolves - the commit arrives with the build, "
                   + "not with the decision.");
    }

    [Test]
    public async Task It_says_the_answer_is_a_decision_and_not_a_build()
    {
        // RULE 4's OTHER HALF, said to the person who asked: nothing has been
        // built yet, and the pin moves only when a build comes back through the
        // door. A verb that printed "built" here would be a claim about the
        // future.
        var handler = new Answering(
            HttpStatusCode.Accepted,
            System.Text.Json.JsonSerializer.Serialize(
                ADecidedBuild(), ProtocolJsonContext.Default.PoolAction));

        var text = VerbOutput.ToText(await Commands(handler).BuildStrategyAsync("dev"));

        await Assert.That(text).Contains("decided");
        await Assert.That(text).DoesNotContain("built ");
    }

    [Test]
    [Arguments(HttpStatusCode.NotFound, "dev names no recipe, so there is nothing to build.")]
    [Arguments(HttpStatusCode.Conflict, "A build of dev is already standing.")]
    public async Task A_refusal_is_the_control_planes_own_sentence(HttpStatusCode status, string said)
    {
        var handler = new Answering(status, said);

        var refused = await Assert.ThrowsAsync<EnvelopeRefusedException>(
            () => Commands(handler).BuildStrategyAsync("dev"));

        await Assert.That(refused!.Message).Contains(said);
    }
}
