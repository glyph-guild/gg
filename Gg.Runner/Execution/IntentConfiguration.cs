namespace Gg.Runner.Execution;

/// <summary>
/// One tracker this runner can read, and what to launch to read it.
/// </summary>
/// <remarks>
/// <b>A command, never a forge.</b> This binary is public and names no tracker;
/// what it holds is the shape - a provider key a tenant chose and a process an
/// operator installed. Which tracker that process talks to is the deployment's
/// business, exactly as <c>GG_VCS_HOSTS</c> keeps which forge a tenant clones
/// from out of here.
/// </remarks>
/// <param name="Key">The provider key, as a flight's intent spells it.</param>
/// <param name="Command">The executable to launch as a tool server.</param>
/// <param name="Arguments">Its arguments, in the order they were declared.</param>
/// <param name="EnvironmentVariable">
/// The variable this server reads its credential from, or null when it needs
/// none. <b>The server's environment is the only place a secret may go</b> - not
/// an argument, which every <c>ps</c> on the host can read.
/// </param>
/// <param name="Locator">
/// The credential to resolve, in the form <c>gg credential add</c> stores, or
/// null. Resolved runner-side and handed to the server; the agent never holds
/// it.
/// </param>
public readonly record struct IntentReader(
    string Key,
    string Command,
    IReadOnlyList<string> Arguments,
    string? EnvironmentVariable = null,
    string? Locator = null);

/// <summary>
/// Which trackers this runner can resolve a work item in.
/// </summary>
/// <remarks>
/// <para>
/// <b>The gap this closes.</b> A flight about a work item reaches an agent that
/// has no tool able to read one: <c>--strict-mcp-config</c> with no
/// <c>--mcp-config</c> is a correct pair - it removes the operator's own servers
/// - and it leaves nothing behind. The design was already written down in
/// <c>ClaudeCodeExecutor</c>'s own remark: <i>"the agent resolves what it points
/// at from inside the customer's environment, with the customer's own
/// credential."</i>
/// </para>
/// <para>
/// <b>Deployment knowledge, so it is configured.</b> The same disposition as
/// <c>VcsConfiguration</c> and <c>DestinationConfiguration</c>, and for the same
/// two reasons: a public binary must not name a forge, and which providers a
/// runner serves is a fact about a machine rather than about this code.
/// </para>
/// <para>
/// <b>Absent is the ordinary state</b> and stays one. Every runner in the fleet
/// declares nothing today, and a link flight or a text flight names no tracker
/// and needs none.
/// </para>
/// </remarks>
public static class IntentConfiguration
{
    /// <summary>The variable naming which trackers this runner can read.</summary>
    public const string ReadersVariable = "GG_INTENT_READERS";

