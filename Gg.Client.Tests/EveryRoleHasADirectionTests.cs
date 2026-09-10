using Gg.Client;
using Gg.Contracts;
using Gg.Contracts.Authoring;

namespace Gg.Client.Tests;

/// <summary>
/// `gg airspace diff` answers a direction for every role, or it answers wrong.
/// </summary>
/// <remarks>
/// <para>
/// <b>Direction is the one thing this verb is for.</b> ADR-0016 § 6 makes who
/// may apply a computation rather than a role table — a tightening lands, a
/// widening rides the gate the widened document declares — and § 7 makes the
/// same computation decide the order applies land in. So a diff that reports
/// the wrong direction is not a cosmetic defect: it is a review of something
/// that will not happen, in an order that will not be taken.
/// </para>
/// <para>
/// <b>Nothing had ever called this verb.</b> No test in this suite reached
/// <c>AirspaceDiffAsync</c> or named <c>EstateDiff</c> before this class, and
/// the stub served neither airspace read — which is how a computation covering
/// two of four roles stayed green.
/// </para>
/// <para>
/// <b>The positive control is the point of the second test.</b> A verb that
/// answered <i>widening</i> for everything would satisfy the first and third
/// tests here and be useless: every apply would order last, and a person would
/// be told to expect a gate for work that lands on its own.
/// </para>
/// </remarks>
public class EveryRoleHasADirectionTests
{
    /// <summary>A session that is signed in, so the verb reaches the transport.</summary>
    private static StoredSession SignedIn { get; } = new()
    {
        SessionToken = "stub-session",
        ExpiresAt = new DateTimeOffset(2027, 1, 1, 0, 0, 0, TimeSpan.Zero),
        TenantId = "stub-tenant",
        PrincipalDisplay = "someone@example.test",
    };

