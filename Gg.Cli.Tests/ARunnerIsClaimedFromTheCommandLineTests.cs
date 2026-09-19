namespace Gg.Cli.Tests;

/// <summary>
/// The five ownership verbs parse to their actions, and a mistake in one is
/// refused with the sentence that fixes it.
/// </summary>
/// <remarks>
/// Slice forty-three, rules 5 to 8. The control plane is the authority on who
/// may do each of these; what this side refuses is only what no control plane
/// could accept - a missing id, and an admin asking to set a runner claimed,
/// which is only ever a person's own act.
/// </remarks>
public class ARunnerIsClaimedFromTheCommandLineTests
{
    [Test]
    public async Task Each_verb_names_one_runner()
    {
        await Assert.That(CliArgs.Parse(["runner", "claim", "r-1"]))
            .IsEqualTo(new CliAction.RunnerClaim("r-1", false));
        await Assert.That(CliArgs.Parse(["runner", "unclaim", "r-1"]))
            .IsEqualTo(new CliAction.RunnerUnclaim("r-1", false));
        await Assert.That(CliArgs.Parse(["runner", "reserve", "r-1"]))
            .IsEqualTo(new CliAction.RunnerReserve("r-1", false));
        await Assert.That(CliArgs.Parse(["runner", "release", "r-1", "--json"]))
            .IsEqualTo(new CliAction.RunnerRelease("r-1", true));
    }

    [Test]
    [Arguments("tenant")]
    [Arguments("open")]
    public async Task An_admin_says_tenant_or_open(string ownership)
    {
        await Assert.That(CliArgs.Parse(["runner", "ownership", "r-1", ownership]))
            .IsEqualTo(new CliAction.RunnerOwnershipSet("r-1", ownership, false));
    }

    [Test]
    public async Task Claimed_is_never_set_by_an_admin()
    {
        var parsed = CliArgs.Parse(["runner", "ownership", "r-1", "claimed"]);

        await Assert.That(parsed).IsTypeOf<CliAction.Unknown>();
        await Assert.That(((CliAction.Unknown)parsed).Message).Contains("gg runner claim");
    }

    [Test]
    [Arguments("claim")]
    [Arguments("unclaim")]
    [Arguments("reserve")]
    [Arguments("release")]
    [Arguments("ownership")]
    public async Task A_verb_with_no_runner_says_how_to_find_one(string verb)
    {
        var parsed = CliArgs.Parse(["runner", verb]);

        await Assert.That(parsed).IsTypeOf<CliAction.Unknown>();
        await Assert.That(((CliAction.Unknown)parsed).Message)
            .StartsWith($"gg runner {verb} needs")
            .Because("the verb's own sentence, not the fall-through's claim that 'runner' is "
                   + "not a gg command - which is false about the verb and useless about the mistake.");
        await Assert.That(((CliAction.Unknown)parsed).Message).Contains("gg runners");
    }
}