    /// <summary>
    /// The readers this environment describes.
    /// </summary>
    /// <remarks>
    /// <c>key=command arg arg, key=command</c> — commas between entries and the
    /// first space inside one separating the command from its arguments. The
    /// list shape is <c>GG_VCS_HOSTS</c>'s, deliberately: an operator
    /// configuring a runner should learn one format, not two.
    /// </remarks>
    public static IReadOnlyList<IntentReader> FromEnvironment(string? declaration = null)
    {
        var raw = declaration ?? Environment.GetEnvironmentVariable(ReadersVariable) ?? "";
        var readers = new List<IntentReader>();

        foreach (var entry in raw.Split(
            ',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var split = entry.IndexOf('=');
            if (split <= 0)
            {
                throw new InvalidOperationException(
                    $"'{entry}' in {ReadersVariable} is not 'provider=command'. Each entry names "
                  + "a provider key a flight's intent can carry and the tool server that reads "
                  + "it, e.g. 'my-tracker=my-tracker-mcp --stdio'.");
            }

            var key = entry[..split].Trim();
            var invocation = entry[(split + 1)..].Trim();

            // THE KEY IS THE TOOL-NAME PREFIX, so it is not cosmetic: an MCP
            // tool arrives as `mcp__<server>__<tool>`, and a reader declared
            // under the key this platform serves its own tool from would
            // shadow it. The agent would then be granted
            // `mcp__gg__nominate_work_kind` against somebody else's process -
            // and the nomination it declared would be read out of a transcript
            // by a runner that never served the call.
            if (string.Equals(key, NominationTool.Server, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"'{key}' in {ReadersVariable} is the key this platform serves its own "
                  + "tools from, so a reader declared under it would shadow them. Name the "
                  + "tracker something else - the key is the tool-name prefix an agent sees, "
                  + "not a label.");
            }

            // `command args | VAR=locator` - the credential half is optional,
            // because a tracker reachable without a secret must not be made to
            // invent one to satisfy a parser.
            var bar = invocation.IndexOf('|');
            var launch = (bar < 0 ? invocation : invocation[..bar]).Trim();
            var credential = bar < 0 ? "" : invocation[(bar + 1)..].Trim();

            if (launch.Length == 0)
            {
                throw new InvalidOperationException(
                    $"'{key}' in {ReadersVariable} declares no command. A provider with nothing "
                  + "to launch would be advertised as readable and then read nothing, which is "
                  + "worse than not declaring it: declare the tool server, or remove the entry.");
            }

            string? variable = null;
            string? locator = null;
            if (credential.Length > 0)
            {
                // HALF A CREDENTIAL DESCRIBES A SERVER THAT STARTS AND FAILS TO
                // AUTHENTICATE. Refused here, where an operator can still fix
                // the line, rather than at a tracker that answers 401.
                var equals = credential.IndexOf('=');
                if (equals <= 0 || equals == credential.Length - 1)
                {
                    throw new InvalidOperationException(
                        $"'{key}' in {ReadersVariable} declares '{credential}' after '|', which "
                      + "is not 'VARIABLE=locator'. Name both: the variable the tool server "
                      + "reads its credential from, and the credential to resolve, e.g. "
                      + "'|TRACKER_TOKEN=local:acme/board'. Omit the whole section if the server "
                      + "needs no credential.");
                }

                variable = credential[..equals].Trim();
                locator = credential[(equals + 1)..].Trim();
            }

            var parts = launch.Split(
                ' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            readers.Add(new IntentReader(
                key, parts[0], [.. parts.Skip(1)], variable, locator));
        }

        return readers;
    }

    /// <summary>The verb this binary serves a tracker reader under.</summary>
    private static readonly string[] ReadVerb = ["runner", "read"];

    /// <summary>
    /// A reader served by this binary rather than launched from a declaration.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>THE SECRET STOPS TRAVELLING IN AN ARGUMENT.</b> An external server can
    /// only be handed a credential through the config that launches it, and that
    /// config is an argument to the agent's process - readable by every
    /// <c>ps</c> on the host. <c>ServerConfig</c> accepts that because there is
    /// no alternative for a program this repository did not write. There is one
    /// here: a locator NAMES a credential and is not one, so it travels in the
    /// argument and the child resolves it from the same store the runner would
    /// have read. Nothing is left for an environment block to carry.
    /// </para>
    /// <para>
    /// <b>The host travels too, and for a duller reason.</b> This child is
    /// started by the AGENT, not by the runner, so it cannot be relied on to
    /// inherit the runner's environment - and a reader that silently read a
    /// different tracker than the one configured would be the worst possible
    /// failure of this whole path. Neither value is a secret.
    /// </para>
    /// <para>
    /// <b>A second verb, not a second tool on the first server.</b>
    /// <c>SelfInvocation</c> names <c>runner tools</c>, whose safety as a child
    /// of a compromised process rests on holding no credential. This one holds
    /// one, so it is a different server reached by a different verb.
    /// </para>
    /// </remarks>
    /// <param name="key">The provider key, as a flight's intent spells it.</param>
    /// <param name="host">The tracker root this reader speaks to, from configuration.</param>
    /// <param name="locator">The credential to resolve, or null where none is needed.</param>
    /// <param name="self">How to start this binary again.</param>
    public static IntentReader Served(
        string key, string host, string? locator, SelfInvocation self)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(host);
        ArgumentNullException.ThrowIfNull(self);

