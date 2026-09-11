using System.Text;
using System.Text.Json;
using Gg.Contracts;
using Gg.Local;

namespace Gg.Console;

/// <summary>
/// Drafts envelope documents with an agent, hosted in a pseudo-terminal gg
/// owns, in the estate's working copy.
/// </summary>
/// <remarks>
/// <para>
/// <b>Not an <see cref="IEditorSession"/>, and that is the whole difference
/// from <see cref="PtyAgentSession"/>.</b> That port is text in, a real child,
/// text out — which is what made "editor or agent" a choice between two
/// implementations rather than two launch paths. Nothing comes back as text
/// here: the tool writes documents into the working copy, so the outcome is on
/// disk and what this answers is a sentence about the session. Sharing the port
/// would mean returning a string the caller would try to open a flight with.
/// </para>
/// <para>
/// <b>The root is handed to the tool server, and it is not a secret.</b> It
/// reaches the server through its own <c>env</c> entry in the MCP config
/// below, which is what makes the server the only thing that has to be told
/// where a document goes. This once said the root <i>"appears in neither the
/// agent's environment nor its command line"</i>; that was false, and here it
/// was never even material. The config is passed as a JSON string in
/// <c>--mcp-config</c>, so it IS on the command line — and the agent's cwd is
/// that same directory anyway, which is the point: it reads the documents
/// above the one it is drafting, because a narrowing only means anything
/// against the root and work kind it constrains.
/// </para>
/// <para>
/// <b>Launched with two flags, like the takeover.</b> Deliberately not
/// <c>--strict-mcp-config</c>: a person is driving this session and keeps their
/// own servers and settings. The cost is stated rather than hidden — an
/// operator who has configured a server under the key <c>gg</c> would shadow
/// this one — and it is why the tool is a validated channel rather than a
/// sandbox: an agent here could already write that directory with its own
/// tools, and what the tool adds is that a document is checked before it lands
/// and its <c>based-on:</c> precondition survives.
/// </para>
/// </remarks>
public sealed class PtyDraftSession
{
    private readonly string _agentCommand;
    private readonly Func<IHostTerminal?> _terminal;
    private readonly SelfInvocation? _self;
    private readonly HostRun _host;
    private readonly string _bar;
    private readonly Action<string> _say;
    private readonly Func<EnvelopeState?> _envelope;

    /// <param name="agentCommand">
    /// The agent, as a command line. Defaults to <c>GG_TAKE_COMMAND</c> and
    /// then to <c>claude</c> — the same two every other agent session here
    /// uses, because a person who has told gg which agent to run has told it
    /// once.
    /// </param>
    /// <param name="terminal">
    /// Where to get a terminal to host on, answering null when there is none.
    /// </param>
    /// <param name="self">
    /// How gg invokes itself to serve its own tools, or null where it cannot
    /// name its own executable. <see cref="SelfInvocation.Current"/> answers
    /// null rather than guessing, and a guess here is a tool server that fails
    /// at startup while the agent has already been told the tool exists.
    /// </param>
    /// <param name="host">How to run the child. A test passes one that cannot.</param>
    /// <param name="bar">The top row, which says what ends the session.</param>
    /// <param name="say">Where a word to the person goes when this cannot run.</param>
    /// <param name="envelope">
    /// The rules in force, for the panel to show. A function rather than a
    /// value, because the console reads the envelope between sessions and what
    /// is true when this is constructed is not what is true when somebody asks.
    /// </param>
    public PtyDraftSession(
        string? agentCommand = null,
        Func<IHostTerminal?>? terminal = null,
        SelfInvocation? self = null,
        HostRun? host = null,
        // WHAT STARTS IT AND WHAT ENDS IT, in that order, because a person
        // reads this row before they type anything. A command nobody is told
        // about is a command nobody has, and this one is the difference
        // between an empty prompt and a session that opens by reading the
        // rules.
        string? bar = null,
        Action<string>? say = null,
        Func<EnvelopeState?>? envelope = null)
    {
        _agentCommand = agentCommand
            ?? Environment.GetEnvironmentVariable("GG_TAKE_COMMAND")
            ?? "claude";
        _terminal = terminal ?? OwnedTerminal.Open;
        _self = self ?? SelfInvocation.Current;
        _host = host ?? PtyHost.RunAsync;
        _bar = bar ?? $"gg · drafting the airspace — /{DraftingPrompt.Qualified} to start "
             + "· ask the agent to submit each document it changes · closing leaves the "
             + "working copy as it stands";
        _say = say ?? System.Console.WriteLine;
        _envelope = envelope ?? (() => null);
    }

