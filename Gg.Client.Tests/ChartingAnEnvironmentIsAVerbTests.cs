using Gg.Client;
using Gg.Contracts;

namespace Gg.Client.Tests;

/// <summary>
/// Charting an environment: the act with an endpoint, a contract type, a
/// serializer registration, and no caller.
/// </summary>
/// <remarks>
/// <para>
/// <b>Found by standing an environment up.</b> The browser-environment spike
/// needed a second environment on a pool host and recorded what that took:
/// four acts, two of which had no product surface at all. One of the two was
/// wrong — <c>gg airspace name strategy ui</c> declares a name and has since
/// the topology door shipped — and this is the one that was right.
/// <c>POST /v1/environments</c> could only be reached with <c>curl</c>.
/// </para>
/// <para>
/// <b>The same shape the declare-name slice already has.</b>
/// <c>ChartEnvironmentRequest</c> is pinned, registered in the vocabulary, and
/// listed in <c>ProtocolJsonContext</c> — every registration except a method
/// that posts it. <c>ANameCanBeDeclaredTests</c> is the precedent, right down to
/// the sentence: <i>"the act with an endpoint, a contract type, and no
/// caller."</i>
/// </para>
/// <para>
/// <b>Two success shapes, and the 202 is the ordinary one.</b> A chart entry is
/// reach that did not exist a moment ago — every envelope in the tenant may then
/// select the name — so it rides a gate and the answer says who decides. The 200
/// is the narrow case of a name already charted.
/// </para>
/// <para>
/// <b>The disposition is read, never derived here.</b> <c>stated</c> and
/// <c>measured</c> are what a meaning earns, and the door is what decides which:
/// <i>"derived from the meaning, never typed"</i>. A client that computed it
/// from whether <c>--means</c> was passed would be a second opinion about
/// somebody else's rule, and would disagree the moment a meaning is registered
/// separately.
/// </para>
/// </remarks>
public class ChartingAnEnvironmentIsAVerbTests
{
    private static StoredSession SignedIn { get; } = new()
    {
        SessionToken = "stub-session",
        ExpiresAt = new DateTimeOffset(2027, 1, 1, 0, 0, 0, TimeSpan.Zero),
        TenantId = "stub-tenant",
        PrincipalDisplay = "someone@example.test",
    };

    private static FlightCommands Against(StubControlPlane stub, HttpClient http)
    {
        http.BaseAddress = new Uri(stub.BaseAddress);
        return new FlightCommands(new ControlPlaneClient(http), new HeldSessionStore(SignedIn));
    }

    [Test]
    public async Task A_chart_that_rides_a_flight_reports_the_flight_and_who_decides()
    {
        await using var stub = new StubControlPlane
        {
            ChartPending = new RegistrationPending
            {
                Flight = "GG-119",
                Awaiting = "an-architect",
                Widens = "environments",
            },
        };

        using var http = new HttpClient();
        var charted = ((VerbResult.EnvironmentCharted)await Against(stub, http)
            .ChartEnvironmentAsync("ui", meaning: null)).Value;

        await Assert.That(charted.Flight).IsEqualTo("GG-119");
        await Assert.That(charted.Awaiting).IsEqualTo("an-architect")
            .Because("a gate nobody is named for is a gate a person cannot go and ask about.");
        await Assert.That(charted.Widens).IsEqualTo("environments");
        await Assert.That(charted.Name).IsEqualTo("ui");
    }

    [Test]
    public async Task A_name_already_charted_is_reported_live_rather_than_pending()
    {
        // THE POSITIVE CONTROL. A verb that reported every chart as pending
        // would satisfy the test above and tell somebody to wait for a gate
        // that will never open, because the name is already theirs.
        await using var stub = new StubControlPlane
        {
            ChartLive = new EnvironmentCharted
            {
                Name = "ui",
                Meaning = null,
                Disposition = LabelDispositions.Stated,
                ChartedBy = "an-architect",
                ChartedAt = new DateTimeOffset(2026, 9, 14, 12, 0, 0, TimeSpan.Zero),
            },
        };

        using var http = new HttpClient();
        var charted = ((VerbResult.EnvironmentCharted)await Against(stub, http)
            .ChartEnvironmentAsync("ui", meaning: null)).Value;

        await Assert.That(charted.Flight).IsNull();
        await Assert.That(charted.ChartedBy).IsEqualTo("an-architect");
        await Assert.That(charted.Disposition).IsEqualTo(LabelDispositions.Stated);
    }

    [Test]
    public async Task The_name_and_the_meaning_reach_the_door()
    {
        // A verb that dropped the meaning would chart a name that can only ever
        // be `stated`, and the difference between stated and measured is the
        // whole reason a meaning exists.
        await using var stub = new StubControlPlane
        {
            ChartPending = new RegistrationPending { Flight = "GG-1", Awaiting = "somebody", Widens = "environments" },
        };

        using var http = new HttpClient();
        _ = await Against(stub, http).ChartEnvironmentAsync(
            "ui", "a browser is present and a display is reachable");

        await Assert.That(stub.ChartedEnvironment).IsNotNull();
        await Assert.That(stub.ChartedEnvironment!.Name).IsEqualTo("ui");
        await Assert.That(stub.ChartedEnvironment.Meaning)
            .IsEqualTo("a browser is present and a display is reachable");
    }

    [Test]
    public async Task A_name_with_no_meaning_sends_no_meaning()
    {
        // Null rather than empty. The door reads absent as "this name is a
        // claim", and an empty string is a meaning nobody wrote.
        await using var stub = new StubControlPlane
        {
            ChartPending = new RegistrationPending { Flight = "GG-1", Awaiting = "somebody", Widens = "environments" },
        };

        using var http = new HttpClient();
        _ = await Against(stub, http).ChartEnvironmentAsync("ui", meaning: null);

        await Assert.That(stub.ChartedEnvironment!.Meaning).IsNull();
    }

    [Test]
    public async Task A_refused_name_carries_the_doors_own_sentence()
    {
        // 400 is a malformed name, and the endpoint's declaration says why it
        // matters: "the registry is what apply refusals point people at, and a
        // chart that could hold a blank line would make that advice a trap."
        // Rewording it here would be a second opinion about somebody else's
        // rule.
        await using var stub = new StubControlPlane
        {
            ChartRefusal = "an environment name cannot be blank.",
        };

        using var http = new HttpClient();

        var refused = await Assert.ThrowsAsync<EnvelopeRefusedException>(async () =>
            await Against(stub, http).ChartEnvironmentAsync(" ", meaning: null));

        await Assert.That(refused!.Message).Contains("cannot be blank");
    }
}
