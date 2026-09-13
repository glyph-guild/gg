using Gg.Contracts;
using Gg.Runner;

namespace Gg.Runner.Tests;

/// <summary>
/// A runner drops a credential its control plane says to drop, whether or not
/// it is flying anything.
/// </summary>
/// <remarks>
/// <para>
/// <b>ON EVERY BEAT, unlike its two neighbours, and the difference is the
/// point.</b> An introduction is answered only inside a hold - <i>not a check,
/// but a session that only exists while a flight does</i> - and an offer is
/// taken only while idle, because acting on one ends the process. Forgetting is
/// neither: a credential that has been revoked is revoked while the machine is
/// busy, and a runner that waited for a gap in its work would hold a live
/// secret for exactly as long as it was useful to somebody.
/// </para>
/// <para>
/// <b>And it is NOT gated by <c>accept-configured</c>.</b> That setting says a
/// person may PUT a credential here; this takes one away. A machine that would
/// not forget when told is a liability, and the asymmetry is deliberate: the
/// two ports are separate so that wiring one cannot wire the other by
/// accident.
/// </para>
/// <para>
/// <b>The locator is validated before it becomes a path</b>, for the reason it
/// is on the way in. This one arrives from a control plane rather than a
/// console, which is the case <c>CredentialStore</c>'s own remark was written
/// about: <i>by the time a runner sees a locator it is data that came back from
/// the control plane, and a path it could steer is a path it could steer
/// anywhere.</i> Deleting is the direction where steering one costs the most.
/// </para>
/// </remarks>
public class ARunnerForgetsWhatItIsToldToTests
{
    /// <summary>A store that remembers what it was told to drop.</summary>
    private sealed class AForgetfulStore : IForgetACredential
    {
        public List<string> Forgotten { get; } = [];

        public bool Forget(string locator)
        {
            Forgotten.Add(locator);
            return true;
        }
    }

    [Test]
    public async Task What_a_beat_names_is_dropped()
    {
        var store = new AForgetfulStore();

        ForgetsWhatItIsTold.Apply(
            ["local:acme/widgets", "local:acme/payments"], store);

        await Assert.That(store.Forgotten)
            .IsEquivalentTo(new[] { "local:acme/widgets", "local:acme/payments" });
    }

    [Test]
    public async Task A_beat_naming_nothing_drops_nothing()
    {
        var store = new AForgetfulStore();

        ForgetsWhatItIsTold.Apply(null, store);
        ForgetsWhatItIsTold.Apply([], store);

        await Assert.That(store.Forgotten).IsEmpty()
            .Because("absence is nothing to do, never everything to drop. A runner ahead of "
                   + "its control plane reads a missing member every single beat.");
    }

    [Test]
    public async Task A_locator_that_could_steer_a_path_is_refused_rather_than_deleted()
    {
        // REFUSED RATHER THAN SANITISED, on the way out as on the way in - and
        // deleting is the direction where steering one costs the most. A runner
        // that took `../../etc/passwd` from its control plane and handed it to
        // a remove would be a control plane with a delete primitive pointed at
        // the whole disk.
        var store = new AForgetfulStore();

        ForgetsWhatItIsTold.Apply(
            ["../../etc/passwd", "local:../../etc/passwd", "/etc/passwd", "local:"], store);

        await Assert.That(store.Forgotten).IsEmpty();
    }

    [Test]
    public async Task One_that_will_not_drop_does_not_stop_the_others()
    {
        // A REVOCATION IS A LIST AND THE LIST MATTERS. Stopping at the first
        // failure would leave later locators held because an earlier one was
        // already gone, which is the ordinary case rather than the rare one:
        // a control plane repeats itself until it is sure.
        var store = new ARefusingStore();

        ForgetsWhatItIsTold.Apply(
            ["local:acme/widgets", "local:acme/payments"], store);

        await Assert.That(store.Asked)
            .IsEquivalentTo(new[] { "local:acme/widgets", "local:acme/payments" });
    }

    private sealed class ARefusingStore : IForgetACredential
    {
        public List<string> Asked { get; } = [];

        public bool Forget(string locator)
        {
            Asked.Add(locator);
            throw new IOException("read-only mount");
        }
    }
}
