using Gg.Client;

namespace Gg.Client.Tests;

/// <summary>
/// A runner's key is trusted once and checked every time after.
/// </summary>
/// <remarks>
/// <para>
/// <b>Because the console learns a runner's key from the control plane.</b>
/// ADR-0013 pins identity at registration so a compromised signalling server
/// cannot substitute fingerprints, and that is weaker than it reads: the key
/// arrives over the introduction, so sealing to whatever comes back defends
/// against a network rather than against the control plane. Trust on first use
/// closes the rest.
/// </para>
/// <para>
/// <b>Refusing rather than warning is a decision about people, not about
/// cryptography.</b> A reinstall rotates a runner's key legitimately, so the
/// check fires on the common case — and a warning that fires on the common case
/// is one people learn to click through. That is how ssh's host-key prompt fails
/// in practice, and it is the failure this shape is chosen to avoid.
/// </para>
/// </remarks>
public class PinnedRunnerKeyTests
{
    private static readonly DateTimeOffset T0 = new(2026, 9, 8, 5, 0, 0, TimeSpan.Zero);

    private static PinnedRunnerKeys InADirectoryOfItsOwn() =>
        new(Path.Combine(
            Directory.CreateTempSubdirectory("gg-pins-").FullName, "pinned-runner-keys.json"));

    [Test]
    public async Task A_runner_never_met_is_pinned_and_says_so()
    {
        var pins = InADirectoryOfItsOwn();

        await Assert.That(pins.Check("runner-a", "key-one", T0)).IsEqualTo(PinVerdict.Pinned)
            .Because("a first introduction and a matching one are both fine and are different "
                   + "facts; a caller that could not tell them apart could not tell a person "
                   + "whether this machine had met the runner before.");
    }

    [Test]
    public async Task The_same_key_matches_the_next_time()
    {
        var pins = InADirectoryOfItsOwn();
        pins.Check("runner-a", "key-one", T0);

        await Assert.That(pins.Check("runner-a", "key-one", T0.AddHours(1)))
            .IsEqualTo(PinVerdict.Matches);
    }

    [Test]
    public async Task A_changed_key_is_refused_rather_than_re_pinned()
    {
        // THE WHOLE POINT. A check that quietly recorded the new key would be a
        // file that always agrees with whatever it was just handed.
        var pins = InADirectoryOfItsOwn();
        pins.Check("runner-a", "key-one", T0);

        await Assert.That(pins.Check("runner-a", "key-two", T0.AddHours(1)))
            .IsEqualTo(PinVerdict.Changed);

        await Assert.That(pins.Read()["runner-a"].PublicKey).IsEqualTo("key-one")
            .Because("a refusal that moved the pin anyway would refuse once and then agree "
                   + "with the substitute forever.");
    }

    [Test]
    public async Task Re_pinning_is_the_one_way_through_and_it_is_explicit()
    {
        var pins = InADirectoryOfItsOwn();
        pins.Check("runner-a", "key-one", T0);
        pins.Check("runner-a", "key-two", T0.AddHours(1));

        pins.Repin("runner-a", "key-two", T0.AddHours(2));

        await Assert.That(pins.Check("runner-a", "key-two", T0.AddHours(3)))
            .IsEqualTo(PinVerdict.Matches)
            .Because("a reinstall is the common cause, so recovery has to be possible - and "
                   + "deliberate, so the rare cause stays visible.");
    }

    [Test]
    public async Task One_runners_key_changing_does_not_disturb_another()
    {
        var pins = InADirectoryOfItsOwn();
        pins.Check("runner-a", "key-one", T0);
        pins.Check("runner-b", "key-two", T0);

        pins.Repin("runner-a", "key-three", T0.AddHours(1));

        await Assert.That(pins.Check("runner-b", "key-two", T0.AddHours(2)))
            .IsEqualTo(PinVerdict.Matches)
            .Because("a rewrite that dropped the others would silently un-pin a fleet.");
        await Assert.That(pins.Read().Count).IsEqualTo(2);
    }

    [Test]
    public async Task Checking_pins_as_it_answers_so_it_cannot_be_half_used()
    {
        // A SEPARATE "now record it" CALL IS ONE A CALLER CAN FORGET, and the
        // forgetting is invisible: every introduction would read as a first one
        // and a substitution would never be noticed.
        var pins = InADirectoryOfItsOwn();
        pins.Check("runner-a", "key-one", T0);

        await Assert.That(File.Exists(pins.FilePath)).IsTrue();
        await Assert.That(pins.Read()["runner-a"].PinnedAt).IsEqualTo(T0)
            .Because("when this machine first met the runner is what a person needs to judge "
                   + "a change against.");
    }

    [Test]
    public async Task A_file_nobody_can_read_is_loud_rather_than_empty()
    {
        // THE ONE PLACE THIS DISTINCTION IS DANGEROUS. Returning "no pins" would
        // silently re-pin every runner on the next introduction, which is
        // exactly what an attacker would want the file to do.
        var path = Path.Combine(
            Directory.CreateTempSubdirectory("gg-pins-bad-").FullName, "pinned-runner-keys.json");
        File.WriteAllText(path, "this is not json");

        var pins = new PinnedRunnerKeys(path);

        var thrown = Assert.Throws<PinnedKeysUnreadableException>(() => pins.Read());

        await Assert.That(thrown!.Message).Contains("could not be read");
        await Assert.That(thrown.Message).Contains(path)
            .Because("a person cannot fix or delete a file the message does not name.");
    }

    [Test]
    public async Task Forgetting_a_pin_lets_the_next_introduction_trust_afresh()
    {
        // THE ONE WAY THROUGH, and it forgets rather than accepts: at the moment
        // a person runs this the console is not talking to the runner, so there
        // is no key to accept - and taking one on a command line would invite
        // pasting it from the same place the wrong key came from.
        var pins = InADirectoryOfItsOwn();
        pins.Check("runner-a", "key-one", T0);

        await Assert.That(pins.Forget("runner-a")).IsTrue();

        await Assert.That(pins.Check("runner-a", "key-two", T0.AddHours(1)))
            .IsEqualTo(PinVerdict.Pinned)
            .Because("forgetting has to make the next one a FIRST introduction, or the "
                   + "recovery does not recover anything.");
    }

    [Test]
    public async Task Forgetting_what_was_never_pinned_is_answered_rather_than_thrown()
    {
        // "Make sure this runner is not pinned" has to be one call, exactly as
        // releasing a reservation nobody holds is - and a person who ran it twice
        // should be told what happened rather than shown an error.
        var pins = InADirectoryOfItsOwn();

        await Assert.That(pins.Forget("never-met")).IsFalse();
    }

    [Test]
    public async Task Forgetting_one_leaves_the_others_pinned()
    {
        var pins = InADirectoryOfItsOwn();
        pins.Check("runner-a", "key-one", T0);
        pins.Check("runner-b", "key-two", T0);

        pins.Forget("runner-a");

        await Assert.That(pins.Check("runner-b", "key-two", T0.AddHours(1)))
            .IsEqualTo(PinVerdict.Matches)
            .Because("a repin that un-pinned a fleet would be a worse cure than the disease.");
    }

    [Test]
    public async Task It_lives_beside_the_runner_credential_rather_than_in_state()
    {
        // A pin is not runtime state. Losing it silently would make every runner
        // look new, which is the one failure that turns this file into
        // decoration.
        await Assert.That(Path.GetDirectoryName(PinnedRunnerKeys.DefaultPath()))
            .IsEqualTo(Path.GetDirectoryName(FileRunnerStore.DefaultPath()));
    }
}
