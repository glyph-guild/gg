using System.Reflection;
using Gg.Contracts.Description;

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
    /// <summary>
    /// Wire protocol revision this binary speaks.
    /// </summary>
    /// <remarks>
    /// Taken from the contract rather than declared here. This used to be its
    /// own literal, with the control plane keeping a second one - two numbers
    /// that had to be equal, in two repositories, with nothing checking.
    /// </remarks>
    public const int Protocol = ProtocolSurface.Revision;

    /// <summary>
    /// Pinned fact vocabulary this binary evaluates against.
    /// </summary>
    /// <remarks>
    /// From the contract, like the protocol revision, and for the same reason.
    /// It was a hand-typed literal here and in two other places until step 7,
    /// and three copies of a number that must agree is how one of them stops
    /// agreeing - which is exactly what happened when source.provenance shipped
    /// under an unchanged 0.1.0.
    /// </remarks>
    public const string FactVocabulary = Gg.Contracts.FactVocabulary.Version;

    // All four come from the contract. The session header in particular used
    // to be a literal here with a comment claiming it "matches the control
    // plane's scheme" - a claim about a repository this one cannot see, which
    // is exactly the kind of thing that is true right up until it is not.
    public const string ProtocolHeader = ProtocolSurface.ProtocolHeader;
    public const string RunnerVersionHeader = ProtocolSurface.RunnerVersionHeader;
    public const string FactVocabularyHeader = ProtocolSurface.FactVocabularyHeader;
    public const string SessionHeader = ProtocolSurface.SessionHeader;

    /// <summary>This binary's own version.</summary>
    public static string Binary { get; } =
        typeof(GgVersions).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion ?? "0.0.0";
}
