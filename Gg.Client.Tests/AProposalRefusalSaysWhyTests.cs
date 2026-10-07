using Gg.Client;

namespace Gg.Client.Tests;

/// <summary>
/// A refused proposal is said in the door's sentence, for a 409 as for a 400, and never as the
/// problem JSON it arrived in (slice sixty-eight).
/// </summary>
/// <remarks>
/// <b>Found checking slice sixty-eight live.</b> A supersede the door refused reached the agent as
/// "Response status code does not indicate success: 409 (Conflict)": the client read the body only
/// for a 400, so the sentence that says what to do - "start a new draft", "not a plan you
/// proposed" - was dropped. The stub answered with a bare string, which is why no test saw it.
/// </remarks>
public class AProposalRefusalSaysWhyTests
{
    private const string Why =
        "ITN-60 is not a plan you proposed, so it is not yours to replace. Propose this one on its own.";

    [Test]
    [Arguments(409)]
    [Arguments(400)]
    public async Task The_sentence_is_what_is_said(int status)
    {
        await using var stub = new StubControlPlane
        {
            ItineraryRefusal = Why,
            ItineraryRefusalStatus = status,
            ItineraryRefusalAsProblem = true,
        };

        var refused = await Assert.ThrowsAsync<ItineraryRefusedException>(
            async () => await ProposingAPlanFileTests.Build(stub)
                .ProposeItineraryAsync(await ProposingAPlanFileTests.FileAsync(ProposingAPlanFileTests.Plan)));

        await Assert.That(refused!.Message).IsEqualTo(Why)
            .Because("the person and the agent read the door's sentence, not its envelope.");
    }
}
