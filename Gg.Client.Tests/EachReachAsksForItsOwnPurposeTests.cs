using Gg.Client;
using Gg.Contracts;

namespace Gg.Client.Tests;

/// <summary>
/// The two things a console reaches a runner for ask for different capabilities.
/// </summary>
/// <remarks>
/// <para>
/// <b>Which is the whole point of a purpose existing.</b> An introduction minted
/// to read a log must not also place a credential — that is what
/// <see cref="RunnerCapabilityPurposes"/> says it is for, and until a console
/// could say which it wanted, every introduction was minted for the same one and
/// the narrowing was decoration.
/// </para>
/// <para>
/// <b>Named on each caller rather than passed at the call site.</b> A literal in
/// the middle of a method is a decision nothing can assert about; these two are
/// the decision, and this file is what makes a third reach declare its own
/// rather than inherit whichever was nearest.
/// </para>
/// <para>
/// <b>It is still not a runner-side check, and that has not changed.</b> The
/// runner cannot verify a capability, so this is the control plane's refusal and
/// the flight log's record of what a console said it was for. What restrains a
/// write on the machine is its own <c>accept-configured</c>.
/// </para>
/// </remarks>
public class EachReachAsksForItsOwnPurposeTests
{
    [Test]
    public async Task Watching_asks_to_tail_a_log()
    {
        await Assert.That(WatchARunner.Purpose)
            .IsEqualTo(RunnerCapabilityPurposes.TailYourOwnLog);
    }

    [Test]
    public async Task Sending_a_credential_asks_to_configure()
    {
        await Assert.That(SendACredential.Purpose)
            .IsEqualTo(RunnerCapabilityPurposes.ConfigureThisRunner);
    }

    [Test]
    public async Task They_are_not_the_same_capability()
    {
        // THE ASSERTION THE OTHER TWO EXIST FOR. Both passing while naming one
        // value would be the state this change was written to end, and a test
        // that only checked each against a constant would go green on it.
        await Assert.That(SendACredential.Purpose).IsNotEqualTo(WatchARunner.Purpose)
            .Because("an introduction minted to read a log must not also place a credential.");
    }

    [Test]
    public async Task Both_are_purposes_the_contract_declares()
    {
        foreach (var purpose in (string[])[WatchARunner.Purpose, SendACredential.Purpose])
        {
            await Assert.That(RunnerCapabilityPurposes.Refused(purpose)).IsNull()
                .Because($"'{purpose}' is asked for on every reach of its kind, so a value "
                       + "the contract would turn away is a console that cannot connect "
                       + "at all.");
        }
    }
}
