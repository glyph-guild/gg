using System.Reflection;
using Gg.Contracts;
using Gg.Contracts.Description;

namespace Gg.Contracts.Tests;

/// <summary>
/// A runner says on its beat whether it will keep a credential, so nobody types
/// one for a machine that will not.
/// </summary>
/// <remarks>
/// <para>
/// <b>The refusal happens after the secret has crossed, which is the whole
/// defect.</b> A person is asked for a token, it is sealed to the runner's key,
/// it goes over the channel — and only then does the runner say it was handed
/// nowhere to keep one. Nothing before that moment could know: the answer lives
/// in the machine's own file, and the control plane has never been told it.
/// </para>
/// <para>
/// <b>What it is NOT.</b> It is not the authorisation. Only the principal who
/// REGISTERED a runner may be introduced to it, and that already answers "may
/// this caller" — <c>accept-configured</c> answers the different question of
/// whether the machine will take one from anybody. Declaring it does not widen
/// or narrow who may ask; it moves WHEN they find out.
/// </para>
/// <para>
/// <b>The machine stays the authority, and a lie gains nothing.</b> A runner
/// that claimed to accept and then refused is where we are today. One that
/// claimed not to accept simply is not offered the chance. Either way the file
/// on the machine is what actually decides, which is the right place for a
/// decision about that machine's disk.
/// </para>
/// <para>
/// <b>Nullable, for the rule its two neighbours follow.</b> A runner that
/// predates this member says nothing, and a control plane that hears nothing
/// must behave exactly as it did — which is to mint, and let the far end
/// refuse. Absence is an older runner, never a refusal.
/// </para>
/// </remarks>
public class ARunnerSaysWhetherItMayBeConfiguredTests
{
    [Test]
    public async Task A_beat_can_carry_whether_this_machine_keeps_a_credential()
    {
        var member = typeof(RunnerHeartbeat).GetProperty("AcceptsConfiguration");

        await Assert.That(member).IsNotNull();

        await Assert.That(member!.PropertyType).IsEqualTo(typeof(bool?))
            .Because("three states and not two: yes, no, and an older runner that was never "
                   + "asked. Collapsing the third into 'no' would stop every runner in the "
                   + "field being configurable the day this shipped.");
    }

    [Test]
    public async Task An_older_runner_says_nothing_and_is_treated_as_it_always_was()
    {
        var member = typeof(RunnerHeartbeat).GetProperty("AcceptsConfiguration")!;

        await Assert.That(new NullabilityInfoContext().Create(member).WriteState)
            .IsEqualTo(NullabilityState.Nullable);

        await Assert.That(new RunnerHeartbeat { Labels = [] }.AcceptsConfiguration).IsNull()
            .Because("absence is an older runner, never a refusal - and a control plane that "
                   + "read it as one would make every machine in the field unconfigurable "
                   + "the first time the two repositories were a version apart.");
    }

    [Test]
    public async Task It_is_declared_like_every_other_member_of_the_beat()
    {
        await Assert.That(ProtocolSurface.JsonMembers[typeof(RunnerHeartbeat)])
            .Contains("acceptsConfiguration")
            .Because("a member nobody wrote down is a member nobody can audit, and this one "
                   + "decides whether a person is asked for a secret at all.");
    }

    [Test]
    public async Task The_beat_still_carries_nothing_that_could_be_a_secret()
    {
        // THE RULE THIS TYPE HAS ALWAYS HAD. A heartbeat is liveness and
        // capability; it is not a place to report what the machine holds. A
        // bool saying "I would keep one" is a fact about configuration, and a
        // member naming WHICH credentials it holds would be a fact about the
        // customer that nobody agreed to publish.
        foreach (var member in typeof(RunnerHeartbeat).GetProperties())
        {
            foreach (var word in (string[])
                ["secret", "token", "password", "credential", "locator"])
            {
                await Assert.That(member.Name.Contains(word, StringComparison.OrdinalIgnoreCase))
                    .IsFalse()
                    .Because($"'{member.Name}' names credential material on the one message "
                           + "every runner sends every few seconds.");
            }
        }
    }
}
