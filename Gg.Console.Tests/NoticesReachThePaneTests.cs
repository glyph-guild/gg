using Gg.Client;
using Gg.Contracts;

namespace Gg.Console.Tests;

/// <summary>
/// A tenant degradation reaches the pane that renders it.
/// </summary>
/// <remarks>
/// <para>
/// <b>A renderer with no producer.</b> <c>PaneText.QueueRows</c> draws
/// <c>AppState.Notices</c> above the queue, present even when the queue is
/// empty, and <see cref="NoticeRowTests"/> asserts all of that - against states
/// the tests construct. Nothing in production ever assigned the field, so no
/// person has ever seen one.
/// </para>
/// <para>
/// <b>And the case it exists for is the case that hides it.</b> When check runs
/// stop being written, every flight still runs, still records its facts and
/// still leaves the queue. Nothing needs anybody. The console is at its most
/// reassuring exactly when this is worst.
/// </para>
/// <para>
/// <b>`whoami` is where they come from</b>, and it was the one read verb whose
/// answer the console could not have: <c>AuthCommands.WhoAmIAsync</c> prints to
/// a writer and returns an exit code, so there was no VALUE to project.
/// <c>IdentityCommands</c> is that value, and the command line now renders the
/// same one it does.
/// </para>
/// </remarks>
public class NoticesReachThePaneTests
{
    private static WhoAmI Who(params TenantNotice[] notices) => new()
    {
        PrincipalId = "p-1",
        PrincipalDisplay = "a-person",
        TenantId = "t-1",
        ExpiresAt = new DateTimeOffset(2099, 1, 1, 0, 0, 0, TimeSpan.Zero),
        Notices = notices,
    };

    private static TenantNotice Degraded() => new()
    {
        Code = TenantNoticeCodes.Egress,
        Detail = "Check runs cannot be written for 'glyph-guild'.",
        Remedy = "Reinstall it from the console.",
        Blocking = true,
    };

    [Test]
    public async Task The_projection_puts_a_notice_where_the_pane_reads_it()
    {
        // Rule 2: Apply is the only path from a verb result into the model, so
        // this is where the wire has to be, not in the boot.
        var state = ConsoleProjection.Apply(
            new AppState(), new VerbResult.Identity(Who(Degraded())));

        await Assert.That(state.Notices.Count).IsEqualTo(1);

        var rows = PaneText.QueueRows(state);

        await Assert.That(rows[0]).Contains("glyph-guild")
            .Because("the renderer and its tests were already here; what was missing was a "
                   + "verb result to draw.");
    }

    [Test]
    public async Task A_healthy_tenant_assigns_an_empty_list_rather_than_leaving_the_default()
    {
        // The twin, and it is not a formality: a field left at its default and a
        // field assigned empty are the same value and different facts, and only
        // one of them means the read happened.
        var state = ConsoleProjection.Apply(
            new AppState { Notices = [Degraded()] }, new VerbResult.Identity(Who()));

        await Assert.That(state.Notices).IsEmpty()
            .Because("a degradation that has been fixed has to leave the pane on the next "
                   + "read, and rule 4 says a refresh is what does it.");
    }

    [Test]
    public async Task The_identity_a_verb_returns_is_the_identity_the_command_line_prints()
    {
        // Parity, which is the whole reason this is a VerbResult and not a
        // second read: what a pane shows is what `--json` would print.
        var result = new VerbResult.Identity(Who(Degraded()));

        var back = VerbOutput.Parse(result.Kind, VerbOutput.ToJson(result));

        await Assert.That(back.Kind).IsEqualTo(VerbResultKinds.Identity);
        await Assert.That(((VerbResult.Identity)back).Value.Notices.Count).IsEqualTo(1);
        await Assert.That(VerbOutput.ToText(result)).Contains("a-person");
    }
}
