namespace Gg.Console;

/// <summary>
/// The repositories a credential can be sent for, as the chooser lists them.
/// </summary>
/// <remarks>
/// <para>
/// <b>Its own list rather than the pane's.</b> The Repositories pane draws what
/// is registered; this draws that plus a final row for the question the prompt
/// used to ask. They run to different lengths, which is why the chooser has its
/// own cursor.
/// </para>
/// <para>
/// <b>By PATH, never by the registry key.</b> A credential reference is keyed by
/// path - <c>gg credential list</c> shows <c>JDX/agile-cortex</c> - and
/// <c>CredentialLocator.ForRepo</c> derives the locator from whatever it is
/// handed. Offering the key would mint <c>local:jdnext</c> where the reference
/// says <c>local:jdx/jdnext</c>, and the runner would resolve neither.
/// </para>
/// </remarks>
public static class CredentialRepositories
{
    /// <summary>The row that asks at the prompt, exactly as this key always did.</summary>
    /// <remarks>
    /// <b>Always last and always present.</b> A repository registered somewhere
    /// this console has not read is still one somebody may be sending a
    /// credential for; without this row the old path would become unreachable
    /// rather than merely unnecessary.
    /// </remarks>
    public const string AtThePrompt = "another - ask me at the prompt";

    /// <summary>One offered repository, and what this machine knows about it.</summary>
    /// <remarks>
    /// <b>The standing is worth a column because it changes what happens
    /// next.</b> A repository this machine already holds a secret for is one the
    /// send reuses without asking; one it does not is a paste. Saying so before
    /// the terminal goes away is the difference between expecting a prompt and
    /// being surprised by one.
    /// </remarks>
    public sealed record Choice(string Path, string Said);

    /// <summary>The columns the chooser draws, named for what they answer.</summary>
    public static IReadOnlyList<string> Columns { get; } = ["repository", "credential here"];

    /// <summary>What the chooser offers, as the table draws it.</summary>
    public static IReadOnlyList<Choice> Rows(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        var standings = state.RepositoryCredentials;

        return
        [
            .. Offered(state).Select(path => new Choice(
                path,
                path == AtThePrompt
                    ? "typed at the prompt"
                    : standings.FirstOrDefault(s => string.Equals(
                        s.Repo, path, StringComparison.Ordinal))?.Standing
                      ?? "not known")),
        ];
    }

    /// <summary>What the chooser offers, in the order it draws them.</summary>
    public static IReadOnlyList<string> Offered(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        // NULL IS "NEVER ASKED", which is the distinction AppState.Repositories
        // already draws and the reason this cannot simply render an empty list:
        // the registry is read when the Repositories pane is first shown, so a
        // person who went straight to the fleet holds none - and "no rows"
        // would tell somebody with several repositories that they have none.
        return
        [
            .. (state.Repositories?.Repositories ?? [])
                .Select(r => r.Path)
                .Where(p => p is { Length: > 0 }),
            AtThePrompt,
        ];
    }

    /// <summary>What the row under the cursor names, or null for the prompt.</summary>
    /// <remarks>
    /// <para>
    /// <b>Read by the loop, never written by the reducer.</b> Answering ends
    /// the session and the send happens out there, so a reducer that recorded
    /// the choice would be a shell command with a second, local effect - which
    /// <c>ShellCommands</c> forbids by name, because the local half happens
    /// whether or not the remote half did. The cursor is already in the model;
    /// reading it where it is used needs nothing new to be stored.
    /// </para>
    /// <para>
    /// <b>Null unless the chooser is actually open.</b> Row zero is a real
    /// repository, so a send that arrived by any other route would otherwise
    /// resolve to whichever one happens to be first - a credential placed for
    /// something nobody named.
    /// </para>
    /// </remarks>
    public static string? Chosen(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (state.Mode is not UiMode.CredentialRepositoryChoice)
        {
            return null;
        }

        var rows = Offered(state);
        var row = Math.Clamp(state.CredentialRepoSelected, 0, rows.Count - 1);

        return rows[row] == AtThePrompt ? null : rows[row];
    }
}
