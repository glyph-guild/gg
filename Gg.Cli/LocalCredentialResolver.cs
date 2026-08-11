using Gg.Client;
using Gg.Contracts;
using Gg.Runner;

namespace Gg.Cli;

/// <summary>
/// The one adapter joining the runner's resolver port to the local store.
/// </summary>
/// <remarks>
/// <para>
/// It lives here and nowhere else because this is the only project that sees
/// both halves. <c>Gg.Runner</c> deliberately cannot reference
/// <c>Gg.Client</c> - a runner must be structurally unable to hold a
/// developer's session - and <c>Gg.Client</c> has no business knowing what a
/// runner is. So the two are joined at the top, by the binary that is already
/// both.
/// </para>
/// <para>
/// The runner process is a child of the same binary, so it gets this for free
/// and reads the same files <c>gg credential add</c> wrote. That is the whole
/// mechanism: the secret is written by a person on this machine and read by a
/// process on this machine, and the control plane holds the string in between.
/// </para>
/// </remarks>
public sealed class LocalCredentialResolver(ICredentialStore store) : ICredentialResolver
{
    private readonly ICredentialStore _store = store;

    public Task<CredentialResolution> ResolveAsync(
        CredentialReference reference, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reference);

        // A locator the store refuses is a diagnosis rather than a crash: by
        // the time a runner sees one it is data that came back from the control
        // plane, and a malformed one must produce a flight-log event naming it
        // instead of an unhandled exception halfway through a claim.
        string? secret;
        try
        {
            secret = _store.Read(reference.Locator);
        }
        catch (ArgumentException malformed)
        {
            return Task.FromResult<CredentialResolution>(
                new CredentialResolution.Unresolvable(malformed.Message));
        }

        return Task.FromResult<CredentialResolution>(secret switch
        {
            // Empty is refused as loudly as missing. An empty secret fetches
            // nothing and fails at the provider, which is a long way from here.
            null => new CredentialResolution.Unresolvable(
                $"no secret stored at {reference.Locator} on this machine. "
              + "Run gg credential add here, for this repository."),
            "" => new CredentialResolution.Unresolvable(
                $"the secret stored at {reference.Locator} is empty."),
            var value => new CredentialResolution.Resolved(value),
        });
    }
}
