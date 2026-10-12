using Gg.Contracts;

namespace Gg.Client.Tests;

/// <summary>
/// <c>gg show</c> names the model a person moved a flight to (slice
/// seventy-six, S76.7-02).
/// </summary>
/// <remarks>
/// <b>Found on the walk.</b> GG-1097 was moved to claude-opus-5-5 mid-run, the
/// runner started the agent over on it, and <c>gg show</c> said nothing: it
/// renders the flight's story, and the choice in force lives on the summary.
/// The slice said <c>gg show</c> prints it, so it reads the summary beside the
/// story, as it reads the hold beside it.
/// </remarks>
public class AShowNamesTheModelInForceTests
{
    private static StoredSession ASession() => new()
    {
        SessionToken = StubControlPlane.IssuedSessionToken,
        ExpiresAt = DateTimeOffset.UtcNow.AddHours(12),
        TenantId = "019fe062-d000-730c-a37d-7247342cd810",
        PrincipalDisplay = "stub-principal",
    };

    private static FlightCommands Build(StubControlPlane stub) =>
        new(new ControlPlaneClient(new HttpClient { BaseAddress = new Uri(stub.BaseAddress) }),
            new HeldSessionStore(ASession()));

    [Test]
    public async Task A_moved_flight_shows_the_model_it_was_moved_to()
    {
        await using var stub = new StubControlPlane
        {
            FlightAnswer = StubControlPlane.AFlight() with { Model = "claude-opus-5-5" },
        };

        var text = VerbOutput.ToText(await Build(stub).ShowAsync("GG-42", withModel: true));

        await Assert.That(text).Contains("model       claude-opus-5-5")
            .Because("a person who moved a flight looks for the move where they look at the flight.");
    }

    [Test]
    public async Task A_flight_nobody_chose_for_shows_no_model_line()
    {
        await using var stub = new StubControlPlane();

        var text = VerbOutput.ToText(await Build(stub).ShowAsync("GG-42", withModel: true));

        await Assert.That(text).DoesNotContain("model ")
            .Because("null is the loop's own model, which the work kind already says; a line "
                   + "naming nothing would read as a choice somebody made.");
    }

    [Test]
    public async Task The_console_modal_is_not_charged_the_second_read()
    {
        await using var stub = new StubControlPlane();

        await Build(stub).ShowAsync("GG-42");

        await Assert.That(stub.ObservedPaths).DoesNotContain("/v1/flights/GG-42")
            .Because("the modal opened over the summary and already holds the model.");
    }
}
