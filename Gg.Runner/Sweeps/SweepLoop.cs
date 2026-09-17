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
/// <param name="delay">How this loop waits. Injected, so a test's waiting is a test's.</param>
/// <param name="narrate">
/// Where this loop says what happened, or null for a caller that is not
/// watching. <c>MaintainLoop</c>'s scar: <i>"this loop reported NOTHING for its
/// whole life, so hours of crash-looping looked identical to hours of quietly
/// working."</i>
/// </param>
public sealed class SweepLoop(
    ISweepProtocol protocol,
    SkillReader skills,
    ISweepExecutor executor,
    IClock clock,
    string transcripts,
    Func<TimeSpan, CancellationToken, Task>? delay = null,
    Action<string>? narrate = null)
{
    private readonly ISweepProtocol _protocol = protocol;
    private readonly SkillReader _skills = skills;
    private readonly ISweepExecutor _executor = executor;
    private readonly IClock _clock = clock;
    private readonly string _transcripts = transcripts;
    private readonly Func<TimeSpan, CancellationToken, Task> _delay = delay ?? Task.Delay;
    private readonly Action<string> _narrate = narrate ?? (_ => { });

    /// <summary>How long to wait before asking again. Zero while things are well.</summary>
    private TimeSpan _backoff = TimeSpan.Zero;

    /// <summary>How this machine's credential ended, once a 401 says one has.</summary>
    private CredentialEnding? _endedCredential;

    /// <summary>
    /// How long between asks.
    /// </summary>
    /// <remarks>
    /// <b>Slower than a pool's, because a sweep is not an event.</b> A watch
    /// triggers on its own period and the control plane decides one row per
    /// tick, so asking every few seconds would be a poll that finds nothing
    /// hundreds of times between sweeps. Half a minute keeps a sweep inside the
    /// bounded latch a watch's period allows without making the wait itself the
    /// thing that delays one.
    /// </remarks>
    public static readonly TimeSpan PollEvery = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Pulls, sweeps and attests until cancelled. 0 is a session that ended.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b><c>MaintainLoop</c>'s three arms, and its three scars.</b> A
    /// transient failure waits and asks again - a deploy, a restart and a cold
    /// start all pass, and that loop died on one and was restarted into the
    /// same wall six times. A refusal waits too: a 400 may be a clock a
    /// fraction out, and when it is permanent a live loop saying so every cycle
    /// is more findable than a crash loop something keeps restarting. A 401
    /// stops, because no amount of waiting fixes this machine's credential.
    /// </para>
    /// <para>
    /// <b>Nothing is attested on the way out.</b> An attestation travels on the
    /// credential that was just refused, so the ledger is not somewhere this
    /// one can be said. The journal is.
    /// </para>
    /// </remarks>
    public async Task<int> RunAsync(string watch, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(watch);

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var swept = await PassAsync(watch, cancellationToken);

                if (swept > 0)
                {
                    _narrate($"Swept {swept} for '{watch}' and reported every one.");
                }

                // A SERVED CYCLE CLEARS IT, so an hour of health does not
                // inherit a bad minute's wait.
                _backoff = TimeSpan.Zero;
            }
            catch (InvalidOperationException refused)
            {
                // THE CONTROL PLANE REFUSED SOMETHING, which is not a reason to
                // stop sweeping: a sweep handed to another runner, a clock a
                // fraction out, a report the contract read differently. The
                // diagnosis is carried whole because it is the only thing
                // anybody can act on.
                _backoff = TransientFailure.Next(_backoff);
                _narrate($"{refused.Message} Asking again in {_backoff.TotalSeconds:0}s.");
            }
            catch (HttpRequestException refused)
                when (refused.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                // THE ONE REFUSAL THAT IS NOT WORTH WAITING FOR. Above the
                // transient arm, because a 401 is this machine's credential and
                // no deploy finishes and heals it.
                _endedCredential = CredentialEnding.For(
                    expiresAt: null, _clock.UtcNow, refused);

                _narrate(_endedCredential.Said);
                break;
            }
            catch (HttpRequestException refusal) when (TransientFailure.IsTransient(refusal))
            {
                _backoff = TransientFailure.Next(_backoff);
                _narrate(TransientFailure.Diagnose(refusal, _backoff));
            }

            try
            {
                await _delay(
                    _backoff == TimeSpan.Zero ? PollEvery : _backoff, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        // Cancellation is still a session that ended; a credential that was
        // taken away is not, and the code says which without anybody parsing
        // the sentence.
        return _endedCredential?.Exit ?? 0;
    }

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
