using Gg.Contracts;
using Gg.Contracts.Description;

namespace Gg.Contracts.Tests;

/// <summary>
/// A runner reports whether its agent is logged in — on its own route, as its
/// own record, and never through the heartbeat.
/// </summary>
/// <remarks>
/// <para>
/// <b>A reading, on the allowance readings' argument.</b> <i>"A heartbeat is
/// liveness only because a runner able to report something about itself can
/// report it while dead"</i> — so this is not a field on the beat. And it is
/// a MEASUREMENT with its own <c>MeasuredAt</c>, not a status a runner
/// declares about itself: the codebase's rule that <i>"a runner never reports
/// its own status"</i> guards a declaration that changes claim state, and this
/// changes none. The runner's not-claiming is the mechanism; the reading only
/// tells a person why.
/// </para>
/// <para>
/// <b>No id in the path</b>, on <c>/v1/runner/renewal</c>'s precedent: the
/// runner header names who is asking, and a path id would offer a runner the
/// chance to name somebody else's.
/// </para>
/// <para>
/// <b>Article VIII at the door.</b> A diagnosis is what the runner composed, not
/// what the agent printed - and the contract refuses one that looks like a
/// token, because the one thing this route must never carry is the credential
/// it is about.
/// </para>
/// </remarks>
public class ARunnerSaysWhetherItsAgentIsLoggedInTests
{
    private static readonly DateTimeOffset T0 = new(2026, 9, 15, 12, 0, 0, TimeSpan.Zero);

    private static AgentReading Ready() => new()
    {
        Provider = "claude",
        State = AgentStates.Ready,
        Source = AgentCredentialSources.Token,
        MeasuredAt = T0,
    };

    [Test]
    public async Task The_route_is_declared_for_runners_with_no_id_in_the_path()
    {
        var endpoint = ProtocolSurface.Endpoints.Single(e =>
            e.Method == "POST" && e.Path == "/v1/runner/agent");

        await Assert.That(endpoint.Audience).IsEqualTo(Audience.Runner);
        await Assert.That(endpoint.Request).IsEqualTo(typeof(AgentReading));
        await Assert.That(endpoint.RequiredHeaders).Contains(ProtocolSurface.RunnerHeader);
        await Assert.That(endpoint.RequiredHeaders).DoesNotContain(ProtocolSurface.SessionHeader)
            .Because("a session on a runner route would let a person report for a machine.");
        await Assert.That(endpoint.Statuses).Contains(202)
            .Because("the write is a command; what the control plane made of it is a read.");
        await Assert.That(endpoint.Statuses).DoesNotContain(200);
        await Assert.That(endpoint.Path).DoesNotContain("{id}")
            .Because("the credential names the runner, and a path id is a fleet to enumerate.");
    }

    [Test]
    public async Task The_heartbeat_cannot_reach_the_reading()
    {
        // NOT A FIELD ON THE BEAT. Reachability rather than a member check, so
        // a future member that nested it would be caught the same way.
        await Assert.That(Reaches(typeof(RunnerHeartbeat), typeof(AgentReading))).IsFalse();
        await Assert.That(Reaches(typeof(HeartbeatAccepted), typeof(AgentReading))).IsFalse();
    }

    [Test]
    public async Task The_states_and_sources_are_closed()
    {
        await Assert.That(AgentStates.All).IsEquivalentTo(
            (string[])[AgentStates.Ready, AgentStates.NeedsLogin]);
        await Assert.That(AgentCredentialSources.All).IsEquivalentTo(
            (string[])[AgentCredentialSources.Token, AgentCredentialSources.Machine, AgentCredentialSources.None]);
    }

    [Test]
    public async Task A_well_formed_reading_validates()
    {
        await Assert.That(AgentReading.Validate(Ready())).IsNull();
        await Assert.That(AgentReading.Validate(Ready() with
        {
            State = AgentStates.NeedsLogin,
            Source = AgentCredentialSources.None,
            Diagnosis = "the agent is not logged in and gg holds no token for it",
        })).IsNull();
    }

    [Test]
    public async Task An_unknown_state_or_source_is_refused()
    {
        await Assert.That(AgentReading.Validate(Ready() with { State = "expired" })).IsNotNull()
            .Because("an unknown state read as ready would clear a gate over a broken machine.");
        await Assert.That(AgentReading.Validate(Ready() with { Source = "keychain" })).IsNotNull();
        await Assert.That(AgentReading.Validate(Ready() with { Provider = " " })).IsNotNull();
    }

    [Test]
    public async Task A_diagnosis_is_bounded_and_may_not_look_like_a_token()
    {
        await Assert.That(AgentReading.Validate(Ready() with
        {
            Diagnosis = new string('x', AgentReading.MaxDiagnosis + 1),
        })).IsNotNull();

        await Assert.That(AgentReading.Validate(Ready() with
        {
            Diagnosis = "the agent said: sk-ant-oat01-something-that-should-never-cross",
        })).IsNotNull()
            .Because("the one thing this route must never carry is the credential it is "
                   + "about, and a diagnosis that copied the agent's output could.");
    }

    [Test]
    public async Task It_is_pinned_and_in_the_vocabulary()
    {
        await Assert.That(typeof(AgentReading).GetCustomAttributes(typeof(PinnedIdAttribute), false))
            .IsNotEmpty();
        await Assert.That(Vocabulary.Types).Contains(typeof(AgentReading));
    }

    /// <summary>Whether a wire type can be reached from another through its members.</summary>
    private static bool Reaches(Type? from, Type wanted, HashSet<Type>? seen = null)
    {
        if (from is null || from == wanted)
        {
            return from is not null;
        }

        seen ??= [];
        if (!seen.Add(from) || from.Namespace?.StartsWith("Gg.Contracts", StringComparison.Ordinal) != true)
        {
            return false;
        }

        return from.GetProperties().Any(p =>
            Reaches(p.PropertyType, wanted, seen)
            || (p.PropertyType.IsGenericType
                && p.PropertyType.GetGenericArguments().Any(a => Reaches(a, wanted, seen))));
    }
}
