using Gg.Contracts;
using Gg.Runner;

namespace Gg.Runner.Tests;

/// <summary>
/// A runner's beat says it keeps credentials exactly when its dispatch does.
/// </summary>
/// <remarks>
/// <para>
/// <b>The declaration is only worth anything if it cannot be wrong.</b> A
/// control plane that refuses to mint a configure introduction because a runner
/// said no is acting on that runner's word; a runner that says yes and then
/// refuses puts a person back where they started, having typed a secret for
/// nothing. Both failures come from the same place - two readings of the same
/// question.
/// </para>
/// <para>
/// <b>So there is one reading.</b> The composition root hands a place to keep a
/// credential to the dispatch, and the same value decides what the beat says.
/// Not the file read twice, and not a flag set beside the port: the port's
/// presence IS the answer, so the two cannot disagree without somebody deleting
/// this test to let them.
/// </para>
/// </remarks>
public class WhatARunnerSaysItTakesIsWhatItTakesTests
{
    private sealed class Quiet : IAnswersAboutItself
    {
        public LogTail Tail(int lines) => new() { Lines = [], Truncated = false };

        public RunnerStatusReport Status() => new()
        {
            Doing = "nothing",
            At = DateTimeOffset.UnixEpoch,
        };
    }

    private sealed class AStore : IKeepACredential
    {
        public bool Keep(string locator, string secret) => true;
    }

    private static RunnerAsk Configuring() => new()
    {
        Kind = RunnerAskKinds.ConfigureCredential,
        ConfigureCredential = new ConfigureCredentialAsk
        {
            Locator = "local:acme/widgets",
            Secret = "ghp-not-a-real-token-8d10",
        },
    };

    [Test]
    public async Task A_runner_wired_to_keep_one_both_says_so_and_does_it()
    {
        // THE "DOES IT" HALF. What the beat SAYS is asserted by the ratchet
        // below, because the value it reports is decided in the composition
        // root and there is nothing here to construct it from - asserting it
        // by building a bool in this file would be a test of this file.
        var dispatch = new AskDispatch(new Quiet(), new AStore());

        await Assert.That(dispatch.Answer(Configuring())).IsNotNull();
    }

    [Test]
    public async Task A_runner_wired_with_nowhere_to_keep_one_says_so_and_refuses()
    {
        var dispatch = new AskDispatch(new Quiet());

        await Assert.That(dispatch.Answer(Configuring())).IsNull()
            .Because("a runner with nowhere to keep a credential refuses for want of a "
                   + "port, and its beat must say the same thing rather than inviting "
                   + "somebody to type a secret it will drop.");

        await Assert.That(dispatch.Refused).IsEqualTo(1);
    }

    [Test]
    public async Task The_root_derives_the_claim_from_the_port_rather_than_the_file()
    {
        // A RATCHET, and the cheapest possible one: the expression that decides
        // what the beat says must be the port, not a second read of
        // accept-configured. Two readings of one question is how a runner comes
        // to claim yes and refuse - which is the failure this whole member
        // exists to prevent, so it must not be reintroduced by the wiring.
        var host = await File.ReadAllTextAsync(HostPath());

        await Assert.That(host).Contains(
            "acceptsConfiguration: keepCredential is not null", StringComparison.Ordinal)
            .Because("the port handed to the dispatch is the answer. A root that read the "
                   + "configuration file again here could say yes while the dispatch "
                   + "refuses, and nothing downstream could tell.");
    }

    private static string HostPath()
    {
        var here = new DirectoryInfo(AppContext.BaseDirectory);

        while (here is not null
            && !File.Exists(Path.Combine(here.FullName, "Gg.Runner", "RunnerHost.cs")))
        {
            here = here.Parent;
        }

        if (here is null)
        {
            throw new InvalidOperationException(
                "Gg.Runner/RunnerHost.cs is not above this test's output directory, so this "
              + "walk would assert over nothing.");
        }

        return Path.Combine(here.FullName, "Gg.Runner", "RunnerHost.cs");
    }
}
