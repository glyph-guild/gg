namespace Gg.Contracts.Tests;

/// <summary>
/// Every launch of a loop's agent is recorded as a <c>loop.model</c> fact
/// (slice seventy-six): which flag the runner passed, or null for the
/// account's default, and whether the child was started over.
/// </summary>
/// <remarks>
/// <para>
/// <b>The owner's words:</b> <i>"we need some of those out of scope things
/// (model flag, runner fact)."</i> Slice seventy-five left which model ran to
/// the transcript. A person who can now move a flight in the air needs the
/// runner's own word that the move happened, and one fact per launch is what
/// lets two of them bracket a relaunch.
/// </para>
/// <para>
/// <b>A kind of its own</b> rather than a member on <c>loop.outcome</c>, which
/// is shipped once per run and could not say a run began on one model and
/// ended on another.
/// </para>
/// </remarks>
public class ALaunchRecordsItsModelTests
{
    [Test]
    public async Task It_is_a_registered_kind_in_the_flight_category()
    {
        await Assert.That(FactKinds.All).Contains(FactKinds.LoopModel)
            .Because("a kind outside All is one no guard counts and no consumer is told about.");
        await Assert.That(FactCategories.Of(FactKinds.LoopModel)).IsEqualTo(FactCategories.Flight)
            .Because("it belongs to one flight's own run and to nothing that outlives it.");
    }

    [Test]
    public async Task A_launch_on_a_named_model_and_one_on_the_default_both_validate()
    {
        var shell = new FactEnvelope
        {
            IdempotencyKey = "k",
            Kind = FactKinds.LoopModel,
            Digest = new string('a', 64),
            ObservedAt = DateTimeOffset.UnixEpoch,
        };

        await Assert.That(FactEnvelope.Validate(shell with
        {
            Model = new LoopModel { LoopId = "investigate", Model = "claude-opus-5-5" },
        })).IsNull();

        await Assert.That(FactEnvelope.Validate(shell with
        {
            Model = new LoopModel { LoopId = "investigate", Model = null, Relaunched = true },
        })).IsNull().Because("null is the account's default, and a relaunch may land there too.");

        await Assert.That(FactEnvelope.Validate(shell)).IsNotNull()
            .Because("the kind says loop.model and no slot carries one.");
    }

    [Test]
    public async Task It_is_on_the_wire_surface_and_the_vocabulary_moved()
    {
        await Assert.That(Description.ProtocolSurface.JsonMembers[typeof(LoopModel)])
            .IsEquivalentTo(["loopId", "model", "relaunched"]);
        await Assert.That(Description.ProtocolSurface.JsonMembers[typeof(FactEnvelope)]).Contains("model")
            .Because("the envelope's slot is a member of the envelope, and the surface lists "
                   + "the envelope's members too.");
        await Assert.That(FactVocabulary.Version).IsEqualTo("0.44.0")
            .Because("a kind that crosses is a vocabulary that moved.");
    }
}
