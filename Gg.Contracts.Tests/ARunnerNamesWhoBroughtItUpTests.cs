using System.Text.Json;
using Gg.Contracts;

namespace Gg.Contracts.Tests;

/// <summary>
/// A runner in the fleet names the principal that registered it.
/// </summary>
/// <remarks>
/// <para>
/// <b>What could not be said before.</b> A fleet row carried an id, a label, a
/// state, a flight and a heartbeat, and nothing about WHO. The console wanting
/// to put a person's own runners at the top of a fifteen-row fleet had one
/// proxy available - the label, which is a machine name - so "mine" could only
/// ever mean "this machine". The same person's runners on a second host sorted
/// in with everybody else's.
/// </para>
/// <para>
/// <b>An id, not a display name.</b> The control plane has recorded
/// <c>RunnerRegistered.RegisteredBy</c> since registration existed, and it is
/// <c>CurrentPrincipal.Display</c> - text a person chose, which two people can
/// share and either can change. Grouping on it would put somebody else's
/// runners in your group on a collision and drop your own on a rename. The id
/// beside it is stable and already exists; this is the half that crosses.
/// </para>
/// <para>
/// <b>Named for <see cref="WhoAmI.PrincipalId"/>, because that is what it is
/// compared against.</b> The console asks the control plane who it is and gets
/// a principal id; a fleet row saying "subject" or "owner" would make the one
/// comparison this field exists for read like a coincidence.
/// </para>
/// <para>
/// <b>Optional, and the reason is upgrade order.</b> A gg newer than the
/// control plane it is pointed at receives a payload without this member, and
/// <c>required</c> would turn that into a deserialization failure - the whole
/// fleet pane, not one column. Every runner registered BEFORE this shipped also
/// has no principal id in its stored event, so empty is a real and permanent
/// answer meaning "nobody recorded", not a transitional one.
/// </para>
/// </remarks>
public class ARunnerNamesWhoBroughtItUpTests
{
    private const string Principal = "01a062f3-42a5-73a4-8bf5-29a4bbb36533";

    private static RunnerSummary ARunner(string registeredBy) => new()
    {
        RunnerId = "01a078bb-4b97-779b-81ff-554c4ea662c0",
        Label = "Kevins-MBP",
        State = RunnerStates.Idle,
        RegisteredByPrincipalId = registeredBy,
    };

    [Test]
    public async Task A_runner_names_the_principal_that_registered_it()
    {
        await Assert.That(ARunner(Principal).RegisteredByPrincipalId).IsEqualTo(Principal);
    }

    [Test]
    public async Task It_is_the_same_value_whoami_answers_with()
    {
        // THE ONE COMPARISON THIS EXISTS FOR, asserted as a comparison rather
        // than as two fields that happen to be strings. If either side ever
        // becomes a different kind of identifier - a display name, an email, a
        // per-tenant membership id - this is where it is noticed.
        var me = new WhoAmI
        {
            PrincipalId = Principal,
            PrincipalDisplay = "Kevin Deenanauth",
            TenantId = "01a062f3-0000-7000-8000-000000000000",
            ExpiresAt = DateTimeOffset.UnixEpoch,
        };

        await Assert.That(ARunner(Principal).RegisteredByPrincipalId).IsEqualTo(me.PrincipalId);
    }

    [Test]
    public async Task A_control_plane_that_does_not_send_it_still_binds()
    {
        // OLDER THAN THIS GG, which is an ordinary state of the world and not a
        // failure: the field is added here first and deployed after. `required`
        // would make that window cost the whole fleet pane rather than one
        // column of it.
        var payload = """
            {"runnerId":"01a078bb-4b97-779b-81ff-554c4ea662c0",
             "label":"Kevins-MBP","state":"idle"}
            """;

        var runner = JsonSerializer.Deserialize<RunnerSummary>(
            payload, JsonSerializerOptions.Web);

        await Assert.That(runner).IsNotNull();
        await Assert.That(runner!.RegisteredByPrincipalId).IsEmpty()
            .Because("nobody recorded is empty, and a consumer grouping on it must find "
                   + "no match rather than a null to dereference.");
    }

    [Test]
    public async Task A_runner_also_names_the_person_a_reader_would_recognise()
    {
        // BOTH HALVES CROSS, and they are for different readers. The id is for
        // the comparison above - it decides which rows are yours. This is for
        // the person looking at the screen, who cannot recognise a uuid and
        // should not be asked to.
        //
        // Which is why the ordering and the marks key on the ID and the modal
        // shows the NAME: a display name two people can share would put
        // somebody else's runner in your group, and an id nobody can read
        // would tell a person nothing about who to go and ask.
        var runner = ARunner(Principal) with { RegisteredBy = "Kevin Deenanauth" };

        await Assert.That(runner.RegisteredBy).IsEqualTo("Kevin Deenanauth");
    }

    [Test]
    public async Task The_name_is_optional_for_a_different_reason_than_the_id()
    {
        // THE ID NEEDED A BACKFILL AND THIS DOES NOT. RegisteredBy has been
        // recorded on the registration event since registration existed and the
        // lens has always projected it, so every runner in the store already has
        // one; the id was added later and no replay can invent it. Both are
        // init-only all the same, because an older control plane sends neither.
        var payload = """
            {"runnerId":"01a078bb-4b97-779b-81ff-554c4ea662c0",
             "label":"Kevins-MBP","state":"idle"}
            """;

        var runner = JsonSerializer.Deserialize<RunnerSummary>(payload, JsonSerializerOptions.Web);

        await Assert.That(runner!.RegisteredBy).IsEmpty();
    }

    [Test]
    public async Task A_runner_nobody_can_be_attributed_to_is_not_an_error()
    {
        // Every runner registered before this shipped is in this state
        // permanently - the principal id is not in its stored event and no
        // replay can invent one. Fourteen of the fifteen on the live fleet.
        await Assert.That(ARunner("").RegisteredByPrincipalId).IsEmpty();
    }
}
