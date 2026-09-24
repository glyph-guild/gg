using System.Diagnostics;

namespace Gg.Runner.Exposures;

/// <summary>
/// Runs <c>cloudflared</c> for one slot, and leaves it running.
/// </summary>
/// <remarks>
/// <para>
/// <b>Outbound only, which is what makes a preview possible from a pool
/// member.</b> The connector dials the provider and reaches the served app over
/// this machine's own loopback, so nothing needs a published port, a firewall
/// rule, or anything from the Docker socket — a socket gg deliberately keeps
/// behind a proxy that refuses any reach outside the declared inventory.
/// </para>
/// <para>
/// <b>A credential and nothing else.</b> Which local service the slot reaches is
/// part of the tunnel's configuration at the provider, set by the tenant when
/// they bound the hostname. So this cannot point a slot at a different service
/// any more than it can rename one, and the most a stolen slot credential can
/// do is serve content at the one address it was minted for.
/// </para>
/// <para>
/// <b>It is left running on purpose.</b> The person who asked for the preview
/// opens the URL after the flight reaches its gate, so a connector that stopped
/// when the loop did would publish an address that answers nothing. It ends when
/// the member does, which is the same lifetime the grant has.
/// </para>
/// <para>
/// <b>Absence is reported rather than thrown.</b> A machine without the binary
/// is a machine that serves no preview, and that is a flight which still did its
/// work — so the diagnosis travels back and the fact is simply absent.
/// </para>
/// </remarks>
public sealed class CloudflaredConnector : IExposureConnector
{
    /// <summary>What the binary is called, when nothing says otherwise.</summary>
    private const string Binary = "cloudflared";

    public async Task<string?> RunAsync(
        string token, int? port, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);

        var start = new ProcessStartInfo(Binary)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        // THE TOKEN IS AN ARGUMENT AND NOT A SHELL STRING. ArgumentList quotes
        // each member itself, so nothing here is parsed by a shell that could
        // split or expand it.
        start.ArgumentList.Add("tunnel");
        start.ArgumentList.Add("--no-autoupdate");
        start.ArgumentList.Add("run");
        start.ArgumentList.Add("--token");
        start.ArgumentList.Add(token);

        // THE PORT THE TENANT'S DOCUMENT NAMED, when it named one. Measured on
        // a real tunnel: this overrides the ingress configured at the provider, which is
        // what lets the document say the number once instead of it living in a
        // provider's dashboard for every hostname.
        //
        // LOOPBACK, ALWAYS. The served app is on this machine; a host part the
        // runner could vary would let a slot reach something that is not the
        // flight's own work.
        if (port is { } served)
        {
            start.ArgumentList.Add("--url");
            start.ArgumentList.Add($"http://127.0.0.1:{served}");
        }

        Process? connector;
        try
        {
            connector = Process.Start(start);
        }
        catch (System.ComponentModel.Win32Exception missing)
        {
            return $"'{Binary}' could not be started on this machine: {missing.Message}. A "
                 + "member that cannot dial a connector serves no preview, and the flight is "
                 + "unaffected.";
        }

        if (connector is null)
        {
            return $"'{Binary}' started no process.";
        }

        // IT EXITING IS THE ONLY FAILURE THIS CAN SEE. A connector that is still
        // running has dialled or is retrying, and both look the same from here;
        // one that has already gone is a refusal with a reason worth carrying.
        //
        // The wait is bounded and does not block the flight: it either exits in
        // that window, which is what a bad token does, or it is up.
        try
        {
            using var grace = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            grace.CancelAfter(TimeSpan.FromSeconds(5));
            await connector.WaitForExitAsync(grace.Token);
        }
        catch (OperationCanceledException)
        {
            return null;
        }

        var said = await connector.StandardError.ReadToEndAsync(CancellationToken.None);

        return $"'{Binary}' exited {connector.ExitCode} rather than staying up"
             + (said is { Length: > 0 } ? $": {said[..Math.Min(said.Length, 400)]}" : ".");
    }
}
