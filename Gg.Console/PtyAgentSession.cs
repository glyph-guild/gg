using System.Text;
using System.Text.Json;
using Gg.Contracts;
using Gg.Local;

namespace Gg.Console;

/// <summary>
/// Composes an intent with an agent, hosted in a pseudo-terminal gg owns, and
/// answers with what the agent submitted.
/// </summary>
/// <remarks>
/// <para>
/// <b>The same port as <see cref="EditorSession"/> and
/// <see cref="PtyEditorSession"/>.</b> Text in, a real child, text out — so the
/// modal that offers "editor or agent" picks between two implementations of one
/// interface rather than branching every launch path twice. Every seam that
/// would have to move if composing had its own port is a seam that would have
/// to be tested again.
/// </para>
/// <para>
/// <b>What comes back arrives by tool call, and there is nothing else it could
/// arrive by.</b> The agent is never told where the intent goes: that path is
/// handed to <c>PlatformToolServer</c> through its own entry in the MCP config
/// written below, and it appears in neither the agent's environment nor its
/// command line. An agent that knew the path could write the file itself, and
/// the tool would be decorative — a governance decision back in the hands of
/// whatever the agent felt like doing. Opening a flight takes a number, is
/// attributed, and is a record somebody has to explain, which is why
/// <c>instructions-in-the-envelope</c> rule 7 refuses to have it parsed out of
/// prose.
/// </para>
/// <para>
/// <b>Launched bare, the way this console already launches an agent.</b>
/// <see cref="TakeSession"/> runs <c>claude</c> with no flags at all, on the
/// grounds that an attended session belongs to the person sitting in front of
/// it. The two flags here are what the tool needs to exist and be callable, and
/// nothing else — in particular <c>--strict-mcp-config</c> is NOT passed, so the
/// person keeps their own servers and settings in a session they are driving.
/// The cost of that choice is stated rather than hidden: an operator who has
/// configured a server under the key <c>gg</c> would shadow this one.
/// </para>
/// </remarks>
public sealed class PtyAgentSession : IEditorSession
{
    /// <summary>The file the tool server writes and this session reads.</summary>
    /// <remarks>
    /// A fixed name inside a directory per session, so the path is derivable by
    /// a test that wants to watch it and by nothing else — the agent never sees
    /// either half.
    /// </remarks>
    private const string IntentFile = "intent.txt";

    private readonly string _agentCommand;
    private readonly Func<IHostTerminal?> _terminal;
    private readonly SelfInvocation? _self;
    private readonly HostRun _host;
    private readonly string? _composeIn;
    private readonly string _bar;
    private readonly string _submitted;
    private readonly Action<string> _say;
    private readonly Func<EnvelopeState?> _envelope;

