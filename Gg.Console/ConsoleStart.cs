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
    public static async Task<AppState> LoadAsync(
        ConsoleData data, string principal = "", CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(data);

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
            var loaded = ConsoleProjection.Apply(new AppState(), flights);
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
            string? partial = null;
            try
            {
                if (await data.ListCredentialsAsync(cancellationToken) is VerbResult.Credentials held)
                {
                    loaded = ConsoleProjection.Apply(loaded, held);
                }
            }
            catch (Exception failure) when (failure is Gg.Client.NotSignedInException
                                                or Gg.Client.ProtocolTooOldException
                                                or HttpRequestException)
            {
                // NAMED, and the same list the whole boot catches. A wider one
                // here would turn a bug in the projection into a console that
                // looks merely degraded.
                partial = "credentials did not load: " + failure.Message;
            }

            return loaded with
            {
                Queue = queue,
                Gates = gates,
                Diagnosis = partial,

                // The logs the loop above already fetched, kept rather than
                // discarded. The selection carries them into the pane.
                Logs = logs,

                // AND THE FIRST ROW'S DETAIL, so the console opens onto content
                // rather than onto a pane waiting for a keystroke. The reducer
                // does the same on every move, out of the same two collections.
                Flight = queue.Count > 0
                    ? flights.Value.Flights.FirstOrDefault(f => string.Equals(
                        f.FlightId, queue[0].FlightId, StringComparison.Ordinal))
                    : null,
                FlightLog = queue.Count > 0 && logs.TryGetValue(queue[0].FlightId, out var first)
                    ? first
                    : null,
                // From the stored session, never typed in. A takeover is an
                // attributed act and this is who it is attributed to.
                Principal = principal,
                TakeSeed = seed,
                // No tree, and its absence is not a gap. The branch is authoritative
                // after slice seven; a local tree is a cache this console may not
                // have, and a takeover that needed one could only ever happen on the
                // machine that ran the flight.
                TakeableTree = null,
            };
        }
        catch (Exception failure) when (failure is Gg.Client.NotSignedInException
                                            or Gg.Client.ProtocolTooOldException
                                            or Gg.Client.FlightNotFoundException
                                            or HttpRequestException)
        {
            // Named exceptions only. Swallowing everything here would turn a
            // bug in the projection into an empty console with a friendly
            // message, which is the shape that hides the next defect.
            return new AppState { Diagnosis = failure.Message };
        }
    }
}
