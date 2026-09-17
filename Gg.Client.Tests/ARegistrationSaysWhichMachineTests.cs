using System.Net;
using System.Text;
using Gg.Contracts;

namespace Gg.Client.Tests;

/// <summary>
/// Registering a runner tells the control plane which machine it is on.
/// </summary>
/// <remarks>
/// <b>Known at every call site and never sent.</b> The resident, the
/// maintainer and the attended runner all build their label from
/// <c>Environment.MachineName</c>, so the host was always in hand. This is the
/// call that carries it, and a caller with no machine to name sends none.
/// </remarks>
public class ARegistrationSaysWhichMachineTests
{
    private sealed class Capturing : HttpMessageHandler
    {
        internal string? SentBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Content is not null)
            {
                SentBody = await request.Content.ReadAsStringAsync(cancellationToken);
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"runnerId":"01a0a856-eac3-7671-9f0e-000000000000","runnerToken":"t","expiresAt":"2026-10-01T00:00:00Z"}""",
                    Encoding.UTF8, "application/json"),
            };
        }
    }

    private static ControlPlaneClient Client(Capturing handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://cp.invalid/") });

    [Test]
    public async Task The_machine_travels_with_the_registration()
    {
        var handler = new Capturing();

        _ = await Client(handler).RegisterRunnerAsync(
            "session", "vmlinux001:maintain", machine: "vmlinux001");

        await Assert.That(handler.SentBody).Contains("\"machine\":\"vmlinux001\"")
            .Because("the label says how the runner was started and this says where, so a "
                   + "maintainer groups with the machine it maintains. Sent: " + handler.SentBody);
    }

    [Test]
    public async Task No_machine_writes_no_key()
    {
        // An older caller, or one with nothing to name, sends the body the
        // control plane has always taken.
        var handler = new Capturing();

        _ = await Client(handler).RegisterRunnerAsync("session", "a-runner");

        await Assert.That(handler.SentBody).DoesNotContain("machine", StringComparison.Ordinal);
    }
}
