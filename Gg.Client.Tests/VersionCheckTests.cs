using Gg.Client;

namespace Gg.Client.Tests;

/// <summary>
/// What this gg makes of what the control plane says is current.
/// </summary>
/// <remarks>
/// <para>
/// <b>The control plane is the only independent channel, and after the walk it
/// is the only one at all.</b> <c>dotnet tool update</c> with no version takes
/// whatever was pushed last; nuget.org repository-signs whatever it accepts,
/// which proves the pipeline rather than the publisher; and on Linux and macOS
/// the client does not verify by default anyway — measured, not assumed. So
/// asking the feed what is current would be asking the attacker.
/// </para>
/// <para>
/// <b>Silence must not read as currency.</b> This project's most repeated
/// defect, on the field where it means nobody ever updates: a person told
/// nothing, who concludes they were told everything is fine.
/// </para>
/// </remarks>
public class VersionCheckTests
{
    [Test]
    public async Task An_older_binary_is_behind()
    {
        var standing = VersionStanding.For(installed: "0.3.0", current: "0.4.0");

        await Assert.That(standing.Kind).IsEqualTo(VersionStandingKind.Behind);
        await Assert.That(standing.Current).IsEqualTo("0.4.0");
    }

    [Test]
    public async Task The_build_metadata_a_binary_carries_is_not_part_of_the_comparison()
    {
        // `gg --version` reports 0.4.0+<sha>, because the informational version
        // carries the commit. Compared as text that is never equal to anything,
        // and every install in the fleet would report itself behind forever.
        var standing = VersionStanding.For(
            installed: "0.4.0+9f144b826de7e7205b4ca79235ec5e93d8e7821e",
            current: "0.4.0");

        await Assert.That(standing.Kind).IsEqualTo(VersionStandingKind.Current);
    }

    [Test]
    public async Task A_version_the_control_plane_has_never_heard_of_is_reported_and_not_celebrated()
    {
        // S32.1-04. A host running something the feed offered and the control
        // plane has never heard of is the SHAPE A STOLEN-KEY PUSH TAKES: an
        // attacker pushes 99.0.0 and every client updating to latest takes it.
        // Rendering that as "you are ahead of the curve" is the one reading
        // that must not be available.
        var standing = VersionStanding.For(installed: "99.0.0", current: "0.4.0");

        await Assert.That(standing.Kind).IsEqualTo(VersionStandingKind.Unrecognised);
        await Assert.That(standing.IsReassuring).IsFalse()
            .Because("a version the control plane never published is not good news, and it is "
                   + "exactly what a package pushed with a stolen key looks like from here.");
    }

    [Test]
    public async Task An_oracle_that_could_not_be_asked_is_an_absence_and_never_agreement()
    {
        // S32.1-02. The control plane is unreachable, or answered nothing
        // usable. What must NOT happen is this rendering as "up to date".
        var standing = VersionStanding.For(installed: "0.4.0", current: null);

        await Assert.That(standing.Kind).IsEqualTo(VersionStandingKind.Unknown);
        await Assert.That(standing.IsReassuring).IsFalse()
            .Because("not having been told is not the same as having been told nothing is wrong, "
                   + "and on this field the difference is whether anybody ever updates.");
        await Assert.That(standing.Current).IsNull();
    }

    [Test]
    public async Task Only_being_current_is_reassuring()
    {
        // The positive half, so IsReassuring is a real discriminator rather
        // than a property that is false for everything and therefore says
        // nothing.
        await Assert.That(VersionStanding.For("0.4.0", "0.4.0").IsReassuring).IsTrue();

        foreach (var (installed, current) in ((string, string?)[])
                 [("0.3.0", "0.4.0"), ("99.0.0", "0.4.0"), ("0.4.0", null), ("0.4.0", "")])
        {
            await Assert.That(VersionStanding.For(installed, current).IsReassuring).IsFalse()
                .Because($"installed {installed} against current {current ?? "<none>"} is not a "
                       + "state that should read as fine.");
        }
    }

    [Test]
    public async Task Being_behind_is_never_a_refusal()
    {
        // Rule 6, and S32.2-04. The protocol floor already blocks with a 426
        // and that stays the only thing that does. A standing is a report; it
        // carries no way to say "stop".
        foreach (var kind in Enum.GetValues<VersionStandingKind>())
        {
            await Assert.That(typeof(VersionStanding).GetProperties()
                .Any(p => p.Name.Contains("Block", StringComparison.OrdinalIgnoreCase)
                       || p.Name.Contains("Refus", StringComparison.OrdinalIgnoreCase)
                       || p.Name.Contains("Fatal", StringComparison.OrdinalIgnoreCase)))
                .IsFalse()
                .Because($"nothing about {kind} may stop a person working. If a member appears "
                       + "here that could, rule 6 has been given away in a refactor.");
        }
    }
}
