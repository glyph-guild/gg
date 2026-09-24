using System.Net;
using Gg.Client;
using Gg.Contracts;
using Gg.Contracts.Description;

namespace Gg.Client.Tests;

/// <summary>
/// An exposure has a door: three routes declared in the contract, a client that
/// knocks on them, and a list that reads an older control plane's silence as
/// "none" rather than as a failure.
/// </summary>
/// <remarks>
/// <para>
/// <b>ADR-0027, the fourth step, and the first that leaves gg.</b> The role, its
/// reader and its working copy all landed without the control plane knowing the
/// word. This declares the routes and teaches the client to use them; the far
/// side grows the handlers next, and cannot move its pin until it does, because
/// a route declared and unserved is refused over there.
/// </para>
/// <para>
/// <b>A 404 on the list is "none", and that is load-bearing rather than lax.</b>
/// It is how a pull keeps working against a control plane pinned below the
/// version that grew the door — which is every control plane on the day this
/// ships. The same rule let the fleet door arrive without breaking anybody, and
/// it is the reason these three routes can be declared before they are served.
/// </para>
/// </remarks>
public class AnExposureHasADoorTests
{
    private const string Name = "jdapp";

    private static Exposure Eight() => new()
    {
        Kind = ExposureKinds.CloudflareTunnel,
        Inventory = new ExposureInventory
        {
            Size = 8,
            Hostnames = "jdapp-{slot}.example.dev",
            Credentials = "local:exposure/jdapp-{slot}",
        },
    };

    [Test]
    public async Task The_contract_declares_the_three_routes()
    {
        var paths = ProtocolSurface.Endpoints
            .Where(e => e.Path.StartsWith("/v1/airspace/exposures", StringComparison.Ordinal))
            .Select(e => $"{e.Method} {e.Path}")
            .ToList();

        await Assert.That(paths).Contains("PUT /v1/airspace/exposures/{name}");
        await Assert.That(paths).Contains("GET /v1/airspace/exposures/{name}");
        await Assert.That(paths).Contains("GET /v1/airspace/exposures")
            .Because("a route nobody declared is a door the far side may serve and no customer "
                   + "auditing the contract can see.");
    }

    [Test]
    public async Task Applying_one_asks_the_door_for_its_name()
    {
        var asked = new List<string>();
        var client = Client(asked, HttpStatusCode.OK, """
            {"version":"v1","appliedAt":"1970-01-01T00:00:00+00:00","changed":true}
            """);

        _ = await client.ApplyExposureAsync("session", Name, Eight());

        await Assert.That(asked).Contains("PUT /v1/airspace/exposures/jdapp");
    }

    [Test]
    public async Task A_control_plane_that_has_no_such_door_holds_no_exposures()
    {
        var asked = new List<string>();
        var client = Client(asked, HttpStatusCode.NotFound, "");

        var listed = await client.ListExposuresAsync("session");

        await Assert.That(listed.Exposures).IsEmpty()
            .Because("this is how a pull keeps working against a control plane pinned below "
                   + "the version that grew this door - which is every one of them on the day "
                   + "it ships. A throw here would take the whole estate down with it.");
    }

    [Test]
    public async Task The_list_reads_what_the_door_serves()
    {
        var client = Client([], HttpStatusCode.OK, """
            {"exposures":[{"name":"jdapp","version":"v2","appliedAt":"1970-01-01T00:00:00+00:00",
            "exposure":{"kind":"cloudflare-tunnel","inventory":{"size":8,
            "hostnames":"jdapp-{slot}.example.dev","credentials":"local:exposure/jdapp-{slot}"}}}]}
            """);

        var listed = await client.ListExposuresAsync("session");

        await Assert.That(listed.Exposures.Count).IsEqualTo(1);
        await Assert.That(listed.Exposures[0].Name).IsEqualTo(Name);
        await Assert.That(listed.Exposures[0].Exposure).IsEqualTo(Eight())
            .Because("the wire and the reader have to agree, and a name-cased mismatch would "
                   + "read back as a document with an empty inventory rather than as an error.");
    }

    private static ControlPlaneClient Client(
        List<string> asked, HttpStatusCode status, string body) =>
        new(new HttpClient(new Recording(asked, status, body))
        {
            BaseAddress = new Uri("https://control.invalid"),
        });

    /// <summary>Answers every request the same way and writes down what was asked.</summary>
    private sealed class Recording(List<string> asked, HttpStatusCode status, string body)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            asked.Add($"{request.Method} {request.RequestUri!.AbsolutePath}");

            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json"),
            });
        }
    }
}
