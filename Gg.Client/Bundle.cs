using Gg.Contracts;

namespace Gg.Client;

/// <summary>Whether a bundle has both halves or only the local one.</summary>
/// <remarks>
/// Stated rather than inferred. A bundle with no flight log and no statement
/// is indistinguishable from a complete one taken on a tenant that has never
/// flown, and those two lead somewhere completely different.
/// </remarks>
public static class BundleCompleteness
{
    public const string Complete = "complete";

    public const string LocalOnly = "local-only";
}

/// <summary>
/// Something working less well than it looks, in one line.
/// </summary>
/// <remarks>
/// The rule this exists for: every silent degradation writes one line, and it
/// is visible in <c>gg doctor</c> and in the bundle. A degradation visible in
/// only one of the two is one somebody reports and we cannot reproduce.
/// </remarks>
public sealed record BundleDegradation
{
    public required string Name { get; init; }

    public required string Detail { get; init; }

    /// <summary>What to do, when the check knew. Null rather than a placeholder.</summary>
    public string? Remedy { get; init; }

    /// <summary>Whether this stops work rather than slowing it.</summary>
    public required bool Blocking { get; init; }
}

/// <summary>
/// The document somebody sends us when it went wrong.
/// </summary>
/// <remarks>
/// <para>
/// <b>There is nowhere here to put a line of runner output.</b> Not by
/// convention - by shape. The live channel carries whatever a tool decided to
/// echo, including things it should not have, and a member capable of holding
/// it is a member somebody eventually fills.
/// </para>
/// <para>
/// What it does carry: versions, the environment fingerprint a flight's own
/// facts can be matched against, every check <c>gg doctor</c> ran, the
/// degradations among them, and the flight log when the control plane was
/// reachable.
/// </para>
/// </remarks>
public sealed record DiagnosticsBundle
{
    public required DateTimeOffset TakenAt { get; init; }

    /// <summary>One of <see cref="BundleCompleteness"/>.</summary>
    public required string Completeness { get; init; }

    /// <summary>Why, in a sentence, whichever it is.</summary>
    public required string CompletenessDetail { get; init; }

    public required string Binary { get; init; }

    public required int Protocol { get; init; }

    public required string FactVocabulary { get; init; }

    /// <summary>
    /// This machine, as the runner would record it.
    /// </summary>
    /// <remarks>
    /// The same fact type and the same fingerprint, so a bundle can be matched
    /// against the environment recorded on a flight. A second description of
    /// the environment invented here would be a second thing to keep in step.
    /// </remarks>
    public required EnvironmentIdentity Environment { get; init; }

    /// <summary>Everything <c>gg doctor</c> asked, passing and not.</summary>
    public required IReadOnlyList<DoctorCheck> Checks { get; init; }

    /// <summary>The subset that is not fine, one line each.</summary>
    public required IReadOnlyList<BundleDegradation> Degradations { get; init; }

    /// <summary>The flight log, when there was a control plane to read it from.</summary>
    public FlightLog? FlightLog { get; init; }
}

/// <summary>
/// Builds the bundle out of what the other verbs already produced.
/// </summary>
/// <remarks>
/// It computes nothing of its own except completeness. Everything here has
/// already been through a verb, which is what keeps the bundle and the rest of
/// <c>gg</c> from disagreeing about the same machine.
/// </remarks>
public static class Bundle
{
    public static DiagnosticsBundle Build(
        DateTimeOffset takenAt,
        EnvironmentIdentity environment,
        DoctorReport doctor,
        FlightLog? flightLog)
    {
        ArgumentNullException.ThrowIfNull(doctor);

        // Read from the connectivity check, not from the absence of a log. An
        // empty log is an ordinary state for somebody who has not flown yet.
        var reachable = doctor.Checks
            .FirstOrDefault(c => c.Name == DoctorChecks.ControlPlane)?.Passed ?? false;

        return new DiagnosticsBundle
        {
            TakenAt = takenAt,
            Completeness = reachable ? BundleCompleteness.Complete : BundleCompleteness.LocalOnly,
            CompletenessDetail = reachable
                ? "The control plane answered, so this bundle has both halves."
                : "The control plane could not be reached, so this bundle contains local material "
                  + "only. There is no flight log in it, and that is a fact about the connection "
                  + "rather than about the tenant.",
            Binary = GgVersions.Binary,
            Protocol = GgVersions.Protocol,
            FactVocabulary = Contracts.FactVocabulary.Version,
            Environment = environment,
            Checks = doctor.Checks,
            Degradations =
            [
                .. doctor.Checks.Where(c => !c.Passed).Select(c => new BundleDegradation
                {
                    Name = c.Name,
                    Detail = c.Detail,
                    Remedy = c.Fixable ? c.Fix : null,
                    Blocking = c.Blocking,
                }),
            ],
            FlightLog = reachable ? flightLog : null,
        };
    }
}