        // THE SAME SHADOWING RULE THE DECLARATION HAS. A key is a tool-name
        // prefix wherever it came from, and one that collides with the
        // platform's own server shadows it just as thoroughly when we are the
        // ones who chose it.
        if (string.Equals(key, NominationTool.Server, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"'{key}' is the key this platform serves its own tools from, so a reader "
              + "served under it would shadow them.");
        }

        return new IntentReader(
            key,
            self.Command,
            [
                // The bootstrap comes from SelfInvocation, which is the one
                // place that knows whether this process needs its own assembly
                // handed back to it.
                .. self.Under(ReadVerb),
                "--provider", key,
                "--host", host,
                .. locator is { Length: > 0 } named ? (string[])["--credential", named] : [],
            ],
            // NOTHING FOR AN ENVIRONMENT BLOCK, which is the whole point: the
            // launch has no secret to place, so it writes none.
            EnvironmentVariable: null,
            Locator: null);
    }

    /// <summary>
    /// Why this runner cannot read a work item in <paramref name="provider"/>,
    /// or null when it can — or when there is nothing to read.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Decided before a loop is spent.</b> An agent handed a work item it has
    /// no tool for would burn the whole wall-clock budget establishing that, and
    /// report it as prose somebody has to interpret. That is not hypothetical:
    /// it is the flight that started this.
    /// </para>
    /// <para>
    /// <b>Null provider is null answer.</b> A link flight and a text flight name
    /// no tracker and never needed one; a refusal that fired on them would
    /// ground the fleet over a declaration nothing wanted.
    /// </para>
    /// </remarks>
    public static string? Unreadable(string? provider, IReadOnlyList<IntentReader> readers)
    {
        ArgumentNullException.ThrowIfNull(readers);

        if (provider is not { Length: > 0 })
        {
            return null;
        }

        if (readers.Any(r => string.Equals(r.Key, provider, StringComparison.Ordinal)))
        {
            return null;
        }

        var declared = readers.Count == 0
            ? "declares none"
            : $"declares {string.Join(", ", readers.Select(r => $"'{r.Key}'"))}";

        // SHORT, AND THE ACTIONABLE HALF FIRST. A reason is shortened on its way
        // to the fact, and this one was long enough that the variable to set was
        // in the part that got cut - which is a refusal that names a problem and
        // hides its remedy. The provider and the variable lead.
        return $"No reader for '{provider}': set {ReadersVariable}, or route this flight to a "
             + $"runner that has one. This runner {declared}. No agent was invoked, because one "
             + "given a work item it cannot open spends the whole budget finding that out.";
    }

    /// <summary>
    /// Why this reader's credential cannot be used, or null.
    /// </summary>
    /// <remarks>
    /// <b>Refused rather than launched empty</b>, which is
    /// <c>NoCredentialResolver</c>'s disposition and for its reason: a server
    /// started with an empty secret fails at the tracker with an authentication
    /// error nobody can trace back to a missing file on this host.
    /// </remarks>
    public static string? Unresolvable(IntentReader reader, string? secret)
    {
        if (reader.Locator is not { Length: > 0 } locator)
        {
            return null;
        }

        return secret is { Length: > 0 }
            ? null
            : $"No credential at '{locator}' for '{reader.Key}' on this runner. It is declared in "
            + $"{ReadersVariable} and this machine does not have it: run `gg credential add`, or "
            + "route this flight to a runner that holds it. No agent was invoked, because a tool "
            + "server started without its credential fails at the tracker instead.";
    }
}
