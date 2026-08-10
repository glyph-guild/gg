namespace Gg.Contracts.Description;

/// <summary>Which credential an endpoint answers to.</summary>
public enum Audience
{
    /// <summary>No credential. Only the path by which one is obtained.</summary>
    Anonymous,

    /// <summary>A person, acting through a browser session or <c>gg</c>.</summary>
    Developer,

    /// <summary>A runner, acting through the runner protocol and nothing else.</summary>
    Runner,
}

/// <summary>One endpoint of the gg-to-control-plane protocol.</summary>
public sealed record Endpoint
{
    public required string Method { get; init; }

    public required string Path { get; init; }

    /// <summary>Which credential this endpoint answers to.</summary>
    public required Audience Audience { get; init; }

    /// <summary>Wire type of the request body, or null if there is none.</summary>
    public Type? Request { get; init; }

    /// <summary>Wire type of the success response body, or null if there is none.</summary>
    public Type? Response { get; init; }

    /// <summary>
    /// Every status this endpoint may answer with. A client must handle all of
    /// them and a server must produce no others.
    /// </summary>
    public required IReadOnlyList<int> Statuses { get; init; }

    /// <summary>Headers the caller must send beyond the version headers.</summary>
    public IReadOnlyList<string> RequiredHeaders { get; init; } = [];
}

/// <summary>
/// The HTTP surface of the protocol, declared once and checked from both sides.
/// </summary>
/// <remarks>
/// <para>
/// This exists because the two halves of the protocol live in two
/// repositories that cannot reference each other. Before it, <c>gg</c> was
/// tested against a stub written in this repo and the control plane against
/// its own edge, so both suites could stay green while the two disagreed
/// about a header name, a status code or a JSON casing - and the first thing
/// to notice would have been a real customer.
/// </para>
/// <para>
/// Now both sides check themselves against THIS, and a disagreement fails
/// somebody's build. It still is not an end-to-end test - nothing here runs a
/// real gg against a real control plane - but it removes the class of
/// divergence that silence was hiding.
/// </para>
/// <para>
/// Header names and the protocol revision live here rather than being
/// declared on each side and asserted equal: one definition cannot drift from
/// itself.
/// </para>
/// </remarks>
public static class ProtocolSurface
{
    /// <summary>
    /// Wire protocol revision. Bumped only when the protocol becomes
    /// incompatible - not when the package version moves.
    /// </summary>
    public const int Revision = 1;

    /// <summary>Carries a developer's session token.</summary>
    public const string SessionHeader = "X-Gg-Session";

    /// <summary>Carries a runner's credential. Deliberately NOT the session header.</summary>
    public const string RunnerHeader = "X-Gg-Runner";

    /// <summary>Carries the revision the caller speaks.</summary>
    public const string ProtocolHeader = "GG-Protocol-Version";

    /// <summary>Carries the calling binary's own version.</summary>
    public const string RunnerVersionHeader = "GG-Runner-Version";

    /// <summary>Carries the fact vocabulary the caller evaluates against.</summary>
    public const string FactVocabularyHeader = "GG-Fact-Vocabulary";

    /// <summary>Names the range a 426 would accept.</summary>
    public const string SupportedProtocolsHeader = "GG-Supported-Protocols";

    /// <summary>Sent on every request to a governed path.</summary>
    public static IReadOnlyList<string> VersionHeaders { get; } =
        [ProtocolHeader, RunnerVersionHeader, FactVocabularyHeader];

    /// <summary>
    /// The path prefixes this declaration governs completely.
    /// </summary>
    /// <remarks>
    /// Scoped rather than "everything under /v1", because the control plane
    /// also serves a tenant API that gg does not speak yet. Within these
    /// prefixes the declaration is CLOSED: a route the control plane serves
    /// and this file does not name is a divergence, and the control plane's
    /// tests fail on it.
    ///
    /// /v1/flights joined at step 4a. The flight read surface sat outside the
    /// declaration while nothing consumed it, which was honest - a declaration
    /// nobody checks is a comment. The console consumes it, so it comes in
    /// under the same closure guarantee as the rest.
    /// </remarks>
    public static IReadOnlyList<string> GovernedPrefixes { get; } =
        ["/v1/auth", "/v1/runner", "/v1/leases", "/v1/flights", "/v1/telemetry"];

