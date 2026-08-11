using Gg.Contracts;
using Gg.Contracts.Description;
using Gg.Runner;

namespace Gg.Runner.Tests;

/// <summary>Resolves whatever it was handed, or refuses to.</summary>
internal sealed class ScriptedResolver : ICredentialResolver
{
    internal Dictionary<string, string> Secrets { get; } = new(StringComparer.Ordinal);

    internal List<string> Asked { get; } = [];

    public Task<CredentialResolution> ResolveAsync(
        CredentialReference reference, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reference);
        Asked.Add(reference.Locator);

        return Task.FromResult<CredentialResolution>(
            Secrets.TryGetValue(reference.Locator, out var secret)
                ? new CredentialResolution.Resolved(secret)
                : new CredentialResolution.Unresolvable($"no secret at {reference.Locator} on this machine"));
    }
}

/// <summary>
/// The runner resolves the secret locally, and an unresolvable one is a
/// diagnosis rather than a stall.
/// </summary>
/// <remarks>
/// <para>
/// ADR-0004: <i>secret-reference indirection fails opaquely. A runner that
/// cannot read a vault produces a stalled flight that looks like a broken
/// product. Diagnostics for this are a feature, not logging.</i>
/// </para>
/// <para>
/// So the loop does not hold, retry, or go quiet. It gives the lease back with
/// a diagnosis naming the reference, and the control plane records it on the
/// flight log where a person will find it.
/// </para>
/// </remarks>
public class CredentialResolutionTests
{
    private static readonly DateTimeOffset T0 = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private const string TheSecret = "ghp-THE-SECRET-VALUE-nobody-should-see";

    private static CredentialReference AReference(string locator = "local:acme/widgets") => new()
    {
        Kind = CredentialKinds.Local,
        Locator = locator,
        Identity = "acme-bot",
        Scopes = [CredentialScopes.Read],
    };

    private static LeaseGranted ALeaseNeeding(params CredentialReference[] references) => new()
    {
        LeaseId = "lease-1",
        Generation = 1,
        FlightId = "flight-1",
        FlightNumber = FlightRef.Format(1042),
        Repos = [],
        Credentials = references,
        ClassificationCeiling = Classifications.Internal,
        ClassificationRules = ClassificationRules.Default,
        ExpiresAt = T0.AddMinutes(10),
        RenewWithinSeconds = 5,
    };

    private static RunnerLoop Build(
        FakeProtocol protocol, MovableClock clock, RecordingObserver observer, ICredentialResolver resolver) =>
        new(protocol, clock,
            (span, token) =>
            {
                token.ThrowIfCancellationRequested();
                clock.Advance(span);
                return Task.CompletedTask;
            },
            observer,
            resolver,
            new NoWorkspace())
        {
            HoldFor = TimeSpan.FromSeconds(3),
        };

    private static CancellationTokenSource StopAfter(RecordingObserver observer, int events)
    {
        var stopping = new CancellationTokenSource();
        var seen = 0;
        observer.OnEvent = _ =>
        {
            if (Interlocked.Increment(ref seen) >= events)
            {
                stopping.Cancel();
            }
        };
        return stopping;
    }

    [Test]
    public async Task Credentials_are_resolved_after_the_lease_and_before_anything_else()
    {
        // The order in the runner rule is load-bearing: lease, then resolve,
        // then materialize. Nothing materializes yet, so what is checked here
        // is that resolution happens at all, on the reference the lease named.
        var clock = new MovableClock(T0);
        var protocol = new FakeProtocol();
        protocol.Claims.Enqueue(new ClaimResult.Granted(ALeaseNeeding(AReference())));
        var resolver = new ScriptedResolver { Secrets = { ["local:acme/widgets"] = TheSecret } };
        var observer = new RecordingObserver();

        using var stopping = StopAfter(observer, 2);
        await Build(protocol, clock, observer, resolver).RunAsync("runner-1", ["linux"], stopping.Token);

        await Assert.That(resolver.Asked).Contains("local:acme/widgets");
        await Assert.That(observer.Events).Contains("released:completed")
            .Because("a resolvable credential changes nothing about the rest of the flight.");
    }

    [Test]
    public async Task An_unresolvable_credential_releases_the_lease_with_a_diagnosis()
    {
        var clock = new MovableClock(T0);
        var protocol = new FakeProtocol();
        protocol.Claims.Enqueue(new ClaimResult.Granted(ALeaseNeeding(AReference())));
        var observer = new RecordingObserver();

        using var stopping = StopAfter(observer, 2);
        await Build(protocol, clock, observer, new ScriptedResolver()).RunAsync("runner-1", ["linux"], stopping.Token);

        await Assert.That(observer.Events).Contains("unresolved:local:acme/widgets")
            .Because("silence here is the stalled flight ADR-0004 named.");
        await Assert.That(protocol.Calls.Any(c => c.StartsWith("release:1:failed", StringComparison.Ordinal)))
            .IsTrue()
            .Because("the lease goes back at once; holding it would block the flight for its whole duration.");
    }

