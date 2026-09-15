using Gg.Contracts;
using Gg.Runner.Execution;

namespace Gg.Runner;

/// <summary>
/// Starts the agent's own login ceremony as a child this runner owns.
/// </summary>
/// <remarks>
/// <para>
/// <b>A port, because the ceremony needs a terminal and this project may not
/// allocate one.</b> Measured in the spike: <c>claude setup-token</c> prints
/// nothing on pipes and its URL within eight seconds under a pseudo-terminal.
/// <c>Gg.Runner</c> takes no terminal library (<c>ProjectReferenceTests</c>,
/// <c>NoTerminalTests</c>), so the class that spawns the child lives in
/// <c>Gg.Cli</c> and is handed in here - the identity key's and the
/// credential keeper's shape.
/// </para>
/// <para>
/// <b>Null is the default and the default is closed.</b> A runner nobody
/// wired with this refuses to begin a login, and says so.
/// </para>
/// </remarks>
public interface IRunAnAgentLogin
{
    Task<IAgentLoginChild> StartAsync(CancellationToken cancellationToken);
}

/// <summary>One running ceremony: the child, and the two things read off it.</summary>
/// <remarks>
/// <b>Disposing it ends the child</b>, whole tree, the way the meter's child
/// is ended. A child waiting for a code nobody will type is a process nobody
/// asked for.
/// </remarks>
public interface IAgentLoginChild : IDisposable
{
    /// <summary>The login URL once the child has printed it, or null once it never will.</summary>
    Task<string?> UrlAsync(CancellationToken cancellationToken);

    /// <summary>Types the code, and reads the token the child mints - or null once it never will.</summary>
    Task<string?> TokenAsync(string code, CancellationToken cancellationToken);
}

/// <summary>What a machine hands the runner when its file says <c>accept-agent-login</c>.</summary>
/// <remarks>
/// <b>Two ports as one decision.</b> Starting the ceremony and keeping what
/// it mints are the same grant: a machine that let the child run and then
/// had nowhere to put the token would have spent a person's browser visit on
/// nothing.
/// </remarks>
public sealed record AgentLoginPorts(IRunAnAgentLogin Runs, IKeepACredential Keeps);

