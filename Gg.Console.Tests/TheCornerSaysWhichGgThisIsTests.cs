using Gg.Contracts;

namespace Gg.Console.Tests;

/// <summary>
/// The badge in the corner: which gg this is, and whether a newer one exists.
/// </summary>
/// <remarks>
/// <para>
/// <b>The control plane already knows.</b> It holds
/// <c>Gg:CurrentVersion</c> and sends a <c>binary</c> notice when a caller is
/// behind - "one shape, three renderers" as that type puts it, and this is a
/// fourth. Nothing here compares versions itself: a console that decided for
/// itself what current means would be a second answer to a question the
/// control plane already answers, and the two would disagree the first time
/// somebody moved one.
/// </para>
/// <para>
/// <b>Advisory, and the contract fixes that.</b> <c>binary</c> is on
/// <c>TenantNoticeCodes.AdvisoryOnly</c>: being behind is reported and may
/// never stop somebody working. So the corner says so and does nothing else -
/// no modal, no refusal, nothing to dismiss.
/// </para>
/// </remarks>
public class TheCornerSaysWhichGgThisIsTests
{
    private static AppState Told(params TenantNotice[] notices) =>
        new() { Notices = notices };

    private static TenantNotice Behind() => new()
    {
        Code = TenantNoticeCodes.Binary,
        Detail = "this gg is 0.48.0 and 0.49.0 is current",
        Remedy = "gg update",
        Blocking = false,
    };

    private static TenantNotice SomethingElse() => new()
    {
        Code = TenantNoticeCodes.Egress,
        Detail = "a runner cannot reach the forge",
        Blocking = false,
    };

    [Test]
    public async Task A_console_nobody_has_told_says_nothing_about_updates()
    {
        await Assert.That(Corner.UpdateWaiting(Told())).IsFalse()
            .Because("no notice is the ordinary state, and a corner that hinted at an update "
                   + "on every console would be a corner nobody reads.");
    }

    [Test]
    public async Task A_behind_notice_is_what_lights_it()
    {
        await Assert.That(Corner.UpdateWaiting(Told(Behind()))).IsTrue();
    }

    [Test]
    public async Task Another_kind_of_notice_does_not()
    {
        await Assert.That(Corner.UpdateWaiting(Told(SomethingElse()))).IsFalse()
            .Because("egress is a fault about reaching a forge, and a version badge that lit "
                   + "up for it would be telling somebody to update over something updating "
                   + "cannot fix.");
    }

    [Test]
    public async Task It_is_found_among_others()
    {
        await Assert.That(Corner.UpdateWaiting(Told(SomethingElse(), Behind()))).IsTrue()
            .Because("notices arrive as a list and nothing promises an order.");
    }

    // ---- what the badge reads ----

    [Test]
    public async Task The_badge_carries_the_version_and_the_short_commit()
    {
        await Assert.That(Corner.Badge("0.49.0+84dba4a3ec687db3d1ec59a41c8596bf2b674c0b", false))
            .IsEqualTo(" gg 0.49.0+84dba4a3 ")
            .Because("eight characters is enough to find the commit and forty is noise in a "
                   + "corner.");
    }

    [Test]
    public async Task A_version_with_no_commit_is_left_alone()
    {
        await Assert.That(Corner.Badge("0.49.0", false)).IsEqualTo(" gg 0.49.0 ")
            .Because("a build with no source revision is a real case - the fallback when the "
                   + "attribute is missing at all - and a trailing `+` would be a wart.");
    }

    [Test]
    public async Task An_update_waiting_says_so_in_the_badge()
    {
        await Assert.That(Corner.Badge("0.48.0+50b2518c", true))
            .IsEqualTo(" gg 0.48.0+50b2518c · update ")
            .Because("the corner is where somebody looks to see which gg this is, so it is "
                   + "where they should learn there is a newer one - and the word rather than "
                   + "a symbol, because a symbol in a corner is a puzzle.");
    }
}
