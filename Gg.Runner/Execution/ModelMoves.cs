namespace Gg.Runner.Execution;

/// <summary>
/// The model a running flight has been moved to, handed from the lease's
/// renewals to the executor holding the child (slice seventy-six).
/// </summary>
/// <remarks>
/// <para>
/// <b>The one thing a renewal says beyond "longer".</b> The lease is composed
/// again on every renewal, and a person may move a flight in the air to another
/// model root lists; the renewal's <c>model</c> is where the runner learns it,
/// and this is how the renewal loop tells the executor without either knowing
/// the other's shape. The loop calls <see cref="Move"/>; the executor awaits
/// <see cref="NextAsync"/> beside the child's exit and reads <see cref="Latest"/>.
/// </para>
/// <para>
/// <b>The same name twice is not a move.</b> Every renewal of a moved flight
/// carries the same name, and a signal per renewal would relaunch the child
/// every few minutes.
/// </para>
/// </remarks>
public sealed class ModelMoves(Action<string>? relaunched = null)
{
    private readonly object _gate = new();
    private string? _latest;
    private TaskCompletionSource _next = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>The model most recently moved to, or null when nobody has moved it.</summary>
    public string? Latest
    {
        get
        {
            lock (_gate)
            {
                return _latest;
            }
        }
    }

    /// <summary>Records a move, waking whoever awaits <see cref="NextAsync"/> when it is new.</summary>
    public void Move(string model)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(model);
        TaskCompletionSource woken;
        lock (_gate)
        {
            if (string.Equals(_latest, model, StringComparison.Ordinal))
            {
                return;
            }

            _latest = model;
            woken = _next;
            _next = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        woken.TrySetResult();
    }

    /// <summary>The executor started the agent over on <paramref name="model"/>.</summary>
    public void Relaunched(string model)
    {
        _ = relaunched;
    }

    /// <summary>Completes at the next move after this call, or now if one is already pending unread.</summary>
    public Task NextAsync(CancellationToken cancellationToken)
    {
        Task next;
        lock (_gate)
        {
            next = _next.Task;
        }

        return next.WaitAsync(cancellationToken);
    }
}
