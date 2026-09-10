using System.Text.RegularExpressions;

namespace Gg.Cli.Tests;

/// <summary>
/// A refresh reads what boot read, because both go through one place.
/// </summary>
/// <remarks>
/// <para>
/// <b>The defect this is written against, and it shipped.</b> Boot built the
/// model and then applied a <c>with</c> block of local facts — the settings,
/// this machine's runner id, its name, and what the control plane offers. The
/// refresh called the same loader and applied none of them, so every one was
/// preserved from the state before it. Taking an offer and reopening help
/// showed the line about the offer that had just been taken.
/// </para>
/// <para>
/// <b>A field added to boot and forgotten in the refresh is the shape, and it
/// will happen again.</b> Two <c>with</c> blocks that have to agree cannot be
/// held to agreeing by anything but attention — the reason
/// <c>ConsoleLoop</c>'s own reload takes the whole model and answers with it
/// rather than naming six fields, which is written up in that method. This is
/// that argument applied one layer out.
/// </para>
/// <para>
/// <b>So it is one function, called twice.</b> The assertion is not that the
/// offer is re-read; it is that there is nowhere for the next field to be
/// added to only one of them.
/// </para>
/// <para>
/// <b>AND THEN HALF OF THAT WAS STILL NOT TRUE, found by using it.</b> One
/// function called twice fixes nothing if the function itself answers from a
/// copy. <c>LocalFacts</c> folds the settings and the airspace path out of
/// <c>InForce.Configuration</c>, which reads the file once per process and
/// caches it for ever - so this class's thesis held for the offer, which is a
/// network read, and failed for everything that comes from the file. Somebody
/// set the airspace path on the airspace tab, the file was written, the reload
/// ran, and the field showed the old value: the same staleness this class was
/// written against, one layer down and behind the fix for it.
/// </para>
/// <para>
/// <b>The cache is right about a verb and wrong about a console.</b> Its own
/// reason - a dozen readers of one run should not be handed different answers
/// - was written for a process that reads the file and exits in milliseconds.
/// The console runs for hours and is the one process that REWRITES its own
/// configuration, so for it "the answer as of now" is the only useful one. A
/// reload is a boot; boot reads the file.
/// </para>
/// </remarks>
public partial class BootAndRefreshReadTheSameThingsTests
{
    private static string Root() => Text("Gg.Cli", "Program.cs");

    private static string Text(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Gg.sln")))
        {
            directory = directory.Parent;
        }

        var at = (directory ?? throw new InvalidOperationException("Gg.sln not found")).FullName;

        return File.ReadAllText(Path.Combine([at, .. parts]));
    }

    /// <summary>The body of the one function boot and the reload both apply.</summary>
    private static string Facts()
    {
        var source = Root();
        var at = source.IndexOf("static AppState LocalFacts", StringComparison.Ordinal);

        return at < 0
            ? throw new InvalidOperationException(
                "LocalFacts was renamed, so this scan reads nothing.")
            : source[at..source.IndexOf("\nstatic ", at + 1, StringComparison.Ordinal)];
    }

    [Test]
    public async Task Both_the_boot_and_the_reload_apply_the_local_facts()
    {
        var source = Root();

        var applications = LocalFacts().Count(source);

        await Assert.That(applications).IsGreaterThanOrEqualTo(3)
            .Because("one declaration and two callers: the boot and the reload. Fewer than "
                   + "three means one of the two paths is building the model its own way, "
                   + "which is how the offer came to be read at boot and never again.");
    }

    [Test]
    public async Task The_reload_does_not_rebuild_the_model_its_own_way()
    {
        // THE OTHER DIRECTION. Sharing a function is worth nothing if the
        // reload also carries a `with` block of its own - the two would drift
        // exactly as they did before, with an extra layer to read first.
        var source = Root();

        var at = source.IndexOf("reload: current =>", StringComparison.Ordinal);

        await Assert.That(at).IsGreaterThan(-1)
            .Because("the reload delegate was renamed, so this scan reads nothing.");

        var delegated = source[at..source.IndexOf("\n        signIn:", at, StringComparison.Ordinal)];

        await Assert.That(delegated).Contains("LocalFacts", StringComparison.Ordinal)
            .Because("a refresh that skips the local facts preserves them from the state "
                   + "before it, which is the whole defect.");

        await Assert.That(delegated).DoesNotContain("Settings =", StringComparison.Ordinal)
            .Because("a second place that assigns them is a second place to forget the "
                   + "next field.");
    }

