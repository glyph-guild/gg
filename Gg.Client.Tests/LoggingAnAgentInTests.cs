using Gg.Contracts;

namespace Gg.Client.Tests;

/// <summary>
/// Logging a runner's agent in from a console: two asks over one conversation,
/// with a person and a browser between them.
/// </summary>
/// <remarks>
/// <para>
/// <b>The same reach as a send, and the same purpose</b>: an introduction
/// minted to configure this runner, because what the ceremony does is place a
/// credential on it - the runner mints the value itself instead of being
/// handed one, and that is a difference in where the secret comes from, not
/// in what the machine ends up holding.
/// </para>
/// <para>
/// <b>An answer of another kind is not this ask's answer.</b> There is no
/// correlation id on the channel; a late <c>begin</c> answer must not land in
/// the <c>finish</c> wait and read as a login that was never finished.
/// </para>
/// </remarks>
public class LoggingAnAgentInTests
{
    [Test]
    public async Task Beginning_and_finishing_are_the_two_kinds_with_their_payloads()
    {
        var begin = LogAnAgentIn.Beginning("claude");
        var finish = LogAnAgentIn.Finishing("claude", "the-code");

        await Assert.That(begin.Kind).IsEqualTo(RunnerAskKinds.BeginAgentLogin);
        await Assert.That(begin.BeginAgentLogin!.Provider).IsEqualTo("claude");
        await Assert.That(begin.FinishAgentLogin).IsNull();

        await Assert.That(finish.Kind).IsEqualTo(RunnerAskKinds.FinishAgentLogin);
        await Assert.That(finish.FinishAgentLogin!.Provider).IsEqualTo("claude");
        await Assert.That(finish.FinishAgentLogin.Code).IsEqualTo("the-code");
        await Assert.That(finish.BeginAgentLogin).IsNull();
    }

    [Test]
    public async Task Its_purpose_is_to_configure_the_runner()
    {
        await Assert.That(LogAnAgentIn.Purpose).IsEqualTo(RunnerCapabilityPurposes.ConfigureThisRunner);
        await Assert.That(LogAnAgentIn.Purpose).IsEqualTo(SendACredential.Purpose)
            .Because("the ceremony places a credential on the runner; it is the send's act "
                   + "with the runner minting the value.");
        await Assert.That(RunnerCapabilityPurposes.Refused(LogAnAgentIn.Purpose)).IsNull();
    }

    [Test]
    public async Task An_answer_of_another_kind_is_not_this_asks_answer()
    {
        var lateBegin = new RunnerSaid
        {
            Kind = RunnerAskKinds.BeginAgentLogin,
            LoginBegun = new AgentLoginBegun { Provider = "claude", Started = true, Url = "https://x" },
        };
        var finished = new RunnerSaid
        {
            Kind = RunnerAskKinds.FinishAgentLogin,
            LoginFinished = new AgentLoginFinished
            {
                Provider = "claude",
                Locator = "local:agent/claude",
                Written = true,
            },
        };

        await Assert.That(LogAnAgentIn.Answers(lateBegin, RunnerAskKinds.FinishAgentLogin)).IsFalse();
        await Assert.That(LogAnAgentIn.Answers(finished, RunnerAskKinds.FinishAgentLogin)).IsTrue();
        await Assert.That(LogAnAgentIn.Answers(null, RunnerAskKinds.FinishAgentLogin)).IsFalse();
        await Assert.That(LogAnAgentIn.Answers(
                new RunnerSaid { Kind = RunnerAskKinds.FinishAgentLogin }, RunnerAskKinds.FinishAgentLogin))
            .IsFalse()
            .Because("the kind alone with no payload is not an answer of that kind.");
    }

    [Test]
    public async Task Silence_names_the_setting_and_the_version()
    {
        var said = LogAnAgentIn.SaidWhenNothingCameBack("gg-pool-ui-3");

        await Assert.That(said).Contains("gg-pool-ui-3");
        await Assert.That(said).Contains("accept-agent-login")
            .Because("the likeliest cause is a decision on that machine, and the sentence says "
                   + "which one.");
        await Assert.That(said).Contains("gg --version")
            .Because("the other cause is a runner too old to have the arm at all.");
        await Assert.That(said).Contains("credential send")
            .Because("a member is closed to the ceremony by decision, and the other way in is "
                   + "named right here.");
    }

    [Test]
    public async Task The_code_prompt_says_it_is_not_echoed()
    {
        await Assert.That(LogAnAgentIn.CodePrompt).Contains("not echoed");
        await Assert.That(LogAnAgentIn.CodePrompt).Contains("browser");
    }
}
