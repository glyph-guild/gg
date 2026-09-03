namespace Gg.Cli.Tests;

/// <summary>
/// A flight about a work item can say which repository it is about.
/// </summary>
/// <remarks>
/// <para>
/// <b><c>FlightLaunchRequest.Repository</c> has always been declared and the CLI
/// has never set it.</b> The field says <i>"which repository the flight is
/// about, validated the same way. Null inherits"</i> — and every flight
/// <c>gg fly</c> opens sends null, so the only repository a flight can have is
/// one its intent URI named. A ticket names a provider and an id and no
/// repository at all.
/// </para>
/// <para>
/// <b>Which makes it the other half of a real gap rather than a new flag.</b>
/// The control plane resolves a ticket flight to no repository, hands the runner
/// an empty working tree, and the agent reports that there is nothing here to
/// work on. It is right; nothing told it what to check out.
/// </para>
/// <para>
/// <b>The registry KEY, not the forge path.</b> A registered entry's path is a
/// display label that may drift and its name is the identity flights refer to —
/// the same distinction <c>FlightIngress.SubjectsOf</c> already turns on — so a
/// flight naming <c>agile-cortex</c> keeps resolving after somebody renames
/// <c>jdx/agile-cortex</c>.
/// </para>
/// </remarks>
public class FlyNamesARepositoryTests
{
    [Test]
    public async Task A_ticket_flight_can_name_the_repository_it_is_about()
    {
        // THE DEFECT. This is the flight that has no repository today and no
        // way to be given one.
        var action = CliArgs.Parse(["fly", "--ticket", "azuredevops#26", "--repo", "agile-cortex"]);

        await Assert.That(action).IsTypeOf<CliAction.Fly>();
        await Assert.That(((CliAction.Fly)action).Repository).IsEqualTo("agile-cortex");
    }

    [Test]
    public async Task A_uri_flight_can_name_one_too()
    {
        // Not only tickets. A link this gg cannot read a repository out of is
        // the same situation arriving by a different route, and a flag that
        // worked for one intent kind and not the other would be a rule nobody
        // could remember.
        var action = CliArgs.Parse(
            ["fly", "--uri", "https://example.invalid/board/1", "--repo", "agile-cortex"]);

        await Assert.That(((CliAction.Fly)action).Repository).IsEqualTo("agile-cortex");
    }

    [Test]
    public async Task Naming_no_repository_stays_the_ordinary_case()
    {
        // THE ANCHOR. Most flights are about a link that names their repository
        // already, and inheriting is what the field's own contract says null
        // means. A default here would put a repository on flights nobody said
        // were about one.
        await Assert.That(((CliAction.Fly)CliArgs.Parse(["fly", "--uri", "https://example.invalid/x"]))
            .Repository).IsNull();
        await Assert.That(((CliAction.Fly)CliArgs.Parse(["fly", "fix the thing"]))
            .Repository).IsNull();
    }

    [Test]
    public async Task The_flag_with_nothing_after_it_is_refused_rather_than_ignored()
    {
        // `gg fly --ticket x#1 --repo` is somebody who meant to name one. Taking
        // it as a flight with no repository would open work against an empty
        // tree and report success.
        await Assert.That(CliArgs.Parse(["fly", "--ticket", "azuredevops#26", "--repo"]))
            .IsTypeOf<CliAction.Unknown>();
    }
}
