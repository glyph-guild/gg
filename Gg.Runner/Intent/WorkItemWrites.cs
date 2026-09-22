using Gg.Contracts;
using Gg.Local;

namespace Gg.Runner.Intent;

/// <summary>
/// One change that was admitted, and what performing it produced.
/// </summary>
/// <param name="Operation">
/// Which of the operations was performed, as the contract spells it.
/// </param>
/// <param name="Target">
/// The item it happened to. <b>Always present here, even for a create</b>,
/// because the whole point of reporting a create is the id the tracker issued -
/// a create that reported nothing would leave an item nothing can find again.
/// </param>
/// <param name="Url">Where a person can look at it, or null where the tracker gave none.</param>
/// <param name="AlreadyDone">
/// Whether this was found rather than made.
/// </param>
/// <remarks>
/// <b><see cref="AlreadyDone"/> is a member rather than a silence.</b> A retry
/// that finds the earlier change and one that makes a new one both end with the
/// tracker in the right state, and only one of them is worth a person knowing
/// about - a run reporting every write as new, on a retry, is a run whose report
/// of what it did is a guess.
/// </remarks>
public sealed record WorkItemWrite(
    string Operation,
    string Target,
    string? Url,
    bool AlreadyDone);

/// <summary>
/// Where admitted changes to work items are performed.
/// </summary>
/// <remarks>
/// <para>
/// <b>The write half of <see cref="IWorkItemSource"/>, and NOT beside it.</b>
/// The read seam lives in <c>Gg.Local</c>, which holds no project reference by
/// charter - a filesystem convention two halves of gg share, and nothing that
/// reads the wire contract. This one names <see cref="WorkItemProposal"/>, so it
/// is here for the reason <c>NominationTool.Unservable</c> is: the charter says
/// where a type that reads <c>Gg.Contracts</c> goes, and it is not there.
/// A separate interface on purpose, though. A runner configured to read a tracker and not to
/// write to one holds a source and no sink, so "no destination, no write" is
/// true at the level of which objects exist rather than at the level of a check
/// somebody could delete. That is the disposition <c>DestinationConfiguration</c>
/// already gives the git side, one system over.
/// </para>
/// <para>
/// <b>It takes what was ADMITTED, never what was proposed.</b> The filtering
/// happened at the control plane, against a menu a person wrote; an
/// implementation that took proposals and an admission and worked out the
/// intersection would be a second copy of that rule, living in the process the
/// threat model trusts least.
/// </para>
/// </remarks>
public interface IWorkItemSink
{
    /// <summary>
    /// Performs every admitted change, in order, and reports what each did.
    /// </summary>
    /// <param name="admitted">
    /// The changes admission said may be performed. Every one of them is
    /// performed and nothing else is.
    /// </param>
    /// <param name="idempotencyKey">
    /// What ties a created item back to the flight that asked for it, so a
    /// retry finds it rather than making a second.
    /// </param>
    /// <remarks>
    /// <b>Idempotent across the seam</b>, on <c>IDestinationAdapter</c>'s
    /// existing rule: the write can succeed and the report of it fail, the
    /// batch is retried, and a retry must find the earlier change rather than
    /// make a second. Setting a field twice is one field and needs nothing;
    /// creating an item twice is two items, which is what the key is for.
    /// </remarks>
    Task<IReadOnlyList<WorkItemWrite>> PerformAsync(
        IReadOnlyList<WorkItemProposal> admitted,
        string idempotencyKey,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Which trackers this runner may WRITE to, and where their apis are.
/// </summary>
/// <remarks>
/// <para>
/// <b>A second declaration, and that is the point</b> —
/// <c>DestinationConfiguration</c>'s argument one system over. Reading a
/// tracker and writing to one are different permissions on different
/// credentials, so a runner configured to read holds no sink: <i>no
/// declaration, no write</i> is true at the level of which objects exist,
/// rather than at the level of a check somebody could delete.
/// </para>
/// <para>
/// <b>Keyed by the destination id an envelope names</b>, because that is what
/// an admission comes back carrying. Keying by host would make one tracker two
/// destinations indistinguishable, which is the thing a menu on each of them
/// exists to keep apart.
/// </para>
/// <para>
/// <b>Absent entirely is ordinary rather than degraded.</b> The same binary
/// runs on a machine that triages and one that never will, and gg names no
/// tracker.
/// </para>
/// </remarks>
/// <summary>
/// One entry of <see cref="TrackerConfiguration.ApisVariable"/>, as written.
/// </summary>
/// <remarks>
/// <b>Usable exactly when <see cref="Problem"/> is null</b>, and then all three
/// of the others are present. The entry is kept either way, because "this
/// machine declares a tracker it could not build a sink for" and "this machine
/// declares no such tracker" are two different things to tell somebody and the
/// second one is what a dropped entry says.
/// </remarks>
public sealed record DeclaredSink
{
    /// <summary>The destination id, when there was one to read.</summary>
    public string? Id { get; init; }

    /// <summary>The tracker root, when there was one to read.</summary>
    public string? Host { get; init; }

    /// <summary>
    /// The credential to resolve - named or derived from the host, never a
    /// secret.
    /// </summary>
    public string? Locator { get; init; }

    /// <summary>Why nothing was built for it, or null when something was.</summary>
    public string? Problem { get; init; }
}

public static class TrackerConfiguration
{
    /// <summary>The variable naming which trackers this runner may write to.</summary>
    public const string ApisVariable = "GG_TRACKER_APIS";

    /// <summary>The sinks this environment describes, by destination id.</summary>
    /// <param name="clientFor">The client to speak to a host through.</param>
    /// <param name="apis">
    /// The declaration, or null to read <see cref="ApisVariable"/>. Passed by
    /// tests; the root reads the environment through the one reader.
    /// </param>
    /// <param name="secretFor">
    /// The credential registered for a destination, resolved on this machine.
    /// </param>
    /// <remarks>
    /// <b>A declared api with no credential THROWS.</b> The sink refuses an
    /// absent credential at construction, and this is where that refusal
    /// belongs: at start-up, in front of the person configuring the machine,
    /// rather than at the first admitted write in front of nobody.
    /// </remarks>
    public static IReadOnlyDictionary<string, IWorkItemSink> FromEnvironment(
        Func<string, HttpClient> clientFor,
        string? apis = null,
        Func<string, string?>? secretFor = null)
    {
        ArgumentNullException.ThrowIfNull(clientFor);

        var sinks = new Dictionary<string, IWorkItemSink>(StringComparer.Ordinal);

        foreach (var declaration in Declared(apis))
        {
            // SKIPPED, NOT THROWN, for rule 2's reason. This value can now arrive
            // from a profile - offered to a machine by a document applied
            // somewhere else - and the shape a NEWER contract writes is exactly
            // what an older build cannot parse. Refusing to start is the one
            // response that cannot be corrected, because the correction arrives
            // as configuration. What a machine lacks is reported by readiness
            // and by the doctor, both of which read the same parse.
            if (declaration is not { Problem: null, Id: { } id, Host: { } host,
                                     Locator: { } locator })
            {
                continue;
            }

            // DECLARED EITHER WAY, and the write is what refuses when the secret
            // is not here. See LackingWorkItemSink for why the destination keeps
            // its entry rather than being left out.
            var secret = secretFor?.Invoke(locator);

            sinks[id] = secret is { Length: > 0 }
                ? new WiqlWorkItemSink(host, secret, clientFor(host))
                : new LackingWorkItemSink(id, locator);
        }

        return sinks;
    }

    /// <summary>
    /// Every entry of <see cref="ApisVariable"/>, usable or not.
    /// </summary>
    /// <param name="apis">
    /// The declaration, or null to read <see cref="ApisVariable"/>. Passed by
    /// tests and by the root; the root reads the environment through the one
    /// reader.
    /// </param>
    /// <remarks>
    /// <para>
    /// <b>Parsed once and read twice</b>, which is <c>ServedTrackers</c>'
    /// reasoning on the read side and arrived at here for the same reason: a
    /// second parser for a line an operator wrote is a second answer to what
    /// they typed.
    /// </para>
    /// <para>
    /// <b>An entry that cannot be used comes back carrying why.</b> The runner
    /// still skips it - that is the whole of rule 2 - but a skip nothing can
    /// report is how a machine ends up declared for a tracker it never built a
    /// sink for, with no line anywhere saying so. <c>gg doctor</c> is where
    /// that reaches a person.
    /// </para>
    /// </remarks>
    public static IReadOnlyList<DeclaredSink> Declared(string? apis = null)
    {
        var declared = apis ?? Environment.GetEnvironmentVariable(ApisVariable) ?? "";
        var entries = new List<DeclaredSink>();

        foreach (var entry in declared.Split(
                     ',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var split = entry.IndexOf('=', StringComparison.Ordinal);

            if (split <= 0 || split == entry.Length - 1)
            {
                entries.Add(new DeclaredSink
                {
                    Problem = $"'{entry}' in {ApisVariable} is not 'destination=api'. Each entry "
                            + "names a destination id an admitted change can land at and the "
                            + "tracker to write it to, e.g. "
                            + "'my-board=https://tracker.example/acme'.",
                });
                continue;
            }

            var id = entry[..split];
            var rest = entry[(split + 1)..];

            // `destination=host` OR `destination=host|locator`, which is the
            // shape GG_INTENT_READERS' served entries already use - a host, then
            // optionally the credential to speak to it with. A machine that
            // already holds a PAT under a name of its own points at that name
            // rather than keeping a second copy under the one derived below.
            var bar = rest.IndexOf('|', StringComparison.Ordinal);
            var host = (bar < 0 ? rest : rest[..bar]).Trim();
            var named = bar < 0 ? null : rest[(bar + 1)..].Trim();

            if (host.Length == 0)
            {
                entries.Add(new DeclaredSink
                {
                    Id = id,
                    Problem = $"'{id}' in {ApisVariable} declares no tracker to write to, so "
                            + "nothing was built for it. Name the tracker root after the '=', "
                            + "or remove the entry.",
                });
                continue;
            }

            // THE CREDENTIAL BELONGS TO THE TRACKER, not to the name of a
            // landing place. This asked the store for the DESTINATION ID, so two
            // destinations aiming at one tracker project each needed their own
            // copy of the same secret under different names - and a runner
            // already holding that tracker's credential was refused an admitted
            // write because its locator did not match the destination id the
            // envelope happened to use. A destination id says where work lands,
            // which is not who may change it.
            var locator = named is { Length: > 0 } ? named : LocatorFor(host);

            // THE CONTRACT DECIDES WHETHER IT IS ONE, and this asks rather than
            // assuming. A locator the store would refuse is one PathFor throws
            // on, so a sink built around it would fail at the first admitted
            // write - which is what this skip avoids without taking the runner
            // down with it.
            if (Gg.Contracts.CredentialLocator.Validate(locator) is { } wrong)
            {
                entries.Add(new DeclaredSink
                {
                    Id = id,
                    Host = host,
                    Problem = $"'{id}' in {ApisVariable} resolves to a credential name this "
                            + $"machine cannot hold, so nothing was built for it: {wrong}",
                });
                continue;
            }

            entries.Add(new DeclaredSink { Id = id, Host = host, Locator = locator });
        }

        return entries;
    }

    /// <summary>The credential locator a tracker host resolves to.</summary>
    /// <remarks>
    /// <para>
    /// <b>The scheme is dropped and the rest is reduced to the locator
    /// charset</b>, so <c>https://Tracker.Example/Acme/Project</c> asks for
    /// <c>tracker.example/acme/project</c>. One project is one credential
    /// however many destinations aim at it, which is the whole point of
    /// deriving this from the host rather than from a destination id.
    /// </para>
    /// <para>
    /// <b>Lowercased, on <c>CredentialLocator.ForRepo</c>'s reasoning</b>: the
    /// same tracker spelled two ways has to be one credential rather than two.
    /// Reduced rather than refused, because a host legitimately carries
    /// characters a locator may not - the port colon most of all - and a
    /// deployment should not have to rename its tracker to have a credential.
    /// </para>
    /// </remarks>
    private static string LocatorFor(string host)
    {
        var scheme = host.IndexOf("://", StringComparison.Ordinal);
        var authority = scheme < 0 ? host : host[(scheme + 3)..];

        var reduced = new string([.. authority.ToLowerInvariant()
            .Select(c => char.IsAsciiDigit(c) || (c >= 'a' && c <= 'z')
                      || c is '.' or '-' or '_' or '/'
                ? c
                : '-')]);

        // A trailing slash would make two spellings of one tracker into two
        // locators, which is the thing this exists to prevent.
        var body = reduced.Trim('/', '-');

        // AND THE PREFIX, because a locator without one is not a locator -
        // `local:` is the only kind this platform has, and PathFor reads the
        // body after it. Deriving the body and forgetting the prefix produced a
        // string the contract refuses, which the test double could not catch
        // because it only records what it is handed.
        return body.Length == 0 ? "" : Gg.Contracts.CredentialLocator.LocalPrefix + body;
    }
}
