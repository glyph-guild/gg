using System.Reflection;

namespace Gg.Client;

/// <summary>
/// The three versions this binary speaks, and the headers that carry them.
/// </summary>
/// <remarks>
/// All three travel on every request. The fact-vocabulary version is the one
/// nobody thinks to print, which is exactly why <c>gg version</c> prints it:
/// a runner evaluating facts against a vocabulary the control plane has moved
/// past is a silent wrong answer, not a loud failure.
/// </remarks>
public static class GgVersions
{
    /// <summary>Wire protocol revision this binary speaks.</summary>
    public const int Protocol = 1;

    /// <summary>Pinned fact vocabulary this binary evaluates against.</summary>
    public const string FactVocabulary = "0.1.0";

    public const string ProtocolHeader = "GG-Protocol-Version";
    public const string RunnerVersionHeader = "GG-Runner-Version";
    public const string FactVocabularyHeader = "GG-Fact-Vocabulary";

    /// <summary>Session token header. Matches the control plane's scheme.</summary>
    public const string SessionHeader = "X-Gg-Session";

    /// <summary>This binary's own version.</summary>
    public static string Binary { get; } =
        typeof(GgVersions).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion ?? "0.0.0";
}
