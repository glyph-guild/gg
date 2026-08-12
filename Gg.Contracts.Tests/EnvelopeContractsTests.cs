using System.Reflection;

namespace Gg.Contracts.Tests;

/// <summary>
/// The envelope schema, and the two things that make it safe to put here.
/// </summary>
/// <remarks>
/// <para>
/// <b>Cardinality one, checked rather than intended.</b> The steel thread is
/// one context binding, one obligation, one loop, one destination — and a
/// second of anything is the next slice arriving early. That is a rule
/// somebody can break in a single commit, so it is a rule with a test.
/// </para>
/// <para>
/// <b>The assembly still takes no third-party dependency.</b> The types and
/// the canonical emitter live here; the PARSER does not, and cannot, because
/// every YAML library is a package reference and this is the artifact a
/// customer audits. <c>ContractsDependencyTests</c> is the assertion; this
/// file is the reason it now has something to catch.
/// </para>
/// </remarks>
public class EnvelopeContractsTests
{
    private static Envelope AnEnvelope() => new()
    {
        Context = new ContextBinding { Scope = "src/**", Constitution = "1.0.0" },
        Obligations =
        [
            new Obligation
            {
                Id = "in-scope",
                Check = ObligationChecks.Machine,
                Rule = ObligationPredicates.NoFileOutsideScope,
            },
        ],
        Loops =
        [
            new Loop
            {
                Id = "implement",
                Executor = ExecutorRungs.Frontier,
                Discharges = ["in-scope"],
                Moves = [LoopMoves.Read, LoopMoves.Edit, LoopMoves.RunTests, LoopMoves.Search],
                Budget = new LoopBudget { WallClock = "30m" },
                OnExhaustion = ExhaustionPolicies.HandoffToHuman,
            },
        ],
        Destinations =
        [
            new Destination
            {
                Id = "pull-request",
                Kind = DestinationKinds.PullRequest,
                Requires = ["in-scope"],
            },
        ],
    };

    [Test]
    public async Task The_steel_thread_validates()
    {
        await Assert.That(Envelope.Validate(AnEnvelope())).IsNull();
    }

    [Test]
    public async Task A_second_obligation_is_the_slice_slipping_and_is_refused()
    {
        var two = AnEnvelope() with
        {
            Obligations =
            [
                .. AnEnvelope().Obligations,
                new Obligation
                {
                    Id = "also-in-scope",
                    Check = ObligationChecks.Machine,
                    Rule = ObligationPredicates.NoFileOutsideScope,
                },
            ],
        };

        var diagnosis = Envelope.Validate(two);

        await Assert.That(diagnosis).IsNotNull();
        await Assert.That(diagnosis).Contains("one obligation");
    }

    [Test]
    public async Task A_second_loop_is_the_slice_slipping_and_is_refused()
    {
        var two = AnEnvelope() with
        {
            Loops = [.. AnEnvelope().Loops, AnEnvelope().Loops[0] with { Id = "review" }],
        };

        await Assert.That(Envelope.Validate(two)).Contains("one loop");
    }

    [Test]
    public async Task A_loop_may_only_discharge_an_obligation_that_exists()
    {
        // The one relationship in the model that can be wrong without being
        // malformed. A loop discharging nothing real is an obligation nothing
        // can satisfy, which is a flight that can never finish.
        var dangling = AnEnvelope() with
        {
            Loops = [AnEnvelope().Loops[0] with { Discharges = ["not-an-obligation"] }],
        };

        var diagnosis = Envelope.Validate(dangling);

        await Assert.That(diagnosis).IsNotNull();
        await Assert.That(diagnosis).Contains("not-an-obligation");
    }

    [Test]
    public async Task A_destination_may_only_require_an_obligation_that_exists()
    {
        var dangling = AnEnvelope() with
        {
            Destinations = [AnEnvelope().Destinations[0] with { Requires = ["invented"] }],
        };

        await Assert.That(Envelope.Validate(dangling)).Contains("invented");
    }

