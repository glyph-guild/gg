using Gg.Contracts;

namespace Gg.Contracts.Tests;

/// <summary>
/// The account's two ceilings, and whether the state between them can occur.
/// </summary>
/// <remarks>
/// <para>
/// <b>Two numbers written by two authors for the same thing.</b>
/// <c>HumanAccount.MaxStatement</c> bounds what a person may write;
/// <c>TakeSeedComposer.MaxAccount</c> bounds how much of it a resuming agent is handed.
/// Nothing has ever compared them, and the comparison is what decides whether
/// <c>AccountStates.Truncated</c> is a state or a spelling mistake: if the seed's
/// budget met or exceeded the upstream cut, nothing could ever be truncated, the
/// value would be unreachable, and the renderer's arm for it would be code that
/// looks like a feature and is not.
/// </para>
/// <para>
/// <b>An unreachable state is worse than a missing one.</b> A missing state is
/// noticed the first time somebody needs it. An unreachable one is read by every
/// future author as a case that has been handled.
/// </para>
/// <para>
/// <b>Characters, on both sides, deliberately asserted.</b> <c>MaxStatement</c>
/// is measured in characters and <c>AccountBytes</c> is named for bytes and
/// holds characters. Naming it here is cheaper than the afternoon somebody
/// spends finding out during an incident.
/// </para>
/// </remarks>
public class SeedAccountBudgetTests
{
    private static TakeSeed Seeded(string account) =>
        TakeSeedComposer.Compose("GG-42", "019fe815-6136-7518-bb57-b06d6d3f411a", null, account);

    [Test]
    public async Task The_upstream_ceiling_leaves_room_for_the_seed_to_cut()
    {
        // THE ROW. Two numbers or one, and if they were one the truncated state
        // could not occur - so this is the assertion the state's existence rests
        // on, rather than a restatement of two constants.
        // THE HEADROOM RATHER THAN THE TWO NUMBERS, because comparing two
        // constants is a comparison the compiler makes and the assertion
        // analyzer says so. What has to be positive is the room between them.
        var headroom = HumanAccount.MaxStatement - TakeSeedComposer.MaxAccount;

        await Assert.That(headroom).IsGreaterThan(0)
            .Because("an account can only be cut for a seed if a person is allowed to write "
                   + "more than the seed carries. Equal ceilings make AccountStates.Truncated "
                   + $"unreachable. Upstream {HumanAccount.MaxStatement}, seed "
                   + $"{TakeSeedComposer.MaxAccount}.");
    }

    [Test]
    public async Task An_account_at_the_upstream_ceiling_is_truncated()
    {
        // REACHED, not reasoned about. The row above says the gap exists; this
        // says something actually falls into it, composed by the production
        // function rather than by a record built to look right.
        var longest = new string('x', HumanAccount.MaxStatement);

        await Assert.That(HumanAccount.Validate(new HumanAccount
        {
            By = "alice",
            Statement = longest,
            Confirmation = AccountConfirmations.Edited,
            ConfirmedAt = DateTimeOffset.UnixEpoch,
        })).IsNull()
            .Because("the fixture has to be an account the contract accepts, or this asserts "
                   + "about a statement that could never have been written.");

        var seed = Seeded(longest);

        await Assert.That(seed.AccountState).IsEqualTo(AccountStates.Truncated);
        await Assert.That(seed.Account!.Length).IsEqualTo(TakeSeedComposer.MaxAccount)
            .Because("cut to the budget, not to something near it.");
        await Assert.That(seed.AccountBytes).IsEqualTo(TakeSeedComposer.MaxAccount)
            .Because("and the count reports what was kept. A count reporting what was WRITTEN "
                   + "would tell a reader the seed holds text it does not have.");
    }

    [Test]
    public async Task An_account_exactly_at_the_budget_is_not_truncated()
    {
        // THE BOUNDARY, in the direction that costs something. An off-by-one the
        // other way marks a whole account as cut and puts a line in front of a
        // person saying they are missing text they are not.
        var seed = Seeded(new string('x', TakeSeedComposer.MaxAccount));

        await Assert.That(seed.AccountState).IsEqualTo(AccountStates.Present);
        await Assert.That(seed.Account!.Length).IsEqualTo(TakeSeedComposer.MaxAccount);
        await Assert.That(seed.AccountAbsence).IsNull();
    }

    [Test]
    public async Task A_truncated_account_says_so_where_it_is_read()
    {
        // THE HALF THAT MATTERS TO THE AGENT READING IT. A cut account that
        // arrives looking whole is one an agent will reason from as though it
        // had the end of the sentence - and the end of an account is where
        // somebody says what they did NOT do.
        var rendered = TakeSeedComposer.Render(Seeded(new string('x', HumanAccount.MaxStatement)));

        await Assert.That(rendered).Contains("truncated")
            .Because("an agent handed a cut account and not told it was cut will finish the "
                   + "sentence itself.");
        await Assert.That(rendered).Contains(TakeSeedComposer.MaxAccount.ToString(
            System.Globalization.CultureInfo.InvariantCulture))
            .Because("and how much it is missing, which is the difference between a warning "
                   + "and a number it can act on.");
    }

    [Test]
    public async Task An_account_that_fits_carries_no_warning()
    {
        // THE LIVENESS TWIN. A renderer that printed the truncation line every
        // time would satisfy the row above and tell every agent in the estate
        // that it is missing text.
        var rendered = TakeSeedComposer.Render(Seeded("I moved rounding into total() at the boundary."));

        await Assert.That(rendered).DoesNotContain("truncated");
        await Assert.That(rendered).Contains("total()");
    }
}
