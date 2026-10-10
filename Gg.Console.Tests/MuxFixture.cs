namespace Gg.Console.Tests;

/// <summary>
/// A mux over a terminal the test made, and agents that are real children on real
/// pseudo-terminals: shell scripts that wait for a file before they say anything more, so a test
/// decides when an agent speaks without sleeping.
/// </summary>
internal sealed class MuxFixture : IDisposable
{
    private readonly string _directory =
        System.IO.Directory.CreateTempSubdirectory("gg-mux-").FullName;

    public MuxFixture(int columns = 70, int rows = 12, string? agentCommand = null)
    {
        Terminal = new HostedTerminal { Columns = columns, Rows = rows };
        Ledger = new MuxLedger(Path.Combine(_directory, "mux", "sessions.jsonl"));
        Drafts = new Gg.Client.ItineraryDrafts(Path.Combine(_directory, "itineraries"));
        Mux = new Mux(() => Terminal, Ledger, agentCommand: agentCommand,
            self: new Gg.Local.SelfInvocation("/usr/local/bin/gg", ["runner", "tools"]));
    }

    public HostedTerminal Terminal { get; }

    public Mux Mux { get; }

    public MuxLedger Ledger { get; }

    public Gg.Client.ItineraryDrafts Drafts { get; }

    public string Directory => _directory;

    /// <summary>A file an agent's script waits for; creating it lets the agent speak.</summary>
    public string Flag(string name) => Path.Combine(_directory, name + ".flag");

    public void Raise(string name) => File.WriteAllText(Flag(name), "");

    /// <summary>
    /// An agent labelled <paramref name="label"/>, launched as compose and plan are, whose bar
    /// reads "bar &lt;label&gt;" and whose ending folds "&lt;label&gt; ended".
    /// </summary>
    public void Agent(string label, string script) =>
        Mux.Launch(label, () =>
        {
            Mux.Host(
                Terminal, "/bin/sh", ["-c", script], _directory,
                (_, _) => new HostedRows([$"bar {label}"], false),
                (_, _) => false,
                CancellationToken.None).GetAwaiter().GetResult();
            return state => state with { LastNomination = $"{label} ended" };
        });

    /// <summary>Prints <paramref name="first"/>, then <paramref name="later"/> once <paramref name="flag"/> exists, then waits for <paramref name="end"/>.</summary>
    public string Speaks(string first, string flag, string later, string end) =>
        $"printf '{first}'; while [ ! -f '{Flag(flag)}' ]; do sleep 0.02; done; printf '{later}'; "
        + $"while [ ! -f '{Flag(end)}' ]; do sleep 0.02; done";

    /// <summary>Shows <paramref name="tab"/> on a thread of its own, as the shell would.</summary>
    public Task<MuxLeave> Showing(MuxTab tab) =>
        Task.Factory.StartNew(() => Mux.Show(tab), CancellationToken.None,
            TaskCreationOptions.LongRunning, TaskScheduler.Default);

    /// <summary>
    /// Moves a menu's cursor down to the item starting <paramref name="item"/> and presses
    /// enter, as a person does: one arrow at a time, each waited for until it is drawn.
    /// </summary>
    public bool Choose(string item)
    {
        for (var moves = 0; moves < 12; moves++)
        {
            var at = Cursor();
            if (at.StartsWith(item, StringComparison.Ordinal))
            {
                Terminal.Type("\r");
                return true;
            }

            Terminal.Type("\u001b[B");
            if (!Until(() => Cursor() != at))
            {
                return false;
            }
        }

        return false;
    }

    /// <summary>The text after the last cursor drawn: what enter would choose.</summary>
    public string Cursor()
    {
        var painted = Terminal.Painted;
        var at = painted.LastIndexOf("▸ ", StringComparison.Ordinal);
        if (at < 0)
        {
            return "";
        }

        var end = painted.IndexOf('\u001b', at);
        return painted[(at + 2)..(end < 0 ? painted.Length : end)];
    }

    public static bool Until(Func<bool> held)
    {
        var deadline = DateTime.UtcNow.AddSeconds(20);
        while (DateTime.UtcNow < deadline)
        {
            if (held())
            {
                return true;
            }

            Thread.Sleep(5);
        }

        return false;
    }

    public void Dispose()
    {
        Mux.EndAll();
        Terminal.Dispose();
        try
        {
            System.IO.Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
        }
    }
}
