using System.Net;
using System.Text.Json;
using Gg.Contracts;
using Gg.Runner.Pools;

namespace Gg.Runner.Tests;

/// <summary>
/// A member is created as a runner: it is told where the control plane is and
/// how to become somebody.
/// </summary>
/// <remarks>
/// <para>
/// <b>This revises a stated principle, deliberately.</b>
/// <c>ImageDigestSeamTests</c> asserts the create spec carries <i>"no credential —
/// what a member can reach is decided by the image it was built from, not by what
/// we inject"</i>. That was right while a member was inert scenery. It is what has
/// kept every pool member from ever running a flight: nothing could tell one where
/// to call or who to be, so the only working example baked a developer's session
/// into an image.
/// </para>
/// <para>
/// <b>A nonce is injected, never a credential.</b>
/// <c>GET /containers/gg-pool-*/json</c> is reachable through the scope proxy, so
/// anything in a member's environment is readable by an inspect for the life of the
/// container. The nonce is single-use and worth nothing once the member has
/// started; the credential itself is fetched by the member over its own connection.
/// </para>
/// <para>
/// <b>What is still refused is the part that matters.</b> No <c>HostConfig</c>, no
/// binds, nothing privileged. That containment is a client-side convention — the
/// proxy filters <c>?name=</c> and does not read the body — so it is asserted here
/// byte for byte rather than trusted to a comment.
/// </para>
/// </remarks>
public class MemberIsARunnerTests
{
    private const string Pinned =
        "registry.example.invalid/base@sha256:" + "b" + "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcde";

    private const string ControlPlane = "https://control.example.invalid";

    /// <summary>Answers a create, and keeps the body it was sent.</summary>
    private sealed class SpecRecordingDaemon : HttpMessageHandler
    {
        internal string? CreateBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/containers/create", StringComparison.Ordinal))
            {
                CreateBody = await request.Content!.ReadAsStringAsync(cancellationToken);
                return new HttpResponseMessage(HttpStatusCode.Created)
                {
                    Content = new StringContent("""{"Id":"abc"}"""),
                };
            }

            if (request.RequestUri.AbsolutePath.EndsWith("/json", StringComparison.Ordinal))
            {
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            }

