using Gg.Client;

namespace Gg.Console;

/// <summary>
/// Reads back the document the cursor is on, as the airspace holds it.
/// </summary>
/// <remarks>
/// <para>
/// A composition-root function, like <see cref="ConsoleEstate"/> beside it.
/// </para>
/// <para>
/// <b>THE READ-BACK THAT DID NOT EXIST.</b> A work kind could be applied
/// successfully and appear in nothing: <c>gg envelope show</c> answers the
/// ROOT document, and the console's envelope modal renders the same. Somebody
/// applied one and concluded twice that it had failed, with the only evidence
/// to the contrary being a diff that reported no changes.
/// </para>
/// <para>
/// <b>Keyed on the cursor, resolved once.</b> Which document `v` means is
/// <c>AirspaceRows.Pointed</c>'s answer, and the keymap asks the same
/// function — so what is fetched cannot differ from what the key offered.
/// </para>
/// <para>
/// <b>A failure keeps the modal and says why.</b> A reading modal that opened
/// on nothing would be indistinguishable from a document with no content, and
/// one of those is a thing to go and fix.
/// </para>
/// </remarks>
public static class ConsoleDocument
{
    public static AppState Read(ConsoleData data, AppState state)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(state);

        if (AirspaceRows.Pointed(state) is not { } pointed)
        {
            // NOT AN ERROR. The cursor moved off a document between the key and
            // the read, which is a thing a person can do.
            return state with
            {
                ReadInFlight = false,
                Document = null,
                Diagnosis = "The cursor is not on a document.",
            };
        }

        try
        {
            if (data.DocumentAsync(pointed.Name).GetAwaiter().GetResult()
                is VerbResult.NamedEnvelopeShown shown)
            {
                return state with
                {
                    ReadInFlight = false,
                    Document = shown.Value,

                    // AND WHAT IT COMPOSES TO, for a work kind. Reading one
                    // without the floor it merges with is half an answer, and
                    // the other half is the pane a person was already reading
                    // and mistaking for the whole.
                    Governing = Governing(data, shown.Value, out var refused),
                    Diagnosis = refused,
                };
            }

            return state with
                {
                    ReadInFlight = false,
                    Document = null,

                    // A STRATEGY READS BACK THROUGH ITS OWN DOOR and lands in
                    // its own answer. Said rather than rendered as an empty
                    // document, because "this is a strategy" is the fact.
                    Diagnosis = $"{pointed.Name} is a {pointed.Role}, which reads back "
                              + "through gg airspace show rather than here.",
                };
        }
        catch (Exception failure) when (failure is EnvelopeRefusedException
                                            or NotSignedInException
                                            or ProtocolTooOldException
                                            or HttpRequestException)
        {
            return state with
            {
                ReadInFlight = false,
                Document = null,
                Diagnosis = failure.Message,
            };
        }
    }

    /// <summary>
    /// What a flight of this document's kind is governed by, or null.
    /// </summary>
    /// <remarks>
    /// <b>ONLY A WORK KIND HAS ONE.</b> Composition is per work kind because
    /// that is what a flight has; a narrowing or a strategy has no such
    /// question, and asking would be refused rather than answered empty.
    /// <para>
    /// A refusal is passed OUT rather than swallowed: layers that will not
    /// compose mean nothing governs that kind until somebody fixes it, which
    /// is the most important thing this pane could say.
    /// </para>
    /// </remarks>
    private static Gg.Contracts.Envelope? Governing(
        ConsoleData data, Gg.Contracts.NamedEnvelopeState document, out string? refused)
    {
        refused = null;

        if (!string.Equals(
                document.Role, Gg.Contracts.Roles.WorkKind, StringComparison.Ordinal))
        {
            return null;
        }

        try
        {
            return data.RulesInForceAsync(document.Name).GetAwaiter().GetResult()
                is VerbResult.RulesInForce rules
                ? rules.Value
                : null;
        }
        catch (Exception failure) when (failure is EnvelopeRefusedException
                                            or NotSignedInException
                                            or ProtocolTooOldException
                                            or HttpRequestException)
        {
            refused = failure.Message;
            return null;
        }
    }
}
