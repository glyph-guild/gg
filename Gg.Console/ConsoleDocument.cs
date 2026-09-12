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
            return data.DocumentAsync(pointed.Name).GetAwaiter().GetResult()
                is VerbResult.NamedEnvelopeShown shown
                ? state with
                {
                    ReadInFlight = false,
                    Document = shown.Value,
                    Diagnosis = null,
                }
                : state with
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
        catch (Exception refused) when (refused is EnvelopeRefusedException
                                            or NotSignedInException
                                            or ProtocolTooOldException
                                            or HttpRequestException)
        {
            return state with
            {
                ReadInFlight = false,
                Document = null,
                Diagnosis = refused.Message,
            };
        }
    }
}
