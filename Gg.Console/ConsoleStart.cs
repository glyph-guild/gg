using Gg.Client;

namespace Gg.Console;

/// <summary>
/// The console's first load, through the verbs and nothing else.
/// </summary>
/// <remarks>
/// Every failure lands in the model as a diagnosis rather than on a screen or
/// in a log, so it survives the UI being destroyed. A console that forgets why
/// it is empty is a console that looks like it is working.
/// </remarks>
public static class ConsoleStart
{
    /// <param name="principal">
    /// Whose session this console is running under.
    /// </param>
    /// <remarks>
    /// <b>Passed rather than loaded, and the guard beside this is why.</b> Every
    /// public read on <c>ConsoleData</c> returns a <c>VerbResult</c>, so what a pane
    /// shows is what <c>--json</c> would print. The principal is not a read: it is
    /// already in the stored session, and routing it through the data layer would
    /// have made it the one value the console could show and a verb could not.
    /// <para>
    /// It is here at all because a takeover is an attributed act - the console has
    /// to know whose session it is before it offers the key.
    /// </para>
    /// </remarks>
    /// <summary>
    /// One read whose failure costs one read's worth.
    /// </summary>
    /// <remarks>
    /// <b>Rule 5's third sentence: <i>failed to load</i> is not <i>empty</i>.</b>
    /// One try around the whole boot returned a bare diagnosis and NO queue, so
    /// a tenant whose credential read was refused lost the pane the console
    /// exists for - and could not tell that from having no work.
    /// <para>
    /// The caught list is NAMED and is the one the whole boot catches. A wider
    /// one here would turn a bug in the projection into a console that looks
    /// merely degraded.
    /// </para>
    /// </remarks>
    private static async Task<AppState> OwnFailureAsync(
        AppState loaded,
        string what,
        Func<CancellationToken, Task<VerbResult>> read,
        List<string> partial,
        CancellationToken cancellationToken)
    {
        try
        {
            return ConsoleProjection.Apply(loaded, await read(cancellationToken));
        }
        catch (Exception failure) when (failure is Gg.Client.NotSignedInException
                                            or Gg.Client.ProtocolTooOldException
                                            or HttpRequestException)
        {
            partial.Add($"{what} did not load: {failure.Message}");
            return loaded;
        }
    }

