namespace Gg.Console;

/// <summary>
/// Which of the five questions the runner modal's lower pane is answering
/// about the runner it is open on.
/// </summary>
/// <remarks>
/// <b>Three different answers, not three renderings of one.</b> What it SAID is
/// its own output. What it RUNS is the charted environments it advertises, and
/// what furnishes them. What runs BESIDE it is the rest of the fleet that
/// advertises the same names. A person debugging a stuck flight needs the
/// second and third; a person debugging a crashed runner needs the first.
/// </remarks>
public enum RunnerView
{
    /// <summary>What the runner wrote to its own output.</summary>
    Log,

    /// <summary>The charted environments it can claim work for.</summary>
    Environments,

    /// <summary>The other runners advertising those same environments.</summary>
    Members,

    /// <summary>The flights it claimed, newest first (slice seventy-one).</summary>
    /// <remarks>
    /// <b>A read of its own</b>, asked for when the pane is shown: the fleet read
    /// names the one flight a runner holds now, and a request per runner at boot
    /// for the rest would be paid by everybody for a pane almost nobody opens.
    /// </remarks>
    Flights,

    /// <summary>The ad hoc agent sessions the machine holds (slice seventy-one).</summary>
    /// <remarks>
    /// <b>No request at all</b>: a runner's heartbeat reports its sessions and the
    /// fleet read carries them, so this pane draws from the row the modal is on.
    /// </remarks>
    Sessions,
}

/// <summary>
/// The runner modal's views, and how to move between them.
/// </summary>
/// <remarks>
/// <b>Pure, and asked by the keymap as well as the pane</b> —
/// <c>AirspaceViews</c>' rule one modal over. Which key is offered and which
/// view is drawn are the same question, and two answers to it would drift.
/// </remarks>
public static class RunnerViews
{
    /// <summary>
    /// All five, always.
    /// </summary>
    /// <remarks>
    /// <b>Unconditional, unlike the airspace's.</b> There a view could be
    /// genuinely inapplicable — there is no composition to show for a strategy.
    /// Here every runner has all three questions asked of it, and the answer to
    /// two of them is often "none, and here is why": a runner that advertises
    /// no charted environment is exactly the runner somebody is looking at when
    /// they wonder why it never claims anything. A tab that vanished would take
    /// that answer away with it.
    /// </remarks>
    public static IReadOnlyList<RunnerView> All { get; } =
        [RunnerView.Log, RunnerView.Environments, RunnerView.Members,
         RunnerView.Flights, RunnerView.Sessions];

    /// <summary>What the tab for a view says.</summary>
    /// <remarks>
    /// <b>Lower case, and three words a person would use</b> —
    /// <c>AirspaceViews.Title</c>'s rule, and they sit along the same kind of
    /// foot. Here rather than in the screen because the screen cannot be
    /// constructed without a terminal, so nothing can ask it what it drew.
    /// </remarks>
    public static string Title(RunnerView view) => view switch
    {
        RunnerView.Environments => "environments",
        RunnerView.Members => "members",
        RunnerView.Flights => "flights",
        RunnerView.Sessions => "sessions",
        _ => "log",
    };

    /// <summary>The next view round.</summary>
    public static RunnerView Next(RunnerView showing)
    {
        var at = All.ToList().IndexOf(showing);

        return at < 0 ? All[0] : All[(at + 1) % All.Count];
    }
}
