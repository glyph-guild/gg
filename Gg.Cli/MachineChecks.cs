using System.Net.Sockets;
using Gg.Client;

/// <summary>
/// The two questions a machine answers about its profile that only it can:
/// can this credential reference be resolved here, and does this forge answer
/// (slice forty-three, rule 25).
/// </summary>
/// <remarks>
/// <b>Each answer is a sentence about the reference or the host</b>, never
/// about anything resolving it produced: a resolved secret is dropped on the
/// spot, and the reading carries only whether there was one.
/// </remarks>
internal static class MachineChecks
{
    /// <summary>How long a forge has to answer before it reads as unreachable.</summary>
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(5);

    /// <summary>Null when this machine's own store resolves the reference, otherwise why not.</summary>
    /// <remarks>
    /// <b>Through the same store a flight resolves through</b> - this machine's
    /// file for a local: reference, its vault by its own identity for a
    /// keyvault:// one - so a reference that verifies here is one a flight
    /// will find. A vault's refusal is its sentence about the reference.
    /// </remarks>
    public static Task<string?> ResolveAsync(string reference, CancellationToken cancellationToken)
    {
        try
        {
            return Task.FromResult(MachineCredentialStore.ThisMachine().Read(reference) is null
                ? $"{reference} is not in this machine's credential store."
                : null);
        }
        catch (Exception refused) when (refused is ArgumentException or InvalidOperationException
                                                  or NotSupportedException or IOException
                                                  or UnauthorizedAccessException)
        {
            return Task.FromResult<string?>(
                $"{reference} cannot be read by this machine's credential store: {refused.Message}");
        }
    }

    /// <summary>Null when the forge's host accepts a connection on 443, otherwise why not.</summary>
    public static async Task<string?> ReachAsync(string host, CancellationToken cancellationToken)
    {
        using var client = new TcpClient();
        using var patience = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        patience.CancelAfter(Patience);

        try
        {
            await client.ConnectAsync(host, 443, patience.Token);
            return null;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return $"{host} did not answer on 443 within {Patience.TotalSeconds:0}s.";
        }
        catch (SocketException unreachable)
        {
            return $"{host} could not be reached on 443: {unreachable.SocketErrorCode}.";
        }
    }
}
