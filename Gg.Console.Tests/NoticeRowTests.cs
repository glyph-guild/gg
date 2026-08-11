using Gg.Console;
using Gg.Contracts;

namespace Gg.Console.Tests;

/// <summary>
/// A degradation the queue would otherwise hide.
/// </summary>
/// <remarks>
/// <para>
/// The queue answers one question - what needs me - and it answers it
/// honestly right up until the moment something stops reporting. When the app
/// that writes check runs is gone, every flight still runs, still records its
/// facts and still leaves the queue; nothing needs anybody, and a pull request
/// somewhere silently has no check on it.
/// </para>
/// <para>
/// So the notice goes ABOVE the rows, in the pane a person is already looking
/// at, and it is present even when the queue is empty. An empty queue is
/// exactly the state this failure produces.
/// </para>
/// </remarks>
public class NoticeRowTests
{
    private static TenantNotice ANotice(string detail = "Check runs cannot be written for 'glyph-guild'.") =>
        new()
        {
            Code = TenantNoticeCodes.Egress,
            Detail = detail,
            Remedy = "Reinstall it from the console.",
            Blocking = true,
        };

    [Test]
    public async Task A_notice_appears_above_the_queue()
    {
        var rows = PaneText.QueueRows(new AppState { Notices = [ANotice()] });

        await Assert.That(rows[0]).Contains("glyph-guild");
    }

    [Test]
    public async Task It_appears_even_when_nothing_needs_me()
    {
        // The state this failure actually produces. A notice shown only
        // alongside rows would be invisible in exactly the case it exists for.
        var rows = PaneText.QueueRows(new AppState { Notices = [ANotice()] });

        await Assert.That(rows.Any(r => r.Contains("nothing needs you", StringComparison.Ordinal))).IsTrue();
        await Assert.That(rows.Count).IsEqualTo(2);
    }

    [Test]
    public async Task A_healthy_tenant_sees_no_extra_row()
    {
        // The twin. A permanent line at the top of the queue is a line people
        // stop reading, which would cost this one its only job.
        var rows = PaneText.QueueRows(new AppState());

        await Assert.That(rows).IsEquivalentTo((string[])["nothing needs you"]);
    }

    [Test]
    public async Task The_remedy_is_shown_and_not_only_the_complaint()
    {
        // A console that says something is broken and not what to do about it
        // sends somebody to a support channel to be told a sentence we already
        // had.
        var rows = PaneText.QueueRows(new AppState { Notices = [ANotice()] });

        await Assert.That(rows[0]).Contains("Reinstall it from the console.");
    }

    [Test]
    public async Task A_notice_with_no_remedy_renders_without_pretending_there_is_one()
    {
        var rows = PaneText.QueueRows(new AppState { Notices = [ANotice() with { Remedy = null }] });

        await Assert.That(rows[0]).Contains("glyph-guild");
        await Assert.That(rows[0].TrimEnd()).DoesNotEndWith("-")
            .Because("a trailing separator with nothing after it reads as text that got cut off.");
    }

    [Test]
    public async Task Control_sequences_in_a_notice_never_reach_the_terminal()
    {
        // The rule this whole file inherits: everything leaving PaneText is
        // stripped, because this is the last code between a control plane and
        // a terminal.
        var rows = PaneText.QueueRows(new AppState
        {
            Notices = [ANotice(detail: "clean\u001b[31mred\u001b[0m")],
        });

        await Assert.That(rows[0]).DoesNotContain("\u001b");
        await Assert.That(rows[0]).Contains("red")
            .Because("stripped rather than dropped - if the text vanished, the absence above would "
                   + "also pass on a row that silently discarded the notice.");
    }

    [Test]
    public async Task Every_notice_gets_a_row()
    {
        // A tenant can have more than one account connected, and naming only
        // the first would leave the second broken and unmentioned.
        var rows = PaneText.QueueRows(new AppState
        {
            Notices = [ANotice(detail: "first account"), ANotice(detail: "second account")],
        });

        await Assert.That(rows.Any(r => r.Contains("first account", StringComparison.Ordinal))).IsTrue();
        await Assert.That(rows.Any(r => r.Contains("second account", StringComparison.Ordinal))).IsTrue();
    }

    [Test]
    public async Task A_notice_survives_the_state_being_written_and_read_back()
    {
        // AppState is dumped to disk across a terminal release, and a notice
        // that vanished there would reappear as "nothing is wrong" the moment
        // somebody opened an editor.
        var restored = AppStateJson.Deserialize(
            AppStateJson.Serialize(new AppState { Notices = [ANotice()] }));

        await Assert.That(restored!.Notices.Count).IsEqualTo(1);
        await Assert.That(restored.Notices[0].Detail).IsEqualTo(ANotice().Detail);
    }
}
