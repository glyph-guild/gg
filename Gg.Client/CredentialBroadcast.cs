using Gg.Contracts;

namespace Gg.Client;

/// <summary>
/// Saying who a credential is about to reach, and opening it once for all of them.
/// </summary>
/// <remarks>
/// <para>
/// <b>ADR-0037 Decision 11's <i>"very clear"</i> is about the list being reviewable</b>,
/// not about a count. A push that said "sending to 4 machines" and then sent is a push
/// nobody reviewed, so the audience is printed machine by machine, each line carrying the
/// locator that machine receives.
/// </para>
/// <para>
/// <b>And one passphrase for the whole audience</b> (rule 3), which falls out of opening
/// once rather than from anything clever: <i>"a broadcast that asked per machine would
/// teach people to script it, and a scripted passphrase is a stored passphrase."</i>
/// </para>
/// </remarks>
public static class CredentialBroadcast
{
    /// <summary>
    /// Says who the credential is about to reach, one line per machine, before any of it
    /// moves.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>It is handed rows and a locator, and no envelope.</b> Describing who will
    /// receive a credential needs nothing that could print one — a method taking a
    /// <c>SealedCredential</c> is a method that could.
    /// </para>
    /// <para>
    /// <b>An empty audience says so.</b> A broadcast that reached nobody and said nothing
    /// is indistinguishable from one that worked, and the usual cause — no profile
    /// declares this locator — is something a person can fix.
    /// </para>
    /// </remarks>
    public static void Announce(
        string locator,
        IReadOnlyList<CredentialAudienceRow> audience,
        Action<string> saying)
    {
        ArgumentException.ThrowIfNullOrEmpty(locator);
        ArgumentNullException.ThrowIfNull(audience);
        ArgumentNullException.ThrowIfNull(saying);

        if (audience.Count == 0)
        {
            saying(
                $"nothing in this tenant declares it needs {locator}, and no machine has reported "
              + "it cannot resolve one, so there is nobody to send it to. "
              + "`gg airspace pull` shows what the fleet profiles declare; `--to <machine>` sends "
              + "it to one machine regardless.");
            return;
        }

        foreach (var row in audience)
        {
            // WHY IT IS IN THE LIST, because the two reasons mean different things to
            // whoever is reading: a declaration is somebody's intent, a report is a
            // machine saying it is currently broken.
            var why = (row.Declared, row.Reported) switch
            {
                (true, true) => "declared, and it has reported it cannot resolve one",
                (true, false) => "declared",
                (false, true) => "NOT declared, but it has reported it cannot resolve one",
                _ => "through its host",
            };

            saying(row.Reachable
                ? $"  {row.Label} <- {row.Locator} ({why})"

                // NOT REACHED, AND NOT HELD. The words "queued" and "pending" are avoided
                // deliberately: nothing stores a credential in between, so either of them
                // would say gg is keeping it for later.
                : row.Through is { Length: > 0 } host
                    ? $"  {row.Label} - not reached: it is a member of {host}, which has no profile "
                    + $"of its own. Push {row.Locator} to {host} and it passes it on."
                    : $"  {row.Label} - not reached: it is offline. {row.Locator} is not held for "
                    + "it; send again when it is back.");
        }
    }

    /// <summary>
    /// Opens the credential once, for every machine in the audience.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>ONCE, OUTSIDE THE LOOP, which is the whole of rule 3.</b> The shape that gets
    /// this wrong resolves the opener inside the per-recipient loop: it looks correct,
    /// works for one recipient, and asks N times for N.
    /// </para>
    /// <para>
    /// <b>Nothing is asked for when there is nobody to send to.</b> A passphrase read and
    /// discarded is one typed for nothing, and a person who typed it would reasonably
    /// assume something moved. An audience of only unreachable machines is the same case:
    /// the list is still worth printing, and there is nothing to unlock a key for.
    /// </para>
    /// <para>
    /// <b>The opener is reusable by construction.</b> It performs an agreement per call
    /// and holds no per-recipient state, so one unlocked key rewraps for each machine —
    /// which is why a prompt count alone is not enough to prove this and the second test
    /// uses it three times.
    /// </para>
    /// </remarks>
    public static SendACredential.ToSend? Opened(
        FileCredentialStore store,
        MachineKey key,
        string locator,
        IReadOnlyList<CredentialAudienceRow> audience,
        ISecretPrompt prompt,
        Action<string>? saying,
        string? personKeyPath = null)
    {
        ArgumentNullException.ThrowIfNull(audience);

        return audience.Any(row => row.Reachable)
            ? SendACredential.EnvelopeFor(
                store, key, locator, prompt, saying, personKeyPath: personKeyPath)
            : null;
    }
}
