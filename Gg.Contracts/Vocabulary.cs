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
    ];
}
