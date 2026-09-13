using System.Reflection;
using Gg.Contracts;
using Gg.Contracts.Description;

namespace Gg.Contracts.Tests;

/// <summary>
/// An introduction carries what it is for, and a purpose nobody can ask for is
/// not a narrowing.
/// </summary>
/// <remarks>
/// <para>
/// <b><c>configure-this-runner</c> was a vocabulary value nothing could
/// reach.</b> It shipped with the ask kind that needed it, and
/// <see cref="RunnerIntroductionRequest"/> carries only an ephemeral key — so a
/// console had no way to say what it wanted an introduction for, and the control
/// plane mints <c>tail-your-own-log</c> for every one. A closed vocabulary with
/// an unreachable member is the <i>port nothing calls</i> shape, on the half of
/// the surface that is supposed to be the narrowing.
/// </para>
/// <para>
/// <b>It cost nothing, and that is the reason to fix it rather than to
/// shrug.</b> The runner cannot verify a capability at all —
/// <see cref="RunnerSealedOffer"/> records why one was taken out of the offer:
/// <i>an unverifiable field that looks like a check is worse than no field.</i>
/// So this purpose is the control plane's refusal and the flight log's record of
/// what a console said it was for, and neither can happen while the console
/// cannot say.
/// </para>
/// <para>
/// <b>Nullable, because the two repositories are not upgraded in step.</b> A
/// console that predates this sends no purpose and must go on being introduced
/// exactly as it was — which is why absence resolves to
/// <c>tail-your-own-log</c> rather than being refused. The rule lives on the
/// contract for <see cref="RunnerSeal"/>'s reason: one rule both sides compile
/// against, rather than two that agree today.
/// </para>
/// </remarks>
public class AConsoleSaysWhatAnIntroductionIsForTests
{
    [Test]
    public async Task An_introduction_can_be_asked_for_by_purpose()
    {
        var member = typeof(RunnerIntroductionRequest).GetProperty("Purpose");

        await Assert.That(member).IsNotNull();

        await Assert.That(new NullabilityInfoContext().Create(member!).WriteState)
            .IsEqualTo(NullabilityState.Nullable)
            .Because("a console one version behind sends none, and must go on being "
                   + "introduced exactly as it was.");
    }

    [Test]
    public async Task It_is_declared_like_every_other_member()
    {
        await Assert.That(ProtocolSurface.JsonMembers[typeof(RunnerIntroductionRequest)])
            .Contains("purpose")
            .Because("a member nobody wrote down is a member nobody can audit, and this one "
                   + "decides what a capability authorises.");
    }

    [Test]
    public async Task Absence_is_the_purpose_every_console_has_always_asked_for()
    {
        // BYTE-FOR-BYTE WHAT IT WAS. The control plane has minted
        // tail-your-own-log for every introduction ever made, so a request
        // without a purpose has to go on meaning exactly that - anything else
        // would change what an older console gets without that console changing.
        await Assert.That(RunnerCapabilityPurposes.Requested(null))
            .IsEqualTo(RunnerCapabilityPurposes.TailYourOwnLog);

        await Assert.That(RunnerCapabilityPurposes.Requested(""))
            .IsEqualTo(RunnerCapabilityPurposes.TailYourOwnLog)
            .Because("an empty string is a member that serialized to nothing, not a purpose.");
    }

    [Test]
    public async Task A_purpose_that_was_asked_for_is_the_one_that_is_minted()
    {
        await Assert.That(RunnerCapabilityPurposes.Requested(
                RunnerCapabilityPurposes.ConfigureThisRunner))
            .IsEqualTo(RunnerCapabilityPurposes.ConfigureThisRunner);
    }

    [Test]
    public async Task A_purpose_nobody_declared_is_refused_and_says_what_is_allowed()
    {
        // THE RULE ON THE CONTRACT, so the control plane refuses by the same
        // list gg offers from. A free string here would make "one purpose" a
        // description of today, which is the closure's own argument.
        var refused = RunnerCapabilityPurposes.Refused("run-anything");

        await Assert.That(refused).IsNotNull();
        await Assert.That(refused!).Contains(RunnerCapabilityPurposes.TailYourOwnLog)
            .Because("a refusal that names a problem and hides what was allowed is half a "
                   + "sentence.");

        await Assert.That(RunnerCapabilityPurposes.Refused(null)).IsNull()
            .Because("absence is an older console, not a bad one.");
    }

    [Test]
    public async Task Every_declared_purpose_survives_the_rule_that_guards_it()
    {
        // DISCOVERED FROM THE VOCABULARY rather than listed, so the next value
        // is covered the day it is written. A purpose in All that Refused turns
        // away would be a value the build offers and the wire rejects.
        foreach (var purpose in RunnerCapabilityPurposes.All)
        {
            await Assert.That(RunnerCapabilityPurposes.Refused(purpose)).IsNull()
                .Because($"'{purpose}' is declared, so asking for it must not be refused.");

            await Assert.That(RunnerCapabilityPurposes.Requested(purpose)).IsEqualTo(purpose);
        }
    }
}
