namespace Gg.Runner.Tests;

/// <summary>
/// The object that answers <c>status</c> is the object the loop narrates to.
/// </summary>
/// <remarks>
/// <para>
/// <b>It was not, and so <c>status</c> was a constant.</b> The host handed the
/// loop <c>narration</c> and built <see cref="WhatThisRunnerSays"/> inside the
/// session factory, wrapping that same narration and handed on only to
/// <c>AskDispatch</c>. Nothing ever called the instance's observer methods, so
/// <c>_doing</c> never left its initialiser: every real runner answered
/// <c>{ Doing: "starting" }</c> for its whole life, and
/// <c>CannotBeFlownByHand</c> - the diagnosis the class exists for - was
/// recorded nowhere.
/// </para>
/// <para>
/// <b>Nothing caught it because every test drives the object by hand.</b> Each
/// one constructs a <c>WhatThisRunnerSays</c>, calls <c>Idle()</c> or
/// <c>Claimed()</c> on it directly, and asks what it says - which is true of the
/// class and says nothing about the composition root. This project's
/// most-repeated failure, and <c>MoveBoundProbeTests</c> is the same shape one
/// file over: a check that runs in the suite and not in the product.
/// </para>
/// <para>
/// <b>So both halves are held.</b> That a loop narrating to it moves what it
/// says, and that the host is what does the narrating.
/// </para>
/// </remarks>
public class TheAnsweringObjectIsTheObservingObjectTests
{
    private static readonly DateTimeOffset T0 = DateTimeOffset.UnixEpoch;

    [Test]
    public async Task A_loop_narrating_to_it_moves_what_it_says()
    {
        using var stopping = new CancellationTokenSource();
        var says = new WhatThisRunnerSays(new SilentObserver(), new NoLog(), () => T0);

        await Assert.That(says.Status().Doing).IsEqualTo("starting");

        await new RunnerLoop(new FakeProtocol(), new MovableClock(T0),
                (_, _) => { stopping.Cancel(); return Task.CompletedTask; },
                says, new NoCredentialResolver(), new NoWorkspace())
            .RunAsync("runner-1", ["linux"], stopping.Token);

        await Assert.That(says.Status().Doing).IsNotEqualTo("starting")
            .Because("a runner that has asked for work and been told there is none is "
                   + "idle, and saying `starting' for ever is the defect this closes.");

        await Assert.That(says.Status().BeatAt).IsNotNull()
            .Because("the loop beat before it asked - that is what makes an idle runner "
                   + "visible at all - and a watcher reads that beat to tell a machine "
                   + "that is waiting from one that has stopped.");
    }

    [Test]
    public async Task And_the_host_is_what_narrates_to_it()
    {
        // THE OTHER HALF, and the half that was missing entirely. The test
        // above passes on a wiring nobody ships; this asserts the wiring is the
        // one `gg runner serve` builds.
        var host = SourceOf("RunnerHost.cs");

        await Assert.That(host).Contains("var says = new WhatThisRunnerSays(")
            .Because("one instance, named, so the two places it has to reach are reading "
                   + "the same object rather than two that agree.");

        var loop = host.IndexOf("new RunnerLoop(", StringComparison.Ordinal);

        await Assert.That(loop).IsGreaterThan(-1);
        await Assert.That(host[loop..(loop + 400)]).Contains("says")
            .Because("the loop's observer is what gets told what this runner is doing, and "
                   + "an object that is not it is an object that is told nothing.");

        await Assert.That(host).Contains("new AskDispatch(says")
            .Because("and the same one answers, or the two halves are back to being "
                   + "separate objects that happen to be the same type.");
    }

    /// <summary>A log for a flight that wrote nothing.</summary>
    private sealed class NoLog : IReadOnlyLog
    {
        public TailRead Tail(int lines) => new([], false);
    }

    private static string SourceOf(string file)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Gg.sln")))
        {
            dir = dir.Parent;
        }

        var root = (dir ?? throw new InvalidOperationException("Gg.sln not found")).FullName;

        return File.ReadAllText(Path.Combine(root, "Gg.Runner", file));
    }
}
