using Gg.Contracts;

namespace Gg.Client;

/// <summary>
/// What kind of thing a credential's holder turned out to be.
/// </summary>
/// <remarks>
/// <b>A closed vocabulary, for <see cref="CredentialStanding"/>'s reason.</b> This
/// is rendered beside a credential, and a column whose values are assembled by
/// whoever wrote the last branch is one nobody can filter or write a legend for.
/// </remarks>
public static class CredentialHolderKinds
{
    /// <summary>A key one of this tenant's people has registered.</summary>
    public const string Person = "person";

    /// <summary>
    /// This machine's own key, which is the answer a reader wants first.
    /// </summary>
    /// <remarks>
    /// <b>It beats every other true answer.</b> A machine that also runs a runner
    /// can find its own key in its own pin file, and of the two facts
    /// — "a runner holds this" and "you can open this" — the second is the one
    /// somebody reading the screen is asking about.
    /// </remarks>
    public const string ThisMachine = "this machine";

    /// <summary>A runner key this console has pinned.</summary>
    /// <remarks>
    /// <b>The second holder of every pushed credential.</b> A push rewraps to the
    /// recipient's identity key, which the console pinned when it first reached
    /// that machine — so without this the holder list after a successful push
    /// reads "you, and somebody".
    /// </remarks>
    public const string Runner = "runner";

    /// <summary>
    /// A real holder gg cannot put a name to.
    /// </summary>
    /// <remarks>
    /// <b>Ordinary rather than a fault</b>, and never a reason to drop the row: a
    /// colleague's runner this console has not pinned, a key registered in a
    /// tenant this machine has not listed, a machine since retired. Each can read
    /// the secret, and a list that hid them would answer "who can open this" with
    /// a number that is wrong in the dangerous direction.
    /// </remarks>
    public const string Unknown = "unknown";

    /// <summary>Every kind, so a reader can enumerate them.</summary>
    public static IReadOnlyList<string> All { get; } = [Person, ThisMachine, Runner, Unknown];
}

/// <summary>
/// One holder of a sealed credential, as a person reads it.
/// </summary>
/// <remarks>
/// <b>A fingerprint and a name, never the key.</b> The key is public and so safe
/// to carry, and it is 120 characters of base64 that mean nothing at a glance —
/// so the fingerprint is what reaches a screen and a state dump. It is derived
/// here from the key rather than copied from whatever the control plane stored
/// beside it.
/// </remarks>
/// <param name="Fingerprint">The short name for the key, derived by the contract.</param>
/// <param name="Named">Whose or which it is, or null when gg cannot say.</param>
/// <param name="Kind">One of <see cref="CredentialHolderKinds"/>.</param>
/// <param name="RetiredAt">
/// When the key stopped being one to seal to, or null while it still is. A
/// credential sealed to a retired key is still sealed to it.
/// </param>
public sealed record CredentialHolder(
    string Fingerprint,
    string? Named,
    string Kind,
    DateTimeOffset? RetiredAt);