    /// <param name="agentCommand">
    /// The agent, as a command line. Defaults to <c>GG_TAKE_COMMAND</c> and then
    /// to <c>claude</c> — the same two the takeover already uses, because a
    /// person who has told gg which agent to run has told it once.
    /// </param>
    /// <param name="terminal">
    /// Where to get a terminal to host on, answering null when there is none.
    /// </param>
    /// <param name="self">
    /// How gg invokes itself to serve its own tools, or null where it cannot
    /// name its own executable. Defaults to
    /// <see cref="SelfInvocation.Current"/>, which answers null rather than
    /// guessing — and a guess here is a tool server that fails at startup while
    /// the agent has already been told the tool exists.
    /// </param>
    /// <param name="host">How to run the child. A test passes one that cannot.</param>
    /// <param name="composeIn">
    /// The directory the intent lands in. Defaults to one made per session and
    /// removed after; named so a test can look inside it.
    /// </param>
    /// <param name="bar">
    /// The top row before anything has been submitted. It says what ends the
    /// session, because a person handed an agent cannot ask it what gg is
    /// waiting for — it does not know either.
    /// </param>
    /// <param name="submitted">
    /// The top row once an intent has landed.
    /// <para>
    /// <b>A second sentence rather than a suffix on the first.</b> What a person
    /// needs to be told changes completely at that moment: before, it is what
    /// ends the session; after, it is that they are done and that submitting
    /// again replaces rather than adds. Somebody who cannot tell which state
    /// they are in submits twice.
    /// </para>
    /// </param>
    /// <param name="say">Where a word to the person goes when this cannot run.</param>
    /// <param name="envelope">
    /// The rules in force, for the panel to show. A function rather than a
    /// value, because the console reads the envelope between sessions and what
    /// is true when this is constructed is not what is true when somebody asks.
    /// <para>
    /// Null answers nothing, and the panel says so out loud rather than showing
    /// an empty box — an envelope nobody read and one with no instructions are
    /// the same blank panel, and only the first is a thing to go and fix.
    /// </para>
    /// </param>
    public PtyAgentSession(
        string? agentCommand = null,
        Func<IHostTerminal?>? terminal = null,
        SelfInvocation? self = null,
        HostRun? host = null,
        string? composeIn = null,
        string bar = "gg · composing — ask the agent to submit when you are happy · "
                   + "closing without submitting composes nothing",
        string submitted = "gg · composing — intent submitted · close when you are done, "
                         + "or submit again to replace it",
        Action<string>? say = null,
        Func<EnvelopeState?>? envelope = null)
    {
        _agentCommand = agentCommand
            ?? Environment.GetEnvironmentVariable("GG_TAKE_COMMAND")
            ?? "claude";
        _terminal = terminal ?? OwnedTerminal.Open;
        _self = self ?? SelfInvocation.Current;
        _host = host ?? PtyHost.RunAsync;
        _composeIn = composeIn;
        _bar = bar;
        _submitted = submitted;
        _say = say ?? System.Console.WriteLine;
        _envelope = envelope ?? (() => null);
    }

    /// <summary>
    /// Runs the agent and answers with the intent it submitted, or nothing.
    /// </summary>
    /// <remarks>
    /// <b>Empty means nothing was submitted, and it is not a failure.</b> An
    /// agent that talked for an hour and never called the tool has produced no
    /// intent; the words on its screen are not one just because they are the
    /// only thing there. gg has no business guessing, and the caller already
    /// treats empty text as "nothing to open".
    /// </remarks>
    public string Edit(string initialText)
    {
        if (_self is null)
        {
            // SAYS SO RATHER THAN GUESSING, which is SelfInvocation's own rule:
            // a server configured with a path that is not this binary is a child
            // that fails at startup, and the agent has already been told the
            // tool exists by then.
            _say("gg cannot name its own executable here, so it cannot serve the tool an "
               + "agent submits an intent with. Nothing was composed.");
            return "";
        }

        var terminal = _terminal();
        if (terminal is null)
        {
            // NO FALLBACK, and the asymmetry with the editor is the point. An
            // editor without a terminal still edits; an agent session without a
            // terminal is not a degraded session, it is no session.
            _say("gg cannot compose with an agent here: there is no terminal to host one in. "
               + "Nothing was composed.");
            return "";
        }

        // WHAT GG IS SHOWING, for as long as this session. A local, because it
        // belongs to one session and outliving one would mean the next opened
        // on whatever the last person left up.
        var showing = HostedView.Closed;

        var ours = _composeIn is null;
        var directory = _composeIn ?? Path.Combine(
            Path.GetTempPath(), "gg-compose-" + Guid.NewGuid().ToString("N")[..8]);

        try
        {
            Directory.CreateDirectory(directory);
            var intent = Path.Combine(directory, IntentFile);

            try
            {
                var parts = _agentCommand.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                _host(
                    terminal,
                    parts[0],
                    [.. parts.Skip(1),
                     "--mcp-config", ServerConfig(_self, intent),
                     // THE QUALIFIED NAME, from the one declaration that owns
                     // all three spellings. A launch grants what the transcript
                     // and the server would each call something slightly
                     // different if this were typed here.
                     "--allowedTools", IntentTool.Qualified],
                    Directory.GetCurrentDirectory(),
                    // ASKED ON EVERY FRAME, and the status answered from the one
                    // thing that knows: whether the file is there. The tool
                    // server writes it by rename, so it is either absent or
                    // whole - which is what makes a stat an honest answer rather
                    // than a race.
                    most => HostedBar.Rows(
                        showing,
                        File.Exists(intent) ? _submitted : _bar,
                        Body(showing, intent),
                        most),

                    // AND GG'S ONE KEY. The panel's state lives here rather than
                    // in the host, because the host holds nothing between calls
                    // and a test asserts it does not.
                    typed =>
                    {
                        if (!HostedBar.Takes(showing, typed))
                        {
                            return false;
                        }

                        showing = HostedBar.Next(showing, typed);
                        return true;
                    },
                    CancellationToken.None).GetAwaiter().GetResult();
            }
            catch (Exception missing) when (
                missing is DllNotFoundException or EntryPointNotFoundException)
            {
                _say("gg could not open its own terminal view: libporta_pty is not installed "
                   + "beside gg, so there is no way to host an agent. Nothing was composed.");
                return "";
            }

            // READ AFTER, NOT WATCHED. Watching is what would let the bar change
            // once the intent lands, which is worth doing and is not this slice:
            // the bar is one string here, and PtyHost takes it once.
            return File.Exists(intent) ? File.ReadAllText(intent) : "";
        }
        finally
        {
            // HOWEVER IT ENDED. The file holds what somebody was proposing, in a
            // directory everybody on this machine can read, and by this point it
            // has already been handed back.
            Forget(directory, ours);
        }
    }

