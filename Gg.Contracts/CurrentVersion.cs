namespace Gg.Contracts;

/// <summary>
/// What version of <c>gg</c> the control plane says is current.
/// </summary>
/// <remarks>
/// <para>
/// <b>The one channel that is not the feed.</b> <c>dotnet tool update</c> with
/// no version takes whatever was pushed last, and what was pushed last is
/// attacker-controlled. nuget.org repository-signs whatever it accepts, which
/// proves the package came through its pipeline rather than that glyph-guild
/// sent it — and on Linux and macOS the client does not verify by default at
/// all, measured rather than assumed. So a host that asks the feed what is
/// current is asking the same party that would lie.
/// </para>
/// <para>
/// <b>One string, and no tenant knowledge.</b> What the current <c>gg</c> is,
/// is not a fact about a customer, so this door takes no session and this type
/// has nowhere to put one. That is what lets a machine that cannot sign in —
/// the one most likely to be far behind — still find out.
/// </para>
/// <para>
/// <b>A version rather than a digest, for now.</b> A version stops a fleet
/// taking <i>latest</i>. A digest would also catch a package republished under
/// a version that already shipped, which is stronger and needs the digest to be
/// knowable at release time. That is an open question, and answering it adds a
/// member here rather than changing this one.
/// </para>
/// </remarks>
[PinnedId("c8f2dd18-330d-4382-8abd-5c86b35cfe06")]
public sealed record CurrentVersion
{
    /// <summary>The version a person should be running.</summary>
    public required string Version { get; init; }
}
