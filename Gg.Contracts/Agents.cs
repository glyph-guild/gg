namespace Gg.Contracts;

/// <summary>
/// Whether a runner's agent can start: the two standings a machine can measure.
/// </summary>
/// <remarks>
/// <b>Two, and closed.</b> An unknown standing read as <c>ready</c> would clear a
/// gate over a broken machine, and one read as <c>needs-login</c> would open one
/// over a working machine - so a value this contract does not declare is
/// refused at the door rather than mapped to either.
/// </remarks>
[VocabularyOf(VocabularyFingerprints.Contract)]
public static class AgentStandings
{
    /// <summary>The agent has a credential source and can be started.</summary>
    public const string Ready = "ready";

    /// <summary>It has none, or the one it has does not work, and a person can fix that.</summary>
    public const string NeedsLogin = "needs-login";

    public static IReadOnlyList<string> All { get; } = [Ready, NeedsLogin];
}

/// <summary>
/// Where the agent's credential came from, as far as the machine can tell.
/// </summary>
/// <remarks>
/// <b><c>machine</c> is the one gg cannot revoke.</b> A login somebody made as
/// the runner's OS user is that person's, and <c>Forget</c> reaches nothing of
/// it; a <c>token</c> is the credential gg placed and gg can withdraw. The
/// difference is what a person reading the gate needs to know before choosing
/// which door to use.
/// </remarks>
[VocabularyOf(VocabularyFingerprints.Contract)]
public static class AgentCredentialSources
{
    /// <summary>A token gg holds, placed in the agent's environment at launch.</summary>
    public const string Token = "token";

    /// <summary>A login the machine's own user made, outside gg.</summary>
    public const string Machine = "machine";

    /// <summary>Nothing the agent could authenticate with.</summary>
    public const string None = "none";

    public static IReadOnlyList<string> All { get; } = [Token, Machine, None];
}

/// <summary>
/// A runner's measurement of whether its agent can start.
/// </summary>
/// <remarks>
/// <para>
/// <b>A reading, on the allowance readings' argument.</b> Its own route rather
/// than a field on the heartbeat, because <i>"a heartbeat is liveness only
/// because a runner able to report something about itself can report it while
/// dead"</i> - and it is a MEASUREMENT with its own <see cref="MeasuredAt"/>,
/// not a status a runner declares about itself. The runner's not-claiming is
/// the mechanism; this only tells a person why, so a gate can open, and when
/// it can close.
/// </para>
/// <para>
/// <b>Article VIII at the door.</b> The diagnosis is a sentence the runner
/// composed, never what the agent printed - and <see cref="Validate"/> refuses
/// one that looks like a token, because the one thing this route must never
/// carry is the credential it is about.
/// </para>
/// </remarks>
[PinnedId("6c2a9f47-8e1d-4b03-9a5f-2d7e1c4b8a90")]
public sealed record AgentReading
{
    /// <summary>The longest diagnosis a reading may carry.</summary>
    /// <remarks>The executor's own bound on a run's reason, for the same reader.</remarks>
    public const int MaxDiagnosis = 280;

    /// <summary>Which agent: the adapter key <c>GG_EXECUTOR_BINARY</c> declares.</summary>
    public required string Provider { get; init; }

    /// <summary>One of <see cref="AgentStandings"/>.</summary>
    /// <remarks>
    /// STANDING, NOT STATE. A runner's state - idle, busy, offline - is the
    /// control plane's to derive and nothing may report it; the surface tests
    /// refuse a request member so named. This is the AGENT's standing, measured
    /// on the machine, and the word is the runner's own for it.
    /// </remarks>
    public required string Standing { get; init; }

    /// <summary>One of <see cref="AgentCredentialSources"/>.</summary>
    public required string Source { get; init; }

    /// <summary>When the machine measured it, so the far side can see how old it is.</summary>
    public required DateTimeOffset MeasuredAt { get; init; }

    /// <summary>What a person reads, when the state is not ready. Composed, never quoted.</summary>
    public string? Diagnosis { get; init; }

    /// <summary>The diagnosis, or null when the reading is well formed.</summary>
    public static string? Validate(AgentReading reading)
    {
        ArgumentNullException.ThrowIfNull(reading);

        if (string.IsNullOrWhiteSpace(reading.Provider))
        {
            return "A reading names the agent it is about. Blank is not unset: a machine with "
                 + "no agent configured reports nothing at all.";
        }

        if (!AgentStandings.All.Contains(reading.Standing, StringComparer.Ordinal))
        {
            return $"'{reading.Standing}' is not a standing this contract declares. Reading an "
                 + "unknown one as ready would clear a gate over a broken machine, and as "
                 + "needs-login would open one over a working machine.";
        }

        if (!AgentCredentialSources.All.Contains(reading.Source, StringComparer.Ordinal))
        {
            return $"'{reading.Source}' is not a credential source this contract declares.";
        }

        if (reading.Diagnosis is { } diagnosis)
        {
            if (diagnosis.Length > MaxDiagnosis)
            {
                return $"A diagnosis is at most {MaxDiagnosis} characters, and this one is "
                     + $"{diagnosis.Length}. It is a sentence for a person, not a transcript.";
            }

            if (diagnosis.Contains("sk-ant-", StringComparison.OrdinalIgnoreCase))
            {
                return "The diagnosis looks like it carries a token. The one thing this route "
                     + "must never carry is the credential it is about.";
            }
        }

        return null;
    }
}
