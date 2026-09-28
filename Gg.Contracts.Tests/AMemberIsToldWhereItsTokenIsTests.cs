namespace Gg.Contracts.Tests;

/// <summary>
/// A member learns where its agent credential is from the credential it
/// redeems, because nothing else can tell it.
/// </summary>
/// <remarks>
/// <para>
/// <b>The channel already exists and already carries a strategy's decisions.</b>
/// <c>MemberCredentialIssued</c> hands back the labels a member may advertise,
/// <i>"decided at mint … from the strategy in force when it was minted"</i>, so
/// that a laptop and a member stop being indistinguishable to the matcher. Where
/// that member reads its agent token is the same kind of fact, settled by the
/// same document, at the same moment.
/// </para>
/// <para>
/// <b>And it must not ride the container instead.</b> A member's environment is
/// readable through the scope proxy for the life of the container — which is why
/// the nonce that gets it here is single-use and worthless once spent. A NAME is
/// safe on that surface and a value is not, but the create body is three
/// variables asserted byte for byte precisely so nobody has to judge that case
/// by case. This keeps that assertion untouched.
/// </para>
/// <para>
/// <b>Nullable, because absence is today.</b> Every member minted before this
/// derives the local file it always did, and a member whose pool declares
/// nothing must go on doing so.
/// </para>
/// </remarks>
public class AMemberIsToldWhereItsTokenIsTests
{
    private const string AVaultReference = "keyvault://a-vault.example.invalid/agent-claude";

    private static MemberCredentialIssued Issued(string? locator) => new()
    {
        RunnerId = "01a0e547-cc8d-72a0-bca6-a9e42fb749b2",
        RunnerToken = "a-token",
        Labels = ["environment=ui"],
        ExpiresAt = new DateTimeOffset(2026, 9, 28, 12, 0, 0, TimeSpan.Zero),
        AgentLocator = locator,
    };

    [Test]
    public async Task The_issued_credential_carries_the_locator()
    {
        await Assert.That(Issued(AVaultReference).AgentLocator).IsEqualTo(AVaultReference)
            .Because("redemption is the one moment a member is told anything by the control "
                   + "plane, and this is the only channel that reaches it.");
    }

    [Test]
    public async Task And_absence_is_the_member_that_derives_its_own_file()
    {
        await Assert.That(Issued(null).AgentLocator).IsNull()
            .Because("every member minted before this had no locator and read the local file. "
                   + "Absent has to keep meaning exactly that.");
    }

    [Test]
    public async Task The_member_is_told_a_place_and_never_a_value()
    {
        // THE BOUNDARY, ASSERTED OVER THE SHAPE rather than intended - the same
        // way the slot's credential reference is. A member may be told where to
        // look; nothing here may hold what it finds.
        var members = typeof(MemberCredentialIssued)
            .GetProperties()
            .Select(p => p.Name)
            .ToList();

        await Assert.That(members).Contains("AgentLocator");
        await Assert.That(members.Where(n =>
                n.Contains("AgentToken", StringComparison.OrdinalIgnoreCase)
                || n.Contains("AgentSecret", StringComparison.OrdinalIgnoreCase)
                || n.Contains("AgentPassword", StringComparison.OrdinalIgnoreCase)))
            .IsEmpty()
            .Because("the control plane holds references rather than values, so there must be "
                   + "no member here an agent's token could ever arrive in.");
    }
}
