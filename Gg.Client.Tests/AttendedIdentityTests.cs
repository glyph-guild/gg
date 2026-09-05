using Gg.Client;

namespace Gg.Client.Tests;

/// <summary>
/// The runner a person flies with is this machine's, and there is one of it.
/// </summary>
/// <remarks>
/// <para>
/// <b>Its own slot, because a pool host already proved one file is not
/// enough.</b> A host runs <c>gg runner up</c> as itself and
/// <c>gg runner maintain</c> as <c>&lt;machine&gt;:maintain</c>, and a single
/// file meant whichever registered last owned the only credential.
/// A hand-flight is a third runner on the same machine and would have been the
/// third claimant on that file.
/// </para>
/// <para>
/// <b>And read-or-register, because registering per start is a defect this
/// product has already shipped once.</b> <c>gg runner up</c> registered
/// unconditionally and one host appeared as eleven runners in <c>gg runners</c>,
/// ten of them permanently offline — one per restart. A hand-flight starts far
/// more often than a fleet runner does, so the same mistake here would be
/// louder rather than quieter.
/// </para>
/// </remarks>
public class AttendedIdentityTests
{
    private static string Slot() =>
        Path.Combine(Path.GetTempPath(), "gg-attended-" + Guid.NewGuid().ToString("n")[..8]);

    private static StoredRunner Fresh(string id, DateTimeOffset expires) => new()
    {
        RunnerId = id,
        RunnerToken = "token-" + id,
        ExpiresAt = expires,
    };

    // ---- S26.4-05 ----

    [Test]
    public async Task The_attended_runner_has_a_name_of_its_own()
    {
        // NAMED THE WAY MAINTAIN IS, because the reason is the same one and a
        // second scheme would be a second thing to remember. The machine, then
        // what it is doing here.
        await Assert.That(AttendedRunner.NameFor("laptop-7")).IsEqualTo("laptop-7:hand");
    }

    [Test]
    public async Task Its_slot_is_not_the_one_gg_runner_up_uses()
    {
        // THE WHOLE POINT OF THE SLOT. Sharing it means a hand-flight and a
        // fleet runner on one machine take each other's credential in turn, and
        // whichever registered last wins - which is the defect PathFor exists to
        // have fixed, arriving a third time.
        var fleet = FileRunnerStore.PathFor("laptop-7");
        var hand = FileRunnerStore.PathFor(AttendedRunner.NameFor("laptop-7"));

        await Assert.That(hand).IsNotEqualTo(fleet);

        // And the maintain service's, which is the second claimant.
        await Assert.That(hand).IsNotEqualTo(FileRunnerStore.PathFor("laptop-7:maintain"));
    }

    // ---- S26.4-06 ----

    [Test]
    public async Task Repeated_hand_flights_reuse_the_slot()
    {
        // "ONE HOST APPEARED AS ELEVEN RUNNERS", named before it is repeated. A
        // person flies by hand several times a day; registering per start would
        // fill `gg runners` with offline rows faster than any fleet runner
        // could.
        var store = new FileRunnerStore(Slot());
        var now = DateTimeOffset.UtcNow;
        var registrations = 0;

        Task<StoredRunner> Register()
        {
            registrations++;
            return Task.FromResult(Fresh("runner-" + registrations, now.AddDays(30)));
        }

        var first = await RunnerIdentity.EnsureAsync(store, Register, now);
        var second = await RunnerIdentity.EnsureAsync(store, Register, now.AddHours(2));
        var third = await RunnerIdentity.EnsureAsync(store, Register, now.AddDays(1));

        await Assert.That(registrations).IsEqualTo(1);
        await Assert.That(second.RunnerId).IsEqualTo(first.RunnerId);
        await Assert.That(third.RunnerId).IsEqualTo(first.RunnerId);
    }

    [Test]
    public async Task A_lapsed_credential_registers_again_rather_than_failing_at_the_first_call()
    {
        // THE TWIN, and it is what stops the row above being "never register".
        // USABLE rather than merely present: a lapsed credential handed to the
        // protocol fails as a 401 on the first call, which is the wrong place
        // for a person to learn they need to sign in.
        var store = new FileRunnerStore(Slot());
        var now = DateTimeOffset.UtcNow;
        var registrations = 0;

        Task<StoredRunner> Register()
        {
            registrations++;
            return Task.FromResult(Fresh("runner-" + registrations, now.AddMinutes(30)));
        }

        await RunnerIdentity.EnsureAsync(store, Register, now);
        var after = await RunnerIdentity.EnsureAsync(store, Register, now.AddHours(4));

        await Assert.That(registrations).IsEqualTo(2);
        await Assert.That(after.RunnerId).IsEqualTo("runner-2");
    }

    [Test]
    public async Task The_slot_survives_the_name_being_unfit_for_a_filename()
    {
        // The maintain name carries a ':' and so does this one, and ':' is not a
        // filename character everywhere. A scheme that flattened two names to
        // one string would recreate the shared slot with extra steps.
        var file = Path.GetFileName(FileRunnerStore.PathFor(AttendedRunner.NameFor("laptop-7")));

        await Assert.That(file).DoesNotContain(":");
        await Assert.That(file).Contains("laptop-7");
        await Assert.That(file).Contains("hand")
            .Because("a slot whose name does not say what it is for is one somebody deletes.");
    }
}
