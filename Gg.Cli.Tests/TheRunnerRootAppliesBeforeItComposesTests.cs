using System.Text.RegularExpressions;

namespace Gg.Cli.Tests;

/// <summary>
/// Where the offer is applied in <c>gg runner up</c>, and what it may not cost.
/// </summary>
/// <remarks>
/// <para>
/// <b>Three properties of one method, and none of them is visible in a unit
/// test.</b> <c>OfferedAtStartup.Decide</c> is pure and covered; what this
/// guards is the composition around it, which lives in a file of top-level
/// statements that cannot be called. A source scan is the shape this repository
/// already uses for the root — see the exit-code ratchet on <c>EmitLocal</c>.
/// </para>
/// <para>
/// <b>The failure each one prevents is silent.</b> Applying after the labels
/// are read writes a file and composes from what it replaced; reading
/// <c>InForce.Configuration</c> again afterwards does the same through the memo;
/// and a fetch that can throw past the caller turns a control-plane outage into
/// a fleet that will not start. All three produce a runner that looks like it
/// is working.
/// </para>
/// </remarks>
public partial class TheRunnerRootAppliesBeforeItComposesTests
{
    /// <summary>The body of <c>RunnerUpAsync</c>, and nothing else.</summary>
    private static string RunnerUp()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Gg.sln")))
        {
            directory = directory.Parent;
        }

        var root = (directory ?? throw new InvalidOperationException("Gg.sln not found")).FullName;
        var source = File.ReadAllText(Path.Combine(root, "Gg.Cli", "Program.cs"));

        var at = source.IndexOf("static async Task<int> RunnerUpAsync()", StringComparison.Ordinal);

        if (at < 0)
        {
            throw new InvalidOperationException(
                "RunnerUpAsync was renamed, so this scan reads nothing and every assertion "
              + "below would pass for a root that had stopped doing any of it.");
        }

        var ends = source.IndexOf("\nstatic ", at + 1, StringComparison.Ordinal);

        return source[at..(ends < 0 ? source.Length : ends)];
    }

    [Test]
    public async Task The_offer_is_decided_before_the_labels_are_read()
    {
        var body = RunnerUp();

        var decides = body.IndexOf("OfferedAtStartup.Decide", StringComparison.Ordinal);
        var labels = body.IndexOf("GG_RUNNER_LABELS", StringComparison.Ordinal);

        await Assert.That(decides).IsGreaterThan(-1)
            .Because("nothing applies an offer at all, so a runner brought up fresh takes "
                   + "whatever its file happened to say.");
        await Assert.That(labels).IsGreaterThan(-1)
            .Because("the labels moved, and this comparison is about where they are read.");

        await Assert.That(decides).IsLessThan(labels)
            .Because("applying after the labels are read writes a file and then composes "
                   + "from the document it replaced - which looks exactly like working.");
    }

    [Test]
    public async Task Nothing_reads_the_memo_again_once_the_offer_has_been_decided()
    {
        // THE SAME DEFECT ONE LAYER DOWN. InForce.Configuration is memoized for
        // the process, so a read after the write returns the OLD document - and
        // the write would have no effect on this run at all.
        var body = RunnerUp();
        var decides = body.IndexOf("OfferedAtStartup.Decide", StringComparison.Ordinal);

        var afterwards = Memo().Matches(body)
            .Where(m => m.Index > decides)
            .Select(m => body[Math.Max(0, m.Index - 60)..m.Index].Split('\n')[^1].Trim() + "…")
            .ToList();

        await Assert.That(afterwards).IsEmpty()
            .Because("every value composed after the offer has to come from what Decide "
                   + "returned, not from the memo it may have replaced. Found: "
                   + string.Join(" | ", afterwards));
    }

    [Test]
    public async Task A_control_plane_that_cannot_be_reached_does_not_stop_the_runner()
    {
        // AN ENHANCEMENT TO BRING-UP, NOT A DEPENDENCY OF IT. A runner that
        // refused to start because it could not ask what was offered would turn
        // one control-plane outage into a fleet that will not come back - which
        // is a far worse failure than stale labels.
        var body = RunnerUp();
        var decides = body.IndexOf("OfferedAtStartup.Decide", StringComparison.Ordinal);

        var guarded = body.LastIndexOf("try", decides, StringComparison.Ordinal);

        await Assert.That(guarded).IsGreaterThan(-1)
            .Because("the heartbeat that carries an offer is a network call on the startup "
                   + "path, and nothing on the startup path may throw past here.");

        await Assert.That(body[guarded..]).Contains("catch (HttpRequestException", StringComparison.Ordinal)
            .Because("unreachable is the case this exists for, and it is the one a fleet "
                   + "meets during a deploy.");
    }

    [Test]
    public async Task Stopping_for_an_offer_is_guarded_by_whether_a_restart_would_take_it()
    {
        // THE RESTART LOOP, and it is the one failure here that never settles.
        // A runner that stopped for ANY offer would meet a directed one it may
        // never take, exit, boot, meet it again, and exit for ever - on every
        // machine in the fleet at once, the moment somebody offers a key that
        // needs a person.
        //
        // Asking Decide is what makes "worth restarting for" the same question
        // as "would be taken", answered by one function rather than by two that
        // agree today.
        var body = RunnerUp();

        // ANCHORED ON THE HANDLER, not on the first cancel in the method: the
        // Ctrl-C handler cancels the same source hundreds of lines earlier, and
        // a scan that found that one would read the startup try/catch and
        // report this guard missing while it was there.
        var handler = body.IndexOf("offered: carried =>", StringComparison.Ordinal);

        await Assert.That(handler).IsGreaterThan(-1)
            .Because("nothing is wired to notice an offer, so a fleet takes one only when "
                   + "something else happens to restart it.");

        var stop = body.IndexOf("stopping.Cancel()", handler, StringComparison.Ordinal);

        await Assert.That(stop).IsGreaterThan(-1)
            .Because("the handler notices and never acts, so nothing restarts.");

        await Assert.That(body[handler..stop]).Contains(".Write is null", StringComparison.Ordinal)
            .Because("the stop has to be conditional on a restart actually writing something, "
                   + "or a directed offer nobody can take restarts this machine for ever.");
    }

    [GeneratedRegex(@"InForce\.Configuration")]
    private static partial Regex Memo();
}