/// <summary>
/// Working out who can open a sealed credential, without opening it.
/// </summary>
/// <remarks>
/// <para>
/// <b>Public keys only, in and out.</b> A holder is a public key — the contract
/// says a refusal may name the holders an envelope is for, because saying so gives
/// nothing away — so working out who can read a credential never means reading it.
/// </para>
/// <para>
/// <b>Matched on the key, never on a fingerprint</b> (slice sixty, rule 5).
/// <see cref="PrincipalKeyFingerprint.Of"/> is derived once on the contract for a
/// stated reason, and matching on a value this side computed would make a second
/// derivation decide who may read a secret.
/// </para>
/// </remarks>
public static class CredentialHolders
{
    /// <summary>
    /// Every holder, named where gg can and said where it cannot.
    /// </summary>
    /// <remarks>
    /// <b>In the envelope's own order.</b> It is the only order both ends agree
    /// on, and it is stable — sorting by name would gather every unknown holder
    /// into one clump whose membership shifts as keys get registered, moving rows
    /// under somebody between refreshes.
    /// </remarks>
    /// <param name="holders">The envelope's holder public keys, in its order.</param>
    /// <param name="keys">What this tenant's people have registered.</param>
    /// <param name="thisMachine">This machine's own public key, when it has one.</param>
    /// <param name="pinned">The runner keys this console has pinned.</param>
    public static IReadOnlyList<CredentialHolder> Of(
        IReadOnlyList<string> holders,
        IReadOnlyList<PrincipalKeySummary> keys,
        string? thisMachine,
        IReadOnlyList<PinnedKey> pinned)
    {
        ArgumentNullException.ThrowIfNull(holders);
        ArgumentNullException.ThrowIfNull(keys);
        ArgumentNullException.ThrowIfNull(pinned);

        var named = new List<CredentialHolder>(holders.Count);

        foreach (var holder in holders)
        {
            // THIS MACHINE FIRST, because it is the answer a reader wants and it
            // beats a pin of the same key.
            if (thisMachine is { Length: > 0 } mine
                && string.Equals(holder, mine, StringComparison.Ordinal))
            {
                named.Add(new CredentialHolder(
                    Fingerprint: Short(holder),
                    Named: null,
                    Kind: CredentialHolderKinds.ThisMachine,
                    RetiredAt: RetirementOf(keys, holder)));
                continue;
            }

            if (keys.FirstOrDefault(
                    k => string.Equals(k.PublicKey, holder, StringComparison.Ordinal))
                is { } person)
            {
                named.Add(new CredentialHolder(
                    // DERIVED HERE rather than taken from person.Fingerprint: the
                    // key is the truth and the stored fingerprint is a reading of
                    // it, so showing a copy would let a wrong one through.
                    Fingerprint: Short(holder),
                    Named: person.Principal,
                    Kind: CredentialHolderKinds.Person,
                    RetiredAt: person.RetiredAt));
                continue;
            }

            if (pinned.FirstOrDefault(
                    p => string.Equals(p.PublicKey, holder, StringComparison.Ordinal))
                is { } runner)
            {
                named.Add(new CredentialHolder(
                    Fingerprint: Short(holder),
                    Named: runner.RunnerId,
                    Kind: CredentialHolderKinds.Runner,
                    RetiredAt: null));
                continue;
            }

            // SAID, NOT DROPPED. No name to give, and inventing one would read as
            // a fact gg had established.
            named.Add(new CredentialHolder(
                Fingerprint: Short(holder),
                Named: null,
                Kind: CredentialHolderKinds.Unknown,
                RetiredAt: null));
        }

        return named;
    }

    /// <summary>
    /// Whether a key this machine holds has also been retired centrally.
    /// </summary>
    /// <remarks>
    /// <b>Both facts are true and a reader needs both.</b> This machine can open
    /// the credential, and the key it opens with may be one nobody should seal to
    /// any more. Which key the control plane considers current has nothing to do
    /// with what is on this disk.
    /// </remarks>
    private static DateTimeOffset? RetirementOf(
        IReadOnlyList<PrincipalKeySummary> keys, string holder) =>
        keys.FirstOrDefault(k => string.Equals(k.PublicKey, holder, StringComparison.Ordinal))
            ?.RetiredAt;

    /// <summary>
    /// The fingerprint for a holder, or a readable stand-in when it is not a key.
    /// </summary>
    /// <remarks>
    /// <b>An envelope is bytes from another machine.</b> A holder that is not
    /// base64 reaches the contract's derivation and throws, and a list that let
    /// that out would be one a single damaged envelope turns into a stack trace,
    /// with every other holder perfectly readable. Article XI: say what is wrong
    /// rather than evaluate to nothing.
    /// </remarks>
    private static string Short(string holder)
    {
        try
        {
            return PrincipalKeyFingerprint.Of(holder);
        }
        catch (ArgumentException)
        {
            return "not a key this gg can read";
        }
        catch (FormatException)
        {
            return "not a key this gg can read";
        }
    }
}
