using Gg.Contracts;

namespace Gg.Runner;

/// <summary>What came of trying to resolve a reference.</summary>
/// <remarks>
/// Two cases and no third. There is deliberately no "resolved to nothing":
/// an empty secret is a secret that fetches nothing and fails much later, in
/// a place with no way back to here.
/// </remarks>
public abstract record CredentialResolution
{
    /// <summary>The secret. It stays on this machine.</summary>
    public sealed record Resolved(string Secret) : CredentialResolution;

    /// <summary>It could not be read, and this is why.</summary>
    public sealed record Unresolvable(string Problem) : CredentialResolution;
}

/// <summary>
/// Turns a reference into a secret, locally.
/// </summary>
/// <remarks>
/// <para>
/// The port is here rather than in the developer client because this assembly
/// cannot reference that one - a runner must be structurally unable to hold a
/// developer's session. The adapter that reads the local file store is
/// therefore assembled in <c>Gg.Cli</c>, which sees both.
/// </para>
/// <para>
/// One implementation of this interface ever holds a secret, and it hands it
/// back to a caller on this machine. Nothing here goes near the wire.
/// </para>
/// </remarks>
public interface ICredentialResolver
{
    Task<CredentialResolution> ResolveAsync(
        CredentialReference reference, CancellationToken cancellationToken = default);
}

/// <summary>
/// Resolves nothing, and says so rather than pretending.
/// </summary>
/// <remarks>
/// The default for a runner with no store wired up, and for every test whose
/// subject is not credentials. Article XI: it refuses loudly instead of
/// returning an empty secret, because a runner that resolved to "" would fail
/// at the provider with an authentication error nobody could trace back here.
/// </remarks>
public sealed class NoCredentialResolver : ICredentialResolver
{
    public Task<CredentialResolution> ResolveAsync(
        CredentialReference reference, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reference);

        return Task.FromResult<CredentialResolution>(new CredentialResolution.Unresolvable(
            $"this runner has no credential store configured, so '{reference.Locator}' cannot be read here"));
    }
}
