using Gg.Contracts;
using Gg.Runner.Environments;

namespace Gg.Runner.Tests;

/// <summary>
/// The shipped example environment speaks the protocol gg actually parses.
/// </summary>
/// <remarks>
/// <para>
/// <b>S58.7-02's subject, guarded where CI can see it.</b> That criterion is a
/// walk — five points bringing a NON-Aspire stack up — and a walk settles it.
/// But the thing the walk runs is a file in this repository, and a file nothing
/// checks drifts: <c>ReadReady</c> could change its mind about
/// <c>ready=yes</c> tomorrow and the example would go on claiming to work with
/// nothing failing. So the walk proves the stack comes up, and this proves the
/// example and the parser still agree about what an answer looks like.
/// </para>
/// <para>
/// <b>Why not only a <c>RealDocker</c> test.</b> There is one below, and it is
/// the honest end-to-end. It is also invisible: CI never runs that category,
/// and <c>DockerPoolAdapterTests</c> carries the note explaining what that cost
/// last time — <i>"nobody noticed because CI never runs the RealDocker
/// category"</i>. A guard that only runs where somebody remembers to run it is
/// most of the way to no guard, so the claims that can be made without a daemon
/// are made without one.
/// </para>
/// <para>
/// <b>The outputs below are recorded, not invented.</b> Both blocks are what
/// the script printed on 2026-10-04 against a real daemon — up, and after
/// <c>detach</c>. Writing plausible output here would test the parser against
/// my idea of the script rather than against the script.
/// </para>
/// </remarks>
public class TheExampleEnvironmentSpeaksTheProtocolTests
{
    private const string Relative = ".goodgrief/environments/compose/hooks.sh";

    /// <summary>What the script printed with the stack up.</summary>
    private const string WhenUp =
        "db=healthy\nweb=healthy\nready=yes\nurl=http://127.0.0.1:18231/\n";

    /// <summary>What it printed after <c>detach</c>.</summary>
    private const string WhenDown =
        "db=absent\nweb=absent\nready=no\nwaiting=db\n";

    [Test]
    public async Task Gg_itself_considers_the_declared_hook_present_and_inside_the_tree()
    {
        // THE PRODUCT'S OWN ANSWER, not a File.Exists. `Missing` is what the
        // runner calls before it spends a grant, and it refuses two different
        // ways - a file nobody wrote, and a path that climbs out of the checkout
        // (S58.5-01/-02). Asking it is the only check that cannot disagree with
        // what the runner will decide.
        await Assert.That(StackScript.Missing(RepoRoot(), Relative)).IsNull()
            .Because("this is the path a tenant document will declare, so if the runner would "
                   + "refuse it then the example cannot be flown and the walk cannot happen.");
    }

    [Test]
    public async Task Every_point_gg_may_invoke_is_handled()
    {
        var script = File.ReadAllText(Path.Combine(RepoRoot(), Relative));

        foreach (var point in EnvironmentPoints.All)
        {
            // AS A CASE LABEL. A point gg invokes and the script does not know
            // falls to the catch-all, which exits non-zero - so an unhandled
            // point is not a silent no-op, it is a failed flight. Including
            // `sync`, which this environment refuses on purpose rather than
            // leaving to that catch-all, because "you declared shared" is a
            // better sentence than "unknown point".
            await Assert.That(script).Contains($"{point})")
                .Because($"gg may invoke '{point}', and a point the script does not name reaches "
                       + "the catch-all and fails the flight.");
        }
    }

    [Test]
    public async Task The_example_names_no_interpreter_the_fleet_lacks()
    {
        var script = File.ReadAllText(Path.Combine(RepoRoot(), Relative));

        // RULE 1, and it is not hypothetical: `pwsh` is absent from the pool
        // host AND from the member image, so a PowerShell hook ships five files
        // nothing can run. `PerformAsync` hands a `.ps1` to `pwsh`, which is
        // exactly the path that would fail on a host.
        await Assert.That(Relative.EndsWith(".ps1", StringComparison.OrdinalIgnoreCase)).IsFalse()
            .Because("a .ps1 is invoked through pwsh, which the fleet does not have.");

        await Assert.That(script).DoesNotContain("pwsh")
            .Because("rule 1: the contract names the points, never a language, and the one "
                   + "language the fleet cannot run is the one worth asserting about.");
    }

