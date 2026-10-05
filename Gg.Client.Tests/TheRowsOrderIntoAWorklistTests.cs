using Gg.Contracts;

namespace Gg.Client.Tests;

/// <summary>
/// The rows come back ordered so that what is unfinished is first.
/// </summary>
/// <remarks>
/// <para>
/// <b>S60.2-04, and the ordering is the feature.</b> A list in registry order
/// makes somebody read past everything already done to find the row they came
/// for — which is the argument <c>CredentialRepositories</c> already makes for
/// the send chooser, in those words. Here it is stronger, because one of these
/// rows predicts a flight failing and the rest say things are fine.
/// </para>
/// <para>
/// <b>One rank, reused rather than rewritten.</b> The chooser already ranks
/// <see cref="CredentialStanding"/>, so that function moves to
/// <c>Gg.Client</c> and both callers share it. A second ranking that agreed
/// today is the hazard this codebase names repeatedly — and here the two lists
/// would come to disagree about which work is urgent, which is worse than
/// either order alone.
/// </para>
/// <para>
/// <b>Two vocabularies, each doing its own job.</b> A row's <c>Resting</c> says
/// how a credential sits on this machine — sealed, plaintext, in a vault — and
/// its <c>Standing</c> says whether anything is left to do about it. Only the
/// second can be ranked: "plaintext" is not more urgent than "sealed", because
/// resealing happens on read with nobody present.
/// </para>
/// </remarks>
public class TheRowsOrderIntoAWorklistTests
{
    private static CredentialSummary ACredential(string repo) => new()
    {
        CredentialId = "id-" + repo,
        Repo = repo,
        AddedAt = DateTimeOffset.UnixEpoch,
        Reference = new CredentialReference
        {
            Kind = CredentialKinds.Local,
            Locator = CredentialLocator.ForRepo(repo),
            Identity = "acme-bot",
            Scopes = [CredentialScopes.Read],
        },
    };

    private static RepositoryRegistered ARepository(
        string path, string credential = RepositoryCredentialModes.Required) => new()
    {
        Name = path.Replace('/', '-'),
        Provider = "github",
        Id = "R_" + path,
        Path = path,
        Credential = credential,
        RegisteredAt = DateTimeOffset.UnixEpoch,
        RegisteredBy = "a-directory:ada",
    };

    [Test]
    public async Task What_nobody_has_registered_comes_before_what_is_merely_elsewhere()
    {
        var rows = CredentialRows.For(
            [ACredential("acme/held"), ACredential("acme/elsewhere")],
            [
                ARepository("acme/held"),
                ARepository("acme/elsewhere"),
                ARepository("acme/orphan"),
                ARepository("acme/public", credential: RepositoryCredentialModes.None),
            ],
            [
                new CredentialAtRest(CredentialLocator.ForRepo("acme/held"), CredentialResting.Sealed),
                new CredentialAtRest(CredentialLocator.ForRepo("acme/elsewhere"), CredentialResting.NotHere),
            ]);

        await Assert.That(rows.Select(r => r.For).ToList()).IsEquivalentTo((List<string>)
        [
            "acme/orphan",     // none registered - add one
            "acme/elsewhere",  // registered, not on this machine - push one
            "acme/held",       // here, nothing to do
            "acme/public",     // authenticates to nothing, never pending
        ]);
    }

    [Test]
    public async Task Rows_that_rank_the_same_are_ordered_by_name()
    {
        // STABLE, so that a refresh does not move the cursor under somebody. The
        // chooser's own rule, for the same reason.
        var rows = CredentialRows.For(
            [],
            [ARepository("acme/zebra"), ARepository("acme/apple"), ARepository("acme/mango")],
            []);

        await Assert.That(rows.Select(r => r.For).ToList()).IsEquivalentTo((List<string>)
            ["acme/apple", "acme/mango", "acme/zebra"]);
    }

    [Test]
    public async Task Plaintext_is_not_ranked_as_work()
    {
        // IT RESEALS ON READ, WITH NOBODY PRESENT. Ranking it as pending would
        // put rows at the top of a worklist that no person can act on, which is
        // how a worklist stops being read.
        var rows = CredentialRows.For(
            [ACredential("acme/old"), ACredential("acme/new")],
            [ARepository("acme/old"), ARepository("acme/new")],
            [
                new CredentialAtRest(CredentialLocator.ForRepo("acme/old"), CredentialResting.Plaintext),
                new CredentialAtRest(CredentialLocator.ForRepo("acme/new"), CredentialResting.Sealed),
            ]);

        await Assert.That(rows.Select(r => r.Standing).Distinct().Single())
            .IsEqualTo(CredentialStanding.Here)
            .Because("both are on this machine and usable; how they rest differs and what is left "
                   + "to do does not.");
    }

    [Test]
    public async Task The_rank_is_the_one_the_send_chooser_already_uses()
    {
        // ONE AUTHORITY, asserted rather than trusted. If these ever disagree,
        // two screens disagree about which credential work is urgent.
        foreach (var standing in (string[])
        [
            CredentialStanding.NoneRegistered,
            CredentialStanding.MissingHere,
            CredentialStanding.Unknown,
            CredentialStanding.Here,
            CredentialStanding.NotNeeded,
        ])
        {
            await Assert.That(CredentialStanding.Rank(standing)).IsGreaterThanOrEqualTo(0);
        }

        await Assert.That(CredentialStanding.Rank(CredentialStanding.NoneRegistered))
            .IsLessThan(CredentialStanding.Rank(CredentialStanding.MissingHere));

        await Assert.That(CredentialStanding.Rank(CredentialStanding.MissingHere))
            .IsLessThan(CredentialStanding.Rank(CredentialStanding.Here));

        await Assert.That(CredentialStanding.Rank(CredentialStanding.Here))
            .IsLessThan(CredentialStanding.Rank(CredentialStanding.NotNeeded));
    }
}