/// <summary>
/// At most one login ceremony per runner: begun on a console's ask, finished
/// with the code a person brings, ended at <see cref="Patience"/> if nobody
/// does.
/// </summary>
/// <remarks>
/// <para>
/// <b>One at a time, under a lock, and never while flying.</b> A second
/// <c>setup-token</c> beside a first would be two children waiting for one
/// person; one beside a flight spends the allowance the flight is spending.
/// The dispatch says which flight, so the person knows what to wait for.
/// </para>
/// <para>
/// <b>The token is held for exactly as long as it takes to write it.</b> It
/// is read off the child, handed to the keeper under the agent's locator,
/// and dropped; nothing here retains it, logs it, or says it. The two journal
/// lines - started, ended - carry the provider and a boolean.
/// </para>
/// <para>
/// <b>Nothing is awaited under the lock.</b> The child's URL can take eight
/// seconds and its token as long as a person takes; the loop's beat sweeps
/// expiry on its own thread and must not wait on either.
/// </para>
/// </remarks>
public sealed class AgentLoginCeremony(
    IAuthenticateAnAgent agent,
    AgentLoginPorts ports,
    IClock clock,
    Action<string>? kept = null,
    Action<string>? saying = null) : IDisposable
{
    private enum Phase
    {
        Idle,
        Starting,
        Waiting,
        Finishing,
    }

    /// <summary>How long a begun ceremony waits for its code before it is ended.</summary>
    public static readonly TimeSpan Patience = TimeSpan.FromMinutes(10);

    /// <summary>How long the child has to print its URL.</summary>
    /// <remarks>Eight seconds measured; this is the bound, not the expectation.</remarks>
    public static readonly TimeSpan UrlPatience = TimeSpan.FromSeconds(30);

    /// <summary>How long the child has to mint a token once the code is typed.</summary>
    public static readonly TimeSpan TokenPatience = TimeSpan.FromSeconds(90);

    private readonly Lock _gate = new();
    private Phase _phase = Phase.Idle;
    private IAgentLoginChild? _child;
    private DateTimeOffset _startedAt;

    public string Provider => agent.Provider;

    /// <summary>Whether a ceremony is begun and waiting for its code.</summary>
    public bool InProgress
    {
        get
        {
            lock (_gate)
            {
                return _phase is Phase.Waiting or Phase.Finishing;
            }
        }
    }

    /// <summary>Pure: whether a ceremony begun at <paramref name="startedAt"/> is over at <paramref name="now"/>.</summary>
    public static bool IsOver(DateTimeOffset startedAt, DateTimeOffset now) =>
        now - startedAt >= Patience;

    /// <summary>
    /// Begins the ceremony, or says why not.
    /// </summary>
    /// <param name="flying">The flight this runner is flying, or null when idle.</param>
    public async Task<AgentLoginBegun> BeginAsync(string? flying, CancellationToken cancellationToken)
    {
        if (flying is { Length: > 0 })
        {
            return Refused(
                $"{flying} is flying on this runner, and a second {Provider} beside it would spend "
              + "the allowance the flight is spending. Wait for it to land and begin again.");
        }

        var now = clock.UtcNow;
        lock (_gate)
        {
            if (_phase is not Phase.Idle)
            {
                return Refused(
                    $"a {Provider} login is already in progress on this runner until "
                  + $"{_startedAt + Patience:u}; finish it with the code, or wait for it to expire.",
                    _startedAt + Patience);
            }

            _phase = Phase.Starting;
            _startedAt = now;
        }

        IAgentLoginChild child;
        try
        {
            child = await ports.Runs.StartAsync(cancellationToken);
        }
        catch (Exception failure) when (failure is not OperationCanceledException)
        {
            lock (_gate)
            {
                _phase = Phase.Idle;
            }

            return Refused(
                $"the {Provider} login ceremony could not be started on this runner "
              + $"({failure.GetType().Name}). Its journal says more.");
        }

        var url = await Bounded(child.UrlAsync, UrlPatience, cancellationToken);

        if (url is not { Length: > 0 })
        {
            child.Dispose();
            lock (_gate)
            {
                _phase = Phase.Idle;
            }

            return Refused(
                $"the {Provider} login ceremony started but printed no login URL before it exited "
              + $"or {UrlPatience.TotalSeconds:0} s passed. Its journal says more.");
        }

        lock (_gate)
        {
            _child = child;
            _phase = Phase.Waiting;
        }

        saying?.Invoke($"agent login started for {Provider}; it expires at {now + Patience:u}");

        return new AgentLoginBegun
        {
            Provider = Provider,
            Started = true,
            Url = url,
            ExpiresAt = now + Patience,
        };
    }

    /// <summary>
    /// Types the code into the begun ceremony, keeps what the agent mints, and
    /// says only where it landed.
    /// </summary>
    public async Task<AgentLoginFinished> FinishAsync(string code, CancellationToken cancellationToken)
    {
        IAgentLoginChild child;
        lock (_gate)
        {
            if (_phase is not Phase.Waiting || _child is null)
            {
                return NotWritten(
                    "no login is in progress on this runner. Begin one first; a ceremony ends when "
                  + "it is finished, when it expires, or when the runner stops.");
            }

            child = _child;
            _phase = Phase.Finishing;
        }

        var token = await Bounded(ct => child.TokenAsync(code, ct), TokenPatience, cancellationToken);

        // WRITTEN, THEN DROPPED. The keeper's answer is the whole of what is
        // remembered about the value.
        var written = token is { Length: > 0 } && ports.Keeps.Keep(agent.Locator, token);
        token = null;

        child.Dispose();
        lock (_gate)
        {
            _child = null;
            _phase = Phase.Idle;
        }

        saying?.Invoke($"agent login for {Provider} ended, written: {(written ? "true" : "false")}");

        if (written)
        {
            kept?.Invoke(agent.Locator);
            return new AgentLoginFinished
            {
                Provider = Provider,
                Locator = agent.Locator,
                Written = true,
            };
        }

        return NotWritten(
            $"the {Provider} agent printed no token after the code was typed - the code may have "
          + "been wrong, or the ceremony had expired on the agent's side. Begin again.");
    }

    /// <summary>Ends a ceremony nobody finished, on the beat's clock.</summary>
    public void Expire(DateTimeOffset now)
    {
        IAgentLoginChild? child;
        lock (_gate)
        {
            if (_phase is not Phase.Waiting || _child is null || !IsOver(_startedAt, now))
            {
                return;
            }

            child = _child;
            _child = null;
            _phase = Phase.Idle;
        }

        child.Dispose();
        saying?.Invoke($"agent login for {Provider} ended, written: false (nobody finished it within {Patience})");
    }

    public void Dispose()
    {
        IAgentLoginChild? child;
        lock (_gate)
        {
            child = _child;
            _child = null;
            _phase = Phase.Idle;
        }

        child?.Dispose();
    }

    private AgentLoginBegun Refused(string why, DateTimeOffset? expiresAt = null) => new()
    {
        Provider = Provider,
        Started = false,
        Diagnosis = why,
        ExpiresAt = expiresAt,
    };

    private AgentLoginFinished NotWritten(string why) => new()
    {
        Provider = Provider,
        Locator = agent.Locator,
        Written = false,
        Diagnosis = why,
    };

    /// <summary>Null when the bound passes before the child answers; the caller's own token still cancels.</summary>
    private static async Task<string?> Bounded(
        Func<CancellationToken, Task<string?>> read, TimeSpan patience, CancellationToken cancellationToken)
    {
        using var patient = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        patient.CancelAfter(patience);
        try
        {
            return await read(patient.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
    }
}
