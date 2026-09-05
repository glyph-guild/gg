using Gg.Console;

namespace Gg.Console.Tests;

/// <summary>
/// The doubles a <see cref="ConsoleLoop"/> test needs, in one place.
/// </summary>
/// <remarks>
/// <para>
/// <b>They were copied instead of shared, and both copies carried a bug.</b>
/// Eight files built their own session double and two built their own reload
/// double; the session ones exit on every key and the reload ones vary in
/// whether they thread. Each bug is invisible in the file that has it and
/// changes what every test in that file is actually exercising, which is the
/// argument for one copy rather than eight careful ones.
/// </para>
/// <para>
/// <b>Anything here is load-bearing for tests that did not write it</b>, so a
/// change to these types is a change to what the whole suite means. Read the
/// remarks before adjusting one to make a single file pass.
/// </para>
/// </remarks>
internal static class ConsoleDoubles
{
    /// <summary>
    /// A session that types keys, reducing its own and exiting on the shell's.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>THE CONTRACT, AND THE BUG EVERY PRIVATE COPY HAD.</b> A real UI
    /// session handles ordinary keys itself and ends only for a command whose
    /// effect lives in the loop. A double that returns every key as its EXIT
    /// never runs <see cref="Reducer.Reduce"/> at all — so nothing under test
    /// is exercised — and then the loop throws <i>"UI session exited with
    /// SelectNext, which the shell does not handle"</i>, which is the loop
    /// correctly enforcing a contract the double broke.
    /// </para>
    /// <para>
    /// <b>That failure is worse than a silent pass</b>, because it looks like a
    /// finding. Five tests went red at once and not one of them was red about
    /// the thing it tests; the temptation is to go and look at the loop.
    /// </para>
    /// </remarks>
    internal sealed class TypesKeys(params Command[] keys) : IUiSession
    {
        private int _at;

        /// <summary>Every state a session was started with, in order.</summary>
        /// <remarks>
        /// One entry per SESSION, not per key — which is the thing worth
        /// asserting on, because a session boundary is where the loop gets to
        /// act and where the model has to survive on its own.
        /// </remarks>
        internal List<AppState> Rendered { get; } = [];

        public UiOutcome Run(AppState state)
        {
            Rendered.Add(state);

            while (_at < keys.Length)
            {
                var key = keys[_at++];

                if (ShellCommands.Handled.Contains(key))
                {
                    return new UiOutcome(key, state);
                }

                state = Reducer.Reduce(state, key);
            }

            return new UiOutcome(Command.Quit, state);
        }
    }

    /// <summary>
    /// A refresh that answers with a read plane, threaded onto what it was given.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A DOUBLE THAT RETURNS A WHOLE STATE IS A BOOT, AND A BOOT LIES.</b> It
    /// passes every assertion about the queue while emptying every pane, which
    /// is precisely the shape the production one had: `reload: _ =>
    /// ConsoleStart.LoadAsync(...)` ignored its argument, so a refresh reset
    /// everything the read plane does not fetch. A test written against a boot
    /// double would have agreed with it.
    /// </para>
    /// <para>
    /// <b>So this assigns onto <c>current</c> and names only what a boot
    /// fetches.</b> The list mirrors <c>ConsoleStart.LoadAsync</c>; if that
    /// learns to fetch something new, this is the second place to change, and
    /// the fact that there IS a second place is the cost of a double at all.
    /// </para>
    /// </remarks>
    internal sealed class Reloads(AppState next)
    {
        internal int Calls { get; private set; }

        internal AppState Load(AppState current)
        {
            Calls++;

            return current with
            {
                Queue = next.Queue,
                Gates = next.Gates,
                Flights = next.Flights,
                Logs = next.Logs,
                Flight = next.Flight,
                FlightLog = next.FlightLog,
                Runners = next.Runners,
                Credentials = next.Credentials,
                TakeSeed = next.TakeSeed,
                TakeableTree = next.TakeableTree,
                Principal = next.Principal,
                Diagnosis = next.Diagnosis,
            };
        }
    }

    /// <summary>An editor that hands back whatever it was given.</summary>
    /// <remarks>
    /// For the many tests whose subject is not the editor. One that recorded or
    /// rewrote would be a different double and should say so in its own file.
    /// </remarks>
    internal sealed class NoEditor : IEditorSession
    {
        public string Edit(string initialText) => initialText;
    }
}