    [Test]
    public async Task The_example_never_reassigns_the_socket_gg_handed_it()
    {
        var script = File.ReadAllText(Path.Combine(RepoRoot(), Relative));

        // THE WHOLE REASON `PlaceInstance` EXISTS. gg points DOCKER_HOST at the
        // granted instance; a hook that set its own would reach the pool host's
        // daemon, which is the one thing the instance exists to keep it away
        // from. Mentioning the name in a comment is fine - assigning it is not.
        await Assert.That(script).DoesNotContain("DOCKER_HOST=")
            .Because("gg sets DOCKER_HOST to the granted instance's socket, and a hook that "
                   + "assigns it reaches the pool host's own daemon instead.");
    }

    [Test]
    public async Task The_report_it_prints_when_up_parses_as_ready_with_an_address()
    {
        var report = StackScript.ReadReady(Exited(0), WhenUp);

        await Assert.That(report.Readiness).IsEqualTo(StackScript.Readiness.Yes)
            .Because("the script answered `ready=yes`, and if the parser stopped reading that "
                   + "spelling the example would claim to work while reporting nothing.");

        await Assert.That(report.Values.ContainsKey(StackScript.UrlKey)).IsTrue()
            .Because("`url` is the one key gg interprets, and a web stack that reported none "
                   + "would leave a person with no address to open.");

        await Assert.That(report.Values[StackScript.UrlKey]).Contains("18231")
            .Because("the address names the port gg handed the hook, not one the stack chose - "
                   + "which is step 4's whole correction.");

        // CARRIED WHOLE, which is S58.3-03. These two mean nothing to gg and
        // are shown to a person, so the test that matters is that an unknown key
        // survives rather than being dropped.
        await Assert.That(report.Values["db"]).IsEqualTo("healthy");
        await Assert.That(report.Values["web"]).IsEqualTo("healthy");

        await Assert.That(report.Values.ContainsKey("ready")).IsFalse()
            .Because("the answer is not also a value: two sources for one fact is how they come "
                   + "to disagree.");
    }

    [Test]
    public async Task The_report_it_prints_when_down_is_a_no_rather_than_a_failure()
    {
        var report = StackScript.ReadReady(Exited(0), WhenDown);

        // `no` IS AN ANSWER AND EXITS ZERO - rule 4. A hook that exited non-zero
        // to mean "not ready" would be indistinguishable from one that broke.
        await Assert.That(report.Readiness).IsEqualTo(StackScript.Readiness.No)
            .Because("not-ready is something the stack said, and the script says it at exit 0.");

        await Assert.That(report.Values["waiting"]).IsEqualTo("db")
            .Because("it names the FIRST service that is not up - `$SERVICES` is in dependency "
                   + "order, so that is the one holding the rest back.");

        await Assert.That(report.Values.ContainsKey(StackScript.UrlKey)).IsFalse()
            .Because("there is nothing to open yet, and an address printed anyway would be one "
                   + "gg stamped for a stack that was not serving.");
    }

    [Test]
    public async Task A_hook_that_could_not_answer_is_not_read_as_a_no()
    {
        // THE THIRD OUTCOME, S58.3-02. Even with a perfectly good report on
        // stdout, a non-zero exit means the script did not get to the end of
        // its own measurement - so the report is not trusted.
        var report = StackScript.ReadReady(Exited(64), WhenUp);

        await Assert.That(report.Readiness).IsEqualTo(StackScript.Readiness.Unanswered)
            .Because("`ready=yes` from a script that then failed is a claim it did not finish "
                   + "making, and reading it as `no` would lose the difference between a stack "
                   + "that is not up and a hook that is broken.");
    }

