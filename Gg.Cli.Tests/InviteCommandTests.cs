using Gg.Cli;

namespace Gg.Cli.Tests;

/// <summary>
/// <c>gg invite</c> — the verb that makes a tenant able to have a second person
/// in it.
/// </summary>
/// <remarks>
/// <para>
/// <b>It takes no arguments, and that is the contract rather than an
/// omission.</b> An invitation names nobody: whoever opens the link becomes a
/// principal in the tenant the caller was in. A verb that took an address would
/// imply gg or the control plane delivers it, and neither does — the person who
/// ran it passes it on, by whatever means they already trust.
/// </para>
/// <para>
/// <b>And it takes no tenant.</b> There is no flag here for one and there must
/// never be: a caller may BE a tenant, never name one. That rule is enforced
/// server-side, but a flag on this surface would be the first sign somebody was
/// about to try.
/// </para>
/// </remarks>
public class InviteCommandTests
{
    [Test]
    public async Task Invite_is_a_verb_that_takes_nothing()
    {
        await Assert.That(CliArgs.Parse(["invite"])).IsTypeOf<CliAction.Invite>();
    }

    [Test]
    public async Task Invite_emits_a_result_either_way()
    {
        // It produces something a person needs to copy, so --json is meaningful
        // here in a way it is not for login. IEmitsResult is what says so.
        await Assert.That(((CliAction.Invite)CliArgs.Parse(["invite", "--json"])).Json).IsTrue();
        await Assert.That(((CliAction.Invite)CliArgs.Parse(["invite"])).Json).IsFalse();
        await Assert.That(CliArgs.Parse(["invite"])).IsAssignableTo<CliAction.IEmitsResult>();
    }

    [Test]
    public async Task Invite_is_advertised_because_it_works()
    {
        // A usage string is a promise. This one is being made in the same
        // commit the verb starts working, which is the only order that keeps
        // the promise true.
        var message = ((CliAction.Unknown)CliArgs.Parse(["frobnicate"])).Message;

        await Assert.That(message).Contains("gg invite");
    }

    [Test]
    public async Task No_flag_here_can_name_a_tenant_or_a_person()
    {
        // THE ABSENCE THAT MATTERS. A tenant flag would be a caller naming one
        // rather than being one; an email flag would be a promise to deliver
        // something nothing here delivers.
        var message = ((CliAction.Unknown)CliArgs.Parse(["frobnicate"])).Message;

        var invite = message.Split('\n').Single(l => l.Contains("gg invite", StringComparison.Ordinal));

        foreach (var flag in (string[])["--tenant", "--email", "--to", "--name"])
        {
            await Assert.That(invite).DoesNotContain(flag)
                .Because($"'{flag}' would make an invitation look addressed, and it is not - "
                       + "whoever opens the link joins.");
        }
    }
}