    private static string Scratch()
    {
        var path = Path.Combine(Path.GetTempPath(), $"gg-direction-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    /// <summary>
    /// Replaces one document's text, keeping the precondition pull wrote.
    /// </summary>
    /// <remarks>
    /// Rendered from a model rather than hand-written, so this fixture cannot
    /// drift from the emitter the way a literal would — and the
    /// <c>based-on:</c> line is prepended exactly as pull prepends it.
    /// </remarks>
    private static void Overwrite(string root, string relative, string basedOn, string body) =>
        File.WriteAllText(
            Path.Combine(root, Path.Combine(relative.Split('/'))),
            $"based-on: {basedOn}\n{body}");

    private static async Task<EstateDiff> DiffAsync(StubControlPlane stub, string root)
    {
        using var http = new HttpClient { BaseAddress = new Uri(stub.BaseAddress) };
        var commands = new FlightCommands(
            new ControlPlaneClient(http), new HeldSessionStore(SignedIn));

        return ((VerbResult.AirspaceDiffed)await commands.AirspaceDiffAsync(root)).Value;
    }

    private static StubControlPlane Holding(AirspaceEstate estate) =>
        new() { Documents = estate.Documents, Strategies = estate.Strategies };

    /// <summary>The pci narrowing with one obligation swapped for another.</summary>
    /// <remarks>
    /// A REMOVAL, which is what makes it a widening: obligations union, so
    /// adding one constrains anybody and the beneficiary owns removal. Swapping
    /// rather than emptying keeps the document a valid narrowing, so what the
    /// verb answers is about direction and not about parsing.
    /// </remarks>
    private static string WithoutTheReview() =>
        EnvelopeText.Render(new EnvelopeNarrowing
        {
            Obligations =
            [
                new Obligation
                {
                    Id = "pci-audit",
                    Check = ObligationChecks.Human,
                    Approver = "an-auditor",
                },
            ],
        });

    /// <summary>The pci narrowing with a second obligation beside the first.</summary>
    private static string WithASecondReview() =>
        EnvelopeText.Render(new EnvelopeNarrowing
        {
            Obligations =
            [
                new Obligation
                {
                    Id = "pci-audit",
                    Check = ObligationChecks.Human,
                    Approver = "an-auditor",
                },
                new Obligation
                {
                    Id = "pci-review",
                    Check = ObligationChecks.Human,
                    Approver = "an-architect",
                },
            ],
        });

    [Test]
    public async Task A_narrowing_that_lost_an_obligation_is_a_widening()
    {
        // THE COMPARATOR ALREADY EXISTS AND IS NEVER CALLED.
        // EnvelopeDirection.Widening(EnvelopeNarrowing, EnvelopeNarrowing) is
        // in the contract, and it is what the control plane runs for this case
        // - so gg reporting a tightening is not a second opinion, it is an
        // answer to a question it never asked.
        var root = Scratch();
        try
        {
            var estate = PullTests.Estate();
            _ = AirspaceTree.Write(root, estate);
            Overwrite(root, "airspace/narrowings/pci.yaml", "pci@v1", WithoutTheReview());

            await using var stub = Holding(estate);
            var diff = await DiffAsync(stub, root);

            var pci = diff.Changes.Single(c => c.Name == "pci");

            await Assert.That(pci.Direction).IsEqualTo(Changeset.Widening)
                .Because("'pci-review' was removed, and removal is the beneficiary's to ask "
                       + "for through the gate. Reported as a tightening it would be ordered "
                       + "first and read as landing on its own.");

            await Assert.That(pci.Field).IsNotNull()
                .Because("the field is data so a person is told WHAT widened, not only that "
                       + "something did.");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public async Task A_narrowing_that_gained_an_obligation_is_a_tightening()
    {
        // THE POSITIVE CONTROL. Without it, answering `widening` for every
        // narrowing would pass the test above and make the verb worthless.
        var root = Scratch();
        try
        {
            var estate = PullTests.Estate();
            _ = AirspaceTree.Write(root, estate);
            Overwrite(root, "airspace/narrowings/pci.yaml", "pci@v1", WithASecondReview());

            await using var stub = Holding(estate);
            var diff = await DiffAsync(stub, root);

            var pci = diff.Changes.Single(c => c.Name == "pci");

            await Assert.That(pci.Direction).IsEqualTo(Changeset.Tightening)
                .Because("obligations union: adding one constrains anybody, so it is safe for "
                       + "whoever owns the document and needs no gate.");

            await Assert.That(pci.Field).IsNull();
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public async Task A_strategy_whose_pool_grew_is_not_reported_as_a_tightening()
    {
        // NOT A JUDGEMENT ABOUT STRATEGIES IN GENERAL - a case whose answer is
        // knowable. The control plane's StrategyDirection calls a larger
        // pool-max a widening in as many words: "warm at once is looser than
        // the N declared". gg holds no comparator for a strategy at all, so
        // what it must not do is claim the opposite of the door.
        var root = Scratch();
        try
        {
            var estate = PullTests.Estate();
            _ = AirspaceTree.Write(root, estate);

            var held = estate.Strategies.Single();
            var wider = held.Strategy with
            {
                Bounds = held.Strategy.Bounds with { PoolMax = held.Strategy.Bounds.PoolMax + 1 },
            };

            Overwrite(
                root, "airspace/strategies/payments-pool.yaml", held.Version,
                EnvelopeText.Render(wider));

            await using var stub = Holding(estate);
            var diff = await DiffAsync(stub, root);

            var pool = diff.Changes.Single(c => c.Name == "payments-pool");

            await Assert.That(pool.Direction).IsNotEqualTo(Changeset.Tightening)
                .Because("a bigger pool is reach that did not exist a moment ago, and telling "
                       + "somebody it only constrains is the one answer that is certainly "
                       + "wrong. Unknown is not neutral - the comparator's own rule is that "
                       + "wherever no order is declared it answers widening rather than "
                       + "deciding.");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public async Task The_widening_is_ordered_after_the_tightening()
    {
        // ADR-0016 § 7, over a changeset holding one of each. Land a widening
        // first and the estate spends the interval between two gates holding
        // something nothing governs - so the order a person reads has to be
        // the order that will happen.
        var root = Scratch();
        try
        {
            var estate = PullTests.Estate();
            _ = AirspaceTree.Write(root, estate);

            // pci loses an obligation: a widening.
            Overwrite(root, "airspace/narrowings/pci.yaml", "pci@v1", WithoutTheReview());

            // root gains one: a tightening.
            var floor = estate.Documents.Single(d => d.Name == "root").Envelope!;
            Overwrite(
                root, "airspace/root.yaml", "root@v1",
                EnvelopeText.Render(floor with
                {
                    Obligations =
                    [
                        .. floor.Obligations,
                        new Obligation
                        {
                            Id = "a-second-look",
                            Check = ObligationChecks.Human,
                            Approver = "an-architect",
                        },
                    ],
                }));

            await using var stub = Holding(estate);
            var diff = await DiffAsync(stub, root);

            await Assert.That(diff.Changes.Select(c => c.Direction).ToList())
                .IsEquivalentTo(new[] { Changeset.Tightening, Changeset.Widening })
                .Because("tightenings first, so every intermediate state is at or below both "
                       + "endpoints - and a diff listing them in one order while apply ran "
                       + "them in another would be a review of something that never occurs.");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
