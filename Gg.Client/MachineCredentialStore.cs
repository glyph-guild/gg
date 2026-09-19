using Gg.Local;

namespace Gg.Client;

/// <summary>
/// Every credential this machine can resolve: its own files, and the vaults its identity can read.
/// </summary>
/// <remarks>
/// <para>
/// <b>Routed by the reference's scheme, and by nothing else.</b> A
/// <c>keyvault://</c> reference goes to the vault; everything else goes to the
/// file store, which refuses what it does not recognise exactly as it did. So a
/// machine with no vault reference anywhere behaves byte for byte as before, and
/// asks the network nothing.
/// </para>
/// <para>
/// <b>A vault is read here and written nowhere.</b> Whoever owns the vault puts
/// the secret there; this machine's identity may read it. Writing or removing a
/// vault reference is refused, which also means <c>gg credential add</c> can never
/// quietly make a second, local copy of a secret that has a home.
/// </para>
/// </remarks>
public sealed class MachineCredentialStore(ICredentialStore local, KeyVaultCredentialSource vault)
    : ICredentialStore
{
    // ONE PER PROCESS, because a runner resolves a handful of references a
    // flight and a client per read is a socket per read.
    private static readonly HttpClient Shared = new() { Timeout = TimeSpan.FromSeconds(30) };

    private readonly ICredentialStore _local = local;
    private readonly KeyVaultCredentialSource _vault = vault;

    /// <summary>This machine's store, as a runner resolves through it.</summary>
    public static MachineCredentialStore ThisMachine() =>
        new(new FileCredentialStore(), new KeyVaultCredentialSource(Shared));

    /// <summary>
    /// The secret, or null - with a vault's refusal written to <paramref name="said"/>
    /// rather than thrown.
    /// </summary>
    /// <remarks>
    /// For the runner's <c>secretFor</c>, which answers a string or nothing and
    /// cannot carry a sentence. The sentence goes to the runner's own log, where
    /// the refusal a flight then records points a person.
    /// </remarks>
    public static string? SecretFor(string locator, TextWriter? said = null)
    {
        try
        {
            return ThisMachine().Read(locator);
        }
        catch (CredentialUnavailableException unavailable)
        {
            (said ?? Console.Error).WriteLine(unavailable.Message);
            return null;
        }
    }

    public string Root => _local.Root;

    public string Protection =>
        _local.Protection
      + $" A {KeyVaultReference.Scheme} reference is read from its vault by this machine's managed "
      + "identity, held in memory for the flight that needs it, and never written here.";

    public string PathFor(string locator) =>
        KeyVaultReference.Names(locator) ? throw NotHere(locator) : _local.PathFor(locator);

    public void Write(string locator, string secret)
    {
        if (KeyVaultReference.Names(locator))
        {
            throw NotHere(locator);
        }

        _local.Write(locator, secret);
    }

    public string? Read(string locator) =>
        KeyVaultReference.Names(locator) ? _vault.Read(locator) : _local.Read(locator);

    // NOT ASKED. Holds promises an answer without reading the secret, and the
    // only way to ask a vault whether it has one is to read it. A vault
    // reference is not on this machine, which is what false says.
    public bool Holds(string locator) =>
        !KeyVaultReference.Names(locator) && _local.Holds(locator);

    public bool Remove(string locator) =>
        KeyVaultReference.Names(locator) ? throw NotHere(locator) : _local.Remove(locator);

    private static ArgumentException NotHere(string locator) =>
        new($"'{locator}' lives in a vault, and this machine only reads it. Put or remove the "
          + "secret in the vault itself.", nameof(locator));
}
