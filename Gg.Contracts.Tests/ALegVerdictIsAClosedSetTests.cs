using Gg.Contracts;

namespace Gg.Contracts.Tests;

/// <summary>
/// <b>S61.4-02</b> - a leg's verdict is one of three words, and a check carrying any other is
/// refused on read.
/// </summary>
/// <remarks>
/// <b>Refused rather than rendered.</b> A verdict gg does not know is a control plane that
/// learned a fourth answer this client cannot explain. Printing it would show a person a word
/// with no meaning attached; refusing says the client is older than the answer, which is the
/// thing they can act on.
/// </remarks>
public class ALegVerdictIsAClosedSetTests
{
    [Test]
    public async Task There_are_three_verdicts()
    {
        await Assert.That(LegVerdicts.All).IsEquivalentTo(
            [LegVerdicts.Opens, LegVerdicts.Stands, LegVerdicts.Refused]);
        await Assert.That(LegVerdicts.Opens).IsEqualTo("opens");
        await Assert.That(LegVerdicts.Stands).IsEqualTo("stands");
        await Assert.That(LegVerdicts.Refused).IsEqualTo("refused");
    }

    [Test]
    public async Task A_check_with_known_verdicts_is_valid()
    {
        await Assert.That(ItineraryCheck.Validate(Check(LegVerdicts.Opens, LegVerdicts.Refused)))
            .IsNull();
    }

    [Test]
    public async Task A_check_with_an_unknown_verdict_is_refused_naming_it()
    {
        var because = ItineraryCheck.Validate(Check(LegVerdicts.Opens, "maybe"));

        await Assert.That(because).IsNotNull();
        await Assert.That(because!).Contains("'maybe'");
    }

    private static ItineraryCheck Check(params string[] verdicts) => new()
    {
        Planner = "plan",
        DestinationId = "the-plan",
        Legs =
        [
            .. verdicts.Select((v, i) => new LegCheck
            {
                Subject = $"piece {i}",
                WorkKind = "implement",
                Verdict = v,
                Reason = "because",
                Obligations = [],
                Gates = [],
                PassedOver = [],
            }),
        ],
    };
}
