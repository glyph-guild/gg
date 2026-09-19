namespace Gg.Client;

/// <summary>
/// Every credential this machine can resolve: its own files, and the vaults its identity can read.
/// </summary>
public sealed class MachineCredentialStore(ICredentialStore local, KeyVaultCredentialSource vault)
    : ICredentialStore
{
    private readonly ICredentialStore _local = local;
    private readonly KeyVaultCredentialSource _vault = vault;

    /// <summary>This machine's store, as a runner resolves through it.</summary>
    public static MachineCredentialStore ThisMachine() => throw new NotImplementedException();

    public string Root => _local.Root;

    public string Protection => _local.Protection;

    public string PathFor(string locator) => throw new NotImplementedException();

    public void Write(string locator, string secret) => throw new NotImplementedException();

    public string? Read(string locator) => throw new NotImplementedException($"{locator}: {_vault}");

    public bool Holds(string locator) => throw new NotImplementedException();

    public bool Remove(string locator) => throw new NotImplementedException();
}