    /// <summary>Refusal for a caller below the protocol floor.</summary>
    public const int ProtocolTooOld = 426;

    /// <summary>Every endpoint gg may call.</summary>
    public static IReadOnlyList<Endpoint> Endpoints { get; } =
    [
        new()
        {
            Method = "POST",
            Path = "/v1/auth/device",
            Audience = Audience.Anonymous,
            Request = typeof(DeviceAuthorizationRequest),
            Response = typeof(DeviceAuthorizationStarted),
            Statuses = [200, ProtocolTooOld],
        },
        new()
        {
            Method = "POST",
            Path = "/v1/auth/device/token",
            Audience = Audience.Anonymous,
            Request = typeof(DeviceTokenRequest),
            Response = typeof(SessionIssued),
            // 202 is a WAIT, not a failure. 410 covers expired, denied,
            // already-used and never-existed alike, so an unauthenticated
            // caller cannot probe for live device codes.
            Statuses = [200, 202, 410, ProtocolTooOld],
        },
        new()
        {
            Method = "GET",
            Path = "/v1/auth/whoami",
            Audience = Audience.Developer,
            Response = typeof(WhoAmI),
            Statuses = [200, 401, 403, ProtocolTooOld],
            RequiredHeaders = [SessionHeader],
        },
        new()
        {
            Method = "POST",
            Path = "/v1/auth/logout",
            Audience = Audience.Developer,
            Statuses = [204, 401, 403, ProtocolTooOld],
            RequiredHeaders = [SessionHeader],
        },
        new()
        {
            Method = "POST",
            Path = "/v1/runners",
            Audience = Audience.Developer,
            Request = typeof(RunnerRegistrationRequest),
            Response = typeof(RunnerRegistered),
            Statuses = [200, 401, 403, ProtocolTooOld],
            RequiredHeaders = [SessionHeader],
        },
        new()
        {
            Method = "GET",
            Path = "/v1/runner/hello",
            Audience = Audience.Runner,
            Statuses = [200, 401, 403, ProtocolTooOld],
            RequiredHeaders = [RunnerHeader],
        },
        new()
        {
            Method = "POST",
            Path = "/v1/runners/{id}/heartbeat",
            Audience = Audience.Runner,
            Request = typeof(RunnerHeartbeat),
            Response = typeof(HeartbeatAccepted),
            // 404 rather than 403 for another runner's id: a runner learning
            // which ids exist is a runner enumerating the tenant's fleet.
            Statuses = [200, 401, 403, 404, ProtocolTooOld],
            RequiredHeaders = [RunnerHeader],
        },
        new()
        {
            Method = "POST",
            Path = "/v1/leases:claim",
            Audience = Audience.Runner,
            Request = typeof(LeaseClaimRequest),
            Response = typeof(LeaseGranted),
            // 204 is "nothing to do", the normal answer for an idle fleet. It
            // is not an error and must not read as one, for the same reason
            // the device poll answers 202.
            Statuses = [200, 204, 401, 403, ProtocolTooOld],
            RequiredHeaders = [RunnerHeader],
        },
        new()
        {
            Method = "POST",
            Path = "/v1/leases/{id}/renew",
            Audience = Audience.Runner,
            Request = typeof(LeaseRenewalRequest),
            Response = typeof(LeaseRenewed),
            // 409 is the generation fence refusing a stale holder. It is a
            // real outcome of correct client behaviour, not a client bug.
            Statuses = [200, 401, 403, 404, 409, ProtocolTooOld],
            RequiredHeaders = [RunnerHeader],
        },
        new()
        {
            Method = "POST",
            Path = "/v1/leases/{id}/release",
            Audience = Audience.Runner,
            Request = typeof(LeaseReleaseRequest),
            Response = typeof(LeaseReleased),
            Statuses = [200, 401, 403, 404, 409, ProtocolTooOld],
            RequiredHeaders = [RunnerHeader],
        },

        // The flight read surface. Developer audience throughout: a runner that
        // could read the flight list could enumerate a tenant's work from a
        // credential meant only to let it hold one lease at a time.
        new()
        {
            Method = "POST",
            Path = "/v1/flights",
            Audience = Audience.Developer,
            Request = typeof(FlightLaunchRequest),
            Response = typeof(FlightLaunched),
            // 202, not 200: the edge dispatches a command and the flight is
            // materialized asynchronously. Answering 200 would claim the
            // flight is readable, and the very next GET might disagree.
            // 400 is a refused intent - Article XI, with a diagnosis.
            Statuses = [202, 400, 401, 403, ProtocolTooOld],
            RequiredHeaders = [SessionHeader],
        },
        new()
        {
            Method = "GET",
            Path = "/v1/flights",
            Audience = Audience.Developer,
            Response = typeof(FlightList),
            Statuses = [200, 401, 403, ProtocolTooOld],
            RequiredHeaders = [SessionHeader],
        },
        new()
        {
            Method = "GET",
            Path = "/v1/flights/{ref}",
            Audience = Audience.Developer,
            Response = typeof(FlightSummary),
            // {ref} is a uuid OR a flight number. Both resolve here, by the
            // one parser in FlightRef; a reference in neither form is a 404
            // rather than a 400, because "GG-nope" names no flight in exactly
            // the way a well-formed id for somebody else's flight does.
            Statuses = [200, 401, 403, 404, ProtocolTooOld],
            RequiredHeaders = [SessionHeader],
        },
        new()
        {
            Method = "GET",
            Path = "/v1/flights/{ref}/log",
            Audience = Audience.Developer,
            Response = typeof(FlightLog),
            Statuses = [200, 401, 403, 404, ProtocolTooOld],
            RequiredHeaders = [SessionHeader],
        },
        new()
        {
            Method = "GET",
            Path = "/v1/telemetry",
            Audience = Audience.Developer,
            Response = typeof(TelemetryDisclosure),
            // Developer, not anonymous. It says nothing about a tenant, but an
            // unauthenticated endpoint disclosing where a deployment ships its
            // logs is a reconnaissance surface for anyone who finds the host.
            Statuses = [200, 401, 403, ProtocolTooOld],
            RequiredHeaders = [SessionHeader],
        },
        new()
        {
            Method = "GET",
            Path = "/v1/runners",
            Audience = Audience.Developer,
            Response = typeof(RunnerList),
            // A person reads the fleet; a runner beats. Same path, and the two
            // audiences never overlap.
            Statuses = [200, 401, 403, ProtocolTooOld],
            RequiredHeaders = [SessionHeader],
        },
    ];

