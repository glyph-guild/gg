using Gg.Contracts;
using Gg.Runner.Vcs;

namespace Gg.Runner.Sweeps;

/// <summary>Where a sweep's work arrives from and its reports go.</summary>
public interface ISweepProtocol
{
    /// <summary>The sweeps decided for this watch. Serving is the claim, control-plane-side.</summary>
    Task<WatchActionList> PullSweepsAsync(string watch, CancellationToken cancellationToken = default);

    /// <summary>Reports one sweep. Idempotent on the attestation id.</summary>
    Task AttestSweepAsync(
        string watch, WatchAttestation attestation, CancellationToken cancellationToken = default);
}

/// <summary>What an executor is handed to run one sweep.</summary>
public sealed record SweepRequest
{
    /// <summary>The sweep as it was served: the watch, its moves and its pin.</summary>
    public required WatchAction Action { get; init; }

    /// <summary>The skill's words, read at the pin.</summary>
    public required string Skill { get; init; }

    /// <summary>Where the executor's transcript is written.</summary>
    public required string TranscriptPath { get; init; }
}

/// <summary>What running one sweep concluded.</summary>
public abstract record SweepExecution
{
    private SweepExecution()
    {
    }

    /// <summary>It ran, and nominated these - possibly nothing.</summary>
    public sealed record Ran(IReadOnlyList<SweepNomination> Nominated) : SweepExecution;

    /// <summary>It could not do its job, and this is why.</summary>
    public sealed record Failed(string Diagnosis) : SweepExecution;
}

/// <summary>Runs one sweep's executor.</summary>
public interface ISweepExecutor
{
    Task<SweepExecution> RunAsync(SweepRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// The resident runner's sweep: pull the decided sweeps for a watch, run each,
/// and attest every one.
/// </summary>
/// <remarks>
/// <para>
/// <b><c>MaintainLoop</c>'s shape, one routine action over.</b> It mints no
/// flight and takes no lease: a sweep arrives as a decided row and leaves as an
/// attestation, which is what keeps the routine tier at zero flights per tick.
/// </para>
/// <para>
/// <b>Every sweep handed out ends in a report</b> - swept, possibly having
/// nominated nothing, or unreachable with the reason. Silence is the fault this
/// loop's sibling spent its life committing.
/// </para>
/// <para>
/// <b>Nothing is launched that cannot do its job.</b> A sweep carrying a
/// diagnosis, a skill that will not read, and a grant with no way to nominate
/// each attest unreachable first: an agent without instructions, or without the
/// one tool its output goes through, would spend tokens to report nothing.
/// </para>
/// </remarks>
/// <param name="transcripts">Where executors' transcripts go, normally <c>LocalPaths.Transcripts()</c>.</param>
public sealed class SweepLoop(
    ISweepProtocol protocol,
    SkillReader skills,
    ISweepExecutor executor,
    IClock clock,
    string transcripts)
{
    private readonly ISweepProtocol _protocol = protocol;
    private readonly SkillReader _skills = skills;
    private readonly ISweepExecutor _executor = executor;
    private readonly IClock _clock = clock;
    private readonly string _transcripts = transcripts;

    /// <summary>
    /// One pass: pull, sweep and attest each sweep this runner was handed.
    /// </summary>
    /// <returns>How many sweeps were attested.</returns>
    public async Task<int> PassAsync(string watch, CancellationToken cancellationToken)
    {
        var decided = await _protocol.PullSweepsAsync(watch, cancellationToken);

        foreach (var action in decided.Actions)
        {
            var attestation = await SweepAsync(action, cancellationToken);
            await _protocol.AttestSweepAsync(watch, attestation, cancellationToken);
        }

        return decided.Actions.Count;
    }

    /// <summary>Runs one sweep and says how it went, in a report the contract accepts.</summary>
    public async Task<WatchAttestation> SweepAsync(
        WatchAction action, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (action.Diagnosis is { Length: > 0 } || action.Skill is null)
        {
            return Unreachable(action, action.Diagnosis
                ?? "The sweep was served with no pin and no reason, so there was nothing to read.");
        }

        if (!action.Moves.Contains(LoopMoves.Propose, StringComparer.Ordinal))
        {
            return Unreachable(action,
                $"This sweep is granted no '{LoopMoves.Propose}' move, and a sweep's only output "
              + "is nominations - so nothing was launched. The sweep kind, or the floor, "
              + "withholds it.");
        }

        var read = await _skills.ReadAsync(
            new RepoTarget
            {
                Provider = action.Skill.Provider,
                Slug = action.Skill.Slug,
                PinnedRef = action.Skill.PinnedRef,
            },
            action.Document.Skill,
            cancellationToken);

        if (read is not SkillRead.Read { Skill: var skill })
        {
            return Unreachable(action, ((SkillRead.Unreadable)read).Diagnosis);
        }

        var ran = await _executor.RunAsync(
            new SweepRequest
            {
                Action = action,
                Skill = skill.Content,
                TranscriptPath = Path.Combine(
                    _transcripts, "sweeps", Gg.Local.LocalPaths.Safe(action.Watch),
                    action.ActionId.ToString("N") + ".ndjson"),
            },
            cancellationToken);

        return ran switch
        {
            SweepExecution.Ran { Nominated: var nominated } => new WatchAttestation
            {
                AttestationId = Guid.CreateVersion7(_clock.UtcNow),
                Watch = action.Watch,
                ActionId = action.ActionId,
                Outcome = WatchOutcomes.Swept,
                // THE FIRST THE CONTRACT CARRIES. A report it refuses loses every
                // nomination in it; the watch's own cap is the board's to apply.
                Nominated = [.. nominated.Take(WatchAttestation.MaxNominations)],
                MeasuredAt = _clock.UtcNow,
                SkillSha = skill.BlobSha,
            },
            SweepExecution.Failed { Diagnosis: var why } => Unreachable(action, why, skill.BlobSha),
            _ => throw new InvalidOperationException(
                $"The executor answered {ran.GetType().Name}, which nothing here reports."),
        };
    }

    private WatchAttestation Unreachable(WatchAction action, string diagnosis, string? skillSha = null) =>
        new()
        {
            AttestationId = Guid.CreateVersion7(_clock.UtcNow),
            Watch = action.Watch,
            ActionId = action.ActionId,
            Outcome = WatchOutcomes.Unreachable,
            MeasuredAt = _clock.UtcNow,
            Diagnosis = string.IsNullOrWhiteSpace(diagnosis)
                ? "The sweep could not do its job and did not say why."
                : diagnosis,
            SkillSha = skillSha,
        };
}
