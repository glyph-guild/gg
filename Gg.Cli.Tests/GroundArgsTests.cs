namespace Gg.Cli.Tests;

/// <summary>
/// `gg ground` stops a flight, and cannot be typed without saying why.
/// </summary>
/// <remarks>
/// <para>
/// <b>The reason is a required argument rather than an optional one, and that
/// is the whole shape of this verb.</b> The wire refuses a blank one, so an
/// optional flag here would only move the refusal from the moment somebody
/// types the command to a round trip later - and the sentence it would refuse
/// with is about a field, where this one is about a decision.
/// </para>
/// <para>
/// <b>`gg decide` is the model, not `gg take`.</b> Both take a flight, but a
/// takeover is resumable and a grounding is an ending: there is no half of this
/// verb that means "start grounding". So there are no modes, no --return
/// counterpart, and one arm.
/// </para>
/// </remarks>
public class GroundArgsTests
{
    [Test]
    public async Task A_flight_and_a_reason_parse()
    {
        var parsed = CliArgs.Parse(["ground", "GG-54", "the fleet cannot serve this yet"]);

        await Assert.That(parsed).IsTypeOf<CliAction.Ground>();

        var ground = (CliAction.Ground)parsed;

        await Assert.That(ground.Reference).IsEqualTo("GG-54");
        await Assert.That(ground.Because).IsEqualTo("the fleet cannot serve this yet");
    }

    [Test]
    public async Task An_id_parses_too_because_the_wire_resolves_both()
    {
        var parsed = (CliAction.Ground)CliArgs.Parse(
            ["ground", "01a0792a-5e1f-7030-a5d8-52fd66e510b0", "stopped for now"]);

        await Assert.That(parsed.Reference).IsEqualTo("01a0792a-5e1f-7030-a5d8-52fd66e510b0")
            .Because("{ref} resolves a uuid or a flight number through the one parser, so "
                   + "this verb does not get an opinion about which was typed.");
    }

    [Test]
    public async Task A_grounding_with_no_reason_is_refused_before_it_is_sent()
    {
        // ARTICLE XII'S NEIGHBOUR. The wire requires it and would refuse this
        // with a 400, but a person who typed the wrong thing should be told by
        // the thing they typed it into - and the refusal here can say what the
        // reason is FOR, which a field-level 400 cannot.
        var parsed = CliArgs.Parse(["ground", "GG-54"]);

        await Assert.That(parsed).IsTypeOf<CliAction.Unknown>();

        var refusal = ((CliAction.Unknown)parsed).Message;

        await Assert.That(refusal).Contains("why")
            .Because($"the reason is the only thing that survives to tell a later reader why "
                   + $"work that could have been done was not. Message: {refusal}");
    }

    [Test]
    public async Task Grounding_nothing_at_all_is_refused()
    {
        await Assert.That(CliArgs.Parse(["ground"])).IsTypeOf<CliAction.Unknown>();
    }

    [Test]
    public async Task It_is_in_the_usage_where_the_other_flight_verbs_are()
    {
        // EveryVerbIsDiscoverableTests walks the parser against the usage, so
        // this would be caught anyway. It is here as well because THIS verb is
        // the one a person goes looking for at the worst moment - a flight that
        // will not move - and finding nothing is how they reach for `withdraw`
        // instead and say something untrue about why it ended.
        var usage = ((CliAction.Unknown)CliArgs.Parse(["nonsense"])).Message;

        await Assert.That(usage).Contains("gg ground");
        await Assert.That(usage).Contains("stop a flight");
    }
}