    [Test]
    public async Task What_a_refresh_re_reads_includes_the_offer()
    {
        // NAMED, because it is the one whose staleness a person sees: they take
        // an offer, the file changes, and the page they took it from would go on
        // advertising it until the console was restarted.
        var source = Root();

        var at = source.IndexOf("static AppState LocalFacts", StringComparison.Ordinal);

        await Assert.That(at).IsGreaterThan(-1);

        var body = source[at..source.IndexOf("\nstatic ", at + 1, StringComparison.Ordinal)];

        await Assert.That(body).Contains("Offered =", StringComparison.Ordinal);
        await Assert.That(body).Contains("Settings =", StringComparison.Ordinal);
        await Assert.That(body).Contains("LocalRunnerId =", StringComparison.Ordinal)
            .Because("a runner registered since boot is a runner this console should see "
                   + "on a refresh, and it was in the boot block for that reason.");
    }

    [Test]
    public async Task The_file_can_be_forgotten_by_the_process_that_rewrote_it()
    {
        // THE CAPABILITY FIRST, because the caller below is worth nothing
        // without it and a scan for a call to a method that does not exist
        // reads as a passing scan the moment somebody renames it.
        var cache = Text("Gg.Cli", "InForce.cs");

        await Assert.That(cache).Contains("void Forget()", StringComparison.Ordinal)
            .Because("gg is the process that writes this file, and a cache with no way to "
                   + "be told so can only be right until the first write. The console "
                   + "wrote a new airspace path and went on reporting the old one for the "
                   + "rest of its run.");
    }

    [Test]
    public async Task Forgetting_does_not_re_say_what_was_already_said()
    {
        // THE PROPERTY THE FIX COULD QUIETLY TAKE. A broken file is said once,
        // on stderr, and then stepped over. If forgetting resets that too, a
        // refresh says it again - and a refresh happens with Terminal.Gui torn
        // down, so the line lands across the screen it is about to rebuild.
        // Two flags, not one: what is cached and what has been said are
        // different facts.
        var cache = Text("Gg.Cli", "InForce.cs");

        await Assert.That(cache).Contains("_reported", StringComparison.Ordinal)
            .Because("the diagnosis is said once for the life of the process while the "
                   + "value is re-read, so one flag cannot hold both.");
    }

    [Test]
    public async Task A_refresh_reads_the_configuration_file_again()
    {
        // THE DEFECT, AND WHERE THE FIX BELONGS. Not at the three ports that
        // write the file - three places to remember is what goes stale - but
        // in the one function whose entire purpose is that boot and a refresh
        // read the same things. Everything the file answers is folded here.
        await Assert.That(Facts()).Contains("InForce.Forget()", StringComparison.Ordinal)
            .Because("this function reads the settings and the airspace path out of a "
                   + "cache the console itself invalidates by writing the file. Folding "
                   + "the copy from boot is how a path somebody just set came back as the "
                   + "one it replaced.");
    }

    [Test]
    public async Task It_is_forgotten_before_anything_is_read_rather_than_after()
    {
        // ORDER IS THE WHOLE THING. Forgotten after the fold and the fold is
        // still the stale one; it would only come good on the reload after the
        // one that mattered, which is worse than not fixing it because it
        // looks intermittent.
        var body = Facts();

        var forgotten = body.IndexOf("InForce.Forget()", StringComparison.Ordinal);
        var read = body.IndexOf("InForce.Configuration", StringComparison.Ordinal);

        await Assert.That(forgotten).IsGreaterThan(-1);
        await Assert.That(read).IsGreaterThan(-1)
            .Because("the settings are read from it, and a scan that finds no read is a "
                   + "scan over a function that moved.");

        await Assert.That(forgotten).IsLessThan(read)
            .Because("forgetting after the read folds the copy from boot and refreshes the "
                   + "one after it, which reads as a console that is right every other "
                   + "time.");
    }

    [GeneratedRegex(@"\bLocalFacts\b")]
    private static partial Regex LocalFacts();
}
