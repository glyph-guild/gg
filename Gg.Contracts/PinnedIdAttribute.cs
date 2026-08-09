namespace Gg.Contracts;

/// <summary>
/// Pins a contract type's wire identity to a GUID so renames never change
/// what crosses the boundary. Ours on purpose: the public artifact a customer
/// audits must not borrow identity from anyone else's framework.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
public sealed class PinnedIdAttribute : Attribute
{
    public PinnedIdAttribute(string id) => Id = Guid.Parse(id);

    public Guid Id { get; }
}
