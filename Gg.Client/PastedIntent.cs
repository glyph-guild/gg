namespace Gg.Client;

/// <summary>
/// What somebody pasted, read as one of the three kinds a flight can have.
/// </summary>
/// <remarks>
/// <para>
/// <b>The console could reach exactly one of three intent kinds.</b>
/// <c>ConsoleData.FlyAsync</c> called the command with <c>uri: null</c> and no
/// provider, so <c>gg fly --uri</c> and <c>gg fly --ticket</c> had no path from
/// the console at all - a person with a work item in front of them could not
/// open a flight against it without leaving for a shell.
/// </para>
/// <para>
/// <b>One reading, in <c>Gg.Client</c>, because both surfaces need it.</b> The
/// command line has flags and says which kind it was told; a prompt has one box
/// and has to look. Putting the looking here keeps it beside
/// <see cref="FlightCommands.FlyAsync"/>, which is the only thing it feeds, and
/// out of a console that would then hold a second spelling of the same rule.
/// </para>
/// <para>
/// <b>It is deliberately not clever.</b> A scheme means a uri, a <c>#</c> means
/// a ticket, and anything else is what somebody typed - which is the free-text
/// intent that already worked. Guessing harder would mean a paste that opens
/// the wrong kind of flight, and the failure would be silent: the flight opens,
/// reaches a runner, and does the wrong work.
/// </para>
/// </remarks>
public static class PastedIntent
{
    /// <summary>What one pasted string turned out to be.</summary>
    public readonly record struct Read(string? Text, string? Uri, string? Provider, string? Id)
    {
        /// <summary>Why this cannot be flown, or null.</summary>
        public string? Refusal { get; init; }
    }

    /// <summary>
    /// <c>provider#id</c>, split into the two fields a ticket is.
    /// </summary>
    /// <remarks>
    /// <b>Split on the FIRST separator, never on every one.</b> A tracker whose
    /// ids contain a <c>#</c> would otherwise lose the tail silently, which is
    /// the truncation failure this repository keeps finding one field at a time.
    /// </remarks>
    public static (string? Provider, string? Id) SplitTicket(string token)
    {
        ArgumentNullException.ThrowIfNull(token);

        var separator = token.IndexOf('#', StringComparison.Ordinal);

        return separator <= 0 || separator == token.Length - 1
            ? (null, null)
            : (token[..separator], token[(separator + 1)..]);
    }

    /// <summary>Reads a pasted line as a uri, a ticket, or text.</summary>
    public static Read Of(string? pasted)
    {
        var trimmed = (pasted ?? "").Trim();

        if (trimmed.Length == 0)
        {
            return new Read(null, null, null, null)
            {
                Refusal = "Nothing was pasted. A flight needs a work item's URL, a "
                        + "provider#id ticket, or a sentence saying what to do.",
            };
        }

        // A URI FIRST, because a url can contain a '#' - a fragment - and
        // reading one as a ticket would split it at the anchor and open a
        // flight against a provider named "https://example.invalid/x".
        if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            return new Read(null, trimmed, null, null);
        }

        if (trimmed.Contains('#', StringComparison.Ordinal))
        {
            var (provider, id) = SplitTicket(trimmed);

            return provider is null
                ? new Read(null, null, null, null)
                {
                    // The parser is the only thing that knows the token was
                    // MEANT to be two things: the contract would see a provider
                    // and no id and say so correctly, but it cannot say "you
                    // left out the #".
                    Refusal = $"'{trimmed}' looks like a ticket and is not one. A ticket is "
                            + "<provider>#<id>, and both halves are needed - the id alone "
                            + "does not say which tracker it is in.",
                }
                : new Read(null, null, provider, id);
        }

        return new Read(trimmed, null, null, null);
    }
}
