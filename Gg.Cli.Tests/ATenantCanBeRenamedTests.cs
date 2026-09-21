using Gg.Cli;

namespace Gg.Cli.Tests;

/// <summary>
/// A tenant can be given a name, by somebody who administers it.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every real tenant is called "somebody's tenant".</b> The sign-up path
/// takes a name and nothing supplies one: the identity provider composes the
/// callback and would never append it, so the fallback - the first
/// administrator's display, possessive - is what every tenant in existence is
/// named. There has never been a way to change it.
/// </para>
/// <para>
/// <b>And it is the first thing this system ever writes to that row.</b>
/// `identity.gg_tenant` has had exactly one INSERT and no UPDATE at all. The
/// rule that tenant settings are written out of band covers a tenant's POLICY
/// - its classification ceiling, its rules - where a tenant raising its own
/// would be the attack. A name grants nothing, so it is not that.
/// </para>
/// </remarks>
public class ATenantCanBeRenamedTests
{
    private static CliAction? Parse(params string[] args) => CliArgs.Parse(args);

    [Test]
    public async Task Naming_a_tenant_is_a_verb()
    {
        await Assert.That(Parse("tenant", "name", "JDXpert"))
            .IsEqualTo(new CliAction.TenantName("JDXpert", Json: false));
    }

    [Test]
    public async Task The_name_is_required()
    {
        // NOT AN EMPTY NAME BY OMISSION. `gg tenant name` with nothing after it
        // reads as a question - "what is it called?" - and answering it by
        // erasing the name is the one thing it must not do.
        await Assert.That(Parse("tenant", "name")).IsTypeOf<CliAction.Unknown>();
    }

    [Test]
    public async Task The_usage_says_who_may_do_it()
    {
        // BECAUSE IT WILL BE REFUSED OTHERWISE, and a person who reads the line
        // and then gets a 403 learns the usage was not telling them everything.
        var usage = ((CliAction.Unknown)CliArgs.Parse(["nonsense"])).Message;

        await Assert.That(usage).Contains("gg tenant name");
        await Assert.That(usage).Contains("administer");
    }
}
