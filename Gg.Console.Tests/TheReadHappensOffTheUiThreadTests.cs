namespace Gg.Console.Tests;

/// <summary>
/// A background read does its reading in the background.
/// </summary>
/// <remarks>
/// <para>
/// <b>REPORTED FROM USE: reading the airspace freezes the whole console.</b>
/// Not the tab — <c>esc</c> and <c>q</c> do nothing either, which is what
/// distinguishes a blocked UI thread from a read that never lands.
/// </para>
/// <para>
/// <b>AND NOTHING WAS BACKGROUNDED.</b> The composition root wraps its arms in
/// <c>Task.Run</c>, but two of them answer with <c>_ =&gt; SomeRead(...)</c> — a
/// lambda that has not read anything yet. <c>Task.Run</c> evaluates the switch,
/// which merely BUILDS that lambda, and the reading happens later when
/// <c>BackgroundReads.Advance</c> invokes it — inside <c>_app.Invoke</c>, on
/// the UI thread. Every HTTP call and the git invocation underneath it run
/// where the keyboard is.
/// </para>
/// <para>
/// <b>One arm already had it right.</b> <c>ShowFlight</c> answers
/// <c>ConsoleFlightLog.Patch(data, current)</c> — CALLED inside the
/// <c>Task.Run</c>, reading there, returning a func that only folds. The other
/// two were written to the same rule and implemented the opposite way, and the
/// comment above them describes the intent rather than the code: <i>"each arm
/// answers with a PATCH rather than a model"</i>.
/// </para>
/// <para>
/// <b>The rule the deferral was protecting is kept.</b> A read that answers
/// with a whole <c>AppState</c> is <i>"a snapshot taken before the person moved
/// and applied after"</i> — so the fetching happens eagerly and the fold
/// carries only what was fetched onto whatever state is live when it lands.
/// </para>
/// </remarks>
public class TheReadHappensOffTheUiThreadTests
{
    private static string Root()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Gg.sln")))
        {
            dir = dir.Parent;
        }

        return dir!.FullName;
    }

    private static string Program() =>
        File.ReadAllText(Path.Combine(Root(), "Gg.Cli", "Program.cs"));

    /// <summary>
    /// The reader's arms, as CODE, from the switch the composition root wires.
    /// </summary>
    /// <remarks>
    /// <b>THE COMMENTS COME OUT, AND THAT IS NOT FASTIDIOUSNESS.</b> Three
    /// guards written in one sitting each matched their own explanation: a
    /// comment saying what shape was removed contains that shape, so the guard
    /// reads the reason as the thing. Left alone it makes the fix unexplainable
    /// - the only way to satisfy it becomes deleting the sentence that says
    /// why, which is the opposite of what a ratchet is for.
    /// </remarks>
    private static string Arms()
    {
        var program = Program();
        var at = program.IndexOf("new Gg.Console.BackgroundReads(", StringComparison.Ordinal);

        if (at < 0)
        {
            throw new InvalidOperationException("the reader is not wired here any more.");
        }

        var arms = program[at..program.IndexOf(
            "is in ShellCommands.Reads", at, StringComparison.Ordinal)];

        return string.Join('\n', arms.Split('\n')
            .Where(line => !line.TrimStart().StartsWith("//", StringComparison.Ordinal)));
    }

    [Test]
    public async Task No_arm_defers_its_read_into_the_patch()
    {
        // `_ =>' IS THE SHAPE OF THE DEFECT. A patch that discards the state it
        // is handed and calls a read is a read that has not happened yet - and
        // it happens next on the UI thread, because that is where a patch is
        // applied.
        //
        // EXCEPT THE DEFAULT ARM, which is `_ => throw' and has to be: a
        // command in Reads with no arm must refuse rather than be served
        // somebody else's answer. So what is asserted is that every discarding
        // lambda here throws, rather than that none exists.
        var arms = Arms();

        foreach (var at in Discarding(arms))
        {
            await Assert.That(arms[at..].TrimStart('_', ' ', '=', '>'))
                .StartsWith("throw", StringComparison.Ordinal)
                .Because("an arm answering with a lambda that ignores its input has done no "
                       + "reading, so the reading is still to come - on the thread that "
                       + "applies it. Arms:\n" + arms);
        }
    }

    /// <summary>Where each discarding lambda begins.</summary>
    private static IReadOnlyList<int> Discarding(string arms)
    {
        var found = new List<int>();

        for (var at = arms.IndexOf("_ =>", StringComparison.Ordinal);
             at >= 0;
             at = arms.IndexOf("_ =>", at + 1, StringComparison.Ordinal))
        {
            found.Add(at);
        }

        return found;
    }

    [Test]
    public async Task Every_arm_answers_with_something_already_read()
    {
        // THE POSITIVE FORM, because "does not contain `_ =>'" would also pass
        // for an arm that read nothing at all.
        var arms = Arms();

        foreach (var read in (string[])["ConsoleEstate.Patch", "ConsoleRepositories.Patch",
                                        "ConsoleFlightLog.Patch"])
        {
            await Assert.That(arms).Contains(read, StringComparison.Ordinal)
                .Because($"{read} is called where the Task.Run is, so what comes back has "
                       + "already been fetched. Arms:\n" + arms);
        }
    }

    [Test]
    public async Task The_fold_carries_what_the_estate_read_produces()
    {
        // WHICH FIELDS, ASSERTED, because a fold that quietly dropped one would
        // read as a pane that never filled - and that is the defect this whole
        // path keeps producing in different clothes.
        var estate = File.ReadAllText(Path.Combine(Root(), "Gg.Console", "ConsoleEstate.cs"));

        var at = estate.IndexOf("public static Func<AppState, AppState> Patch", StringComparison.Ordinal);

        await Assert.That(at).IsGreaterThan(-1);

        var patch = estate[at..estate.IndexOf("\n    }", at, StringComparison.Ordinal)];

        foreach (var field in (string[])["Estate", "Envelope", "Diagnosis"])
        {
            await Assert.That(patch).Contains(field + " =", StringComparison.Ordinal)
                .Because($"the estate read writes {field}, so a fold that omits it loses "
                       + "what the read went and got. Patch:\n" + patch);
        }
    }
}