    /// <summary>
    /// Runs the agent in the working copy, and answers with a sentence about
    /// the session.
    /// </summary>
    /// <remarks>
    /// <b>It does not report what changed, because it does not know.</b> The
    /// documents the agent submitted are files now, and the diff read that
    /// follows is what says which — one place computing direction, from the
    /// comparator the door itself runs. A count guessed here would be a second
    /// answer to the question the pane is about to render.
    /// </remarks>
    /// <param name="root">The estate's working copy, which becomes the cwd.</param>
    public string Draft(string? root)
    {
        if (root is not { Length: > 0 } tree)
        {
            return "No working copy is configured, so there is nowhere to draft. "
                 + "gg config set airspace <path>.";
        }

        if (!Directory.Exists(tree))
        {
            // SAID RATHER THAN CREATED. A directory gg made because a draft
            // wanted one is an estate nobody pulled, and the first apply out of
            // it would submit documents against no precondition at all.
            return $"Nothing was drafted: {tree} is not there. Pull the airspace first.";
        }

        if (_self is null)
        {
            return "gg cannot name its own executable here, so it cannot serve the tool an "
                 + "agent submits a document with. Nothing was drafted.";
        }

        var terminal = _terminal();
        if (terminal is null)
        {
            // NO FALLBACK, and the asymmetry with an editor is the point. An
            // editor without a terminal still edits; an agent session without
            // one is not a degraded session, it is no session.
            _say("gg cannot draft with an agent here: there is no terminal to host one in.");
            return "Nothing was drafted: there is no terminal to host an agent in.";
        }

        // READ ONCE, BEFORE THE CHILD HAS THE SCREEN. The envelope comes off
        // the control plane, and doing that on the keypress would freeze the
        // panel for a round trip - a key that appears to do nothing for a
        // second is a key somebody presses again.
        var envelope = _envelope();
        var showing = HostedView.Closed;

        try
        {
            var parts = _agentCommand.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            _host(
                terminal,
                parts[0],
                [.. parts.Skip(1),
                 "--mcp-config", ServerConfig(_self, tree, Rules(envelope)),
                 // THE QUALIFIED NAMES, from the one declaration each owns.
                 // Both, and named individually: --allowedTools takes a list,
                 // and a grant of the `mcp__gg` prefix instead would widen
                 // what this launch permits every time the platform adds
                 // another tool to its own server.
                 //
                 // GRANTED RATHER THAN LEFT TO PROMPT, for the reading tool as
                 // much as the writing one. An ungranted tool still appears
                 // and still asks - and a permission prompt in front of a
                 // question the agent asked to answer its own uncertainty is
                 // the friction most likely to make it guess instead.
                 "--allowedTools", DocumentTool.Qualified, AirspaceContextTool.Qualified,
                 AirspacePullTool.Qualified],
                tree,
                most => HostedBar.Rows(showing, _bar, Body(showing, envelope), most),
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
               + "beside gg, so there is no way to host an agent.");
            return "Nothing was drafted: libporta_pty is not installed beside gg.";
        }

        return "The drafting session ended. Anything the agent submitted is in the working "
             + "copy now, and unapplied.";
    }

