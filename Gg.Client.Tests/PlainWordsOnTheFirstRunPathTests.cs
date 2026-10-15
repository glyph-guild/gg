using System.Net;
using Gg.Client;
using Gg.Contracts;

namespace Gg.Client.Tests;

/// <summary>
/// The client-side half of the first-run wording: what happened, then what to do.
/// </summary>
public class PlainWordsOnTheFirstRunPathTests
{
    private sealed class AnswersNull : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.Accepted)
            {
                Content = new StringContent("null", System.Text.Encoding.UTF8, "application/json"),
                RequestMessage = request,
            });
    }

    private static ControlPlaneClient AnsweringNull() =>
        new(new HttpClient(new AnswersNull()) { BaseAddress = new Uri("http://cp.test/") });

    [Test]
    public async Task A_flight_the_control_plane_did_not_confirm_says_what_to_check()
    {
        var launch = new FlightLaunchRequest
        {
            Name = "fix it",
            Intent = new FlightIntent { Kind = FlightIntentKinds.Text, Text = "fix it" },
        };

        try
        {
            await AnsweringNull().LaunchFlightAsync("token", launch);
        }
        catch (InvalidOperationException unconfirmed)
        {
            await Assert.That(unconfirmed.Message).Contains("did not confirm the new flight");
            await Assert.That(unconfirmed.Message).Contains("Run gg flights to check before running gg fly again")
                .Because("running gg fly again without checking could open the flight twice.");
            await Assert.That(unconfirmed.Message).Contains("run gg bundle");
            return;
        }

        throw new InvalidOperationException("an empty answer must not read as an opened flight");
    }

    [Test]
    public async Task A_read_answered_with_nothing_points_at_gg_bundle()
    {
        try
        {
            await AnsweringNull().ListFlightsAsync("token");
        }
        catch (InvalidOperationException empty)
        {
            await Assert.That(empty.Message).Contains("The control plane sent back no flight list.");
            await Assert.That(empty.Message).Contains("run gg bundle and send the result to the Good Grief team");
            return;
        }

        throw new InvalidOperationException("an empty answer must not read as an empty list");
    }

    [Test]
    public async Task A_check_this_machine_cannot_fix_names_a_next_step()
    {
        var text = VerbOutput.ToText(new VerbResult.Diagnosis(new DoctorReport
        {
            Checks =
            [
                new DoctorCheck
                {
                    Name = DoctorChecks.Channel,
                    Passed = false,
                    Detail = "a data channel did not open on this binary",
                    Blocking = false,
                    Fixable = false,
                },
            ],
        }));

        await Assert.That(text).Contains("This machine cannot fix this. Run gg bundle and send the "
                                       + "result to the Good Grief team.");
    }

    [Test]
    public async Task Whoami_with_no_sign_in_says_what_to_run()
    {
        var who = new IdentityCommands(AnsweringNull(), new EmptySessions());

        try
        {
            await who.ShowAsync();
        }
        catch (NotSignedInException refused)
        {
            await Assert.That(refused.Message)
                .IsEqualTo("This machine is not signed in. Run gg login, then try again.");
            return;
        }

        throw new InvalidOperationException("no session must be refused");
    }

    private sealed class EmptySessions : ISessionStore
    {
        public StoredSession? Read() => null;

        public void Write(StoredSession session)
        {
        }

        public void Clear()
        {
        }
    }
}