    /// <param name="current">
    /// What the person already has. Empty at boot; the live model on a refresh.
    /// </param>
    /// <remarks>
    /// <b>THE MODEL IS THREADED, NOT REBUILT.</b> This method is the boot AND
    /// the refresh, and it started from <c>new AppState()</c> - so every field
    /// it does not read reset to a default on every write. The browse pane
    /// closed under the person using it; the sentence saying what their
    /// keypress did was discarded by the re-read that keypress triggered; and a
    /// refresh that could not reach the control plane emptied the console.
    /// <para>
    /// The alternative was a longer list in <c>ConsoleLoop.Reloaded</c> of what
    /// must survive, which is the same defect one slice later: it grows every
    /// time anybody adds a field to <see cref="AppState"/> and forgetting one
    /// is silent. Assigning the read plane onto what is already there needs no
    /// list at all - the loader names what it read, which it has to do anyway.
    /// </para>
    /// </remarks>
    public static async Task<AppState> LoadAsync(
        ConsoleData data,
        string principal = "",
        AppState? current = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(data);

        var start = current ?? new AppState();

        try
        {
            var flights = (VerbResult.Flights)await data.ListAsync(cancellationToken);
            var runners = (VerbResult.Runners)await data.RunnersAsync(cancellationToken);

            var logs = new Dictionary<string, Gg.Contracts.FlightLog>(StringComparer.Ordinal);
            foreach (var flight in flights.Value.Flights)
            {
                if (await data.LogAsync(flight.FlightId, cancellationToken) is VerbResult.Log log)
                {
                    logs[flight.FlightId] = log.Value;
                }
            }

            // WHAT IS WAITING ON A PERSON. Without it the gate modal had the
            // evidence and not the question - no obligation id - so pressing
            // approve could not have posted anything even once the key reached the
            // shell. The list, because the selected row picks one out of it.
            //
            // FETCHED BEFORE THE QUEUE IS BUILT, and it used to be after. Six
            // lines, and they are why the pane called "flights needing me" could
            // not contain a flight that needs me: the queue was derived without
            // them, so QueueReason.AwaitingDecision was declared, rendered, and
            // produced by nothing. The console has held this answer at boot the
            // whole time and showed it only in a modal that opens on a row the
            // queue could not have.
            var gates = await data.GatesAsync(cancellationToken) is VerbResult.Gates waiting
                ? waiting.Value
                : null;

            var queue = ConsoleProjection.Queue(flights.Value, logs, runners.Value, gates);

            // THE PRINCIPAL AND THE SEED, which is what makes the takeover key do
            // anything. Before this, ConsoleStart returned a queue and nothing
            // else - AppState.TakeSeed and AppState.Principal were assigned nowhere
            // outside tests - so ConsoleLoop.Took reached "this console is not
            // configured to take flights over" on every real press, and
            // HandedBack reached its twin. Eleventh instance of a thing being
            // registered and never invoked.
            //
            // The seed for the FIRST row only, and that is deliberate: fetching one
            // per flight would be a request per row on every load, for panes nobody
            // has scrolled to. The selection moving is what fetches the next one.
            var seed = queue.Count > 0
                    && await data.SeedAsync(queue[0].FlightNumber, cancellationToken)
                        is VerbResult.Taken taken
                ? taken.Value
                : null;

            // THROUGH APPLY, WHICH IS RULE 2. Apply is the one path from a verb
            // result into the model, it already had arms for three of these
            // four, and what it lacked was a caller. Assigning them here by hand
            // would be the second projection this slice exists to prevent - one
            // layer down and harder to see.
            var loaded = ConsoleProjection.Apply(start, flights);
            loaded = ConsoleProjection.Apply(loaded, runners);

            // THE CREDENTIAL REFERENCES, and the only new request this step
            // makes. The field and the renderer both existed and nothing
            // fetched them, so the console could never show what it holds a
            // reference to. It holds no secret - kind, locator, identity,
            // scopes - which is why it may sit in a model that is dumped.
            //
            // AND ITS FAILURE IS ITS OWN. Rule 5's third sentence: `failed to
            // load` is not `empty`. One try around the whole boot returned a
            // bare diagnosis and NO queue, so a tenant whose credential read
            // was refused lost the pane the console exists for - and could not
            // tell that from having no work. What one read loses is now one
            // read's worth.
            //
            // AND WHAT THIS TENANT SHOULD KNOW, which is the other read whose
            // failure is its own. AppState.Notices is drawn above every queue,
            // present even when the queue is empty, and was assigned by nothing
            // at all - so a control plane reporting a degradation on every call
            // was reporting it to nobody. It is exactly the failure the queue
            // hides by construction: when check runs stop being written every
            // flight still runs and still leaves the queue, so "nothing needs
            // you" stays true and the pane is at its most reassuring when this
            // is worst.
            //
            // WRITTEN AS CALLS RATHER THAN METHOD GROUPS, and the reach ratchet
            // is why: `data.ListCredentialsAsync` with no parentheses is
            // invisible to anything reading for a call site, which is what a
            // person skimming this file is also doing. It fired on both of these
            // the moment they were passed as groups.
            var partial = new List<string>();

            loaded = await OwnFailureAsync(
                loaded, "credentials", ct => data.ListCredentialsAsync(ct), partial, cancellationToken);
            loaded = await OwnFailureAsync(
                loaded, "notices", ct => data.IdentityAsync(ct), partial, cancellationToken);

            // AND THE SELECTED ROW'S DETAIL, THROUGH THE REDUCER'S OWN RULE, so
            // the console opens onto content rather than onto a pane waiting for
            // a keystroke - and so a refresh re-reads under the cursor instead
            // of jumping to the top.
            //
            // This used to be two lines reading `queue[0]`, which is a second
            // copy of what Reducer.Detail does on every arrow key. Correct at
            // boot, where the cursor IS at the top, and wrong from the moment
            // step 3 made this method the refresh: the flight pane then showed
            // the first row's flight under the selected row's name.
            return Reducer.Detail(loaded with
            {
                Queue = queue,
                Gates = gates,
                Diagnosis = partial.Count == 0 ? null : string.Join("; ", partial),

                // The logs the loop above already fetched, kept rather than
                // discarded. The selection carries them into the pane.
                Logs = logs,

                // From the stored session, never typed in. A takeover is an
                // attributed act and this is who it is attributed to.
                Principal = principal,
                TakeSeed = seed,
                // No tree, and its absence is not a gap. The branch is authoritative
                // after slice seven; a local tree is a cache this console may not
                // have, and a takeover that needed one could only ever happen on the
                // machine that ran the flight.
                TakeableTree = null,
            });
        }
        catch (Exception failure) when (failure is Gg.Client.NotSignedInException
                                            or Gg.Client.ProtocolTooOldException
                                            or Gg.Client.FlightNotFoundException
                                            or HttpRequestException)
        {
            // Named exceptions only. Swallowing everything here would turn a
            // bug in the projection into an empty console with a friendly
            // message, which is the shape that hides the next defect.
            //
            // AND IT ANSWERS WITH WHAT THE PERSON HAS, which is empty at boot
            // and is the last good model on a refresh. It used to answer with
            // `new AppState()` on both, so pressing the refresh key with the
            // network down emptied the console - and did it THROUGH
            // ConsoleLoop.Reloaded's catch rather than into it, because this
            // catch fires first and returns normally.
            return start with { Diagnosis = failure.Message };
        }
    }
}
