using System.Net;
using System.Text.RegularExpressions;
using Gg.Client;
using Gg.Contracts;
using Gg.Local;

namespace Gg.Client.Tests;

/// <summary>
/// The long tail of messages outside the first-run path: takeovers, the update
/// plan, configuration offers, hand-flight refusals and naming a runner.
/// </summary>
/// <remarks>
/// The same voice as <see cref="PlainWordsOnTheFirstRunPathTests"/>: what
/// happened, what was not changed, then what to run.
/// </remarks>
public class PlainWordsInTheRemainingMessagesTests
{
    private static readonly Regex RuledOut = new(
        @"\b(you|your|yours|tenant|principal|nonce|mint|minted|redeem|redeemed|somebody|hold|holds|held)\b|n't\b",
        RegexOptions.IgnoreCase);

    private sealed class Answers(HttpStatusCode status, string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json"),
                RequestMessage = request,
            });
    }

    private sealed class OneSession : ISessionStore
    {
        public StoredSession? Read() => new()
        {
            SessionToken = "a-session",
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(1),
            PrincipalDisplay = "edge",
            TenantId = "019ff8aa-1111-7000-8000-0000000000ff",
        };

        public void Write(StoredSession session)
        {
        }

        public void Clear()
        {
        }
    }

    private static TakeCommands Take(HttpStatusCode status, string body) =>
        new(new ControlPlaneClient(
                new HttpClient(new Answers(status, body)) { BaseAddress = new Uri("http://cp.test/") }),
            new OneSession());

    private static async Task<string> Refusal(TakeCommands take)
    {
        try
        {
            await take.TakeAsync("GG-42");
        }
        catch (TakeoverRefusedException refused)
        {
            return refused.Message;
        }

        throw new InvalidOperationException("the takeover must be refused");
    }

    [Test]
    public async Task A_takeover_nobody_is_named_for_says_what_to_run()
    {
        var said = await Refusal(Take(HttpStatusCode.Conflict, "null"));

        await Assert.That(said)
            .IsEqualTo("GG-42 has already been taken over, and the control plane cannot say by "
                     + "whom. Nothing was changed. Run gg show GG-42 to see its current state.");
    }

    [Test]
    public async Task A_takeover_somebody_else_has_names_them_and_when_to_try_again()
    {
        var said = await Refusal(Take(HttpStatusCode.Conflict, """
            {"by":"Ada","since":"2026-08-19T09:12:00+00:00","heldUntil":"2026-08-19T09:42:00+00:00"}
            """));

        await Assert.That(said).Contains("Ada took over GG-42");
        await Assert.That(said).Contains("Nothing was changed");
        await Assert.That(said).Contains("run gg take GG-42 again after that");
        await Assert.That(RuledOut.IsMatch(said)).IsFalse();
    }

    [Test]
    public async Task A_takeover_of_no_flight_says_how_to_list_them()
    {
        var said = await Refusal(Take(HttpStatusCode.NotFound, ""));

        await Assert.That(said).Contains("does not name a flight in this organisation");
        await Assert.That(said).EndsWith("Run gg flights to list flights.");
    }

    [Test]
    public async Task A_control_plane_that_did_not_answer_shows_no_runtime_text()
    {
        var failure = new HttpRequestException("Connection refused (cp.test:443)");

        var said = ControlPlaneFailure.Said(failure);

        await Assert.That(said).IsEqualTo(
            "Could not reach the control plane. Run gg doctor to check the connection.");
        await Assert.That(said).DoesNotContain("Connection refused");
        await Assert.That(ControlPlaneFailure.Said(new NotSignedInException("Sign in first.")))
            .IsEqualTo("Sign in first.")
            .Because("a message gg already worded for a person passes through unchanged.");
    }

    [Test]
    public async Task An_answer_with_an_error_status_points_at_gg_bundle()
    {
        var said = ControlPlaneFailure.Sentence(
            "https://cp.test",
            new HttpRequestException("Internal Server Error", null, HttpStatusCode.InternalServerError));

        await Assert.That(said).StartsWith("The control plane at https://cp.test answered with an error (HTTP 500).");
        await Assert.That(said).Contains("run gg bundle");
    }

    [Test]
    public async Task Naming_a_machine_in_an_empty_fleet_names_a_real_command()
    {
        var resolved = RunnerNamed.Resolve("vmlinux001", []);

        await Assert.That(resolved.Said).Contains("gg fleet enroll");
        await Assert.That(resolved.Said).DoesNotContain("gg fleet token")
            .Because("gg fleet token is not a command; gg fleet enroll creates a token.");
        await Assert.That(RuledOut.IsMatch(resolved.Said)).IsFalse();
    }

    [Test]
    public async Task An_update_refusal_uses_no_contractions_and_says_nothing_changed()
    {
        var container = UpdatePlans.For(
            new InstallShape(InstallKind.Container, null), "0.88.1", "0.88.2", null, false, "/tmp/x");
        var unknown = UpdatePlans.For(
            new InstallShape(InstallKind.Unknown, null), "0.88.1", "0.88.2", null, false, "/tmp/x");

        foreach (var refusal in new[] { container.Refusal, unknown.Refusal })
        {
            await Assert.That(refusal).Contains("nothing was changed");
            await Assert.That(RuledOut.IsMatch(refusal!)).IsFalse();
        }
    }

    [Test]
    public async Task A_single_file_gg_with_no_installer_names_the_setting()
    {
        var plan = UpdatePlans.For(
            new InstallShape(InstallKind.Native, null), "0.88.1", "0.88.2", null, false, "/tmp/x");

        await Assert.That(plan.Refusal).Contains("gg config set installer <url>");
    }

    [Test]
    public async Task An_offer_that_moved_says_nothing_was_written_without_addressing_the_reader()
    {
        try
        {
            ConfigCommands.Accept(
                new OfferedConfiguration { Version = "v2", Settings = [], OfferedAt = DateTimeOffset.UtcNow },
                "v1",
                Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "gg.toml"));
        }
        catch (ConfigurationRefused refused)
        {
            await Assert.That(refused.Message).Contains("nothing was written");
            await Assert.That(RuledOut.IsMatch(refused.Message)).IsFalse();
            return;
        }

        throw new InvalidOperationException("an offer that moved must be refused");
    }

    [Test]
    public async Task A_hand_flight_refusal_names_the_setting_and_no_internal_word()
    {
        foreach (var satisfier in new[]
                 {
                     ChecklistSatisfiers.Nobody, ChecklistSatisfiers.DeclinedByBound,
                     ChecklistSatisfiers.Withheld, "fleet",
                 })
        {
            var remedy = new HandRefusal
            {
                Requirement = "environment=aspire-payments",
                Reason = Reason.For(ReasonKinds.NoRunnerAdvertises, ["environment=aspire-payments"]),
                Satisfier = satisfier,
                Disposition = LabelDispositions.Stated,
            }.Remedy;

            await Assert.That(remedy).DoesNotContain("bound");
            await Assert.That(RuledOut.IsMatch(remedy)).IsFalse();
        }

        var nobody = new HandRefusal
        {
            Requirement = "environment=aspire-payments",
            Reason = Reason.For(ReasonKinds.NoRunnerAdvertises, ["environment=aspire-payments"]),
            Satisfier = ChecklistSatisfiers.Nobody,
            Disposition = LabelDispositions.Stated,
        }.Remedy;

        await Assert.That(nobody).Contains("gg config set runner-labels");
    }
}
