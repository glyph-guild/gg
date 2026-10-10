using Gg.Runner;
using Porta.Pty;

namespace Gg.Cli;

/// <summary>
/// Gives an ad hoc session's agent a terminal of its own: the `claude` CLI as it
/// is, in a pty, at the size the console asked for (slice seventy, ADR-0039
/// Decision 2).
/// </summary>
/// <remarks>
/// <b>Here, because the runner may not allocate a terminal</b> - <c>SetupTokenLogin</c>'s
/// reason and shape. The child is told nothing gg invents beyond what a local mux
/// agent is told: its session id, so history can carry the conversation on.
/// </remarks>
public sealed class AgentSessionHost(
    string binary, Func<AgentSessionStart, IReadOnlyList<string>>? arguments = null) : IHostAgentSessions
{
    private readonly string _binary = binary;
    private readonly Func<AgentSessionStart, IReadOnlyList<string>> _arguments = arguments ?? ArgumentsFor;

    /// <summary>
    /// Started by its id, or resumed by it - and, when a person delegated a credential to it,
    /// with gg's manage and itinerary tool servers (ADR-0039 Amendment 2).
    /// </summary>
    /// <remarks>
    /// <b>The reads granted, the acts asked each time</b>, as the local gg agent is started
    /// (<c>Mux.StartManaging</c>). The credential itself is in the environment, never here: an
    /// argument is visible to anybody who can list processes.
    /// </remarks>
    public static IReadOnlyList<string> ArgumentsFor(AgentSessionStart start) =>
        ArgumentsFor(start, Gg.Local.SelfInvocation.Current);

    /// <summary>The same, given how this binary invokes itself; no tools when it cannot say.</summary>
    public static IReadOnlyList<string> ArgumentsFor(AgentSessionStart start, Gg.Local.SelfInvocation? self)
    {
        ArgumentNullException.ThrowIfNull(start);

        List<string> argv = start.Resume ? ["--resume", start.SessionId] : ["--session-id", start.SessionId];

        if (start.Tools && self is not null)
        {
            argv.Add("--mcp-config");
            argv.Add(ToolServers(self, start.SessionId));
            argv.Add("--allowedTools");
            argv.AddRange(Gg.Local.ManageTool.Reads.Select(Gg.Local.ManageTool.Qualified));
            argv.AddRange(Gg.Local.PlanningTool.All.Select(Gg.Local.PlanningTool.Qualified));
        }

        return argv;
    }

    /// <summary>Both servers under their own keys; the itinerary's draft is named after the session.</summary>
    private static string ToolServers(Gg.Local.SelfInvocation self, string sessionId)
    {
        using var buffer = new MemoryStream();
        using (var json = new System.Text.Json.Utf8JsonWriter(buffer))
        {
            json.WriteStartObject();
            json.WriteStartObject("mcpServers");
            Server(Gg.Local.ManageTool.Server, self.Under("manage", "tools"));
            Server(Gg.Local.PlanningTool.Server, self.Under("itinerary", "tools", "--draft", $"session-{sessionId}"));
            json.WriteEndObject();
            json.WriteEndObject();

            void Server(string key, IReadOnlyList<string> arguments)
            {
                json.WriteStartObject(key);
                json.WriteString("command", self.Command);
                json.WriteStartArray("args");
                foreach (var argument in arguments)
                {
                    json.WriteStringValue(argument);
                }

                json.WriteEndArray();
                json.WriteEndObject();
            }
        }

        return System.Text.Encoding.UTF8.GetString(buffer.ToArray());
    }

    public async Task<IAgentSessionChild> StartAsync(AgentSessionStart start, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(start);

        var environment = new Dictionary<string, string>(start.Environment) { ["TERM"] = "xterm-256color" };

        var connection = await PtyProvider.SpawnAsync(
            new PtyOptions
            {
                Name = "gg-agent-session",
                Cols = start.Columns,
                Rows = start.Rows,
                Cwd = start.Directory,
                App = _binary,
                CommandLine = [.. _arguments(start)],
                Environment = environment,
            },
            cancellationToken);

        return new Child(connection);
    }

    private sealed class Child : IAgentSessionChild
    {
        private readonly IPtyConnection _connection;
        private readonly TaskCompletionSource<int> _exited = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Child(IPtyConnection connection)
        {
            _connection = connection;
            _connection.ProcessExited += (_, e) => _exited.TrySetResult(e.ExitCode);
        }

        public Task<int> Exited => _exited.Task;

        public int Read(byte[] buffer)
        {
            try
            {
                var read = _connection.ReaderStream.Read(buffer, 0, buffer.Length);

                if (read == 0)
                {
                    _exited.TrySetResult(_connection.WaitForExit(5000) ? _connection.ExitCode : -1);
                }

                return read;
            }
            catch (Exception ended) when (ended is IOException or ObjectDisposedException)
            {
                _exited.TrySetResult(_connection.WaitForExit(5000) ? _connection.ExitCode : -1);
                return 0;
            }
        }

        public void Write(ReadOnlySpan<byte> bytes)
        {
            try
            {
                _connection.WriterStream.Write(bytes);
                _connection.WriterStream.Flush();
            }
            catch (Exception ended) when (ended is IOException or ObjectDisposedException)
            {
            }
        }

        public void Resize(int columns, int rows)
        {
            try
            {
                _connection.Resize(columns, rows);
            }
            catch (Exception ended) when (ended is IOException or ObjectDisposedException or InvalidOperationException)
            {
            }
        }

        public void Kill()
        {
            try
            {
                _connection.Kill();
            }
            catch (Exception ended) when (ended is IOException or ObjectDisposedException or InvalidOperationException)
            {
            }
        }

        public void Dispose() => _connection.Dispose();
    }
}

/// <summary>Whether this machine's file opted it into ad hoc sessions, and where they live.</summary>
/// <remarks>
/// <b><c>agent-session-roots</c> is retired</b> (slice seventy-one, ADR-0039 Decision 7): each
/// session gets a directory of its own under <see cref="Home"/>, and the console chooses none.
/// </remarks>
public sealed record LocalAgentSessions(IHostAgentSessions Host, string Home)
{
    /// <summary>The host and home, or null for a machine whose file did not opt in.</summary>
    /// <param name="agentBinary">The agent the machine declares - the executor's binary.</param>
    public static LocalAgentSessions? For(Gg.Local.Configuration? file, string agentBinary)
    {
        ArgumentNullException.ThrowIfNull(agentBinary);

        return file?.AcceptAgentSessions is true
            ? new LocalAgentSessions(new AgentSessionHost(agentBinary), AgentSessions.DefaultHome())
            : null;
    }
}
