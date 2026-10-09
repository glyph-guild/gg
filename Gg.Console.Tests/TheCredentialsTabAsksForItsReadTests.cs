namespace Gg.Console.Tests;

/// <summary>
/// The credentials pane asks somebody for its read, so it cannot sit unread for
/// ever.
/// </summary>
/// <remarks>
/// <para>
/// <b>S60.4-02, and it guards the one failure in this console that no other
/// ratchet catches.</b> <c>ConsoleRefresh.ForTabAsync</c> says so itself:
/// <i>"Without this arm the fall-through below answers `Nothing`, the state member
/// stays null, `HasRead` stays false, and the pane says 'reading the plans' for
/// ever — with no test failing anywhere. That is the dead-tab failure, and it is
/// why this arm has a test of its own rather than only a pane."</i>
/// </para>
/// <para>
/// <b>A pane can be complete in nine places and dead.</b> Every switch can know
/// the tab, the rows can project, the columns can be right, and the model member
/// stays null because nothing ever fetched it. The arm is a line of code with no
/// compiler and no ratchet behind it.
/// </para>
/// <para>
/// <b>And it must fetch BOTH halves.</b> A credential row is keyed on repositories
/// ∪ credentials, so an arm that read only the credentials would draw a list with
/// every gap missing — and the gap is the row the pane exists for. The repositories
/// read also has a second consumer that must not lose it: the send chooser reads
/// <c>state.Repositories</c>, and the pane that used to fetch it is this one.
/// </para>
/// </remarks>
public class TheCredentialsTabAsksForItsReadTests
{
    [Test]
    public async Task The_refresh_arm_exists_for_it()
    {
        // READ AS SOURCE TEXT, because the failure is an absent arm and a missing
        // arm is invisible to a type system - the fall-through answers `Nothing`
        // and everything compiles.
        var source = Source("Gg.Console", "ConsoleRefresh.cs");

        await Assert.That(source).Contains("TabId.Credentials")
            .Because("without an arm here the pane is dead and nothing fails: the state member "
                   + "stays null, HasRead stays false, and the pane says 'reading' for ever.");
    }

    [Test]
    public async Task And_the_arm_reads_the_repositories_too()
    {
        // BOTH HALVES IN ONE ARM. The rows are keyed on repositories union
        // credentials, so reading only the credentials draws a list with every
        // gap missing - and the gap is the row this pane exists for.
        // THE ARM DELEGATES, so the reads are in the helper it names rather than
        // inline - which is the shape the board's and the fleet's arms already use
        // for the same reason. So the slice to read is the helper's body.
        var source = Source("Gg.Console", "ConsoleRefresh.cs");

        await Assert.That(Between(source, "TabId.Credentials =>", "TabId.Envelope"))
            .Contains("TheCredentialsAndWhatTheyAreForAsync")
            .Because("the arm has to reach the reads somehow, and naming the helper is how.");

        var helper = Between(source, "private static async Task<Func<AppState, AppState>> TheCredentials", "TheFleetAndWhatItHasLeftAsync");

        await Assert.That(helper).Contains("ListCredentialsAsync")
            .Because("the credentials half has to be in the read that serves the credentials tab.");

        await Assert.That(helper).Contains("RepositoriesAsync")
            .Because("and so does the repositories half, or the pane shows no gaps and the send "
                   + "chooser loses the registry it reads.");

        await Assert.That(helper).Contains("ListKeysAsync")
            .Because("and the keys, or `who can open it` reads \"2 nobody here can name\" for "
                   + "every credential - true and useless.");
    }

    [Test]
    public async Task The_tick_is_not_told_to_skip_this_tab()
    {
        // AutoRefresh's predicate NAMES THE TABS THAT ASK NOBODY - it is
        // `tab is not TabId.Intents`, because a filter is local - so inclusion is
        // the default and what matters is that nothing excluded this one. Read as
        // source because the predicate is private, and making it public for a test
        // would be widening a mechanism to assert a fact about a tab.
        var source = Source("Gg.Console", "AutoRefresh.cs");

        await Assert.That(source).Contains("Reads(TabId tab)")
            .Because("if the predicate were renamed this test would be reading nothing.");

        await Assert.That(Between(source, "Reads(TabId tab)", "}")).DoesNotContain("Credentials")
            .Because("a credential pushed from another machine, or a reseal on this one, changes "
                   + "this pane with nobody touching the keyboard - so the tick must ask.");
    }

    [Test]
    public async Task The_scan_can_see_an_arm_that_is_missing()
    {
        // POISON TWIN. "The arm is there" is also what a scan over the wrong file
        // returns, and this class would look diligent either way.
        var source = Source("Gg.Console", "ConsoleRefresh.cs");

        await Assert.That(source).IsNotEmpty();
        await Assert.That(source).DoesNotContain("TabId.ATabNobodyHasWritten")
            .Because("if the scan matched anything it would pass over a file with no arms at all.");
        await Assert.That(source).Contains("TabId.Queue")
            .Because("and it must see the arms that have always been there, or it is reading "
                   + "something that is not this file.");
    }

    private static string Source(string project, string file)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Gg.sln")))
        {
            dir = dir.Parent;
        }

        return File.ReadAllText(Path.Combine(dir!.FullName, project, file));
    }

    /// <summary>The slice of source between two markers, for reading one arm.</summary>
    private static string Between(string source, string from, string to)
    {
        var start = source.IndexOf(from, StringComparison.Ordinal);

        // AFTER the start, not the file's first occurrence. The arm and the helper
        // both name the fleet's helper, so searching from zero found a marker that
        // precedes the slice and silently returned nothing - which read as "the
        // reads are missing" when they were there.
        var end = start < 0 ? -1 : source.IndexOf(to, start, StringComparison.Ordinal);

        return start < 0 || end < 0 ? "" : source[start..end];
    }
}