    /// <summary>
    /// The rules in force, as an agent should read them, or null.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>THE FIRST CALLER RenderComposed HAS EVER HAD.</b> It annotates every
    /// obligation with the layer that declared it and has sat unused in the
    /// contract since it was written, while
    /// <c>instructions-in-the-envelope</c> S30.5-04 asked for exactly that
    /// view. It is right here and wrong in <c>gg envelope show</c> for the
    /// reason its own remark gives: the annotation is a REPORT and not
    /// authorable, so it belongs where somebody reads rather than where
    /// somebody edits — and an agent about to write a narrowing needs to know
    /// which layer already carries an obligation it is tempted to repeat.
    /// </para>
    /// <para>
    /// <b>The header is a line about the render, not a second render.</b> The
    /// version is what makes this a precondition rather than advice — a
    /// document drafted against v7 is a different claim from one drafted
    /// against whatever is current — and no renderer emits it, because it is a
    /// fact about the state rather than about the envelope.
    /// </para>
    /// <para>
    /// <b>Null when the session has none, and that is ordinary.</b> A console
    /// that cannot reach a control plane still drafts: the working copy is
    /// local. The tool says so rather than leaving an agent to read silence as
    /// "there are no rules".
    /// </para>
    /// </remarks>
    private static string? Rules(EnvelopeState? envelope) => envelope is { } state
        ? $"in force: {state.Version}, last changed "
        + $"{state.UpdatedAt:yyyy-MM-dd} by {state.UpdatedBy}"
        + Environment.NewLine + Environment.NewLine
        + EnvelopeText.RenderComposed(state.Envelope)
        : null;

    /// <summary>What the open view has to show.</summary>
    /// <remarks>
    /// <b>The envelope is rendered by the same function the console's own pane
    /// uses.</b> Two renderings of the rules in force would be two things to
    /// keep in agreement, and the one that drifts is the one nobody is looking
    /// at.
    /// </remarks>
    private static string Body(HostedView showing, EnvelopeState? envelope) => showing switch
    {
        HostedView.Envelope => envelope is { } state
            ? PaneText.Envelope(new AppState { Envelope = state })
            : "",

        // NOTHING TO SHOW HERE, and it is not the intent view's absence. What a
        // drafting session produces is files, and gg naming which ones would be
        // a count it has not read - the diff after the session is what says.
        _ => "",
    };

    /// <summary>gg's own tool server, as the flag's JSON.</summary>
    /// <remarks>
    /// <para>
    /// <b>Written rather than serialized, because this binary publishes
    /// AOT.</b> Reflection-based serialization of an anonymous type is refused
    /// at compile time and cannot be source-generated, so the document is
    /// written directly — which also means every value here is escaped by the
    /// writer rather than by hand.
    /// </para>
    /// <para>
    /// <b>One environment entry, and it is the whole mechanism.</b> The tool
    /// server is the only thing in this arrangement permitted to know where a
    /// document goes, and it learns it here rather than by looking for it.
    /// </para>
    /// </remarks>
    private static string ServerConfig(SelfInvocation self, string root, string? rules)
    {
        using var buffer = new MemoryStream();

        using (var json = new Utf8JsonWriter(buffer))
        {
            json.WriteStartObject();
            json.WriteStartObject("mcpServers");
            json.WriteStartObject(DocumentTool.Server);

            json.WriteString("command", self.Command);
            json.WriteStartArray("args");
            foreach (var argument in self.Arguments)
            {
                json.WriteStringValue(argument);
            }
            json.WriteEndArray();

            json.WriteStartObject("env");
            json.WriteString(DocumentTool.RootVariable, root);

            // OMITTED RATHER THAN EMPTY when there are none. An environment
            // entry set to "" is a value the server would have to tell apart
            // from an absent one, and both mean the same thing here.
            if (rules is { Length: > 0 } composed)
            {
                json.WriteString(AirspaceContextTool.EnvelopeVariable, composed);
            }

            json.WriteEndObject();

            json.WriteEndObject();
            json.WriteEndObject();
            json.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }
}
