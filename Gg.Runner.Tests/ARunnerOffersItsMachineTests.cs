using System.Net;

namespace Gg.Runner.Tests;

/// <summary>
/// A runner tells the control plane which machine it is on, and an older
/// control plane that does not know the route is not a fault.
/// </summary>
/// <remarks>
/// <b>The key offer's shape, one fact over.</b> A runner reusing a stored
/// credential never registers again, so this is how a long-lived resident or
/// maintainer gets a machine at all.
/// </remarks>
public class ARunnerOffersItsMachineTests
{
    private sealed class Answering(HttpStatusCode status) : HttpMessageHandler
    {
        internal HttpRequestMessage? Seen { get; private set; }

        internal string? SentBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Seen = request;
            if (request.Content is not null)
            {
                SentBody = await request.Content.ReadAsStringAsync(cancellationToken);
            }

            return new HttpResponseMessage(status);
        }
    }

    private static RunnerProtocolClient Client(Answering handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://cp.invalid/") }, "token");

    [Test]
    public async Task The_offer_names_the_machine_and_no_runner()
    {
        var handler = new Answering(HttpStatusCode.NoContent);

        var said = await Client(handler).OfferMachineAsync("vmlinux001");

        await Assert.That(said).IsTrue();
        await Assert.That(handler.Seen!.RequestUri!.AbsolutePath).IsEqualTo("/v1/runner/machine")
            .Because("the credential names the runner, so the path carries no id.");
        await Assert.That(handler.SentBody).Contains("\"machine\":\"vmlinux001\"");
    }

    [Test]
    public async Task A_control_plane_that_does_not_serve_the_route_is_not_a_fault()
    {
        // THE TWO REPOSITORIES ARE NOT UPGRADED IN STEP. A runner that went
        // down because an older control plane answered 404 here would be a
        // runner killed by a display grouping.
        var handler = new Answering(HttpStatusCode.NotFound);

        var said = await Client(handler).OfferMachineAsync("vmlinux001");

        await Assert.That(said).IsFalse()
            .Because("unheard is a result, not an exception - the runner still takes work.");
    }
}
