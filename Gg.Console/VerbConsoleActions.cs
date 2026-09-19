using Gg.Client;
using Gg.Contracts;

namespace Gg.Console;

/// <summary>
/// The shell's writes, performed through the same verbs the CLI uses.
/// </summary>
/// <remarks>
/// <para>
/// <b>One writer per transition.</b> This reaches <c>ConsoleData</c>, which reaches
/// <c>FlightCommands</c> - the one place a decision is posted, which
/// <c>GateModalTests</c> holds structurally. A console that posted its own would be
/// a second path to one state transition, and nothing would say which was right
/// when the two disagreed.
/// </para>
/// <para>
/// <b>Sync over async, deliberately and only here.</b> The shell runs between UI
/// lifetimes and is synchronous; the verbs are not. Bridging at this edge is what
/// <c>ConsoleStart.LoadAsync(...).GetAwaiter().GetResult()</c> already does, and
/// keeping <c>IConsoleActions</c> a port is what lets the loop be tested with no
/// HTTP at all.
/// </para>
/// </remarks>
public sealed class VerbConsoleActions(
    ConsoleData data, ISecretPrompt prompt, IClipboard? clipboard = null) : IConsoleActions
{
    private readonly ConsoleData _data = data;

    /// <summary>
    /// Asks for the things a person types. Runs in the shell, never in a modal.
    /// </summary>
    /// <remarks>
    /// The console's old objection to registering a credential was that a prompt
    /// inside a Terminal.Gui modal is a keyboard path with its own escape-hatch
    /// rules. It is; this is not one. By the time this is called the UI session is
    /// over and the terminal belongs to this process alone, which is the same
    /// arrangement $EDITOR has always had.
    /// </remarks>
    private readonly ISecretPrompt _prompt = prompt;

    private readonly IClipboard _clipboard = clipboard ?? new SystemClipboard();

    /// <summary>
    /// Posts the answer and says what was sent.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>What comes back is not interpreted.</b> The sentence returned describes the
    /// POST; what the gate became is the control plane's answer and arrives on the
    /// next load. A console that reported the outcome it hoped for would be deciding.
    /// </para>
    /// <para>
    /// <b>The observations are true here in a way they are not on the command
    /// line.</b> <c>gg decide GG-42 …</c> reports <c>evidenceRendered: false</c>
    /// honestly - nothing was shown, so nothing was read. The gate modal DID render
    /// the evidence before the key was pressed, and this is the one caller entitled
    /// to say so.
    /// </para>
    /// <para>
    /// <b><c>SecondsToDecide</c> stays null, and that is a stated limit rather than a
    /// convenient zero.</b> The number wants the instant the evidence was rendered,
    /// which lives in the reducer - and the reducer touches no clock, on purpose,
    /// because that is what makes every interaction discipline in this console
    /// testable. Putting a clock there to win a field nobody reads yet would be a
    /// bad trade; the honest answer is that this caller cannot measure it.
    /// </para>
    /// </remarks>
    /// <summary>
    /// Submit and look once: the verb's patience, with none of the waiting.
    /// </summary>
    /// <remarks>
    /// <b>The waiting moved to the watcher.</b> The verb's default watches the
    /// gate list for up to thirty seconds, which is right for a script that
    /// needs the answer and wrong between two UI sessions, where it is thirty
    /// seconds of a screen held with nothing on it.
    /// </remarks>
    private static readonly ObservationBound AtOnce = new()
    {
        Wait = TimeSpan.Zero,
        FirstDelay = TimeSpan.Zero,
        MaxDelay = TimeSpan.Zero,
    };

    public Receipt Decide(string flight, string obligation, bool approved, string? reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(flight);
        ArgumentException.ThrowIfNullOrWhiteSpace(obligation);

        // WHAT THE PERSON ANSWERED, not what the obligation becomes. The first draft
        // of this said ObligationOutcomes.Satisfied and a structural guard refused
        // it - correctly, and for a better reason than the token: satisfied and
        // violated are what the CONTROL PLANE records, and a client that named them
        // would be deciding what "approved" means. `gg decide` passes the word the
        // person typed, and so does this.
        var outcome = approved ? DecisionOutcomes.Approved : DecisionOutcomes.Rejected;

        try
        {
            var decided = _data.DecideAsync(
                flight, obligation, outcome,
                new DecisionObservations
                {
                    Interactive = true,
                    EvidenceRendered = true,
                    SecondsToDecide = null,
                },
                reason,
                bound: AtOnce).GetAwaiter().GetResult();

            // A REFUSAL IS STILL SAID AT ONCE - the door's own no, which the
            // submit returns before anything is looked at.
            if (decided is VerbResult.Decided { Value.Observation: { State: ObservationStates.Refused } refused })
            {
                return new Receipt($"{flight}: {obligation} was not answered — {refused.Because}");
            }

            // AND ANYTHING ELSE IS LOOKED FOR. The gate closes when the
            // decision's cascade reaches the receptor that closes it; whether
            // the one look already saw that or not, the watcher folds the gate
            // list and says so.
            return new Receipt(
                $"{flight}: {obligation} answered {outcome}. It leaves the queue when the gate closes.",
                new Expectation
                {
                    Kind = ExpectationKind.GateAnswered,
                    Id = flight,
                    Obligation = obligation,
                });
        }
        catch (Exception refusal) when (refusal is DecisionRefusedException
                                            or NotSignedInException
                                            or FlightNotFoundException
                                            or ProtocolTooOldException
                                            or HttpRequestException)
        {
            // NAMED EXCEPTIONS, and the model stays intact. Swallowing everything
            // here would turn a bug into a console that looks like it answered - the
            // exact shape this whole change exists to remove.
            return new Receipt($"{flight}: {obligation} was not answered — {refusal.Message}");
        }
    }

    /// <summary>Posts the answer to a nomination, and says what was sent.</summary>
    /// <remarks>
    /// <para>
    /// <b>Through the same verb <c>gg board open</c> uses</b>, for
    /// <see cref="Decide"/>'s reason one method up: two paths to one state
    /// transition is how a console's view and the control plane's record drift
    /// apart, and nothing would say which was right.
    /// </para>
    /// <para>
    /// <b>What comes back is not interpreted.</b> `open' starts an admission
    /// pass that composes the envelope again and may refuse - so this says what
    /// was SENT, and what the row became arrives on the next load. A console
    /// that reported the outcome it hoped for would be deciding.
    /// </para>
    /// </remarks>
    public Receipt AnswerNomination(string nomination, bool open, string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nomination);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        // THE CONTRACT'S OWN WORDS FOR THE TWO ENDINGS A PERSON MAY CAUSE, and
        // the verb refuses any other - the board, the world, the clock and the
        // rules own the rest.
        var outcome = open ? NominationEndings.Opened : NominationEndings.Declined;

        if (!Guid.TryParse(nomination, out var id))
        {
            // THE ROW CARRIES A STRING AND THE DOOR TAKES A GUID, so somebody
            // has to say what an unparseable one means. It cannot happen from a
            // row this console drew, which is exactly why it is worth a
            // sentence rather than an exception nobody sees.
            return new Receipt($"'{nomination}' is not a nomination this console can answer.");
        }

        try
        {
            var decided = _data.DecideNominationAsync(id, outcome, reason).GetAwaiter().GetResult();

            // THE FLIGHT AN OPENING STARTED, which the report names once the row
            // has ended. It was discarded, so the row left the board and nothing
            // took its place until the next tick.
            var started = open
                && decided is VerbResult.NominationDecided { Value.Nomination.FlightId: { } flight }
                    ? flight.ToString()
                    : null;

            return Receipt.Opened(
                $"Answered {outcome}. What it became is on the board when this refreshes.",
                started);
        }
        catch (Exception refusal) when (Expected(refusal))
        {
            return new Receipt($"Nothing was answered — {refusal.Message}");
        }
    }

    /// <summary>Opens a flight, and says what came back.</summary>
    /// <remarks>
    /// The number is not here. `gg fly` answers 202 - the flight is materialized
    /// asynchronously, so at the moment this returns nobody knows what it will be
    /// called. Saying so beats printing a blank where a name goes.
    /// </remarks>
    public string KeepBack(string allowance, double? share)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(allowance);

        try
        {
            _data.FloorAsync(allowance, share, share).GetAwaiter().GetResult();

            return share is { } kept
                ? $"{allowance} keeps {(int)Math.Round(kept * 100)}% of each window back. "
                + "Fleet work stops there; your own does not."
                : $"{allowance} keeps nothing back. Fleet work may spend all of it.";
        }
        catch (Exception refusal) when (Expected(refusal))
        {
            return refusal.Message;
        }
    }

    public Receipt Fly(string intent, IReadOnlyList<string> repositories, string? workKind)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(intent);

        try
        {
            var opened = _data.FlyAsync(intent, repositories, workKind)
                .GetAwaiter().GetResult();

            return Launched(opened);
        }
        catch (Exception refusal) when (Expected(refusal))
        {
            return new Receipt($"Nothing was opened — {refusal.Message}");
        }
    }

    public Receipt FlyTicket(
        string provider, string id, IReadOnlyList<string> repositories,
        string? workKind)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        try
        {
            var opened = _data.FlyTicketAsync(provider, id, repositories, workKind)
                .GetAwaiter().GetResult();

            return Launched(opened);
        }
        catch (Exception refusal) when (Expected(refusal))
        {
            return new Receipt($"Nothing was opened — {refusal.Message}");
        }
    }

    /// <summary>What the door answered, and the flight it named.</summary>
    /// <remarks>
    /// <b>The id crosses, not only the sentence.</b> It is the one way to ask
    /// whether the flight has appeared yet, and it used to be written into the
    /// sentence and dropped.
    /// </remarks>
    private static Receipt Launched(VerbResult opened) => opened is VerbResult.Launched launched
        ? Receipt.Opened(
            $"Opened {launched.Value.FlightId}. Its number is minted when it materializes, "
            + "and it appears here when it does.",
            launched.Value.FlightId)
        : new Receipt("The flight was accepted.");

    /// <summary>
    /// Whether this work item has flown before, and what to say if it has.
    /// </summary>
    /// <remarks>
    /// <b>A CHECK THAT CANNOT RUN IS NOT A CLEAN CHECK.</b> An unreachable
    /// control plane answers the same way a duplicate does - with a sentence -
    /// because treating "I could not ask" as "there are none" turns an outage
    /// into duplicate flights nobody meant to open. The wording says which it
    /// is, so a person answering knows what they are deciding.
    /// </remarks>
    public string? AlreadyFlown(string provider, string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        try
        {
            var flown = _data.FlownAsync(provider, id).GetAwaiter().GetResult();

            if (flown is not VerbResult.Flights listed || listed.Value.Flights.Count == 0)
            {
                return null;
            }

            var names = string.Join(", ", listed.Value.Flights
                .Select(f => f.FlightNumber is { Length: > 0 } number ? number : f.FlightId));

            return $"{provider}#{id} has already been flown: {names}.";
        }
        catch (Exception refusal) when (Expected(refusal))
        {
            return $"This console could not check whether {provider}#{id} has already been "
                 + $"flown — {refusal.Message}";
        }
    }

    /// <summary>
    /// Registers a credential for a repository somebody names.
    /// </summary>
    /// <remarks>
    /// <b>The value never passes through here.</b> The repository is a fact and is
    /// read with <c>ReadLine</c>; the identity and the secret are read by
    /// <c>CredentialCommands</c> itself, with the echo off for the one that needs it.
    /// So there is no frame in this project holding it, which is what makes this
    /// safe in a console whose model is written to disk.
    /// </remarks>
    public string AddCredential()
    {
        try
        {
            var repo = _prompt.ReadLine("Which repository is this credential for? ").Trim();

            if (repo.Length == 0)
            {
                return "Nothing was registered: no repository was named.";
            }

            // THE SCOPE, ASKED FOR. Registering read-only by fiat meant a
            // console that could never grant a runner what it needs to land
            // work, and said nothing about it.
            var asked = _prompt.ReadLine(
                $"Which scope? {string.Join(" or ", CredentialScopes.All)} "
              + $"(return for {CredentialScopes.Read}): ").Trim();

            // AN EMPTY ANSWER IS NOT A WRONG ANSWER. The narrow scope is the
            // ordinary one, and pressing return is how a person says so.
            var scope = asked.Length == 0 ? CredentialScopes.Read : asked;

            // REFUSED BY NAME rather than narrowed to read. Somebody who typed
            // `admin` and was quietly given a reading credential finds out at
            // the push, one flight later, with nothing pointing here.
            if (!CredentialScopes.All.Contains(scope, StringComparer.Ordinal))
            {
                return $"Nothing was registered: '{scope}' is not a scope gg can ask "
                     + $"for. It asks for one of: {string.Join(", ", CredentialScopes.All)}.";
            }

            var added = _data.AddAsync(repo, [scope]).GetAwaiter().GetResult();

            // THE REFERENCE, which is what crosses the wire anyway: kind, locator,
            // identity and scopes. Never the value, and there is nothing here that
            // could carry it.
            return added is VerbResult.CredentialAdded registered
                ? $"Registered {registered.Value.Reference.Locator} as "
                + $"{registered.Value.Reference.Identity}, scopes "
                + string.Join(",", registered.Value.Reference.Scopes) + "."
                : $"A credential for {repo} was registered.";
        }
        catch (Exception refusal) when (Expected(refusal))
        {
            // THE MESSAGE IS SHOWN, KEPT, DUMPED AND BUNDLED, so what it may contain
            // matters more here than anywhere else in this file. It carries the
            // refusal's own words, and the refusal never saw the value.
            return $"Nothing was registered — {refusal.Message}";
        }
    }

    /// <summary>
    /// Forgets a credential, by the repository a person knows it by.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Re-read rather than resolved from the model.</b> The console holds a
    /// credential list that may be a minute old, and removing the wrong
    /// credential because the list moved is not a mistake this may make.
    /// </para>
    /// <para>
    /// <b>Typing the repository IS the confirmation.</b> There is no modal,
    /// because there is no way to do this by accident: nothing is removed until
    /// somebody has named it, and a name that matches nothing removes nothing
    /// and says which names would have worked.
    /// </para>
    /// </remarks>
    public string ForgetCredential()
    {
        try
        {
            var repo = _prompt.ReadLine("Which repository's credential should be forgotten? ")
                .Trim();

            if (repo.Length == 0)
            {
                return "Nothing was forgotten: no repository was named.";
            }

            if (_data.ListCredentialsAsync().GetAwaiter().GetResult()
                    is not VerbResult.Credentials held)
            {
                return "Nothing was forgotten: the credential list could not be read.";
            }

            var match = held.Value.Credentials.FirstOrDefault(
                c => string.Equals(c.Repo, repo, StringComparison.Ordinal));

            if (match is null)
            {
                // WHICH NAMES WOULD HAVE WORKED, because a person who mistyped
                // and a person whose credential is already gone need different
                // next moves, and "not found" is both.
                var known = held.Value.Credentials.Count == 0
                    ? "this tenant holds none"
                    : string.Join(", ", held.Value.Credentials.Select(c => c.Repo));

                return $"Nothing was forgotten: no credential for {repo} ({known}).";
            }

            var removed = _data.RemoveCredentialAsync(match.CredentialId)
                .GetAwaiter().GetResult();

            // THE REFERENCE, never the value - and the local secret goes with
            // it, which CredentialCommands does off the reference that comes
            // back. A store cleaned on one side only is the leak this closes.
            return removed is VerbResult.CredentialRemoved gone
                ? $"Forgot {gone.Value.Reference.Locator}, which acted as "
                + $"{gone.Value.Reference.Identity}."
                : $"The credential for {repo} was forgotten.";
        }
        catch (Exception refusal) when (Expected(refusal))
        {
            return $"Nothing was forgotten — {refusal.Message}";
        }
    }

    /// <summary>
    /// Issues an invitation and puts the link where a person can get at it.
    /// </summary>
    /// <remarks>
    /// <b>WHERE, never what.</b> Whoever holds the link becomes a principal in this
    /// tenant, so it is a capability - and this returns a sentence that goes into a
    /// model which is serialized to disk under <c>GG_STATE_DUMP</c> and handed to the
    /// diagnostics bundle. <c>SeedPlacer</c> is the piece the original exemption said
    /// did not exist yet: clipboard first, a named file otherwise, never failing.
    /// </remarks>
    public string Invite()
    {
        try
        {
            var issued = _data.InviteAsync().GetAwaiter().GetResult();

            if (issued is not VerbResult.Invited invitation)
            {
                return "An invitation was issued.";
            }

            // ITS OWN FILE. Sharing the takeover seed's name would have an
            // invitation overwrite the document somebody was about to read in order
            // to pick a flight up, which is the one collision that costs work.
            var placed = SeedPlacer.Place(
                invitation.Value.InvitationUrl, _clipboard, Path.GetTempPath(),
                SeedPlacer.InvitationFile);

            return placed switch
            {
                SeedPlacement.Clipboard =>
                    "An invitation is on your clipboard. It expires "
                  + $"{invitation.Value.ExpiresAt:yyyy-MM-dd HH:mm} UTC.",
                SeedPlacement.File(var path, var why) =>
                    $"No clipboard here ({why}). The invitation is at {path}, and it expires "
                  + $"{invitation.Value.ExpiresAt:yyyy-MM-dd HH:mm} UTC.",
                _ => "An invitation was issued.",
            };
        }
        catch (Exception refusal) when (Expected(refusal))
        {
            return $"No invitation was issued — {refusal.Message}";
        }
    }

    /// <summary>
    /// The refusals a console carries on from, rather than dying on.
    /// </summary>
    /// <remarks>
    /// Named, because swallowing everything would turn a bug into a console that
    /// looks like it worked - the exact shape this whole change exists to remove.
    /// </remarks>
    private static bool Expected(Exception failure) =>
        failure is NotSignedInException
                or FlightIntentException
                or FlightNotFoundException
                or CredentialScopeException
                or DecisionRefusedException
                or ProtocolTooOldException
                or HttpRequestException;
}