    private static StackScript.Performance Exited(int code) =>
        new(StackOutcomes.Exited, code, TimeSpan.FromSeconds(1), Survived: false);

    internal static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null
            && !File.Exists(Path.Combine(directory.FullName, "Gg.Contracts", "fact-vocabulary.json")))
        {
            directory = directory.Parent;
        }

        return (directory ?? throw new InvalidOperationException("repository root not found")).FullName;
    }
}

/// <summary>
/// The example environment really comes up, through gg's own invoker.
/// </summary>
/// <remarks>
/// <para>
/// <b>S58.7-02, end to end, and it needs a daemon.</b> Everything here goes
/// through <see cref="StackScript.PerformAsync"/> rather than through a shell,
/// because the thing being proved is that the five points work <i>as gg
/// invokes them</i> — one executable, the point as its argument, stdout
/// captured only for <c>ready</c>.
/// </para>
/// <para>
/// <b>No instance is granted here, so <c>DOCKER_HOST</c> is left alone</b> and
/// the stack lands on whatever daemon the machine running this test has. That is
/// the one difference from a flight, and it is the difference the walk on a pool
/// host exists to close: published ports there land in the instance's network
/// namespace, which is why <c>ready</c> asks the daemon for health instead of
/// probing an address itself.
/// </para>
/// </remarks>
[Category("RealDocker")]
public class TheExampleEnvironmentReallyComesUpTests
{
    private const string Relative = ".goodgrief/environments/compose/hooks.sh";

    [Test]
    public async Task The_five_points_bring_a_non_Aspire_stack_up_and_take_it_down()
    {
        var root = TheExampleEnvironmentSpeaksTheProtocolTests.RepoRoot();
        var script = StackScript.Within(root, Relative)
            ?? throw new InvalidOperationException("the example hook is not inside the tree");

        // A PORT NOTHING ELSE ON THIS MACHINE IS USING, handed to the hook the
        // way gg hands it: through the environment, not through the compose file.
        const int Port = 18232;

        try
        {
            var prepared = await Perform(script, StackScript.Prepare, root, Port);

            await Assert.That(prepared.Exit).IsEqualTo(0)
                .Because("prepare only has to leave the images present, which is all warmth is "
                       + "since reclaim's volume prune is unfiltered.");

            var broughtUp = await Perform(script, StackScript.Attach, root, Port);

            await Assert.That(broughtUp.Exit).IsEqualTo(0)
                .Because("attach starts detached, waits for health and exits 0 - rule 3.");

            await Assert.That(broughtUp.Survived).IsFalse()
                .Because("no process survives the hook: the instance's daemon owns what is "
                       + "running, which keeps reclaim the single way an instance empties.");

            var answered = await Perform(script, StackScript.Ready, root, Port, capture: true);
            var report = StackScript.ReadReady(answered, answered.Said);

            await Assert.That(report.Readiness).IsEqualTo(StackScript.Readiness.Yes)
                .Because("the stack is up, so the hook answers yes - and this is the assertion "
                       + "that makes five general points more than a story about Aspire.");

            await Assert.That(report.Values[StackScript.UrlKey])
                .IsEqualTo($"http://127.0.0.1:{Port}/")
                .Because("it reports the address built from the port gg gave it.");
        }
        finally
        {
            // ALWAYS, because a test that leaves a stack running takes the port
            // with it and the next run fails for a reason that has nothing to do
            // with the code.
            await Perform(script, StackScript.Detach, root, Port);
        }
    }

    private static Task<StackScript.Performance> Perform(
        string script, string point, string root, int port, bool capture = false) =>
        StackScript.PerformAsync(
            script, StackScript.ArgumentFor(point), root, StackScript.Patience,
            capture: capture, previewPort: port);
}
