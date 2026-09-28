namespace Gg.Cli.Tests;

/// <summary>
/// A member writes the agent locator its credential carried into its own
/// configuration, so every later read finds it and `gg config show` can say so.
/// </summary>
/// <remarks>
/// <para>
/// <b>Written rather than held in a variable, for the reason the keys beside it
/// are.</b> A member opens <c>accept-configured</c> and <c>accept-agent-login</c>
/// into its own file at first start because <i>"a permission nobody can see is a
/// permission somebody forgot they granted, and a member is the machine nobody
/// can look inside"</i>. Where it reads its agent token is the same shape of
/// fact, and the settings system already resolves the environment then the file —
/// so writing it is what makes it survive the boot and show up on the page.
/// </para>
/// <para>
/// <b>First start is the only start.</b> A member is created warm and replaced,
/// never restarted, so the branch that spends the nonce runs exactly once per
/// member. Anything not written there is not written at all — the defect that
/// once left two members refusing configuration while their own
/// <c>gg config show</c> said they would accept it.
/// </para>
/// <para>
/// <b>And absence stays absence.</b> A member whose pool declares no locator must
/// go on deriving the local file, so nothing is written for it — a key grown onto
/// the file would be this side inventing an answer the tenant never gave.
/// </para>
/// </remarks>
public class AMemberKeepsWhereItWasToldToLookTests
{
    private const string AVaultReference = "keyvault://a-vault.example.invalid/agent-claude";

    [Test]
    public async Task What_the_credential_carried_is_what_the_member_keeps()
    {
        var written = LocalCredentialKeeper.Opened(existing: null, agentLocator: AVaultReference);

        await Assert.That(written.AgentLocator).IsEqualTo(AVaultReference)
            .Because("this is the only moment a member is told anything, and the settings system "
                   + "reads the file - so a locator not written here is one no later read finds.");
    }

    [Test]
    public async Task And_the_doors_it_already_opened_stay_open()
    {
        var written = LocalCredentialKeeper.Opened(existing: null, agentLocator: AVaultReference);

        await Assert.That(written.AcceptConfigured).IsTrue();
        await Assert.That(written.AcceptAgentLogin).IsTrue()
            .Because("a member reading its token from a vault still needs the ceremony's door: "
                   + "the vault may be empty on the day it comes up, and then a person logging "
                   + "it in from the console is the only way through.");
    }

    [Test]
    public async Task A_pool_that_declares_none_has_none_written()
    {
        var written = LocalCredentialKeeper.Opened(existing: null, agentLocator: null);

        await Assert.That(written.AgentLocator).IsNull()
            .Because("absent means the local file every machine derives, and writing a key for "
                   + "it would be this side answering a question the tenant did not.");
    }

    [Test]
    public async Task It_does_not_disturb_what_the_file_already_said()
    {
        var existing = new Gg.Local.Configuration { ControlPlane = "https://example.invalid/" };

        var written = LocalCredentialKeeper.Opened(existing, agentLocator: AVaultReference);

        await Assert.That(written.ControlPlane).IsEqualTo("https://example.invalid/")
            .Because("the control plane's address is read from this same file before this runs, "
                   + "so a write that dropped it would leave a member unable to say where it "
                   + "answers to.");
    }
}
