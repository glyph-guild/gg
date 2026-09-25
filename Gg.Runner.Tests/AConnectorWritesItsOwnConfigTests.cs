using Gg.Runner.Exposures;

namespace Gg.Runner.Tests;

/// <summary>
/// A connector derives its credentials from the tunnel token and names the port
/// itself, rather than taking an origin from the provider.
/// </summary>
/// <remarks>
/// <para>
/// <b>Measured on a live tunnel, 2026-09-25, after two wrong answers.</b>
/// <c>--url</c> does NOT override a provider-managed tunnel: cloudflared
/// accepted it at startup and one second later logged
/// <i>"Updated to new configuration"</i> and replaced it with the edge's
/// origin. Over half an hour that tunnel took three pushed versions, one of
/// which silently removed its own hostname rule and left it serving 404. None
/// of them came from the machine.
/// </para>
/// <para>
/// <b>A locally configured tunnel took none.</b> Same host, same minute, a
/// second slot run from a config file logged no configuration push at all and
/// served the port that file named. So the port belongs where the app is, and
/// this is the honest way to put it there.
/// </para>
/// <para>
/// <b>The token is enough, and no <c>cert.pem</c> is needed to run one.</b> A
/// tunnel token is base64 JSON of exactly three values — <c>a</c>, <c>t</c> and
/// <c>s</c> — which are the account tag, tunnel id and tunnel secret a
/// credentials file holds under longer names. Binding a HOSTNAME to a tunnel
/// still needs a person once, and that is the only part that does.
/// </para>
/// <para>
/// <b>And it takes the secret out of the process list.</b> Measured on the same
/// host: <c>--token</c> puts 184 characters of tunnel secret in argv, and
/// <c>/proc/&lt;pid&gt;/cmdline</c> is world readable.
/// </para>
/// </remarks>
public class AConnectorWritesItsOwnConfigTests
{
    /// <summary>A token shaped as cloudflared writes them, carrying no real secret.</summary>
    private static string AToken(
        string tunnel = "54fa15cc-eb1b-4a28-957b-231cdc069881",
        string account = "an-account-tag",
        string secret = "bm90LWEtcmVhbC10dW5uZWwtc2VjcmV0LXZhbHVl")
    {
        var json = $$"""{"a":"{{account}}","t":"{{tunnel}}","s":"{{secret}}"}""";
        return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(json));
    }

    [Test]
    public async Task The_credentials_are_derived_from_the_token()
    {
        var derived = TunnelFiles.CredentialsFrom(AToken());

        await Assert.That(derived).IsNotNull();
        await Assert.That(derived!.TunnelId).IsEqualTo("54fa15cc-eb1b-4a28-957b-231cdc069881");
        await Assert.That(derived.Json).Contains("\"AccountTag\"")
            .Because("cloudflared reads a credentials file by those names, and a token holds "
                   + "the same three values under 'a', 't' and 's'.");
        await Assert.That(derived.Json).Contains("\"TunnelSecret\"");
    }

    [Test]
    public async Task A_token_that_is_not_one_is_refused_without_repeating_it()
    {
        // NOT THROWN, AND NOT ECHOED. A machine whose slot credential is
        // something else entirely serves no preview, and the diagnosis travels
        // to a flight log - so it must not carry what it was handed.
        var refused = TunnelFiles.CredentialsFrom("this-is-not-a-tunnel-token");

        await Assert.That(refused).IsNull();
    }

    [Test]
    public async Task The_config_names_the_hostname_and_the_port()
    {
        var config = TunnelFiles.ConfigFor(
            "54fa15cc-eb1b-4a28-957b-231cdc069881",
            "/run/gg/jdapp-03.json",
            "jdapp-03.goodgrief.dev",
            8080);

        await Assert.That(config).Contains("tunnel: 54fa15cc-eb1b-4a28-957b-231cdc069881");
        await Assert.That(config).Contains("credentials-file: /run/gg/jdapp-03.json");
        await Assert.That(config).Contains("hostname: jdapp-03.goodgrief.dev");
        await Assert.That(config).Contains("service: http://localhost:8080");
    }

    [Test]
    public async Task It_ends_in_a_catch_all_so_another_hostname_reaches_nothing()
    {
        // WHAT A 404 MEANT IN THE WALK. A request for a hostname this slot does
        // not serve fell to the catch-all, which is how we told "routed to the
        // wrong tunnel" apart from "origin is down". Without it cloudflared
        // refuses the config outright.
        var config = TunnelFiles.ConfigFor(
            "54fa15cc-eb1b-4a28-957b-231cdc069881", "/run/gg/x.json", "jdapp-03.example.dev", 8080);

        await Assert.That(config).Contains("service: http_status:404");
        await Assert.That(config.LastIndexOf("http_status:404", StringComparison.Ordinal))
            .IsGreaterThan(config.IndexOf("hostname:", StringComparison.Ordinal))
            .Because("ingress is evaluated in order, so a catch-all above the hostname would "
                   + "answer everything and the slot would serve nothing.");
    }

    [Test]
    public async Task The_secret_is_never_an_argument()
    {
        // MEASURED: --token put 184 characters of tunnel secret into argv, and
        // /proc/<pid>/cmdline is world readable. On a resident runner the agent
        // runs as another user and could read it there.
        var arguments = TunnelFiles.ArgumentsFor("/run/gg/jdapp-03.yml");

        await Assert.That(arguments).DoesNotContain("--token");
        await Assert.That(arguments).Contains("--config");
        await Assert.That(arguments).Contains("/run/gg/jdapp-03.yml");
    }
}
