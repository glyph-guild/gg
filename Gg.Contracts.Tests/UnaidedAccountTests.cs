namespace Gg.Contracts.Tests;

/// <summary>
/// An account written with no inference at all.
/// </summary>
/// <remarks>
/// <para>
/// <b>THE CONTRACT ALREADY NAMED THE STATE AND HAD NO VALUE FOR IT.</b>
/// <c>WasProposed</c>'s own remark says <i>"a replaced account had a proposal
/// that was discarded, and one written with no inference at all is a different
/// thing again"</i> — and <c>AccountConfirmations</c> carried three values, every
/// one of them a relationship to a proposal. So the case the prose distinguishes
/// could not be expressed.
/// </para>
/// <para>
/// <b><c>replaced</c> was the tempting answer and it is the harmful one.</b> Its
/// own remark says it is <i>"the signal that the feature is not working"</i> —
/// the escape hatch for a proposal so bad it was easier to rewrite than to
/// edit. A hand-flight has no proposal to be bad, so recording `replaced` would
/// fire that signal on every one of them and make the inference look broken
/// wherever it had simply never run.
/// </para>
/// <para>
/// <b>A hand-flight is what makes this reachable.</b> The person is handed a
/// terminal, does the work, and writes what they did in the return file — with
/// nothing proposed to them, because <c>HandSession</c> runs on the takeover path
/// and not this one. Their words are the only account an attended flight has, and
/// they are the most informative thing in its record: <c>loop.attended</c>
/// declares turns, moves and the tool bound all unmeasured.
/// </para>
/// </remarks>
public class UnaidedAccountTests
{
    [Test]
    public async Task Nothing_proposed_is_its_own_confirmation()
    {
        await Assert.That(AccountConfirmations.All).IsEquivalentTo(new[]
        {
            AccountConfirmations.Accepted,
            AccountConfirmations.Edited,
            AccountConfirmations.Replaced,
            AccountConfirmations.Unaided,
        });
    }

    [Test]
    public async Task An_unaided_account_validates()
    {
        var written = new HumanAccount
        {
            By = "somebody",
            Statement = "I renamed the column and backfilled it in two passes.",
            Confirmation = AccountConfirmations.Unaided,
            ConfirmedAt = DateTimeOffset.UnixEpoch,
        };

        await Assert.That(HumanAccount.Validate(written)).IsNull();
        await Assert.That(written.WasProposed).IsNull()
            .Because("how much of the proposal survived is not a question about an account "
                   + "that had none, and false would say a proposal was made and kept "
                   + "nothing.");
    }

    [Test]
    public async Task It_is_not_replaced_because_replaced_means_a_proposal_was_bad()
    {
        // THE DISTINCTION THIS VALUE EXISTS FOR, said as an assertion rather
        // than only in prose. `replaced` is a quality signal about the
        // inference; using it here would report a failure of something that
        // never ran, on every hand-flight forever.
        await Assert.That(AccountConfirmations.Unaided)
            .IsNotEqualTo(AccountConfirmations.Replaced);
    }
}