    /// <summary>
    /// Whether a concrete request path is this endpoint, treating <c>{...}</c>
    /// segments as placeholders.
    /// </summary>
    /// <remarks>
    /// The control plane can compare its route patterns to <see cref="Endpoint.Path"/>
    /// literally; a client cannot, because what it actually put on the wire has
    /// a real lease id in it. Both sides need the same answer, so the matching
    /// rule lives here rather than being written twice and drifting.
    /// </remarks>
    public static bool Matches(Endpoint endpoint, string method, string concretePath)
    {
        ArgumentNullException.ThrowIfNull(endpoint);

        if (!string.Equals(endpoint.Method, method, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var declared = endpoint.Path.Split('/');
        var actual = concretePath.Split('/');
        if (declared.Length != actual.Length)
        {
            return false;
        }

        for (var i = 0; i < declared.Length; i++)
        {
            var segment = declared[i];
            if (segment.StartsWith('{') && segment.EndsWith('}'))
            {
                // A placeholder matches anything except emptiness: an empty
                // segment means the client sent /v1/leases//renew, which is a
                // missing id rather than a match.
                if (actual[i].Length == 0)
                {
                    return false;
                }
                continue;
            }

            if (!string.Equals(segment, actual[i], StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>The endpoint a concrete request belongs to, or null.</summary>
    public static Endpoint? Find(string method, string concretePath) =>
        Endpoints.FirstOrDefault(e => Matches(e, method, concretePath));

    /// <summary>
    /// The JSON member names each wire type must serialize to.
    /// </summary>
    /// <remarks>
    /// Declared, not derived. If each side derived these from its own
    /// serializer they would agree with themselves and prove nothing - which
    /// is exactly how a camelCase-versus-PascalCase split survives two green
    /// test suites. Compared as a SET: JSON is not positional, so member order
    /// is not part of the contract.
    /// </remarks>
    public static IReadOnlyDictionary<Type, IReadOnlyList<string>> JsonMembers { get; } =
        new Dictionary<Type, IReadOnlyList<string>>
        {
            [typeof(ProtocolHello)] = ["protocolVersion", "component", "componentVersion"],
            [typeof(DeviceAuthorizationRequest)] = ["deviceLabel"],
            [typeof(DeviceAuthorizationStarted)] =
                ["deviceCode", "userCode", "verificationUri", "pollIntervalSeconds", "expiresAt"],
            [typeof(DeviceTokenRequest)] = ["deviceCode"],
            [typeof(SessionIssued)] = ["sessionToken", "expiresAt", "principalDisplay", "tenantId"],
            [typeof(WhoAmI)] = ["principalId", "principalDisplay", "tenantId", "expiresAt"],
            [typeof(RunnerRegistrationRequest)] = ["label", "protocolVersion"],
            [typeof(RunnerRegistered)] = ["runnerId", "runnerToken", "expiresAt"],
            [typeof(RunnerHeartbeat)] = ["labels"],
            [typeof(HeartbeatAccepted)] = ["nextHeartbeatSeconds"],
            [typeof(LeaseClaimRequest)] = ["runnerId", "labels", "maxWaitSeconds"],
            [typeof(LeaseRepoRef)] = ["provider", "slug", "pinnedRef"],
            [typeof(LeaseGranted)] =
                ["leaseId", "generation", "flightId", "flightNumber", "repos",
                 "classificationCeiling", "expiresAt", "renewWithinSeconds"],
            [typeof(LeaseRenewalRequest)] = ["generation"],
            [typeof(LeaseRenewed)] = ["expiresAt", "generation"],
            [typeof(LeaseReleaseRequest)] = ["generation", "disposition", "detail"],
            [typeof(LeaseReleased)] = ["flightId", "disposition"],
            [typeof(FlightIntent)] = ["kind", "uri", "text"],
            [typeof(FlightLaunchRequest)] = ["name", "intent"],
            [typeof(FlightLaunched)] = ["flightId", "flightNumber"],
            [typeof(FlightSummary)] =
                ["flightId", "flightNumber", "name", "intent", "createdAt",
                 "runnerProtocolVersion", "factVocabularyVersion", "constitutionVersion", "envelopeVersion"],
            [typeof(FlightList)] = ["flights"],
            [typeof(FlightLogEntry)] = ["at", "kind", "detail"],
            [typeof(FlightLog)] = ["flightId", "flightNumber", "entries"],
            [typeof(RunnerSummary)] =
                ["runnerId", "label", "state", "currentFlightId", "currentFlightNumber", "lastHeartbeatAt"],
            [typeof(RunnerList)] = ["runners"],
            [typeof(TelemetryDisclosure)] = ["exporting", "destination"],
        };
}
