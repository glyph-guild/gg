using Gg.Client;

namespace Gg.Console;

/// <summary>
/// A secret typed inside a UI session, carried to the shell act that needs it.
/// </summary>
/// <remarks>
/// <para>
/// <b>WHY THIS EXISTS, and it is the only secret in this project that does.</b> Every
/// other credential act reads what a person types in the SHELL, with the UI torn down —
/// <c>ConsoleSecretPrompt</c> and the argument beside it. The broadcast is different only
/// because the owner asked for the passphrase field to sit under the audience it unlocks:
/// reading the list and then being thrown to a bare terminal to type is two screens for
/// one decision.
/// </para>
/// <para>
/// <b>It is NOT on the model, which is the whole point.</b> <c>AppState</c> is serialized
/// to JSON, written under <c>GG_STATE_DUMP</c> and fed to the diagnostics bundle, so a
/// passphrase on it would be a passphrase in a support attachment. This is the
/// <i>"non-serializable handle on a controller outside the store"</i> the console's own
/// rule provides for, and <c>APassphraseTypedInTheConsoleIsNotInTheDumpTests</c> holds the
/// model's shape against ever gaining one.
/// </para>
/// <para>
/// <b>Taken, not read.</b> <see cref="ReadSecret"/> hands the value over and forgets it in
/// the same call, so a second act cannot pick up what the first one was given — and an
/// abandoned review leaves nothing behind for the next keypress to use.
/// </para>
/// <para>
/// <b>It is an <see cref="ISecretPrompt"/> so that nothing downstream knows the
/// difference.</b> <c>CredentialCommands</c> asks its prompt for a passphrase exactly as
/// it does on the command line; which end of the program the answer came from is not its
/// business.
/// </para>
/// </remarks>
public sealed class HeldSecret : ISecretPrompt
{
    private string? _held;

    /// <summary>Holds what was typed, replacing anything already here.</summary>
    /// <remarks>
    /// <b>Replacing, because a stale one is worse than none.</b> A review abandoned
    /// half-typed and then reopened must not be sent with the first attempt's answer.
    /// </remarks>
    public void Hold(string typed) => _held = typed;

    /// <summary>Forgets whatever is held.</summary>
    /// <remarks>
    /// Called when a review is abandoned, so that escaping leaves nothing behind — which is
    /// half of what <c>AReviewThatIsEscapedMovesNothingTests</c> is about.
    /// </remarks>
    public void Forget() => _held = null;

    /// <summary>Whether anything is held, without taking it.</summary>
    public bool Holding => _held is not null;

    /// <summary>
    /// The secret, taken rather than read: it is gone from here once this returns.
    /// </summary>
    /// <remarks>
    /// <b>An empty string when nothing is held</b>, which the caller treats as a refusal the
    /// same way it treats somebody pressing return at a prompt. Throwing would turn "the
    /// field was never filled in" into a crash out of a keystroke.
    /// </remarks>
    public string ReadSecret(string prompt)
    {
        var held = _held ?? "";
        _held = null;
        return held;
    }

    /// <summary>
    /// Refused: a line is not a secret, and this holder exists only for the one that is.
    /// </summary>
    /// <remarks>
    /// <b>Loudly rather than returning empty.</b> Anything reaching for a LINE here has been
    /// wired to the wrong prompt, and an empty answer would make that look like a person
    /// declining to type.
    /// </remarks>
    public string ReadLine(string prompt) =>
        throw new InvalidOperationException(
            "A held secret answers one question and it is not this one. Something asked for a "
          + $"line ('{prompt}') through the holder that carries a typed passphrase to the shell; "
          + "a line belongs to the prompt that owns the terminal.");
}
