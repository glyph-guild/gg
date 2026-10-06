using System.Security.Cryptography;
using Gg.Client;
using Gg.Contracts;

namespace Gg.Client.Tests;

/// <summary>
/// The machine's own key reaches a rewrap through the same seam a person's does,
/// so sealing at rest is unchanged and there is only one path.
/// </summary>
/// <remarks>
/// <para>
/// <b>S64.1-02, which is the half that keeps step 1 from being a widening.</b>
/// Changing <c>Rewrap</c> to take an agreement would be worth nothing if the
/// machine kept a second overload taking its key: every existing caller already
/// takes that one, so it would be the path in use and a person's key would still
/// be unreachable. One seam, taken by both.
/// </para>
/// <para>
/// <b>The machine keeps the store and loses only the push</b> (Decision 4).
/// Sealing a credential at rest, and opening one with nobody present, stay the
/// machine's job — a runner has to resolve a credential at three in the morning.
/// What moves to the person is the act of MOVING one. So
/// <c>CredentialSeal.Open</c> keeps its machine-key form on purpose, and this
/// file asserts that it does, because deleting it would stop a runner working
/// and no test about a push would notice.
/// </para>
/// <para>
/// <b>And the derivation is declared once.</b> Before this step
/// <c>PersonKey.AgreeWith</c> was a byte-for-byte copy of <c>RunnerSeal</c>'s
/// private <c>Agree</c> — same hash, same length, same labelled info — which is
/// the <i>"two derivations that agree today"</i> hazard <c>Credentials.cs</c> is
/// written to prevent, sitting in the middle of the cryptography. The two agree
/// today; the one that drifted would produce an envelope that opens for the
/// sender and not the recipient.
/// </para>
/// </remarks>
public class AMachineKeyAgreesTheSameWayTests
{
    private const string Value = "a-token-nobody-should-see";

    private static string APath() =>
        Path.Combine(Path.GetTempPath(), "gg-machine-" + Guid.NewGuid().ToString("N"), "machine-key");

    private static string PublicHalf(ECDiffieHellman key) =>
        Convert.ToBase64String(key.PublicKey.ExportSubjectPublicKeyInfo());

    [Test]
    public async Task A_machine_rewraps_through_the_agreement_seam()
    {
        var machine = MachineKey.LoadOrCreate(APath());
        using var runner = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);

        var mine = CredentialSeal.Seal(Value, [machine.PublicKey]);
        var theirs = CredentialSeal.Rewrap(mine, machine, PublicHalf(runner));

        await Assert.That(CredentialSeal.Open(theirs, runner)).IsEqualTo(Value)
            .Because("a host reselling to its own members is Decision 5's recursion, and it is a "
                   + "machine doing the rewrapping with nobody present.");
    }

    [Test]
    public async Task Both_kinds_of_key_satisfy_one_seam()
    {
        // THE POINT, STATED AS A TYPE QUESTION. If these are two interfaces, or
        // one of them is a concrete type, then Rewrap has two callers' shapes to
        // satisfy and the next key added will make three.
        await Assert.That(typeof(IAgreeAsAHolder).IsAssignableFrom(typeof(MachineKey))).IsTrue()
            .Because("the machine's key is a holder's key that happens to be a file.");

        await Assert.That(typeof(IAgreeAsAHolder).IsAssignableFrom(typeof(PersonKey))).IsTrue()
            .Because("a person's key is a holder's key that may one day be a token, which is the "
                   + "whole reason the seam is an agreement rather than a key.");
    }

    [Test]
    public async Task A_machine_still_opens_what_it_sealed_for_itself()
    {
        // DECISION 4, ASSERTED SO IT CANNOT BE TIDIED AWAY. Nothing about a push
        // would fail if this stopped working; a runner would, at three in the
        // morning, with no person to ask.
        var machine = MachineKey.LoadOrCreate(APath());

        var mine = CredentialSeal.Seal(Value, [machine.PublicKey]);

        await Assert.That(CredentialSeal.Open(mine, machine.ForOpeningWhatThisMachineSealed()))
            .IsEqualTo(Value)
            .Because("the machine path's key IS a file on the machine by necessity, so this "
                   + "overload is right and is kept deliberately.");
    }

    [Test]
    public async Task The_two_derivations_are_one_derivation()
    {
        // MEASURED RATHER THAN READ. If PersonKey's HKDF and RunnerSeal's ever
        // part company, the symptom is an envelope that opens for whoever sealed
        // it and nobody else - so the check is that bytes sealed by one open
        // under the other, both ways round.
        var machine = MachineKey.LoadOrCreate(APath());
        using var other = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);

        // BOTH DIRECTIONS IN ONE PASS. The seal wraps the content key with
        // RunnerSeal's own ephemeral agreement; MachineKey's adapter has to
        // unwrap THAT, and what it wraps for the recipient has to open under
        // RunnerSeal's agreement in turn. Either half drifting breaks this.
        var mine = CredentialSeal.Seal(Value, [machine.PublicKey]);
        var alsoOther = CredentialSeal.Rewrap(mine, machine, PublicHalf(other));

        await Assert.That(CredentialSeal.Open(alsoOther, other)).IsEqualTo(Value)
            .Because("what one side wrapped, the other unwraps - which is only true while there is "
                   + "one labelled derivation rather than two that agree today.");
    }
}
