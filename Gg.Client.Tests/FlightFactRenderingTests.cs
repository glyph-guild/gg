using Gg.Contracts;
using Gg.Contracts.Description;

namespace Gg.Client.Tests;

/// <summary>
/// <c>gg show</c> renders what only the runner could have known.
/// </summary>
/// <remarks>
/// Through the existing verb path: the facts arrive on the flight summary the
/// verb already returns, so there is no second fetch route and the JSON and the
/// human rendering stay two views of one document.
/// </remarks>
public class FlightFactRenderingTests
{
    private static FlightSummary AFlightWith(params FactEnvelope[] facts) => new()
    {
        FlightId = "019fe815-6136-7518-bb57-b06d6d3f411a",
        FlightNumber = FlightRef.Format(42),
        Name = "nightly audit",
        Intent = new FlightIntent { Kind = FlightIntentKinds.Text, Text = "look at it" },
        CreatedAt = new DateTimeOffset(2026, 8, 11, 12, 0, 0, TimeSpan.Zero),
        RunnerProtocolVersion = 1,
        FactVocabularyVersion = "0.1.0",
        ConstitutionVersion = "1.0.0",
        EnvelopeVersion = "none",
        Facts = facts,
    };

    private static FactEnvelope AnEnvironmentFact() => new()
    {
        IdempotencyKey = "k1",
        Kind = FactKinds.EnvironmentIdentity,
        Digest = new string('a', 64),
        ObservedAt = new DateTimeOffset(2026, 8, 11, 12, 0, 0, TimeSpan.Zero),
        Environment = new EnvironmentIdentity
        {
            HostFingerprint = new string('b', 64),
            ImageDigest = null,
            Locks = [new LockHash { Path = "package-lock.json", Sha256 = new string('c', 64) }],
            Tools = [new ToolVersion { Name = "git", Version = "2.50.1" }],
            Provenance = EnvironmentProvenance.Fresh,
        },
    };

    private static FactEnvelope AForkFact() => new()
    {
        IdempotencyKey = "k2",
        Kind = FactKinds.SourceProvenance,
        Digest = new string('d', 64),
        ObservedAt = new DateTimeOffset(2026, 8, 11, 12, 0, 0, TimeSpan.Zero),
        Source = new SourceProvenance
        {
            Provider = "forge",
            Slug = "acme/widgets",
            RequestedRef = "refs/pull/7/head",
            ResolvedRef = "refs/pull/7/head",
            HeadCommit = "1234567890abcdef1234567890abcdef12345678",
            HeadIsFork = true,
            ForkSlug = "someone-else/widgets",
            FileCount = 12,
            Bytes = 34567,
        },
    };

    [Test]
    public async Task The_environment_fact_is_rendered()
    {
        var text = VerbOutput.ToText(new VerbResult.Flight(AFlightWith(AnEnvironmentFact())));

        await Assert.That(text).Contains(FactKinds.EnvironmentIdentity);
        await Assert.That(text).Contains(EnvironmentProvenance.Fresh);
        await Assert.That(text).Contains("package-lock.json");
        await Assert.That(text).Contains("2.50.1");
    }

    [Test]
    public async Task A_fork_head_is_named_as_one_rather_than_left_to_be_inferred()
    {
        // A run that examined a fork and did not say so is a false fact, which
        // this design treats as unrecoverable. The rendering says which, either
        // way, rather than only when it is interesting.
        var forked = VerbOutput.ToText(new VerbResult.Flight(AFlightWith(AForkFact())));
        var direct = VerbOutput.ToText(new VerbResult.Flight(AFlightWith(
            AForkFact() with { Source = AForkFact().Source! with { HeadIsFork = false, ForkSlug = null } })));

        await Assert.That(forked).Contains("someone-else/widgets");
        await Assert.That(forked).Contains("fork");
        await Assert.That(direct).Contains("the base repository");
    }

    [Test]
    public async Task A_flight_with_no_facts_says_so_rather_than_showing_nothing()
    {
        var text = VerbOutput.ToText(new VerbResult.Flight(AFlightWith()));

        await Assert.That(text).Contains("none yet")
            .Because("no facts and facts we failed to render look identical otherwise, and only one "
                   + "of them means the runner never got there.");
    }

    [Test]
    public async Task The_rendering_round_trips_through_the_json()
    {
        // The property every verb has: the human output is a rendering of the
        // document, with no second source. Facts do not get an exception.
        var result = new VerbResult.Flight(AFlightWith(AnEnvironmentFact(), AForkFact()));
        var again = VerbOutput.Parse(result.Kind, VerbOutput.ToJson(result));

        await Assert.That(VerbOutput.ToText(again)).IsEqualTo(VerbOutput.ToText(result));
    }

    [Test]
    public async Task Nothing_rendered_could_be_a_line_of_somebody_s_source()
    {
        // Paths, counts, hashes and a commit. The control plane's own absence
        // scan is the real proof; this is the last surface before a screen, and
        // a screen is a place people screenshot into tickets.
        var text = VerbOutput.ToText(new VerbResult.Flight(AFlightWith(AnEnvironmentFact(), AForkFact())));

        await Assert.That(text).Contains("12 file(s)")
            .Because("a count is what a file list would have been.");
        await Assert.That(text).Contains("34,567 bytes")
            .Because("disk is the first resource we consume in somebody else's environment, and this "
                   + "is where a person finds out how much.");
    }
}
