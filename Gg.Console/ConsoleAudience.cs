using Gg.Client;

namespace Gg.Console;

/// <summary>
/// Reading who a credential is wanted by, beside the console rather than instead of it.
/// </summary>
/// <remarks>
/// <para>
/// <b><c>ConsoleRunnerFlights</c>' shape, for its reasons.</b> The review and where a
/// credential is both used to be the shell's: the session ended, the runner list and the
/// fleet profiles were read with the screen gone, and a new session was built over the
/// answer - a blink before the question was even asked. The modal now opens at once and
/// this fills it.
/// </para>
/// <para>
/// <b>A patch, and one that checks it is still wanted.</b> A read that lands after the
/// person closed the modal, or moved to another credential, folds nothing - reopening a
/// review somebody walked away from is a send waiting for a stray keypress.
/// </para>
/// </remarks>
public static class ConsoleAudience
{
    /// <summary>What to fold, once the read has landed.</summary>
    public static Func<AppState, AppState> Patch(ConsoleData data, AppState state)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(state);

        if (state.AudienceFor is not { Length: > 0 } locator)
        {
            return current => current;
        }

        try
        {
            return Fold(locator, data.AudienceAsync(locator).GetAwaiter().GetResult(), failure: null);
        }
        catch (Exception failed)
        {
            // SAID BY THE CONSOLE, never by a throw: this runs on a task beside a console
            // somebody is using.
            return Fold(locator, result: null, failure: failed.Message);
        }
    }

    /// <summary>The fold itself, so a test can hold it without a control plane.</summary>
    public static Func<AppState, AppState> Fold(string locator, VerbResult? result, string? failure)
    {
        ArgumentException.ThrowIfNullOrEmpty(locator);

        return current =>
        {
            ArgumentNullException.ThrowIfNull(current);

            // STILL WANTED: the same credential, with its modal still open.
            if (current.Mode is not (UiMode.CredentialAudience or UiMode.CredentialWhereabouts)
                || !string.Equals(current.AudienceFor, locator, StringComparison.Ordinal))
            {
                return current;
            }

            if (result is VerbResult.CredentialAudienceFound found)
            {
                return current with
                {
                    Audience = found.Value.Machines,
                    AudienceFor = found.Value.For,
                };
            }

            // CLOSED WITH WHY, rather than left open over an empty list that would read as
            // nobody wanting it.
            return current with
            {
                Mode = UiMode.Normal,
                Audience = [],
                AudienceFor = null,
                LastCredential = failure ?? $"Who needs {locator} could not be read.",
            };
        };
    }
}
