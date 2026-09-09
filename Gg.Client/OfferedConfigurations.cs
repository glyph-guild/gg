using Gg.Contracts;
using Gg.Local;

namespace Gg.Client;

/// <summary>What a machine did with an offer, or why it did nothing.</summary>
public sealed record OfferTaken
{
    /// <summary>The configuration to write, or null when nothing is to be written.</summary>
    public Configuration? Configuration { get; init; }

    /// <summary>Why the offer was not taken, or null when there was no refusal.</summary>
    public string? Refused { get; init; }

    /// <summary>Whether an offer is sitting here unaccepted.</summary>
    /// <remarks>
    /// <b>So the feature is not invisible until somebody happens to turn it
    /// on.</b> A machine that has not opted in still says an offer arrived,
    /// which is how an operator finds out there is one to look at.
    /// </remarks>
    public bool Waiting { get; init; }

    /// <summary>Whether taking it would actually change anything.</summary>
    public bool Changed { get; init; }
}

/// <summary>
/// Taking an offer, or refusing it — which is mostly what this does.
/// </summary>
/// <remarks>
/// <para>
/// <b>Here rather than in <c>Gg.Local</c>, because it needs both sides.</b> The
/// offer is a wire type and the file is a local document, and <c>Gg.Local</c>
/// references nothing — not even <c>Gg.Contracts</c>, because a local path is
/// not a wire type. This is the one place that can see both.
/// </para>
/// <para>
/// <b>It writes nothing.</b> It answers what the file would become; the caller
/// with the terminal free is what puts it there. The same split every write in
/// this product uses.
/// </para>
/// </remarks>
public static class OfferedConfigurations
{
    /// <summary>What this machine should do with an offer.</summary>
    /// <param name="attended">
    /// Whether a person is doing this. False is a runner applying one on its
    /// own, and a directed offer never applies that way — the whole reason
    /// those keys are offerable at all is that somebody sees what is being
    /// repointed.
    /// </param>
    public static OfferTaken Accept(
        OfferedConfiguration offered, Configuration into, bool attended = true)
    {
        ArgumentNullException.ThrowIfNull(offered);
        ArgumentNullException.ThrowIfNull(into);

        // REFUSED BEFORE THE GATE IS EVEN CONSULTED, so a malformed offer reads
        // the same on a machine that opted in and one that did not. A refusal
        // that depended on the local setting would tell a control plane which
        // machines have it on.
        if (OfferedConfiguration.Validate(offered) is { } refused)
        {
            return new OfferTaken { Refused = refused };
        }

        if (into.AcceptOffered is not true)
        {
            return new OfferTaken { Waiting = offered.Settings.Count > 0 };
        }

        // WAITING, NOT REFUSED, and the difference matters to whoever reads it.
        // A directed offer is one somebody may take; it simply may not take
        // itself. Reporting it as a refusal would send an operator looking for
        // something wrong with the document.
        if (!attended && OfferedConfiguration.NeedsAPerson(offered))
        {
            return new OfferTaken { Waiting = true };
        }

        if (!attended && into.AcceptUnattended is not true)
        {
            return new OfferTaken { Waiting = offered.Settings.Count > 0 };
        }

        var changed = into;

        foreach (var setting in offered.Settings)
        {
            var member = Configuration.Members.FirstOrDefault(
                m => string.Equals(m.Key, setting.Key, StringComparison.Ordinal));

            if (member is null)
            {
                // AN OFFERABLE KEY THE FILE CANNOT HOLD. Unreachable while the
                // two lists agree, and asserted so - but a key admission would
                // accept and nothing could apply is the silent no-op arriving
                // through the wire, so it is refused rather than skipped.
                return new OfferTaken
                {
                    Refused = $"'{setting.Key}' may be offered but this version has nowhere "
                            + "to keep it, so accepting would record a value nothing reads.",
                };
            }

            changed = member.With(changed, setting.Value);
        }

        // THE OFFER DOES NOT GET A WAY ROUND Validate. A control plane must not
        // be able to write into this file something a person could not.
        if (Configuration.Validate(changed) is { } wrong)
        {
            return new OfferTaken { Refused = wrong };
        }

        return new OfferTaken
        {
            Configuration = changed,
            Changed = changed != into,
        };
    }
}
