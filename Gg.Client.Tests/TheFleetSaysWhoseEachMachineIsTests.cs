using System.Net;
using System.Text;
using System.Text.Json;
using Gg.Contracts;
using Gg.Contracts.Description;

namespace Gg.Client.Tests;

/// <summary>
/// Whose each machine is - the tenant's, open, or one person's - and whether its
/// owner keeps it to their own flights, said in <c>gg runners</c> and changed by
/// five verbs that each go to one door.
/// </summary>
/// <remarks>
/// <para>
/// <b>Ownership and reservation were one column.</b> <c>reserved_to</c> said
/// both whose a runner was and that it took only that person's flights, so a
/// person could not own a machine that still took the tenant's work, and a
/// fleet machine could not belong to the tenant and to nobody. ADR-0025 § 6
/// splits them; slice forty-three, rules 4 to 8 and 12.
/// </para>
/// <para>
/// <b>And a person could not see which machines were theirs</b> - slice
/// forty-two's open question, because a personal watch sweeps only on its
/// person's runners, and a person who cannot tell which those are cannot tell
/// why their watch never swept.
/// </para>
/// </remarks>
public class TheFleetSaysWhoseEachMachineIsTests
{
    private const string Runner = "01a06385-322f-7371-93a2-ce35db5c4fbe";

    private static RunnerSummary ARunner(string label) => new()
    {
        RunnerId = Guid.NewGuid().ToString(),
        Label = label,
        State = RunnerStates.Idle,
        LastHeartbeatAt = new DateTimeOffset(2026, 9, 19, 8, 0, 0, TimeSpan.Zero),
    };

    private static string Listed(params RunnerSummary[] runners) =>
        VerbOutput.ToText(new VerbResult.Runners(new RunnerList { Runners = runners }));

    // ---- gg runners ----

    [Test]
    public async Task Each_row_says_whose_the_machine_is()
    {
        var text = Listed(
            ARunner("vmlinux001") with { Ownership = RunnerOwnerships.Tenant, Resident = true, Profile = "dev-worker" },
            ARunner("vmlinux002") with { Ownership = RunnerOwnerships.Open },
            ARunner("kevins-mac") with
            {
                Ownership = RunnerOwnerships.Claimed, Owner = "Kevin", OwnerPrincipalId = "p-1", Reserved = true,
            },
            ARunner("shared-box") with
            {
                Ownership = RunnerOwnerships.Claimed, Owner = "Ana", OwnerPrincipalId = "p-2",
            });

        var rows = text.Split('\n');

        await Assert.That(rows.Single(r => r.Contains("vmlinux001"))).Contains("the tenant's");
        await Assert.That(rows.Single(r => r.Contains("vmlinux001"))).Contains("resident");
        await Assert.That(rows.Single(r => r.Contains("vmlinux001"))).Contains("profile dev-worker");
        await Assert.That(rows.Single(r => r.Contains("vmlinux002"))).Contains("open");
        await Assert.That(rows.Single(r => r.Contains("kevins-mac"))).Contains("Kevin's, reserved");
        await Assert.That(rows.Single(r => r.Contains("shared-box"))).Contains("Ana's")
            .Because("claimed and not reserved is the case the split exists for: a person's "
                   + "machine that still takes the tenant's work.");
        await Assert.That(rows.Single(r => r.Contains("shared-box"))).DoesNotContain("reserved");
    }

    [Test]
    public async Task An_older_control_plane_row_claims_nothing_about_ownership()
    {
        // EMPTY IS NOT OPEN. A row reading "open" from a control plane with no
        // claim door would invite a verb that cannot succeed.
        var row = Listed(ARunner("old-box"));

        await Assert.That(row).DoesNotContain("open");
        await Assert.That(row).DoesNotContain("tenant");
        await Assert.That(row).DoesNotContain("reserved");
    }

    [Test]
    public async Task An_owner_display_cannot_write_to_the_terminal()
    {
        var row = Listed(ARunner("box") with
        {
            Ownership = RunnerOwnerships.Claimed, Owner = "Eve\u001b[2J", OwnerPrincipalId = "p-3",
        });

        await Assert.That(row).DoesNotContain("\u001b");
    }

