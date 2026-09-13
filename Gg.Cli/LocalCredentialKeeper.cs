using Gg.Client;
using Gg.Runner;

namespace Gg.Cli;

/// <summary>
/// The one adapter joining the runner's keep-a-credential port to the local
/// store.
/// </summary>
/// <remarks>
/// <para>
/// <b><see cref="LocalCredentialResolver"/>'s sibling, and here for its
/// reason.</b> <c>Gg.Runner</c> deliberately cannot reference
/// <c>Gg.Client</c> - a runner must be structurally unable to hold a
/// developer's session - and <c>Gg.Client</c> has no business knowing what a
/// runner is. So the two are joined at the top, by the binary that is already
/// both. That one reads; this one writes.
/// </para>
/// <para>
/// <b>The same store, which is the whole point of one adapter per
/// direction.</b> A credential placed over the channel and one typed at
/// <c>gg credential add</c>'s prompt land in the same file, at 0600 inside a
/// 0700 directory, and are read back by the same resolver. A second path for
/// the same secret is how a flight comes to resolve one and not the other.
/// </para>
/// <para>
/// <b>It answers rather than throws, and that is not politeness.</b> What calls
/// it is a dispatch arm on a channel <c>CLAUDE.md</c> calls hostile, and an
/// exception out of that arm is a peer that can end a runner's conversation
/// whenever it likes. A refusal is an answer; a crash is a denial of service
/// with a stack trace.
/// </para>
/// <para>
/// <b>Nothing here logs what it was given.</b> The runner rule is that the
/// secret must not appear in any log line, and a catch that reported what
/// failed to store would be the one place it did.
/// </para>
/// </remarks>
public sealed class LocalCredentialKeeper(ICredentialStore store) : IKeepACredential
{
    private readonly ICredentialStore _store = store;

    /// <summary>
    /// Somewhere to keep a credential, or null when this machine has not said
    /// it may be given one.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>ONE PLACE DECIDES, and it decides by handing over a port or not.</b>
    /// Two composition paths bring a runner up - a person's
    /// <c>gg runner up</c> and a member redeeming its nonce - and a rule
    /// written in both is a rule that drifts. The dangerous direction is a
    /// member that builds one directly, which would be a machine accepting
    /// secrets with nothing written down saying it may.
    /// </para>
    /// <para>
    /// <b>Null rather than a flag the dispatch consults.</b> A runner handed
    /// nowhere to keep a credential refuses for want of a PORT, exactly as one
    /// handed no private key is unreachable - so there is no permission check
    /// anywhere downstream to get wrong, and nothing to forget.
    /// </para>
    /// </remarks>
    public static IKeepACredential? For(Gg.Local.Configuration? file, ICredentialStore store) =>
        file?.AcceptConfigured is true ? new LocalCredentialKeeper(store) : null;

    /// <summary>
    /// The same configuration with this machine opted in.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>What a MEMBER writes at its first start, and only a member.</b> A
    /// laptop opts in by opening its own file; a member has none an operator
    /// can reach, so it opts itself in on the authority of the single-use nonce
    /// its tenant minted to create it. The line is written rather than assumed
    /// so that <c>gg config show</c> inside the container says so - a
    /// permission nobody can see is a permission somebody forgot they granted.
    /// </para>
    /// <para>
    /// <b>Merged, never replaced.</b> <c>ConfigurationFile.Write</c> replaces
    /// the whole document, so writing a fresh one here would silently drop
    /// whatever the image had baked in - the sort of loss nobody notices until
    /// a flight cannot reach a forge.
    /// </para>
    /// </remarks>
    public static Gg.Local.Configuration Opened(Gg.Local.Configuration? existing) =>
        (existing ?? new Gg.Local.Configuration()) with { AcceptConfigured = true };

    public bool Keep(string locator, string secret)
    {
        // THE DISPATCH ALREADY VALIDATED THE LOCATOR and the store validates it
        // again, which is not redundant: they are two machines' rules about the
        // same string, and the one that owns the disk gets the last word.
        try
        {
            _store.Write(locator, secret);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (IOException)
        {
            // A FULL DISK OR A READ-ONLY MOUNT is a real answer to "did it
            // land", and the console says `written: false` rather than losing
            // the conversation the person is having.
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }
}
