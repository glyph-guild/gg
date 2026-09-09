namespace Gg.Contracts;

/// <summary>
/// The settings a control plane may offer a machine.
/// </summary>
/// <remarks>
/// <para>
/// <b>Enumerated, never a forbidden list.</b> A list of names that must NOT
/// arrive passes on the third member nobody thought of, which is the reasoning
/// <c>FlightNomination</c> already writes down for its own two members. This is
/// the whole set, and anything else is refused by name.
/// </para>
/// <para>
/// <b>What is in it can only ever be data.</b> Relay addresses, and the labels a
/// machine advertises work for. Wrong relays degrade a connection and grant
/// nothing; labels can only ever offer to do less or different work, never more.
/// </para>
/// <para>
/// <b>What is deliberately out, and why each one is an instruction wearing
/// configuration's clothes.</b> The executor binary is a control plane naming a
/// program a machine executes. The intent readers' value <i>is</i> a command
/// line, with a credential variable in it. The forge hosts and destination APIs
/// redirect where code is cloned from and where a proposal is opened. The
/// control plane address is a redirect and self-referential. The pool endpoint
/// is the scope-enforcing proxy, and moving it removes the scope enforcement.
/// </para>
/// <para>
/// <b>And the keys that decide whether ANY of this is accepted are not here</b>,
/// which is the safety argument rather than a detail: a control plane that could
/// offer them would be granting itself the permission. They live in the local
/// file, off by default, and an operator turns them on per machine.
/// </para>
/// <para>
/// <b>Two, not three.</b> A hold in seconds was proposed and dropped: it is set
/// by nobody, measured, and a key offered for a need nothing evidences is
/// surface with no argument behind it.
/// </para>
/// </remarks>
[VocabularyOf(VocabularyFingerprints.Contract)]
public static class OfferableKeys
{
    /// <summary>Relay addresses for the runner and console connection.</summary>
    public const string StunServers = "stun-servers";

    /// <summary>Which work a machine advertises for.</summary>
    public const string RunnerLabels = "runner-labels";

    public static IReadOnlyList<string> All { get; } = [StunServers, RunnerLabels];
}

/// <summary>One setting a control plane offers.</summary>
/// <remarks>
/// <b>A key and a value and nothing else.</b> Every field somebody will want to
/// add — a reason, a priority, a "required" flag — makes the offer more useful
/// and makes it an instruction. The nomination's own two-member shape is the
/// precedent, and it says the pressure runs one way.
/// </remarks>
[PinnedId("0a3f5c81-6d29-4e7b-9c04-5f18b2e7a63d")]
public sealed record OfferedSetting
{
    /// <summary>One of <see cref="OfferableKeys"/>. Anything else is refused.</summary>
    public required string Key { get; init; }

    public required string Value { get; init; }
}

/// <summary>
/// Configuration a control plane offers, which a machine may accept.
/// </summary>
/// <remarks>
/// <para>
/// <b>Carried on a response the machine already asked for.</b> Nothing connects
/// inbound to a laptop; a runner heartbeats and a console refreshes, so an offer
/// rides what is already being fetched. "Push" is what it feels like to an
/// operator; a carried offer is what it is, and saying so keeps anyone from
/// building a channel.
/// </para>
/// <para>
/// <b>Versioned, so accepting is recorded against an exact document</b> and
/// re-offering one already accepted changes nothing.
/// </para>
/// </remarks>
[PinnedId("b7e04c96-1a58-4d3f-82b6-0e947fd3512c")]
public sealed record OfferedConfiguration
{
    /// <summary>Names this exact offer, permanently.</summary>
    public required string Version { get; init; }

    /// <summary>What is offered. Empty is a real answer: nothing is offered.</summary>
    public required IReadOnlyList<OfferedSetting> Settings { get; init; }

    public required DateTimeOffset OfferedAt { get; init; }

    /// <summary>Why this offer cannot be taken, or null when it can.</summary>
    /// <remarks>
    /// A sentence naming the offending key, never a bool — the rule
    /// <see cref="Envelope.Validate"/> states, and it matters more here: the
    /// person reading it did not write the document.
    /// </remarks>
    public static string? Validate(OfferedConfiguration offered)
    {
        ArgumentNullException.ThrowIfNull(offered);

        foreach (var setting in offered.Settings)
        {
            if (!OfferableKeys.All.Contains(setting.Key, StringComparer.Ordinal))
            {
                return $"'{setting.Key}' is not a setting a control plane may offer. It may "
                     + $"offer: {string.Join(", ", OfferableKeys.All)}. Everything else is "
                     + "either an instruction wearing configuration's clothes, or the key "
                     + "that decides whether offers are accepted at all.";
            }

            if (string.IsNullOrWhiteSpace(setting.Value))
            {
                return $"'{setting.Key}' is offered with a blank value. Blank is not the same "
                     + "as unset, and an offer that clears a setting by accident is one "
                     + "nobody reviewed.";
            }
        }

        return string.IsNullOrWhiteSpace(offered.Version)
            ? "An offer names no version, so accepting it could not be recorded against "
            + "anything and the same offer would arrive again for ever."
            : null;
    }
}
