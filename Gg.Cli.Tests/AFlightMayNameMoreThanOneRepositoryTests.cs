namespace Gg.Cli.Tests;

/// <summary>
/// A flight may name more than one repository, because some work needs two.
/// </summary>
/// <remarks>
/// <para>
/// <b>The runner has been able to do this all along.</b>
/// <c>IWorkspace.PrepareAsync</c> takes <c>lease.Repos</c> — a list — and
/// answers with <c>WorkspaceResult.Trees</c>, plural; every tree is cloned and
/// observed. <c>FlightRepo</c> is a list on the wire and the envelope's
/// <c>repositories:</c> bound has been a SET since contract 0.112.0. The only
/// thing that could never say two was the flight.
/// </para>
/// <para>
/// <b>GG-102 is what that costs.</b> <c>score-hal</c>'s rubric lives at
/// <c>.claude/skills/hal/</c> in one repository and the work item's code in
/// another — the kind's own instruction says so — and a flight names one. The
/// agent read the item, confirmed it was in scope, and stopped: <i>"I don't
/// have the HAL rubric … this flight is pinned to the JDNext working tree"</i>.
/// </para>
/// <para>
/// <b>Repeated rather than comma-separated.</b> A slug already contains a
/// separator of its own (<c>JDX/JDNext</c>), and a list flattened into one
/// argument is a list somebody eventually quotes wrongly. Every other repeated
/// value in this parser is its own flag.
/// </para>
/// </remarks>
public class AFlightMayNameMoreThanOneRepositoryTests
{
    private static CliAction.Fly Parse(params string[] arguments) =>
        (CliAction.Fly)CliArgs.Parse(arguments);

    [Test]
    public async Task Two_repositories_both_reach_the_action()
    {
        var parsed = Parse(
            "fly", "--ticket", "ado#18001",
            "--repo", "JDX/JDNext",
            "--repo", "JDX/agile-cortex");

        await Assert.That(parsed.Repositories).IsEquivalentTo(
            (IReadOnlyList<string>)["JDX/JDNext", "JDX/agile-cortex"]);
    }

    [Test]
    public async Task The_order_typed_is_the_order_carried()
    {
        // THE FIRST ONE IS NOT ARBITRARY. A flight's repositories are cloned in
        // the order they are named and the prompt lists them in that order, so
        // an author putting the one the work is about first is saying something
        // a reader can act on. Sorting them here would take that away.
        var parsed = Parse(
            "fly", "--ticket", "ado#18001",
            "--repo", "JDX/agile-cortex",
            "--repo", "JDX/JDNext");

        var named = parsed.Repositories!;

        await Assert.That(named[0]).IsEqualTo("JDX/agile-cortex");
        await Assert.That(named[1]).IsEqualTo("JDX/JDNext");
    }

    [Test]
    public async Task One_repository_still_parses_exactly_as_it_did()
    {
        // EVERY FLIGHT EVER OPENED NAMES ONE OR NONE. A change that made the
        // single case travel differently would re-aim work that is currently
        // fine, which is the same rule the brief was added under.
        var parsed = Parse("fly", "--ticket", "ado#18001", "--repo", "JDX/JDNext");

        await Assert.That(parsed.Repositories).IsEquivalentTo(
            (IReadOnlyList<string>)["JDX/JDNext"]);
    }

    [Test]
    public async Task Naming_none_carries_none_rather_than_an_empty_name()
    {
        // ABSENCE IS A CHOICE NOT TO NARROW, which is what null has always
        // meant here: the envelope's bound supplies the repository when it
        // names exactly one. An empty list must read as that same absence.
        var parsed = Parse("fly", "--ticket", "ado#18001");

        await Assert.That(parsed.Repositories).IsEmpty();
    }

    [Test]
    public async Task The_same_repository_twice_is_refused_rather_than_cloned_twice()
    {
        // CLONING IT TWICE IS THE CHEAP HARM; the expensive one is that two
        // trees of one repository give an agent two answers to "what does this
        // file say" and no rule for choosing.
        var parsed = CliArgs.Parse([
            "fly", "--ticket", "ado#18001",
            "--repo", "JDX/JDNext", "--repo", "JDX/JDNext"]);

        var unknown = await Assert.That(parsed).IsTypeOf<CliAction.Unknown>();

        await Assert.That(unknown!.Message).Contains("JDX/JDNext", StringComparison.Ordinal);
    }
}