    [Test]
    public async Task The_summary_carries_ownership_under_its_declared_names()
    {
        var json = JsonSerializer.Serialize(
            ARunner("box") with
            {
                Ownership = RunnerOwnerships.Claimed, Owner = "Kevin", OwnerPrincipalId = "p-1",
                Reserved = true, Resident = true, Profile = "dev-worker",
            },
            ProtocolJsonContext.Default.RunnerSummary);

        foreach (var member in new[] { "ownership", "owner", "ownerPrincipalId", "reserved", "resident", "profile" })
        {
            await Assert.That(json).Contains($"\"{member}\":");
        }

        await Assert.That(ProtocolSurface.JsonMembers[typeof(RunnerSummary)])
            .Contains("ownership").And.Contains("owner").And.Contains("ownerPrincipalId")
            .And.Contains("reserved").And.Contains("resident").And.Contains("profile");
    }

    // ---- the doors ----

    [Test]
    public async Task Claiming_and_unclaiming_are_one_door_two_ways_round()
    {
        await Assert.That(ProtocolSurface.Endpoints.Any(e =>
                e.Method == "POST" && e.Path == "/v1/runners/{id}/claim"
                && e.Audience == Audience.Developer
                && e.Request == typeof(RunnerClaimRequest) && e.Response == typeof(RunnerOwnership)
                && e.Statuses.Contains(403) && e.Statuses.Contains(409)))
            .IsTrue()
            .Because("403 is a tenant runner - not ever - and 409 somebody else's - not now.");

        await Assert.That(ProtocolSurface.Endpoints.Any(e =>
                e.Method == "DELETE" && e.Path == "/v1/runners/{id}/claim"
                && e.Response == typeof(RunnerOwnership) && e.Statuses.Contains(403)))
            .IsTrue();

        await Assert.That(ProtocolSurface.Endpoints.Any(e =>
                e.Method == "PUT" && e.Path == "/v1/runners/{id}/ownership"
                && e.Request == typeof(RunnerOwnershipRequest) && e.Response == typeof(RunnerOwnership)
                && e.Statuses.Contains(403) && e.Statuses.Contains(409)))
            .IsTrue()
            .Because("an admin's door, refusing a claimed runner with 409 - unclaim it first.");
    }

    /// <summary>Answers as the control plane would, and remembers being asked.</summary>
    private sealed class Answering(HttpStatusCode status, string? body = null) : HttpMessageHandler
    {
        internal HttpRequestMessage? Seen { get; private set; }

        internal string? SentBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Seen = request;
            if (request.Content is not null)
            {
                SentBody = await request.Content.ReadAsStringAsync(cancellationToken);
            }

