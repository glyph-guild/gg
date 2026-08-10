namespace Gg.Contracts;

/// <summary>
/// The complete vocabulary: every event and fact type that may cross the
/// boundary between a customer's environment and the control plane. A type
/// missing from this list fails the build — silently absent is not a state
/// this manifest allows.
/// </summary>
public static class Vocabulary
{
    public static IReadOnlyList<Type> Types { get; } =
    [
        typeof(ProtocolHello),
        typeof(DeviceAuthorizationRequest),
        typeof(DeviceAuthorizationStarted),
        typeof(DeviceTokenRequest),
        typeof(SessionIssued),
        typeof(WhoAmI),
        typeof(RunnerRegistrationRequest),
        typeof(RunnerRegistered),
        typeof(RunnerHeartbeat),
        typeof(HeartbeatAccepted),
        typeof(LeaseClaimRequest),
        typeof(LeaseRepoRef),
        typeof(LeaseGranted),
        typeof(LeaseRenewalRequest),
        typeof(LeaseRenewed),
        typeof(LeaseReleaseRequest),
        typeof(LeaseReleased),
        typeof(FlightIntent),
        typeof(FlightLaunchRequest),
        typeof(FlightLaunched),
        typeof(FlightSummary),
        typeof(FlightList),
        typeof(FlightLogEntry),
        typeof(FlightLog),
        typeof(RunnerSummary),
        typeof(RunnerList),
    ];
}
