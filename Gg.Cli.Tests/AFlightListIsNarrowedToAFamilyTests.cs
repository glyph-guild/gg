using System.Net;
using Gg.Client;

namespace Gg.Cli.Tests;

/// <summary>
/// <c>gg flights --kind &lt;name&gt;</c> asks for a kind's family, alone or with
/// <c>--intent</c> in either order, and refuses a missing or malformed name before asking
/// (S80.2-01, ADR-0041 Decision 7).
/// </summary>
public class AFlightListIsNarrowedToAFamilyTests
{
    private sealed class Recording : HttpMessageHandler
    {
        public Uri? Asked { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Asked = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"flights":[]}""", System.Text.Encoding.UTF8, "application/json"),
                RequestMessage = request,
            });
        }
    }

    [Test]
    public async Task A_kind_is_asked_for_alone_or_beside_an_intent_in_either_order()
    {
        var alone = (CliAction.Flights)CliArgs.Parse(["flights", "--kind", "investigate"]);
        await Assert.That(alone.Kind).IsEqualTo("investigate");
        await Assert.That(alone.Intent).IsNull();

        foreach (var args in new[]
        {
            new[] { "flights", "--intent", "ado#18492", "--kind", "investigate" },
            new[] { "flights", "--kind", "investigate", "--intent", "ado#18492" },
        })
        {
            var both = CliArgs.Parse(args);
            await Assert.That(both).IsTypeOf<CliAction.Flights>().Because(string.Join(' ', args));
            await Assert.That(((CliAction.Flights)both).Kind).IsEqualTo("investigate");
            await Assert.That(((CliAction.Flights)both).Intent).IsEqualTo("ado#18492");
        }
    }

    [Test]
    public async Task A_missing_or_malformed_name_is_refused_before_anything_is_asked()
    {
        await Assert.That(CliArgs.Parse(["flights", "--kind"])).IsTypeOf<CliAction.Unknown>();

        var malformed = CliArgs.Parse(["flights", "--kind", "Not A Name"]);
        await Assert.That(malformed).IsTypeOf<CliAction.Unknown>();
        await Assert.That(((CliAction.Unknown)malformed).Message).Contains("Not A Name");
    }

    [Test]
    public async Task The_client_sends_the_family_it_was_asked_for()
    {
        var handler = new Recording();
        var client = new ControlPlaneClient(new HttpClient(handler) { BaseAddress = new Uri("http://cp.test/") });

        await client.ListFlightsAsync("token", kind: "investigate");

        await Assert.That(handler.Asked!.Query).Contains("kind=investigate");
    }
}