            return new HttpResponseMessage(status)
            {
                Content = body is null
                    ? new StringContent("")
                    : new StringContent(body, Encoding.UTF8, "application/json"),
            };
        }
    }

    private static ControlPlaneClient Against(Answering handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://control.example.invalid") });

    private const string Claimed = $$"""
        {"runnerId":"{{Runner}}","ownership":"claimed","owner":"Kevin","ownerPrincipalId":"p-1",
         "ownedAt":"2026-09-19T08:00:00+00:00","reserved":false}
        """;

    private const string Reserved = $$"""
        {"runnerId":"{{Runner}}","reservedTo":"Kevin","reservedAt":"2026-09-19T08:00:00+00:00"}
        """;

    [Test]
    public async Task A_claim_goes_to_the_claim_door_and_comes_back_owned()
    {
        var handler = new Answering(HttpStatusCode.OK, Claimed);

        var owned = await Against(handler).ClaimRunnerAsync("a-session", Runner);

        await Assert.That(handler.Seen!.Method).IsEqualTo(HttpMethod.Post);
        await Assert.That(handler.Seen.RequestUri!.AbsolutePath).IsEqualTo($"/v1/runners/{Runner}/claim");
        await Assert.That(owned!.Ownership).IsEqualTo(RunnerOwnerships.Claimed);
        await Assert.That(owned.Owner).IsEqualTo("Kevin");
    }

    [Test]
    public async Task The_other_four_doors_are_the_ones_the_contract_names()
    {
        var unclaim = new Answering(HttpStatusCode.OK, Claimed);
        _ = await Against(unclaim).UnclaimRunnerAsync("a-session", Runner);
        await Assert.That(unclaim.Seen!.Method).IsEqualTo(HttpMethod.Delete);
        await Assert.That(unclaim.Seen.RequestUri!.AbsolutePath).IsEqualTo($"/v1/runners/{Runner}/claim");

        var ownership = new Answering(HttpStatusCode.OK, Claimed);
        _ = await Against(ownership).SetRunnerOwnershipAsync("a-session", Runner, RunnerOwnerships.Tenant);
        await Assert.That(ownership.Seen!.Method).IsEqualTo(HttpMethod.Put);
        await Assert.That(ownership.Seen.RequestUri!.AbsolutePath).IsEqualTo($"/v1/runners/{Runner}/ownership");
        await Assert.That(ownership.SentBody).IsEqualTo("""{"ownership":"tenant"}""");

        var reserve = new Answering(HttpStatusCode.OK, Reserved);
        var reserved = await Against(reserve).ReserveRunnerAsync("a-session", Runner);
        await Assert.That(reserve.Seen!.Method).IsEqualTo(HttpMethod.Post);
        await Assert.That(reserve.Seen.RequestUri!.AbsolutePath).IsEqualTo($"/v1/runners/{Runner}/reservation");
        await Assert.That(reserved!.ReservedTo).IsEqualTo("Kevin");

        var release = new Answering(HttpStatusCode.OK, $$"""{"runnerId":"{{Runner}}"}""");
        _ = await Against(release).ReleaseRunnerAsync("a-session", Runner);
        await Assert.That(release.Seen!.Method).IsEqualTo(HttpMethod.Delete);
        await Assert.That(release.Seen.RequestUri!.AbsolutePath).IsEqualTo($"/v1/runners/{Runner}/reservation");
    }

    [Test]
    [Arguments(HttpStatusCode.Forbidden, "vmlinux001 is the tenant's; an admin has said nobody claims it.")]
    [Arguments(HttpStatusCode.Conflict, "vmlinux002 is Ana's. Ask her to unclaim it.")]
    public async Task A_refusal_arrives_as_the_control_plane_said_it(HttpStatusCode status, string said)
    {
        var handler = new Answering(
            status, JsonSerializer.Serialize(new Dictionary<string, string> { ["detail"] = said }));

        var refused = await Assert.ThrowsAsync<RunnerOwnershipRefusedException>(
            () => Against(handler).ClaimRunnerAsync("a-session", Runner));

        await Assert.That(refused!.Message).IsEqualTo(said);
    }

    [Test]
    public async Task A_runner_this_tenant_does_not_have_is_null()
    {
        var owned = await Against(new Answering(HttpStatusCode.NotFound)).ClaimRunnerAsync("a-session", Runner);

        await Assert.That(owned).IsNull();
    }

    // ---- what the verbs print ----

    [Test]
    public async Task A_claim_says_whether_the_machine_still_takes_the_tenants_work()
    {
        var text = VerbOutput.ToText(new VerbResult.RunnerOwned(new RunnerOwnership
        {
            RunnerId = Runner, Ownership = RunnerOwnerships.Claimed, Owner = "Kevin",
        }));

        await Assert.That(text).Contains("Kevin");
        await Assert.That(text).Contains("still takes the tenant's work");
        await Assert.That(text).Contains($"gg runner reserve {Runner}");
    }

    [Test]
    public async Task Open_and_tenant_each_say_what_a_person_may_do_next()
    {
        var open = VerbOutput.ToText(new VerbResult.RunnerOwned(new RunnerOwnership
        {
            RunnerId = Runner, Ownership = RunnerOwnerships.Open,
        }));
        var tenant = VerbOutput.ToText(new VerbResult.RunnerOwned(new RunnerOwnership
        {
            RunnerId = Runner, Ownership = RunnerOwnerships.Tenant,
        }));

        await Assert.That(open).Contains("open");
        await Assert.That(open).Contains("claim");
        await Assert.That(tenant).Contains("the tenant's");
        await Assert.That(tenant).Contains("nobody may claim it");
    }

    [Test]
    public async Task A_reservation_says_whose_flights_the_machine_takes()
    {
        var reserved = VerbOutput.ToText(new VerbResult.RunnerReservation(new RunnerReserved
        {
            RunnerId = Runner, ReservedTo = "Kevin",
        }));
        var released = VerbOutput.ToText(new VerbResult.RunnerReservation(new RunnerReserved
        {
            RunnerId = Runner,
        }));

        await Assert.That(reserved).Contains("only Kevin's flights");
        await Assert.That(released).Contains("the tenant's work");
    }

    [Test]
    public async Task Each_result_round_trips_as_json()
    {
        var owned = new VerbResult.RunnerOwned(new RunnerOwnership
        {
            RunnerId = Runner, Ownership = RunnerOwnerships.Claimed, Owner = "Kevin", Reserved = true,
        });
        var reservation = new VerbResult.RunnerReservation(new RunnerReserved { RunnerId = Runner, ReservedTo = "Kevin" });

        await Assert.That(VerbOutput.Parse(owned.Kind, VerbOutput.ToJson(owned))).IsEqualTo(owned);
        await Assert.That(VerbOutput.Parse(reservation.Kind, VerbOutput.ToJson(reservation)))
            .IsEqualTo(reservation);
    }
}
