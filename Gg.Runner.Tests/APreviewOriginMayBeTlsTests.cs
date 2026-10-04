using Gg.Contracts;
using Gg.Runner.Exposures;

namespace Gg.Runner.Tests;

/// <summary>
/// An exposure may say its origin speaks TLS, and the connector then dials it.
/// </summary>
/// <remarks>
/// <para>
/// <b>Measured against a real application, 2026-10-04.</b> JDNext's web resource is
/// <c>WithHttpsEndpoint</c> and its dev server is configured <c>ssl: true</c> with an
/// explicit certificate — it does not answer plain HTTP at all. Its auth cookies carry
/// the <c>__Secure-</c> prefix, which a browser refuses to set on a non-HTTPS origin,
/// and Entra's OIDC redirect URIs are HTTPS. So "serve the preview over HTTP instead"
/// is not a configuration choice there; it is dismantling the application's auth.
/// </para>
/// <para>
/// <b>And gg wrote <c>http://</c> with no way to say otherwise.</b> `TunnelFiles` hard-coded
/// the scheme into the generated ingress, so a tunnel pointed at that stack handed
/// cloudflared a plaintext request against a TLS listener and the preview answered
/// nothing. The exposure document is where the port is already said — <i>"said once,
/// here, rather than in a provider's dashboard per hostname"</i> — so the scheme belongs
/// beside it rather than in a second place that can disagree.
/// </para>
/// <para>
/// <b>Absent means http, because that is what every existing document means.</b> The same
/// disposition <see cref="ExposureInventory.Port"/> already takes: null is a real answer
/// and the one every document gives today. A scheme nobody wrote cannot change what a
/// tunnel already does.
/// </para>
/// <para>
/// <b>And TLS here is always to a development certificate</b>, so the generated config has
/// to say <c>noTLSVerify</c> or cloudflared refuses the origin it was just told to use.
/// The certificate is the environment's own, on the machine's own loopback; verifying it
/// would be verifying a name the stack never claimed.
/// </para>
/// </remarks>
public class APreviewOriginMayBeTlsTests
{
    [Test]
    public async Task The_schemes_are_named_and_closed()
    {
        await Assert.That(OriginSchemes.All).IsEquivalentTo(new[]
        {
            OriginSchemes.Http,
            OriginSchemes.Https,
        });

        foreach (var written in (string[]) ["HTTPS", "tcp", "ws", "https://", ""])
        {
            var refusal = OriginSchemes.Validate(written);

            await Assert.That(refusal).IsNotNull()
                .Because($"'{written}' is not a scheme this version can dial, and a connector "
                       + "handed one it does not understand writes an ingress nothing answers.");

            await Assert.That(refusal!).Contains(OriginSchemes.Https)
                .Because("the refusal names what was expected, so a reader who guessed at the "
                       + "spelling can see the set rather than guess again.");
        }
    }

    [Test]
    public async Task Absent_is_http_so_every_document_that_exists_is_unchanged()
    {
        // THE WHOLE COMPATIBILITY CLAIM, and the reason it is a separate test: every
        // exposure in the field today says nothing about a scheme, and none of them may
        // change behaviour because this member arrived.
        await Assert.That(OriginSchemes.Validate(null)).IsNull();

        var config = TunnelFiles.ConfigFor(
            "a-tunnel", "/tmp/creds.json", "jdapp-3.goodgrief.dev", 8080, scheme: null);

        await Assert.That(config).Contains("service: http://localhost:8080");

        await Assert.That(config).DoesNotContain("noTLSVerify")
            .Because("an http origin has nothing to verify, and a block written anyway would "
                   + "be a difference in every existing machine's config file.");
    }

    [Test]
    public async Task An_https_origin_is_dialled_as_https_and_does_not_verify_a_dev_certificate()
    {
        var config = TunnelFiles.ConfigFor(
            "a-tunnel", "/tmp/creds.json", "jdapp-3.goodgrief.dev", 8080,
            scheme: OriginSchemes.Https);

        await Assert.That(config).Contains("service: https://localhost:8080")
            .Because("the stack this exists for does not answer plain HTTP, so the ingress has "
                   + "to name the scheme the listener actually speaks.");

        await Assert.That(config).Contains("noTLSVerify")
            .Because("the certificate is the environment's own development one on loopback; "
                   + "without this cloudflared refuses the origin it was just told to use.");

        // STILL ONE INGRESS AND STILL A CATCH-ALL. The shape of the file is not the
        // thing being changed, and a 404 arm that went missing would turn "no route"
        // into "the origin is down" - the distinction the file's own remarks keep.
        await Assert.That(config).Contains("service: http_status:404");
    }

    [Test]
    public async Task The_health_origin_follows_the_same_scheme_as_the_ingress()
    {
        // BECAUSE IT IS THE SAME LISTENER. `ExposureServed.Origin` is what gg dials to
        // ask "is the preview serving"; pointed at http while the ingress says https, it
        // would report a stack down that is serving perfectly, and the diagnosis would
        // send somebody to look at the wrong end.
        await Assert.That(ExposurePort.OriginFor(8080, OriginSchemes.Https))
            .IsEqualTo("https://localhost:8080");

        await Assert.That(ExposurePort.OriginFor(8080, scheme: null))
            .IsEqualTo("http://localhost:8080");
    }
}
