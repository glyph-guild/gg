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

    /// <summary>Where this machine keeps the two files a slot is served from.</summary>
    /// <remarks>
    /// <b>Under the user's own cache, not a shared temp.</b> A credentials file
    /// in a world-writable directory is one another account can replace, and
    /// the point of writing it at all was to keep the secret out of somewhere
    /// anybody can read.
    /// </remarks>
    private static string Root => Path.Combine(
        Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData,
            Environment.SpecialFolderOption.DoNotVerify),
        "good-grief",
        "exposure");

    /// <summary>Writes a file nobody but this account can read.</summary>
    /// <remarks>
    /// The mode is applied to the handle before anything is written, so there
    /// is no window in which the secret exists at a wider permission. On
    /// Windows the call is a no-op and the file inherits the directory's ACL,
    /// which is why the directory is under the user's own profile.
    /// </remarks>
    private static async Task WriteOwnerOnlyAsync(
        string path, string contents, CancellationToken cancellationToken)
    {
        var options = new FileStreamOptions
        {
            Mode = FileMode.Create,
            Access = FileAccess.Write,
            Share = FileShare.None,
        };

        if (!OperatingSystem.IsWindows())
        {
            options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        }

        await using var file = new StreamWriter(new FileStream(path, options));
        await file.WriteAsync(contents.AsMemory(), cancellationToken);
    }

    public async Task<string?> RunAsync(
        string secret, string hostname, int? port, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secret);
        ArgumentException.ThrowIfNullOrWhiteSpace(hostname);

        var start = new ProcessStartInfo(Binary)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        start.ArgumentList.Add("tunnel");
        start.ArgumentList.Add("--no-autoupdate");
        start.ArgumentList.Add("run");

        if (port is { } served)
        {
            // LOCALLY CONFIGURED, because a provider-managed tunnel ignores what
            // the machine asks for. Measured on a live tunnel: --url was taken
            // at startup and replaced a second later by a configuration pushed
            // from the edge, and that tunnel went on to take three pushed
            // versions in half an hour - one of which removed its own hostname
            // rule and left it answering 404.
            if (TunnelFiles.CredentialsFrom(secret) is not { } credentials)
            {
                return "the credential for this slot is not a tunnel token, so no connector "
                     + "could be configured from it. Its value is not repeated here.";
            }

            string configPath;
            try
            {
                Directory.CreateDirectory(Root);
                var credentialsPath = Path.Combine(Root, $"{credentials.TunnelId}.json");
                configPath = Path.Combine(Root, $"{credentials.TunnelId}.yml");

                // THE SECRET LANDS OWNER-ONLY, and the mode is set BEFORE the
                // bytes: a file created world-readable and narrowed afterwards
                // is readable for however long that takes.
                await WriteOwnerOnlyAsync(credentialsPath, credentials.Json, cancellationToken);
                await File.WriteAllTextAsync(
                    configPath,
                    TunnelFiles.ConfigFor(credentials.TunnelId, credentialsPath, hostname, served),
                    cancellationToken);
            }
            catch (Exception unwritable) when (
                unwritable is IOException or UnauthorizedAccessException)
            {
                return $"this machine could not write the files a connector runs from: "
                     + unwritable.Message;
            }

            start.ArgumentList.Clear();
            foreach (var argument in TunnelFiles.ArgumentsFor(configPath))
            {
                start.ArgumentList.Add(argument);
            }
        }
        else
        {
            // NO PORT NAMED, so the provider's own ingress decides - which is
            // what an exposure document that names none is asking for, and what
            // every document written before the port existed says.
            //
            // THE TOKEN IS AN ARGUMENT HERE, and that is the cost of this path:
            // argv is world-readable through /proc. A document that names its
            // port does not pay it.
            start.ArgumentList.Add("--token");
            start.ArgumentList.Add(secret);
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
