using Gg.Contracts;

namespace Gg.Client;

/// <summary>
/// The words a column may use for how a credential rests on this machine.
/// </summary>
/// <remarks>
/// <para>
/// <b>A closed vocabulary, for <see cref="CredentialStanding"/>'s reason:</b> a
/// column whose values are assembled by whoever wrote the last branch is a
/// column nobody can filter, test or render a legend for. These are rendered by
/// `gg credential list` and, next, by a pane.
/// </para>
/// <para>
/// <b>Why a word exists at all, when <c>ICredentialStore.ProtectionFor</c>
/// already answers.</b> That answers in a *sentence* — the sealed one runs to
/// about 180 characters — which is right for `gg doctor`, where it is printed
/// once, and impossible in a column printed once per credential. What must not
/// happen is a second authority: a reader deriving "sealed" by looking for the
/// word inside the sentence passes today and starts lying about whether a secret
/// is encrypted the first time somebody rewords it. So the store answers both,
/// out of one decision, and callers ask.
/// </para>
/// </remarks>
public static class CredentialResting
{
    /// <summary>Sealed to this machine's own key.</summary>
    public const string Sealed = "sealed";

    /// <summary>
    /// Here, readable, and from before this machine sealed anything.
    /// </summary>
    /// <remarks>
    /// <b>The row somebody is reading this column to find.</b> Resealing happens
    /// on read, so a credential nothing has read since the migration stays like
    /// this indefinitely — which is correct behaviour and still the thing a
    /// person wants to know.
    /// </remarks>
    public const string Plaintext = "plaintext";

    /// <summary>
    /// Kept in a vault, which this machine reads and never writes.
    /// </summary>
    /// <remarks>
    /// <b>Not "missing", and the distinction is load-bearing.</b>
    /// <c>ICredentialStore.Holds</c> answers false for every vault reference on
    /// purpose — the only way to ask a vault whether it has one is to read it —
    /// so a column built on presence reports every working vault credential as
    /// one nobody added. How it rests there is the vault's to say, not ours.
    /// </remarks>
    public const string InAVault = "in a vault";

    /// <summary>Registered for this tenant, and not on this machine.</summary>
    /// <remarks>
    /// Ordinary rather than wrong: a colleague's credential belongs on their
    /// laptop, which is the correction `gg doctor` already carries.
    /// </remarks>
    public const string NotHere = "not here";

    /// <summary>
    /// The locator is not one this machine can place, so there is nowhere to
    /// look.
    /// </summary>
    /// <remarks>
    /// <b>A diagnosis rather than a blank</b> (Article XI). A locator the local
    /// store refuses reaches <c>PathFor</c> and throws, and a list that let that
    /// out would be one that a single bad row anywhere in the tenant turns into
    /// a stack trace — with every other row fine.
    /// </remarks>
    public const string Unplaceable = "not a locator this machine can place";

    /// <summary>
    /// Nothing said how it rests, which is not the same as nothing being wrong.
    /// </summary>
    /// <remarks>
    /// <see cref="CredentialStanding.Unknown"/>'s argument, in the one list that
    /// is about secrets: a read that half-failed leaves this empty, and a caller
    /// rendering that as "here" would be this project's recurring failure.
    /// </remarks>
    public const string NotKnown = "not known";

    /// <summary>Every word, so a reader can enumerate them.</summary>
    public static IReadOnlyList<string> All { get; } =
        [Sealed, Plaintext, InAVault, NotHere, Unplaceable, NotKnown];
}

/// <summary>
/// One credential's resting shape, as this machine sees it.
/// </summary>
/// <remarks>
/// <b>Keyed by the locator, and carrying no secret.</b> This travels into a
/// console's state, onto a screen and into a state dump. The locator belongs in
/// all three — it is a reference, `gg credential list` prints it on purpose, and
/// the control plane already holds it — and nothing that could open one does.
/// </remarks>
/// <param name="Locator">The locator this is about.</param>
/// <param name="Resting">One of <see cref="CredentialResting"/>.</param>
public sealed record CredentialAtRest(string Locator, string Resting);

/// <summary>
/// Asking a store how a tenant's credentials rest here, and reading the answers
/// back.
/// </summary>
/// <remarks>
/// <para>
/// <b>By locator, never by position.</b> The registry comes from the control
/// plane and the resting shapes from this machine, so a list indexed alongside
/// another is a list that labels every row with somebody else's answer the
/// moment the two disagree about order or length — and both would still render
/// fine. <see cref="RepositoryCredentials"/> states the same rule one layer
/// over.
/// </para>
/// <para>
/// <b>It takes a delegate rather than a store</b>, which is
/// <c>CredentialStanding.For</c>'s shape. It keeps this callable from a test
/// without a filesystem, and it keeps the word <c>CredentialStore</c> out of
/// anything the console's structural scan reads.
/// </para>
/// </remarks>
public static class CredentialsAtRest
{
    /// <summary>
    /// How each of these rests, asked once per credential.
    /// </summary>
    /// <remarks>
    /// <b>Nothing here opens a credential.</b> `gg doctor`'s credentials row
    /// answers a near-identical question by calling <c>Read</c>, which decrypts
    /// every credential on the machine and reseals the plaintext ones on the way
    /// past. A list runs far more often than a doctor, and one that decrypted
    /// would pull every secret on the machine into a process whose job is to
    /// print four columns.
    /// </remarks>
    /// <param name="credentials">What the control plane holds references for.</param>
    /// <param name="restingOf">
    /// The store's answer for one locator — <c>ICredentialStore.RestingOf</c>.
    /// </param>
    public static IReadOnlyList<CredentialAtRest> For(
        IReadOnlyList<CredentialSummary> credentials,
        Func<string, string> restingOf)
    {
        ArgumentNullException.ThrowIfNull(credentials);
        ArgumentNullException.ThrowIfNull(restingOf);

        var resting = new List<CredentialAtRest>(credentials.Count);

        foreach (var credential in credentials)
        {
            var locator = credential.Reference.Locator;

            // CAUGHT PER ROW, so one unplaceable locator does not take the
            // readable ones with it. The registry is the tenant's and this
            // machine does not get to validate it, only to say what it can and
            // cannot do about each line.
            string answer;
            try
            {
                answer = restingOf(locator);
            }
            catch (ArgumentException)
            {
                answer = CredentialResting.Unplaceable;
            }

            resting.Add(new CredentialAtRest(locator, answer));
        }

        return resting;
    }

    /// <summary>
    /// What was recorded for a locator, or <see cref="CredentialResting.NotKnown"/>.
    /// </summary>
    /// <remarks>
    /// An absent entry is not good news, and never means the credential is here.
    /// </remarks>
    public static string RestingOf(IReadOnlyList<CredentialAtRest> resting, string locator)
    {
        ArgumentNullException.ThrowIfNull(resting);

        return resting.FirstOrDefault(
            r => string.Equals(r.Locator, locator, StringComparison.Ordinal))?.Resting
            ?? CredentialResting.NotKnown;
    }
}
