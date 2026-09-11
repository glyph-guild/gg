using Gg.Cli;
using Gg.Client;
using Gg.Contracts;

namespace Gg.Cli.Tests;

/// <summary>
/// Making somebody an administrator, from a terminal.
/// </summary>
/// <remarks>
/// <para>
/// <b>The bit had no front door.</b> <c>WhoAmI.IsAdmin</c> is declared, the
/// control plane derives it, and the console gates the fleet pane on it — and
/// nothing anywhere ever set it. A privilege distinction that no caller can
/// turn on is a gate holding a door nobody built, which is the
/// assembled-and-discarded shape at the level of a whole feature.
/// </para>
/// <para>
/// <b>Two words rather than a flag.</b> <c>--revoke</c> would make granting
/// the default of a verb whose other half takes privilege away, and the
/// shorter spelling would be the more dangerous one. <c>grant</c> and
/// <c>revoke</c> are equals and neither is assumed.
/// </para>
/// <para>
/// <b>It answers with whoami, not with an echo.</b> The useful confirmation
/// is what the control plane now says about the caller — and for the first
/// grant in a tenant, which a person makes to THEMSELVES, it is the only way
/// to see that it took.
/// </para>
/// </remarks>
public class AnAdminIsGrantedByAVerbTests
{
    [Test]
    public async Task Granting_and_revoking_are_two_words_and_neither_is_the_default()
    {
        var granted = CliArgs.Parse(["admin", "grant", "p-7"]);
        await Assert.That(granted).IsTypeOf<CliAction.Admin>();
        await Assert.That(((CliAction.Admin)granted).PrincipalId).IsEqualTo("p-7");
        await Assert.That(((CliAction.Admin)granted).Granted).IsTrue();

        var revoked = CliArgs.Parse(["admin", "revoke", "p-7"]);
        await Assert.That(((CliAction.Admin)revoked).Granted).IsFalse();

        var json = CliArgs.Parse(["admin", "grant", "p-7", "--json"]);
        await Assert.That(((CliAction.Admin)json).Json).IsTrue();
    }

    [Test]
    public async Task A_bare_admin_names_both_words_rather_than_choosing_one()
    {
        var refused = CliArgs.Parse(["admin"]);

        var message = ((CliAction.Unknown)refused).Message;

        await Assert.That(message).Contains("grant", StringComparison.Ordinal);
        await Assert.That(message).Contains("revoke", StringComparison.Ordinal)
            .Because("a verb that governs privilege may not guess which half was meant.");
        await Assert.That(message).Contains("gg whoami", StringComparison.Ordinal)
            .Because("the argument is a principal id, and whoami is the only place a "
                   + "person can read one.");
    }

    [Test]
    public async Task It_is_on_the_usage_page()
    {
        var refused = CliArgs.Parse(["definitely-not-a-verb"]);

        await Assert.That(((CliAction.Unknown)refused).Message)
            .Contains("gg admin", StringComparison.Ordinal);
    }

    [Test]
    public async Task Whoami_says_whether_this_person_administers_the_tenant()
    {
        var who = new WhoAmI
        {
            PrincipalId = "p-7",
            PrincipalDisplay = "kdee",
            TenantId = "t-1",
            ExpiresAt = DateTimeOffset.UnixEpoch,
        };

        var ordinary = VerbOutput.ToText(new VerbResult.Identity(who));

        await Assert.That(ordinary).DoesNotContain("Administers", StringComparison.Ordinal)
            .Because("most people are not administrators, and a line saying so on every "
                   + "whoami would teach a reader to skip the one that matters.");

        var admin = VerbOutput.ToText(new VerbResult.Identity(who with { IsAdmin = true }));

        await Assert.That(admin).Contains("Administers", StringComparison.Ordinal)
            .Because("the contract has carried this bit since the allowance slice and no "
                   + "text surface rendered it, so a grant could not be confirmed by the "
                   + "one verb whose job is answering who you are.");
    }
}
