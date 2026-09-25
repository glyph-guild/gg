using System.Text;
using System.Text.Json;

namespace Gg.Runner.Exposures;

/// <summary>A tunnel's credentials, derived from the token that names them.</summary>
public sealed record TunnelCredentials
{
    /// <summary>The tunnel's id, which is a UUID and not a secret.</summary>
    public required string TunnelId { get; init; }

    /// <summary>The credentials file's contents.</summary>
    public required string Json { get; init; }
}

/// <summary>
/// The two files a locally configured connector runs from, and the arguments
/// that point it at them.
/// </summary>
/// <remarks>
/// <para>
/// <b>Locally configured, because a provider-managed tunnel ignores what the
/// machine asks for.</b> Measured on a live tunnel: <c>--url</c> was accepted
/// at startup and replaced a second later by a configuration pushed from the
/// edge. That tunnel took three pushed versions in half an hour, one of which
/// removed its own hostname rule and left it answering 404, and none of them
/// came from the machine serving it.
/// </para>
/// <para>
/// <b>Pure on purpose.</b> Everything interesting here — what the credentials
/// are derived from, which port the ingress names, what a request for another
/// hostname meets — is decided by these two strings, and none of it should need
/// a tunnel or a filesystem to assert.
/// </para>
/// </remarks>
public static class TunnelFiles
{
    /// <summary>
    /// The credentials a token names, or null when it is not a token at all.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A token IS the credentials.</b> It is base64 JSON of exactly three
    /// values — <c>a</c>, <c>t</c> and <c>s</c> — which cloudflared reads from a
    /// credentials file as <c>AccountTag</c>, <c>TunnelID</c> and
    /// <c>TunnelSecret</c>. So a tenant who registered a token has registered
    /// everything, and nobody needs a <c>cert.pem</c> to run the tunnel.
    /// </para>
    /// <para>
    /// <b>Null rather than thrown, and the value is never repeated.</b> A
    /// machine whose slot credential is something else entirely serves no
    /// preview, and that diagnosis travels to a flight log — so it must not
    /// carry what it was handed.
    /// </para>
    /// </remarks>
    public static TunnelCredentials? CredentialsFrom(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        try
        {
            var padded = token.PadRight(token.Length + ((4 - (token.Length % 4)) % 4), '=');
            using var decoded = JsonDocument.Parse(Convert.FromBase64String(padded));
            var root = decoded.RootElement;

            if (root.ValueKind is not JsonValueKind.Object
                || !root.TryGetProperty("a", out var account)
                || !root.TryGetProperty("t", out var tunnel)
                || !root.TryGetProperty("s", out var secret))
            {
                return null;
            }

            return new TunnelCredentials
            {
                TunnelId = tunnel.GetString() ?? "",
                // WRITTEN, NOT SERIALIZED. Native AOT forbids the reflecting
                // overload, and a hand-rolled string would be this file
                // inventing JSON escaping for a value it must not corrupt.
                Json = Written(
                    account.GetString() ?? "",
                    tunnel.GetString() ?? "",
                    secret.GetString() ?? ""),
            };
        }
        catch (Exception malformed) when (
            malformed is FormatException or JsonException or InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary>The credentials file's bytes, escaped by the writer.</summary>
    private static string Written(string account, string tunnel, string secret)
    {
        using var buffer = new MemoryStream();
        using (var json = new Utf8JsonWriter(buffer))
        {
            json.WriteStartObject();
            json.WriteString("AccountTag", account);
            json.WriteString("TunnelID", tunnel);
            json.WriteString("TunnelSecret", secret);
            json.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    /// <summary>The config file a connector serves one slot from.</summary>
    /// <remarks>
    /// <b>The catch-all is last and is not optional.</b> Ingress is evaluated in
    /// order, so one above the hostname would answer everything and the slot
    /// would serve nothing — and cloudflared refuses a config whose final rule
    /// names a hostname. It is also what tells "routed to the wrong tunnel"
    /// apart from "the origin is down": the first answers 404, the second 502.
    /// </remarks>
    public static string ConfigFor(
        string tunnelId, string credentialsPath, string hostname, int port)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tunnelId);
        ArgumentException.ThrowIfNullOrWhiteSpace(credentialsPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(hostname);
        ArgumentOutOfRangeException.ThrowIfLessThan(port, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(port, 65535);

        var text = new StringBuilder();
        text.AppendLine("# Written by gg for one exposure slot. Do not edit: it is rewritten");
        text.AppendLine("# whenever this machine brings its slot up.");
        text.AppendLine($"tunnel: {tunnelId}");
        text.AppendLine($"credentials-file: {credentialsPath}");
        text.AppendLine("no-autoupdate: true");
        text.AppendLine("ingress:");
        text.AppendLine($"  - hostname: {hostname}");
        text.AppendLine($"    service: http://localhost:{port}");
        text.AppendLine("  - service: http_status:404");

        return text.ToString();
    }

    /// <summary>What the connector is run with, which is a path and nothing else.</summary>
    /// <remarks>
    /// <b>No secret among the arguments.</b> Measured: <c>--token</c> put the
    /// whole tunnel secret into argv, and <c>/proc/&lt;pid&gt;/cmdline</c> is
    /// world readable — so on a machine where the agent runs as another user it
    /// could simply read it.
    /// </remarks>
    public static IReadOnlyList<string> ArgumentsFor(string configPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(configPath);

        return ["--config", configPath, "tunnel", "run"];
    }
}