            return new HttpResponseMessage(HttpStatusCode.NoContent);
        }
    }

    private static MemberSpec ASpec() => new()
    {
        Image = Pinned,
        ControlPlane = ControlPlane,
        Nonce = "a-single-use-nonce",
    };

    private static async Task<JsonDocument> CreatedAsync(MemberSpec spec)
    {
        var daemon = new SpecRecordingDaemon();
        var adapter = new DockerPoolAdapter(
            new HttpClient(daemon) { BaseAddress = new Uri("http://pull-point") });

        _ = await adapter.RefreshAsync("gg-pool-dev", "gg-pool-dev-1", spec);

        return JsonDocument.Parse(daemon.CreateBody!);
    }

    private static IReadOnlyList<string> EnvOf(JsonDocument spec) =>
        [.. spec.RootElement.GetProperty("Env").EnumerateArray().Select(e => e.GetString()!)];

    [Test]
    public async Task A_member_is_told_where_the_control_plane_is()
    {
        // Without this a member starts, finds the built-in localhost default,
        // and answers to itself while looking configured.
        using var spec = await CreatedAsync(ASpec());

        await Assert.That(EnvOf(spec)).Contains($"GG_CONTROL_PLANE={ControlPlane}");
    }

    [Test]
    public async Task A_member_is_told_where_to_ask_for_its_own_address()
    {
        // MEASURED ON gg-pool-ui-1, AND IT IS THE WHOLE OF WHY A MEMBER CANNOT
        // BE REACHED. A console asking to be introduced got as far as opening
        // the channel and then NoRouteBetweenUs: ICE never connected. The
        // member offered only the address it can see, 172.17.x inside its
        // container, because nothing ever told it where to ask what its public
        // one is. The deployment tells the resident - GG_STUN_SERVERS is on
        // that unit - and the member it warms was told nothing.
        //
        // AND THE ROUTE IS THERE. A STUN request from inside that container
        // answers 20.127.66.195:49106 from a local 49106, and the console's own
        // answers 76.14.3.224:59070 from 59070: both mappings keep the port, so
        // the two ends can meet. Nobody was asking.
        using var spec = await CreatedAsync(
            ASpec() with { StunServers = "stun:one.invalid:3478,stun:two.invalid:3478" });

        await Assert.That(EnvOf(spec))
            .Contains("GG_STUN_SERVERS=stun:one.invalid:3478,stun:two.invalid:3478")
            .Because("a member is the one machine class nobody can open a shell on to "
                   + "configure, so what it needs to be reachable has to arrive with it.");
    }

    [Test]
    public async Task A_member_is_told_nothing_when_the_deployment_names_no_server()
    {
        // ABSENCE, NOT AN EMPTY CLAIM. StunConfiguration reads "told nothing"
        // from a missing variable and an empty one alike, and writing the
        // variable with nothing in it would say this deployment named a server
        // and the server is the empty string.
        using var spec = await CreatedAsync(ASpec());

        await Assert.That(EnvOf(spec).Any(
                e => e.StartsWith("GG_STUN_SERVERS", StringComparison.Ordinal)))
            .IsFalse();
    }

    [Test]
    public async Task A_member_is_NOT_told_what_to_advertise()
    {
        // WRITTEN THE OTHER WAY FIRST, and it was wrong. A member receives what
        // it may advertise in the REDEEM response, decided control-plane-side
        // from the strategy - so an environment variable saying the same thing
        // would be a second source of truth for exactly the value this slice
        // exists to stop taking on a runner's word.
        using var spec = await CreatedAsync(ASpec());

        await Assert.That(EnvOf(spec).Any(
                e => e.StartsWith("GG_RUNNER_LABELS", StringComparison.Ordinal)))
            .IsFalse()
            .Because("labels come with the credential. Two places to read them is one place "
                   + "to disagree.");
    }

    [Test]
    public async Task A_member_is_given_a_nonce_and_never_a_credential()
    {
        // THE WHOLE REASON A NONCE EXISTS. An inspect of this container is
        // permitted through the scope proxy, so what is here has to be worth
        // nothing the moment the member has started.
        using var spec = await CreatedAsync(ASpec());
        var env = EnvOf(spec);

        await Assert.That(env).Contains("GG_MEMBER_NONCE=a-single-use-nonce");

        foreach (var forbidden in (string[])["GG_RUNNER_TOKEN", "GG_SESSION", "GG_CREDENTIAL"])
        {
            await Assert.That(env.Any(e => e.StartsWith(forbidden, StringComparison.Ordinal)))
                .IsFalse()
                .Because($"'{forbidden}' would be readable by a docker inspect for the life of "
                       + "the container, which is exactly what the nonce avoids.");
        }
    }

    [Test]
    public async Task The_member_still_knows_which_image_it_is()
    {
        // The seam slice twenty built and never got to use: a runner inside a
        // made member ships a fact carrying this digest.
        using var spec = await CreatedAsync(ASpec());

        await Assert.That(EnvOf(spec)).Contains($"GG_IMAGE_DIGEST={Pinned}");
    }

    [Test]
    public async Task Nothing_privileged_and_no_binds_are_ever_sent()
    {
        // THE ANCHOR, and it carries more weight than it looks. The proxy
        // filters ?name= and does not read the create body, so this containment
        // is held HERE or nowhere.
        using var spec = await CreatedAsync(ASpec());

        var members = spec.RootElement.EnumerateObject().Select(p => p.Name).ToArray();

        await Assert.That(members).IsEquivalentTo((string[])["Image", "Labels", "Env"])
            .Because("a member is the image, the label that says which pool, and what it needs "
                   + "to become a runner. HostConfig would pass the proxy unexamined.");
    }

    [Test]
    public async Task A_member_with_no_nonce_is_not_created_at_all()
    {
        // Article XI. A member that cannot become anybody claims nothing and
        // reports nothing, and a pool full of them is the 196 wearing a better
        // image - so it is refused where the refusal can name the cause.
        var daemon = new SpecRecordingDaemon();
        var adapter = new DockerPoolAdapter(
            new HttpClient(daemon) { BaseAddress = new Uri("http://pull-point") });

        var observed = await adapter.RefreshAsync(
            "gg-pool-dev", "gg-pool-dev-1", ASpec() with { Nonce = null });

        await Assert.That(observed.Outcome).IsEqualTo(PoolOutcomes.Failed);
        await Assert.That(daemon.CreateBody).IsNull()
            .Because("nothing was created, so nothing has to be cleaned up. A member that "
                   + "cannot register is a container the pool would count and never use.");
    }
}