    [Test]
    public async Task The_release_names_which_reference_and_what_failed()
    {
        var clock = new MovableClock(T0);
        var protocol = new FakeProtocol();
        protocol.Claims.Enqueue(new ClaimResult.Granted(ALeaseNeeding(AReference())));
        var observer = new RecordingObserver();

        using var stopping = StopAfter(observer, 2);
        await Build(protocol, clock, observer, new ScriptedResolver()).RunAsync("runner-1", ["linux"], stopping.Token);

        var failure = protocol.LastCredentialFailure;

        await Assert.That(failure).IsNotNull();
        await Assert.That(failure!.Reference.Locator).IsEqualTo("local:acme/widgets");
        await Assert.That(failure.Reference.Kind).IsEqualTo(CredentialKinds.Local);
        await Assert.That(failure.Reference.Identity).IsEqualTo("acme-bot");
        await Assert.That(failure.Problem).IsNotEmpty()
            .Because("which reference, which locator, what failed - all three, or it is a shrug.");
    }

    [Test]
    public async Task A_lease_needing_no_credential_resolves_nothing()
    {
        // Nothing has credentials attached until somebody registers one, and a
        // runner that refused a flight for a credential it was never told about
        // would be Article XI pointed at itself.
        var clock = new MovableClock(T0);
        var protocol = new FakeProtocol();
        protocol.Claims.Enqueue(new ClaimResult.Granted(ALeaseNeeding()));
        var resolver = new ScriptedResolver();
        var observer = new RecordingObserver();

        using var stopping = StopAfter(observer, 2);
        await Build(protocol, clock, observer, resolver).RunAsync("runner-1", ["linux"], stopping.Token);

        await Assert.That(resolver.Asked).IsEmpty();
        await Assert.That(observer.Events).Contains("released:completed");
    }

    [Test]
    public async Task One_unresolvable_reference_is_enough_to_stop_the_flight()
    {
        // Not "resolve what you can". A flight running with half its
        // credentials produces a partial result nobody can tell from a whole
        // one, which is the failure mode Article XI exists for.
        var clock = new MovableClock(T0);
        var protocol = new FakeProtocol();
        protocol.Claims.Enqueue(new ClaimResult.Granted(
            ALeaseNeeding(AReference(), AReference("local:acme/other"))));
        var resolver = new ScriptedResolver { Secrets = { ["local:acme/widgets"] = TheSecret } };
        var observer = new RecordingObserver();

        using var stopping = StopAfter(observer, 2);
        await Build(protocol, clock, observer, resolver).RunAsync("runner-1", ["linux"], stopping.Token);

        await Assert.That(observer.Events).Contains("unresolved:local:acme/other");
        await Assert.That(protocol.Calls.Any(c => c.Contains("failed", StringComparison.Ordinal))).IsTrue();
    }

    [Test]
    public async Task The_resolved_secret_appears_in_nothing_the_runner_sends()
    {
        // The runner rule, asserted rather than intended: the resolved secret
        // never leaves this machine. Checked over every serialized request the
        // loop produced, not over the ones a reading of the code suggests.
        var clock = new MovableClock(T0);
        var protocol = new FakeProtocol();
        protocol.Claims.Enqueue(new ClaimResult.Granted(ALeaseNeeding(AReference())));
        var resolver = new ScriptedResolver { Secrets = { ["local:acme/widgets"] = TheSecret } };
        var observer = new RecordingObserver();

        using var stopping = StopAfter(observer, 2);
        await Build(protocol, clock, observer, resolver).RunAsync("runner-1", ["linux"], stopping.Token);

        foreach (var sent in protocol.Serialized)
        {
            await Assert.That(sent).DoesNotContain(TheSecret);
        }

        // Poison twin: the recorder saw something, so its silence is silence
        // about the secret rather than about everything.
        await Assert.That(protocol.Serialized).IsNotEmpty();
        await Assert.That(string.Join("\n", protocol.Serialized)).Contains("lease-1");
    }

    [Test]
    public async Task Nothing_the_observer_reports_could_carry_the_secret()
    {
        // The observer writes to stdout in a real runner, and stdout is what a
        // customer pastes into a ticket.
        var clock = new MovableClock(T0);
        var protocol = new FakeProtocol();
        protocol.Claims.Enqueue(new ClaimResult.Granted(ALeaseNeeding(AReference())));
        var resolver = new ScriptedResolver { Secrets = { ["local:acme/widgets"] = TheSecret } };
        var observer = new RecordingObserver();

        using var stopping = StopAfter(observer, 2);
        await Build(protocol, clock, observer, resolver).RunAsync("runner-1", ["linux"], stopping.Token);

        await Assert.That(observer.Events).IsNotEmpty();
        foreach (var reported in observer.Events)
        {
            await Assert.That(reported).DoesNotContain(TheSecret);
        }
    }

    [Test]
    public async Task A_runner_with_no_store_configured_refuses_rather_than_pretends()
    {
        // The default resolver resolves nothing and says so. It must not
        // return an empty string, which is a secret that fetches nothing and
        // fails somewhere far away.
        var resolution = await new NoCredentialResolver().ResolveAsync(AReference());

        await Assert.That(resolution).IsTypeOf<CredentialResolution.Unresolvable>();
        await Assert.That(((CredentialResolution.Unresolvable)resolution).Problem).IsNotEmpty();
    }
}
