using Gg.Client;
using Gg.Contracts;

namespace Gg.Cli.Tests;

/// <summary>
/// A board row says whose it is, when it is somebody's.
/// </summary>
/// <remarks>
/// <para>
/// <b>S42.5-02; slice forty-two rules 13 and 14.</b> ADR-0024 puts a personal
/// watch's rows on the tenant's board - one board - and only the row's person
/// may decide one. A listing that shows everybody's rows without saying whose
/// they are is a list where the one thing a reader can act on is invisible,
/// and where the 403 they get for answering somebody else's arrives with no
/// warning.
/// </para>
/// <para>
/// <b>The display beside the subject.</b> A subject is opaque - a provider and
/// an id - so a row showing only that names nobody a person recognises; a
/// display alone cannot tell two people with one name apart. Both, and the
/// display first, because that is the half a reader scans.
/// </para>
/// <para>
/// <b>A tenant row says nothing.</b> It belongs to nobody, which is what the
/// topology read already says by saying nothing, and a line reading "for: the
/// tenant" on every row of an ordinary board would be noise that hides the
/// personal rows it exists to make visible.
/// </para>
/// </remarks>
public class TheBoardSaysWhoseRowItIsTests
{
    private const string Subject = "fake:01a0bb8c-2794-765e-a25e-eb32cfc4c875";

    private static NominationSummary ARow(string? forWhom = null, string? display = null) => new()
    {
        NominationId = Guid.Parse("01a0a4a7-3c60-7c9a-9f0e-7d0f6c2a51b3"),
        Nominator = "watch:my-queue",
        Subject = "work-item:tracker.example/4242",
        Version = "7",
        WorkKind = "implement",
        Mode = DestinationOpening.Gated,
        State = "standing",
        MadeAt = new DateTimeOffset(2026, 9, 19, 12, 0, 0, TimeSpan.Zero),
        IntentKey = "https://tracker.example/acme/_workitems/edit/4242",
        For = forWhom,
        ForDisplay = display,
    };

    private static string TextOf(NominationSummary row) =>
        VerbOutput.ToText(new VerbResult.Board(new BoardPage
        {
            Nominations = [row],
            IncludedEnded = false,
        }));

    [Test]
    public async Task A_personal_row_says_whose_it_is_by_display_and_subject()
    {
        var text = TextOf(ARow(Subject, "Kevin"));

        await Assert.That(text).Contains("Kevin")
            .Because("the display is the half a reader recognises.");
        await Assert.That(text).Contains(Subject)
            .Because("the subject is what the document writes and what tells two people "
                   + "with one display apart.");
    }

    [Test]
    public async Task A_tenant_row_says_nothing_about_whose_it_is()
    {
        var text = TextOf(ARow());

        await Assert.That(text).DoesNotContain("for:")
            .Because("a tenant row belongs to nobody, and the topology read already says "
                   + "that by saying nothing - a line on every row would hide the personal "
                   + "ones this exists to show.");
    }

    [Test]
    public async Task A_person_who_has_left_still_renders_as_their_subject()
    {
        // THE DISPLAY IS DROPPED WHEN SOMEBODY LEAVES THE TENANT - the rule
        // RunnerReserved's sentence follows one surface over. A row that then
        // printed "(unnamed)" would read as a rendering fault rather than as a
        // person who is gone, and the subject is still the answer to whose it is.
        var text = TextOf(ARow(Subject));

        await Assert.That(text).Contains(Subject);
        await Assert.That(text).DoesNotContain("(unnamed)");
    }
}
