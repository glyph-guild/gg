namespace Gg.Local;

/// <summary>
/// What an operator chose, on this machine.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every value is the STRING its variable carries, not a parsed shape.</b>
/// <c>VcsConfiguration.FromEnvironment(string? declared)</c> and its five
/// siblings already take their declaration as text, and that optional parameter
/// is the seam this type plugs into — so a file value reaches the existing
/// parser, the existing refusal message and the existing tests untouched. A
/// parsed shape here would have to be rendered back to feed that seam, which is
/// one value in two representations and a second thing to get wrong.
/// </para>
/// <para>
/// <b>Every member is optional, and absent is not the same as blank.</b> Absent
/// means the operator chose nothing and the environment or the default answers.
/// Blank is refused: a person who typed <c>""</c> wrote a value, the reader
/// would see silence, and nothing on any screen would say the line did nothing.
/// </para>
/// <para>
/// <b>What is deliberately NOT here.</b> The three path roots
/// (<c>XDG_CONFIG_HOME</c>, <c>XDG_STATE_HOME</c>, <c>XDG_CACHE_HOME</c>),
/// because this file's own location is computed from the first of them — a
/// value inside it could not be read before it was needed. And the
/// per-invocation signals (<c>GG_STATE_DUMP</c>, <c>GG_MEMBER_NONCE</c>,
/// <c>GG_MEMBER_CONTROL_PLANE</c>, <c>GG_IMAGE_DIGEST</c>), which are one
/// process telling another something rather than settings anybody chose.
/// </para>
/// <para>
/// <b>And no secret.</b> This file names which credential to use and never the
/// credential — <c>IntentReader.Locator</c>'s rule, one level up. Secrets stay
/// in <c>credentials/*.secret</c> at 0600.
/// </para>
/// </remarks>
public sealed record Configuration
{
    /// <summary>The control plane this machine reads and writes.</summary>
    public string? ControlPlane { get; init; }

    /// <summary>The editor a handoff gives the terminal to.</summary>
    public string? Editor { get; init; }

    /// <summary>What `t` starts to hand somebody a flight's tree.</summary>
    public string? TakeCommand { get; init; }

    /// <summary>Trackers this binary reads work items from itself.</summary>
    public string? IntentHosts { get; init; }

    /// <summary>Trackers read by a tool server somebody installed.</summary>
    public string? IntentReaders { get; init; }

    /// <summary>Which forge each provider key clones from.</summary>
    public string? VcsHosts { get; init; }

    /// <summary>Where a proposal is opened, per provider key.</summary>
    public string? DestinationApis { get; init; }

    /// <summary>The agent binary a runner invokes.</summary>
    public string? ExecutorBinary { get; init; }

    /// <summary>The labels this machine's runner advertises.</summary>
    public string? RunnerLabels { get; init; }

    /// <summary>How long a lease claim waits, in seconds.</summary>
    /// <remarks>
    /// A number rather than the text its variable carries, unlike every member
    /// above it. There is no <c>FromEnvironment</c> seam for this one — the
    /// composition root parses it inline — so there is no string shape to
    /// preserve, and a number is what a person editing the file would write.
    /// </remarks>
    public int? RunnerHoldSeconds { get; init; }

    /// <summary>The scope-enforcing proxy a pool maintainer works through.</summary>
    public string? PoolEndpoint { get; init; }

    /// <summary>Relay addresses for the runner and console peer connection.</summary>
    public string? StunServers { get; init; }

    /// <summary>Every text member, beside the key it is written under.</summary>
    /// <remarks>
    /// <b>One list, read by the blank rule and by the renderer's key check.</b>
    /// A member added without an entry here is a member nothing holds to the
    /// blank rule, which is the one blank nobody checked — so the list is here
    /// rather than repeated at each use.
    /// </remarks>
    internal static IReadOnlyList<(string Key, Func<Configuration, string?> Of)> Text { get; } =
    [
        ("control-plane", c => c.ControlPlane),
        ("editor", c => c.Editor),
        ("take-command", c => c.TakeCommand),
        ("intent-hosts", c => c.IntentHosts),
        ("intent-readers", c => c.IntentReaders),
        ("vcs-hosts", c => c.VcsHosts),
        ("destination-apis", c => c.DestinationApis),
        ("executor-binary", c => c.ExecutorBinary),
        ("runner-labels", c => c.RunnerLabels),
        ("pool-endpoint", c => c.PoolEndpoint),
        ("stun-servers", c => c.StunServers),
    ];

    /// <summary>Why this configuration cannot be used, or null when it can.</summary>
    /// <remarks>
    /// A sentence naming the offending value, never a bool —
    /// <c>Envelope.Validate</c>'s rule, and this document is hand-edited more
    /// often than an envelope is.
    /// </remarks>
    public static string? Validate(Configuration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        foreach (var (key, of) in Text)
        {
            if (of(configuration) is { } value && string.IsNullOrWhiteSpace(value))
            {
                return $"'{key}' is set to a blank value. Blank is not the same as unset: "
                     + "whoever typed it wrote a value, and the reader would see nothing "
                     + $"there and use the default instead. Remove the line to mean unset.";
            }
        }

        if (configuration.RunnerHoldSeconds is { } hold && hold < 1)
        {
            return $"'runner-hold-seconds' is {hold}, and a hold is at least one second. "
                 + "A claim that waits no time comes back empty as fast as the machine can "
                 + "ask, which is a busy loop against the control plane rather than a "
                 + "configuration.";
        }

        foreach (var (key, address) in ((string Key, string? Value)[])
                 [("control-plane", configuration.ControlPlane),
                  ("pool-endpoint", configuration.PoolEndpoint)])
        {
            // THE SCHEME IS CHECKED, NOT JUST ABSOLUTENESS, and the difference
            // is the mistake this is for. `localhost:5199` parses as absolute -
            // `localhost:` is read as the scheme - so a person who copied the
            // default without its `http://` would get a document that validated
            // and a machine that could not reach anything. The default in the
            // help text is `http://localhost:5199`, which makes dropping the
            // scheme the likeliest thing anybody types wrong here.
            if (address is { Length: > 0 }
             && (!Uri.TryCreate(address, UriKind.Absolute, out var uri)
                 || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)))
            {
                return $"'{key}' is '{address}', which is not an http or https address. It "
                     + "is where a request is sent, so a value like this fails at the first "
                     + "call with a message about the call rather than about the file.";
            }
        }

        return null;
    }
}
