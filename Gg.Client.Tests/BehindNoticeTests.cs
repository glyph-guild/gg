using Gg.Contracts;

namespace Gg.Client.Tests;

/// <summary>
/// Being behind, on the channel a tenant already watches.
/// </summary>
/// <remarks>
/// <para>
/// <b>Three renderers, one shape, written once on the control plane.</b>
/// <c>gg doctor</c> turns a notice into a check, the console puts it on the
/// queue row, and the tenant page shows it as a banner — so a fourth signal for
/// "there is a newer gg" would be a fourth place for the same fact to be worded
/// differently. This is the third signal joining an existing channel rather
/// than opening one.
/// </para>
/// <para>
/// <b>The control plane is the only party that can raise it.</b> It knows what
/// is current, and it has been receiving <c>GG-Runner-Version</c> on every
/// request since the header existed — so it can already see a fleet drifting
/// and has never been asked. The comparison belongs where both halves are.
/// </para>
/// <para>
/// <b>And it is advisory, always.</b> <c>Blocking</c> is the control plane's to
/// decide and a reader may never promote it — but rule 6 settles what the
/// answer is here: being behind is reported, and the 426 stays the only thing
/// that refuses.
/// </para>
/// </remarks>
public class BehindNoticeTests
{
    [Test]
    public async Task There_is_a_code_for_being_behind()
    {
        // Without one, the control plane has to invent a string and the three
        // renderers each decide what it means - which is the drift the closed
        // list exists to prevent.
        await Assert.That(TenantNoticeCodes.All).Contains(TenantNoticeCodes.Binary);
    }

    [Test]
    public async Task The_code_says_nothing_about_which_forge_or_feed()
    {
        // The same rule the existing codes are held to. It is doubly relevant
        // here: this notice is ABOUT a package on a feed, so naming the feed is
        // the obvious thing to do and would put a channel's name in a binary
        // that is meant to outlive the channel.
        foreach (var named in (string[])["nuget", "git" + "hub", "dotnet"])
        {
            await Assert.That(
                TenantNoticeCodes.Binary.Contains(named, StringComparison.OrdinalIgnoreCase))
                .IsFalse()
                .Because($"'{TenantNoticeCodes.Binary}' names {named}, and how gg is distributed "
                       + "is not what the code is about - it is about this binary being old.");
        }
    }

    [Test]
    public async Task A_behind_notice_never_blocks()
    {
        // Rule 6, checked where a renderer would read it. The control plane
        // decides Blocking and a reader may not promote it; what this asserts
        // is that gg's own understanding of the notice is advisory, so a
        // control plane that ever sent Blocking=true on this code would be
        // contradicting the client rather than configuring it.
        var notice = new TenantNotice
        {
            Code = TenantNoticeCodes.Binary,
            Detail = "this gg is 0.3.0 and 0.4.0 is current",
            Remedy = "gg update",
            Blocking = false,
        };

        await Assert.That(BehindNotice.IsAdvisoryOnly(notice.Code)).IsTrue()
            .Because("being behind is reported, never blocking - the protocol floor already "
                   + "refuses with a 426 and that stays the only thing that does.");
    }
}
