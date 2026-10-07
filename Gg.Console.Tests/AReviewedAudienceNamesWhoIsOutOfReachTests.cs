using Gg.Client;

namespace Gg.Console.Tests;

/// <summary>
/// A machine that cannot be reached is marked in the review, and confirming sends to
/// nobody when none of them can be.
/// </summary>
/// <remarks>
/// <para>
/// <b>S64.6-03, and the same refusal the printed list already makes.</b> A row that read
/// like the others would promise a push that cannot happen; on a tenant whose fleet is
/// mostly pool members that is most of the list.
/// </para>
/// <para>
/// <b>And it names the host, because "unreachable" alone is useless.</b> A pool member has
/// no profile of its own — the enrolment's insert does not write one — so Decision 5's
/// host-to-member recursion is the only path to it, and this slice does not build that.
/// What a person can do is push to the host, which is a thing the row has to say.
/// </para>
/// <para>
/// <b>Confirming an all-unreachable list must not read a passphrase.</b> One typed for a
/// push that is not going to happen reads, to the person who typed it, as something having
/// moved.
/// </para>
/// </remarks>
public class AReviewedAudienceNamesWhoIsOutOfReachTests
{
    private static readonly CredentialAudienceRow Reachable = new(
        RunnerId: "host", Label: "vmlinux001", Locator: "local:acme/widgets",
        Declared: true, Reported: false, Reachable: true, Through: null);

    private static readonly CredentialAudienceRow Member = new(
        RunnerId: "member", Label: "gg-pool-ui-2", Locator: "local:acme/widgets",
        Declared: false, Reported: false, Reachable: false, Through: "vmlinux001");

    private static AppState Reviewing(params CredentialAudienceRow[] rows) =>
        new()
        {
            ActiveTab = TabId.Credentials,
            CredentialsVisible = true,
            Mode = UiMode.CredentialAudience,
            Audience = rows,
            AudienceFor = "local:acme/widgets",
        };

    [Test]
    public async Task An_unreachable_machine_is_marked_in_its_row()
    {
        var row = Rows.Audience(Reviewing(Reachable, Member))
            .Single(r => r.Machine == "gg-pool-ui-2");

        await Assert.That(row.Why).Contains("not")
            .Because("a row identical to a reachable machine's would promise a push that cannot "
                   + "happen. Said: " + row.Why);
    }

    [Test]
    public async Task And_it_names_the_host_that_would_pass_it_on()
    {
        var row = Rows.Audience(Reviewing(Reachable, Member))
            .Single(r => r.Machine == "gg-pool-ui-2");

        await Assert.That(row.Why).Contains("vmlinux001")
            .Because("told only that it is unreachable a person can do nothing; told which host "
                   + "holds it they can push to the host, which is the path that exists. Said: "
                   + row.Why);
    }

    [Test]
    public async Task The_count_is_of_machines_that_will_actually_receive_it()
    {
        var said = PaneText.Audience(Reviewing(Reachable, Member));

        await Assert.That(said).Contains("1")
            .Because("two rows, one recipient. A count of rows would tell somebody two machines "
                   + "are getting it. Said: " + said);
    }

    [Test]
    public async Task A_list_with_nobody_reachable_cannot_be_confirmed_into_a_passphrase()
    {
        var after = Reducer.Reduce(Reviewing(Member), Command.ConfirmAudience);

        await Assert.That(AudienceReview.AsksForThePassphrase(after)).IsFalse()
            .Because("a passphrase read for a push that is not going to happen reads, to whoever "
                   + "typed it, as something having moved.");
    }

    [Test]
    public async Task And_it_says_why_rather_than_doing_nothing_silently()
    {
        var after = Reducer.Reduce(Reviewing(Member), Command.ConfirmAudience);

        await Assert.That(PaneText.Audience(after)).Contains("gg-pool-ui-2")
            .Because("the list stays, naming the machine and its host, because that is the whole "
                   + "of what a person can act on here.");
    }

    [Test]
    public async Task An_empty_audience_says_nobody_needs_it()
    {
        var said = PaneText.Audience(Reviewing());

        await Assert.That(said).IsNotEmpty()
            .Because("nobody needing a credential is a fact worth a sentence - the usual cause is "
                   + "that no fleet profile declares it, which a person can fix.");
    }
}
