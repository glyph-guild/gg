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
    public static IReadOnlyList<string> Columns { get; } = ["repository", "what enter does"];

    /// <summary>
    /// What answering on this row will actually do.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The action, not the state.</b> This column said "here" and "missing
    /// here" - the standing, which is true and leaves a person to work out what
    /// pressing enter costs them. The interesting fact is whether they are
    /// about to be asked for a token.
    /// </para>
    /// <para>
    /// <b>Forwarding is already what happens and was invisible.</b>
    /// <c>SendACredential.SecretFor</c> prefers this machine's copy - <i>"the
    /// ordinary case is somebody who has already run gg credential add"</i> -
    /// so a held credential reaches the runner with nobody typing. A capability
    /// nothing announces is one nobody uses.
    /// </para>
    /// <para>
    /// <b>And none-registered names its remedy rather than its state.</b> That
    /// is the door a flight is actually refused at: a runner holding the secret
    /// is half of it, the control plane holding a reference is the other half,
    /// and sending from here creates no reference at all.
    /// </para>
    /// </remarks>
    private static string Does(string standing) => standing switch
    {
        Gg.Client.CredentialStanding.Here => "forward the one held here",
        Gg.Client.CredentialStanding.MissingHere => "ask you to paste one",
        Gg.Client.CredentialStanding.NoneRegistered => "register one first: gg credential add",
        Gg.Client.CredentialStanding.NotNeeded => "nothing - it authenticates to nothing",
        _ => "not known - it will ask, and pasting is safe",
    };

    /// <summary>
    /// How near the top a standing puts a row.
    /// </summary>
    /// <remarks>
    /// <b>Every standing names a different remedy, and only two of them are
    /// this key's.</b> Missing here and none registered both mean somebody has
    /// to send one, so they are what the cursor should open among. Unknown
    /// follows, because nothing said is not the same as nothing wrong. Held
    /// comes after - re-sending is rotation, which is real and rarer - and not
    /// needed is last, because a repository that authenticates to nothing is
    /// not pending work.
    /// </remarks>
    private static int Rank(string said) => said switch
    {
        Gg.Client.CredentialStanding.NoneRegistered => 0,
        Gg.Client.CredentialStanding.MissingHere => 1,
        Gg.Client.CredentialStanding.Here => 3,
        Gg.Client.CredentialStanding.NotNeeded => 4,
        _ => 2,
    };

    /// <summary>What the chooser offers, as the table draws it.</summary>
    /// <remarks>
    /// <b>Ordered by what is left to do, then by name.</b> A list in registry
    /// order makes somebody read past everything already done to find the row
    /// they came for - and the row they came for is the whole reason this
    /// screen has a key.
    /// </remarks>
    public static IReadOnlyList<Choice> Rows(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        var standings = state.RepositoryCredentials;

        return
        [
            .. Offered(state)
                .Select(path => (
                    Path: path,
                    Standing: standings.FirstOrDefault(s => string.Equals(
                        s.Repo, path, StringComparison.Ordinal))?.Standing ?? "unknown"))
                .OrderBy(row => Rank(row.Standing))
                .ThenBy(row => row.Path, StringComparer.Ordinal)
                .Select(row => new Choice(row.Path, Does(row.Standing))),
        ];
    }

    /// <summary>Every registered repository, by path.</summary>
    /// <remarks>
    /// <b>No synthetic row.</b> There was one - "ask me at the prompt" - and it
    /// was a fallback for a console that had not read the registry. The
    /// registry is a background read now, so the fallback was the screen
    /// declining to answer its own question.
    /// </remarks>
    public static IReadOnlyList<string> Offered(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        return
        [
            .. (state.Repositories?.Repositories ?? [])
                .Select(r => r.Path)
                .Where(p => p is { Length: > 0 }),
        ];
    }

    /// <summary>What the row under the cursor names, or null when there is none.</summary>
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

        // THE ROWS AS DRAWN, not the registry order. The cursor is an index
        // into what a person is looking at, and Rows sorts what is left to do
        // to the top - so reading Offered here would hand back whichever
        // repository happens to sit at that position in the registry instead.
        var rows = Rows(state);

        return rows.Count == 0
            ? null
            : rows[Math.Clamp(state.CredentialRepoSelected, 0, rows.Count - 1)].Path;
    }
}