    [Test]
    public async Task An_unknown_predicate_is_refused_by_name()
    {
        // Article XI. A rule nothing can evaluate must not become an
        // obligation that quietly never fires - a silently-false obligation is
        // indistinguishable from a satisfied one, which is this system's most
        // dangerous failure.
        var unknown = AnEnvelope() with
        {
            Obligations = [AnEnvelope().Obligations[0] with { Rule = "vibes" }],
        };

        var diagnosis = Envelope.Validate(unknown);

        await Assert.That(diagnosis).Contains("vibes");
        await Assert.That(diagnosis).Contains(ObligationPredicates.NoFileOutsideScope)
            .Because("naming what was expected is the difference between a diagnosis and a refusal.");
    }

    [Test]
    public async Task An_unknown_executor_rung_is_refused_by_name()
    {
        var unknown = AnEnvelope() with
        {
            Loops = [AnEnvelope().Loops[0] with { Executor = "cheap" }],
        };

        await Assert.That(Envelope.Validate(unknown)).Contains("cheap");
    }

    [Test]
    public async Task A_budget_that_is_not_a_duration_is_refused()
    {
        var nonsense = AnEnvelope() with
        {
            Loops = [AnEnvelope().Loops[0] with { Budget = new LoopBudget { WallClock = "soon" } }],
        };

        await Assert.That(Envelope.Validate(nonsense)).Contains("soon");
    }

    [Test]
    public async Task A_duration_reads_the_same_on_both_sides()
    {
        // Declared here rather than parsed twice, for the reason every other
        // shared rule is: two implementations of one grammar agree until they
        // do not, and the disagreement surfaces as a budget that expired early
        // on one side.
        await Assert.That(EnvelopeDurations.TryParse("30m", out var half)).IsTrue();
        await Assert.That(half).IsEqualTo(TimeSpan.FromMinutes(30));

        await Assert.That(EnvelopeDurations.TryParse("2h", out var two)).IsTrue();
        await Assert.That(two).IsEqualTo(TimeSpan.FromHours(2));

        await Assert.That(EnvelopeDurations.TryParse("45s", out var seconds)).IsTrue();
        await Assert.That(seconds).IsEqualTo(TimeSpan.FromSeconds(45));

        foreach (var bad in (string[])["", "30", "m", "30x", "-5m", "1.5h", "30 m"])
        {
            await Assert.That(EnvelopeDurations.TryParse(bad, out _)).IsFalse()
                .Because($"'{bad}' is not a duration, and a budget nobody can read is a budget "
                       + "nobody enforces.");
        }
    }

    // ---- the assembly this lives in ----

    [Test]
    public async Task Every_envelope_type_is_pinned_and_in_the_vocabulary()
    {
        // VocabularyTests asserts this over the whole assembly. Named here too,
        // because the criterion is about the ENVELOPE types specifically and a
        // reader of this file should not have to go and check.
        foreach (var type in (Type[])
                 [typeof(Envelope), typeof(ContextBinding), typeof(Obligation),
                  typeof(Loop), typeof(LoopBudget), typeof(Destination),
                  typeof(EnvelopeState), typeof(EnvelopeApplied)])
        {
            await Assert.That(type.GetCustomAttribute<PinnedIdAttribute>()).IsNotNull()
                .Because($"{type.Name} crosses the boundary and must not borrow its identity from a name.");
            await Assert.That(Vocabulary.Types).Contains(type);
        }
    }

    [Test]
    public async Task The_emitter_is_here_and_the_parser_is_not()
    {
        // The split that lets the schema live in an assembly with no
        // dependencies: model -> text is deterministic and hand-written; text
        // -> model needs a grammar, and every grammar is a package.
        var assembly = typeof(Envelope).Assembly;

        await Assert.That(assembly.GetType("Gg.Contracts.EnvelopeText")).IsNotNull();

        var parserish = assembly.GetExportedTypes()
            .Where(t => t.Name.Contains("Parser", StringComparison.Ordinal)
                     || t.Name.Contains("Yaml", StringComparison.Ordinal))
            .Select(t => t.Name)
            .ToList();

        await Assert.That(parserish).IsEmpty()
            .Because("a parser here would be a package reference here. Found: "
                   + string.Join(", ", parserish));
    }
}
