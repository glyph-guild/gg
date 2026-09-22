using System.Net;
using Gg.Client;

namespace Gg.Client.Tests;

/// <summary>
/// Asking what version is current fails to an absence, every way it can fail.
/// </summary>
/// <remarks>
/// <para>
/// <b>The sentence was there and the code was not.</b> This method has always
/// said it "returns null for every way this can fail, on purpose" and caught
/// two exception types. A body it could not read threw out of it and took the
/// process down with a stack trace - found by pointing gg at something that
/// answered 200 with a different shape.
/// </para>
/// <para>
/// <b>It matters more than when that was written.</b> This answer no longer
/// only prints: <c>gg update</c> acts on it. The one input deciding whether a
/// machine replaces its own binary must not be able to kill the verb by being
/// malformed - and "malformed" includes a control plane one version ahead, a
/// proxy returning an error page, and anything impersonating either.
/// </para>
/// </remarks>
public class TheVersionOracleFailsToNothingTests
{
    private static ControlPlaneClient Answering(HttpStatusCode status, string body) =>
        new(new HttpClient(new Canned(status, body))
        {
            BaseAddress = new Uri("http://control.test/"),
        });

    private sealed class Canned(HttpStatusCode status, string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json"),
            });
    }

    [Test]
    public async Task A_body_of_the_wrong_shape_is_an_absence()
    {
        var asked = Answering(HttpStatusCode.OK, """{"current":"0.50.0"}""");

        await Assert.That(await asked.CurrentVersionAsync()).IsNull()
            .Because("this is the one that escaped: a 200 whose body does not carry the "
                   + "member this reads threw a JsonException out of the verb.");
    }

    [Test]
    public async Task A_body_that_is_not_json_at_all_is_an_absence()
    {
        var asked = Answering(HttpStatusCode.OK, "<html>a proxy said no</html>");

        await Assert.That(await asked.CurrentVersionAsync()).IsNull()
            .Because("a captive portal or an error page answering 200 is the ordinary way a "
                   + "machine on somebody else's network sees this.");
    }

    [Test]
    public async Task An_empty_body_is_an_absence()
    {
        await Assert.That(await Answering(HttpStatusCode.OK, "").CurrentVersionAsync()).IsNull();
    }

    [Test]
    public async Task A_refusal_is_an_absence()
    {
        var asked = Answering(HttpStatusCode.InternalServerError, "nope");

        await Assert.That(await asked.CurrentVersionAsync()).IsNull();
    }

    [Test]
    public async Task A_version_that_is_there_comes_back()
    {
        var asked = Answering(HttpStatusCode.OK, """{"version":"0.50.0"}""");

        await Assert.That(await asked.CurrentVersionAsync()).IsEqualTo("0.50.0")
            .Because("a guard that swallowed the good answer too would be worse than the "
                   + "crash it replaced.");
    }
}
