namespace Gg.Console;

/// <summary>
/// Reading the broadcast review: which credential it is about, and whether the
/// passphrase may be asked for yet.
/// </summary>
/// <remarks>
/// <para>
/// <b>Two questions the reducer and the view both ask</b>, answered in one place so they
/// cannot come to disagree about which stage a review is in — the shape of hazard this
/// codebase keeps naming.
/// </para>
/// <para>
/// <b>It reads state and nothing else.</b> No store, no client, no delegate: a review is
/// rendered, and anything a render path can reach is something a later change can make it
/// resolve a credential through.
/// </para>
/// </remarks>
public static class AudienceReview
{
    /// <summary>
    /// The credential under the cursor on the credentials tab, or null when there is none.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Under the CURSOR rather than typed.</b> The CLI has to be told a locator because
    /// it cannot see a screen; asking somebody to retype what they are looking at is the
    /// thing a pane exists to avoid.
    /// </para>
    /// <para>
    /// <b>Through <see cref="Rows.Credentials"/>, not through the credential list.</b> The
    /// pane prepends and orders its rows, so the cursor is an index into what is DRAWN —
    /// reading the model's list directly is how a selection lands one row off, which this
    /// console has already been bitten by once.
    /// </para>
    /// </remarks>
    public static string? Chosen(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        var rows = Rows.Credentials(state);

        return state.CredentialsSelected >= 0 && state.CredentialsSelected < rows.Count
            ? rows[state.CredentialsSelected].Credential
            : null;
    }

    /// <summary>Whether the passphrase field is on screen.</summary>
    /// <remarks>
    /// <para>
    /// <b>Only after the list has been read</b>, which is the owner's call: a field already
    /// focused invites typing before reading, and "I did not mean that audience" should be
    /// a cheap mistake.
    /// </para>
    /// <para>
    /// <b>And only when somebody can actually receive it.</b> A passphrase read for a push
    /// that is not going to happen reads, to whoever typed it, as something having moved —
    /// so an audience of nothing but pool members and offline machines never gets that far,
    /// and the list stays up saying why.
    /// </para>
    /// </remarks>
    public static bool AsksForThePassphrase(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        // THE MODE IS NOT CHECKED HERE, DELIBERATELY. It is a separate dimension of the
        // keymap's context and the binding is already scoped by it, so re-checking it
        // would make this answer two questions - and the derivation that feeds the
        // context has to be able to read this flag off a model in any mode, which is
        // what TheSignInModalReadsTests holds. What it DOES check is that somebody can
        // receive the credential, because a passphrase read for a push that is not
        // going to happen reads, to whoever typed it, as something having moved.
        return state.AudienceAsked && state.Audience.Any(row => row.Reachable);
    }

    /// <summary>How many machines will actually receive the credential.</summary>
    /// <remarks>
    /// <b>Recipients, not rows.</b> A count of rows would tell somebody two machines are
    /// getting it when one of them is a pool member nothing can reach.
    /// </remarks>
    public static int Recipients(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        return state.Audience.Count(row => row.Reachable);
    }
}