    /// <summary>What the open view has to show.</summary>
    /// <remarks>
    /// <b>The envelope is rendered by the same function the console's own pane
    /// uses.</b> Two renderings of the rules in force would be two things to
    /// keep in agreement, and the one that drifts is the one nobody is looking
    /// at — which is the argument `instructions-in-the-envelope` makes about
    /// prompts, one surface over.
    /// </remarks>
    private string Body(HostedView showing, string intent) => showing switch
    {
        HostedView.Envelope => _envelope() is { } state
            ? PaneText.Envelope(new AppState { Envelope = state })
            : "",

        // ONLY AFTER IT LANDS, because before that gg does not know. An agent
        // composes in its own session and hands the result back by tool call;
        // there is nothing to show until it does, and guessing from the screen
        // is what rule 7 forbids.
        HostedView.Intent => File.Exists(intent) ? File.ReadAllText(intent) : "",

        _ => "",
    };

    /// <summary>Removes the intent, and the directory if this session made it.</summary>
    /// <remarks>
    /// <b>Only what it owns.</b> A caller that named the directory keeps it —
    /// deleting somebody else's directory because a session happened to use it
    /// is the kind of tidying that eventually takes something with it.
    /// </remarks>
    private static void Forget(string directory, bool ours)
    {
        try
        {
            var intent = Path.Combine(directory, IntentFile);
            if (File.Exists(intent))
            {
                File.Delete(intent);
            }

            if (ours && Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
        catch (IOException)
        {
            // Cleanup is not worth failing a session that already produced its
            // answer, and the caller has that answer by now.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    /// <summary>gg's own tool server, as the flag's JSON.</summary>
    /// <remarks>
    /// <para>
    /// <b>Written rather than serialized, because this binary publishes AOT.</b>
    /// Reflection-based serialization of an anonymous type is refused at compile
    /// time and cannot be source-generated, so the document is written directly
    /// — which also means every value here is escaped by the writer rather than
    /// by hand. The same reasoning, and the same shape, as the runner's own
    /// config writer.
    /// </para>
    /// <para>
    /// <b>The runner's version takes no environment at all, and this one takes
    /// exactly one.</b> That difference is the whole mechanism: the tool server
    /// is the only thing in this arrangement permitted to know where an intent
    /// goes, and it learns it here rather than by looking for it.
    /// </para>
    /// </remarks>
    private static string ServerConfig(SelfInvocation self, string intentPath)
    {
        using var buffer = new MemoryStream();

        using (var json = new Utf8JsonWriter(buffer))
        {
            json.WriteStartObject();
            json.WriteStartObject("mcpServers");
            json.WriteStartObject(IntentTool.Server);

            json.WriteString("command", self.Command);
            json.WriteStartArray("args");
            foreach (var argument in self.Arguments)
            {
                json.WriteStringValue(argument);
            }
            json.WriteEndArray();

            json.WriteStartObject("env");
            json.WriteString(IntentTool.PathVariable, intentPath);
            json.WriteEndObject();

            json.WriteEndObject();
            json.WriteEndObject();
            json.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }
}
