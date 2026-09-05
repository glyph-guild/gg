namespace Gg.Contracts;

/// <summary>
/// Whether a fact still carries anything that can drive a terminal.
/// </summary>
/// <remarks>
/// <para>
/// <b>Declared here because both sides need the same answer.</b> The runner
/// strips at production, before the digest, so the fact as produced is clean and
/// its hash is the clean one's hash. The control plane re-validates at ingress
/// and refuses what is dirty. Two field lists would drift, and the field that
/// drifted would be the one nobody checked.
/// </para>
/// <para>
/// <b>It detects rather than cleans.</b> Cleaning on the far side would make the
/// stored bytes disagree with the hash that proves what they were, so the only
/// honest answers there are accept or refuse. A dirty fact means a runner that
/// did not strip its own output, which is misconfigured or modified - and that
/// is a thing a tenant should be told rather than tidied away.
/// </para>
/// <para>
/// This is the same shape as classification: the runner filters before egress
/// and the control plane re-derives at ingress, and neither is the only control.
/// </para>
/// </remarks>
public static class FactCleanliness
{
    /// <summary>
    /// The diagnosis, or null when nothing in this fact carries a control
    /// sequence.
    /// </summary>
    /// <remarks>
    /// The offending FIELD is named. "This fact is dirty" sends somebody reading
    /// all of them, and the whole point of refusing rather than cleaning is that
    /// somebody can act on it.
    /// </remarks>
    public static string? Unclean(FactEnvelope fact)
    {
        ArgumentNullException.ThrowIfNull(fact);

        if (First(
            [
                ("kind", fact.Kind, false),
                ("idempotencyKey", fact.IdempotencyKey, false),
                ("digest", fact.Digest, false),
            ]) is { } bad)
        {
            return Diagnosis(bad);
        }

        if (fact.Loop is { } loop && First(
            [
                ("loop.loopId", loop.LoopId, false),
                ("loop.outcome", loop.Outcome, false),
                // Prose a person reads, so line breaks are not a defect here.
                ("loop.reason", loop.Reason, true),
                ("loop.executor", loop.Executor, false),
                .. loop.MovesUsed.Select(m => ("loop.movesUsed", m, false)),
            ]) is { } badLoop)
        {
            return Diagnosis(badLoop);
        }

        if (fact.Source is { } source && First(
            [
                ("source.provider", source.Provider, false),
                ("source.slug", source.Slug, false),
                ("source.requestedRef", source.RequestedRef, false),
                ("source.resolvedRef", source.ResolvedRef, false),
                ("source.headCommit", source.HeadCommit, false),
                ("source.forkSlug", source.ForkSlug ?? "", false),
            ]) is { } badSource)
        {
            return Diagnosis(badSource);
        }

        if (fact.Change is { } change && First(
            [
                ("change.baseCommit", change.BaseCommit, false),
                ("change.headCommit", change.HeadCommit, false),
                ("change.resolution", change.Resolution, false),
                ("change.diffBasis", change.DiffBasis, false),
                .. change.Paths.Select(p => ("change.paths.path", p.Path, false)),
                .. change.Paths.Select(p => ("change.paths.change", p.Change, false)),
                .. change.Directories.Select(d => ("change.directories", d.Directory, false)),
                .. change.Languages.Select(l => ("change.languages", l.Language, false)),
            ]) is { } badChange)
        {
            return Diagnosis(badChange);
        }

        if (fact.Environment is { } environment && First(
            [
                ("environment.hostFingerprint", environment.HostFingerprint, false),
                ("environment.imageDigest", environment.ImageDigest ?? "", false),
                ("environment.provenance", environment.Provenance, false),
                .. environment.Tools.Select(t => ("environment.tools.name", t.Name, false)),
                .. environment.Tools.Select(t => ("environment.tools.version", t.Version, false)),
                .. environment.Locks.Select(l => ("environment.locks.path", l.Path, false)),
            ]) is { } badEnvironment)
        {
            return Diagnosis(badEnvironment);
        }

        if (fact.Transcript is { } transcript && First(
            [
                ("transcript.locator", transcript.Locator, false),
                ("transcript.mediaType", transcript.MediaType, false),
                ("transcript.scope", transcript.Scope, false),
            ]) is { } badTranscript)
        {
            return Diagnosis(badTranscript);
        }

        if (fact.Pushed is { } pushed && First(
            [
                ("pushed.slug", pushed.Slug, false),
                ("pushed.branch", pushed.Branch, false),
                ("pushed.commit", pushed.Commit, false),
            ]) is { } badPush)
        {
            return Diagnosis(badPush);
        }

        if (fact.Landed is { } landed && First(
            [
                ("landed.destinationId", landed.DestinationId, false),
                ("landed.branch", landed.Branch, false),
                ("landed.pullRequestUri", landed.PullRequestUri, false),
            ]) is { } badLanding)
        {
            return Diagnosis(badLanding);
        }

        if (fact.Human is { } human && First(
            [
                ("human.by", human.By, false),
                // Prose a person wrote for a reader, so line breaks are theirs.
                ("human.statement", human.Statement, true),
                ("human.confirmation", human.Confirmation, false),
            ]) is { } badHuman)
        {
            return Diagnosis(badHuman);
        }

        if (fact.Nomination is { } nomination && First(
            [
                ("nomination.workKind", nomination.WorkKind, false),
                // Prose the agent wrote for a reader, so line breaks are its own.
                ("nomination.reason", nomination.Reason, true),
            ]) is { } badNomination)
        {
            return Diagnosis(badNomination);
        }

        if (fact.Question is { } question && First(
            [
                // Prose the agent wrote for a reader, so line breaks are its
                // own - a question laid out over three lines is one somebody
                // wrote to be read, and this is the field a person reads while
                // deciding something.
                ("question.question", question.Question, true),
            ]) is { } badQuestion)
        {
            return Diagnosis(badQuestion);
        }

        if (fact.LoopDigest is { } summary && First(
            [
                ("loopDigest.loopId", summary.LoopId, false),
                ("loopDigest.stopReason", summary.StopReason, false),
                .. summary.FilesReadNotEdited.Select(f => ("loopDigest.filesReadNotEdited", f, false)),
                .. summary.FilesEdited.Select(f => ("loopDigest.filesEdited", f, false)),
                .. summary.Searches.Select(s => ("loopDigest.searches", s, false)),
                .. summary.RefusedMoves.Select(m => ("loopDigest.refusedMoves", m, false)),
                .. summary.Errors.Select(e => ("loopDigest.errors.source", e.Source, false)),
                .. summary.Errors.Select(e => ("loopDigest.errors.detail", e.Detail, false)),
            ]) is { } badDigest)
        {
            return Diagnosis(badDigest);
        }

        return null;
    }

    /// <summary>The first field whose value changes when it is stripped.</summary>
    private static string? First(IReadOnlyList<(string Field, string Value, bool Prose)> fields)
    {
        foreach (var (field, value, prose) in fields)
        {
            if (!string.Equals(ControlText.Strip(value, prose), value, StringComparison.Ordinal))
            {
                return field;
            }
        }

        return null;
    }

    private static string Diagnosis(string field) =>
        $"'{field}' carries terminal control sequences. It is refused rather than cleaned: the "
      + "digest was computed over the fact as it was produced, so altering it here would make what "
      + "is stored disagree with the hash that proves what it was. The runner strips its own "
      + "output at production, so a fact arriving dirty came from one that is misconfigured or "
      + "modified.";
}
