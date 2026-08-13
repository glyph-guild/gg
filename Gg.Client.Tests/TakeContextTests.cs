using Gg.Client;
using System.Reflection;
using Gg.Contracts;

namespace Gg.Client.Tests;

/// <summary>
/// The seed: measurements, plus the agent's account, marked as its words.
/// </summary>
/// <remarks>
/// <para>
/// <b>Measurements are computed by us and are always present. The account is the
/// agent's own words and is optional.</b> The account is what stops a person
/// re-deriving a decision the agent already made and recorded nowhere else -
/// and marking it is what stops that decision being read as a measurement.
/// </para>
/// <para>
/// <b>Context goes NEAR the executor, never into it.</b> Sandcastle tried to
/// pre-fill Claude Code's composer, found no flag that fills without submitting,
/// and settled on clipboard plus a blank session. Ungainly and correct: the
/// alternative is screen-scraping another program's terminal interface.
/// </para>
/// </remarks>
public class TakeContextTests
{
    private const string Esc = "\u001b";

    private const string Bel = "\u0007";

    private static LoopDigest ADigest() => new()
    {
        LoopId = "implement",
        FilesReadNotEdited = ["src/util.py", "README.md"],
        FilesEdited = ["src/greet.py"],
        Searches = ["def slugify"],
        Errors = [new DigestError { Source = "Bash", Detail = "No module named pytest" }],
        RefusedMoves = ["WebFetch"],
        Attempts = 6,
        StopReason = LoopOutcomes.Exhausted,
    };

    private static TakeSeed Seed(string? account, LoopDigest? digest = null) =>
        TakeSeedComposer.Compose(
            "GG-42", "019ff8aa-1111-7000-8000-000000000001", "/work/tree",
            digest ?? ADigest(), account, verdict: "violated: etc/passwd is outside src/**");

    // ---- measurements are required; the account is not ----

    [Test]
    public async Task Nothing_in_the_seed_requires_the_account()
    {
        // STRUCTURAL, not a null check somebody remembers. The measurements are
        // non-nullable and the account is a string?, so a takeover that depended
        // on the account would not compile.
        var account = typeof(TakeSeed).GetProperty(nameof(TakeSeed.Account))!;
        var measurements = typeof(TakeSeed).GetProperty(nameof(TakeSeed.Measurements))!;

        await Assert.That(new NullabilityInfoContext().Create(account).WriteState)
            .IsEqualTo(NullabilityState.Nullable)
            .Because("a seed with no account is an ordinary seed.");

        await Assert.That(new NullabilityInfoContext().Create(measurements).WriteState)
            .IsEqualTo(NullabilityState.NotNull)
            .Because("measurements are computed by us and exist for every flight that ran.");
    }

    [Test]
    public async Task A_flight_that_produced_no_digest_still_gets_a_seed()
    {
        // The runner was killed before it measured anything. This is the flight
        // MOST likely to need taking over, and a composer that refused because a
        // list was empty would fail exactly it.
        // Composed directly: the helper above defaults the digest, and a helper
        // that quietly supplies the thing being tested for absence is how a test
        // passes without testing.
        var seed = TakeSeedComposer.Compose(
            "GG-42", "019ff8aa-1111-7000-8000-000000000001", "/work/tree",
            digest: null, account: null);

        await Assert.That(seed.Measurements.FilesEdited).IsEmpty();
        await Assert.That(seed.Measurements.StopReason).IsEqualTo("unknown");
        await Assert.That(TakeSeedComposer.Render(seed)).Contains("GG-42");
    }

    // ---- a missing account is visibly missing ----

    [Test]
    public async Task A_missing_account_is_loud_rather_than_an_absent_section()
    {
        // "The agent said nothing" and "its words were dropped" must not read
        // identically. This project's most repeated defect is a plausible value
        // where the absence should have been loud.
        var rendered = TakeSeedComposer.Render(Seed(account: null));

        await Assert.That(rendered).Contains("NO ACCOUNT");
        await Assert.That(rendered).Contains("everything there is")
            .Because("a person has to know the measurements are the whole story, not the visible "
                   + "part of a longer one.");
    }

    [Test]
    public async Task A_present_account_is_marked_as_the_agents_words()
    {
        // The marking is the point. A reader who cannot tell a claim from a
        // measurement will treat the claim as one.
        var rendered = TakeSeedComposer.Render(
            Seed("I could not satisfy the scope rule without touching etc/passwd."));

        await Assert.That(rendered).Contains("THE AGENT'S OWN ACCOUNT");
        await Assert.That(rendered).Contains("not a measurement");
        await Assert.That(rendered).Contains("etc/passwd");

        var measured = rendered[..rendered.IndexOf("THE AGENT'S OWN ACCOUNT", StringComparison.Ordinal)];

        await Assert.That(measured).Contains("MEASURED")
            .Because("and the two are separated, or the marking is decoration.");
    }

    [Test]
    public async Task A_truncated_account_says_that_it_is_truncated()
    {
        // A summary that stops mid-sentence with no mark reads as an agent that
        // stopped mid-sentence.
        var seed = Seed(new string('x', TakeSeedComposer.MaxAccount + 500));

        await Assert.That(seed.AccountState).IsEqualTo(AccountState.Truncated);
        await Assert.That(TakeSeedComposer.Render(seed)).Contains("truncated");
        await Assert.That(seed.Account!.Length).IsEqualTo(TakeSeedComposer.MaxAccount);
    }

    // ---- the account is not in the digest ----

    [Test]
    public async Task The_account_is_not_a_field_of_the_digest()
    {
        // STRUCTURAL, and the failure it prevents has a six-month detection
        // time. Article XIII compares accumulated flights, which needs records
        // computed identically every run; agent prose differs every run. A digest
        // carrying prose would still LOOK like a digest.
        var digest = typeof(LoopDigest).GetProperties().Select(p => p.Name).ToList();

        foreach (var prose in (string[])["Account", "Reason", "Summary", "Narrative", "Words"])
        {
            await Assert.That(digest).DoesNotContain(prose)
                .Because($"'{prose}' on the digest would make two runs of the same work compare "
                       + "unequal, and comparison across flights is the whole of the hardening.");
        }

        // And the liveness half: the digest really is the thing being checked.
        await Assert.That(digest).Contains(nameof(LoopDigest.FilesReadNotEdited));
    }

    // ---- the seed carries no control sequences ----

    [Test]
    public async Task The_seed_contains_no_control_sequences_wherever_they_came_from()
    {
        // Stripped at production now, so this inherits it - and it is asserted
        // HERE anyway, because this is the one place in the product where text is
        // deliberately put into a terminal, and inheriting a property is not the
        // same as having it.
        var poisoned = $"{Esc}]0;pwned{Bel}the agent said {Esc}[31mthis{Esc}[0m";

        var seed = TakeSeedComposer.Compose(
            "GG-42", "flight-1", "/work/tree",
            ADigest() with
            {
                FilesEdited = [$"src/{Esc}[31mgreet.py"],
                Searches = [$"{Esc}]0;pwned{Bel}slugify"],
            },
            poisoned,
            verdict: $"violated{Esc}[0m");

        var rendered = TakeSeedComposer.Render(seed);

        await Assert.That(rendered).DoesNotContain(Esc);
        await Assert.That(rendered).DoesNotContain(Bel);
        await Assert.That(rendered).Contains("the agent said this")
            .Because("stripped rather than dropped: the account still says what it said.");
        await Assert.That(rendered).Contains("slugify");
    }

    [Test]
    public async Task The_accounts_line_breaks_survive_because_it_is_prose()
    {
        var seed = Seed("first line\nsecond line");

        await Assert.That(seed.Account).IsEqualTo("first line\nsecond line");
        await Assert.That(TakeSeedComposer.Render(seed)).Contains("second line");
    }

    // ---- nothing types into the child ----

    [Test]
    public async Task The_seed_reaches_the_clipboard_when_there_is_one()
    {
        var clipboard = new RecordingClipboard();

        var placement = SeedPlacer.Place("the seed", clipboard, Path.GetTempPath());

        await Assert.That(placement).IsTypeOf<SeedPlacement.Clipboard>();
        await Assert.That(clipboard.Copied).IsEqualTo("the seed");
    }

    [Test]
    public async Task A_machine_with_no_clipboard_gets_a_named_file_and_the_takeover_proceeds()
    {
        // Headless machines exist. A takeover that refused to start because a
        // copy-paste helper was missing would be a feature defeated by its own
        // convenience.
        var directory = Path.Combine(
            Path.GetTempPath(), "gg-seed-" + Guid.NewGuid().ToString("N")[..8]);

        try
        {
            var placement = SeedPlacer.Place(
                "the seed", new RefusingClipboard(), directory);

            var file = placement as SeedPlacement.File;

            await Assert.That(file).IsNotNull()
                .Because("the seed still has to reach the person.");
            await Assert.That(File.ReadAllText(file!.Path)).IsEqualTo("the seed");
            await Assert.That(file.Why).Contains("no clipboard here")
                .Because("named, or a person is left wondering why they are reading a file.");
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Test]
    public async Task Nothing_in_the_take_path_types_into_the_child()
    {
        // The rule Sandcastle paid for. Writing to a child's stdin, or sending
        // it keystrokes, is screen-scraping another program's interface by
        // another name.
        var source = File.ReadAllText(SourceOf("TakeSession.cs"));
        var code = string.Join('\n', source.Split('\n')
            .Where(l => !l.TrimStart().StartsWith("//", StringComparison.Ordinal)
                     && !l.TrimStart().StartsWith("///", StringComparison.Ordinal)
                     && !l.TrimStart().StartsWith("*", StringComparison.Ordinal)));

        foreach (var typing in (string[])
                 ["RedirectStandardInput", "StandardInput", "SendKeys", "WriteLine(seed"])
        {
            await Assert.That(code).DoesNotContain(typing)
                .Because($"'{typing}' is typing into the child, which is the thing this design "
                       + "exists to avoid.");
        }

        await Assert.That(code).Contains("WorkingDirectory")
            .Because("the child starts in the flight's tree, which is the whole of the placement.");
    }

    // ---- the fallback must not quietly become normal ----

    [Test]
    public async Task A_missing_account_writes_a_line_in_the_doctor_and_the_bundle()
    {
        // THE FOURTH RULE. A seed without the account still works, which is
        // exactly the danger: handoff degrades to measurements-only and the
        // feature stops doing the thing it was built for with nobody noticing.
        var quiet = Doctor.HandoffAccountCheck(accountsMissing: 0);
        var degraded = Doctor.HandoffAccountCheck(accountsMissing: 3);

        await Assert.That(quiet.Passed).IsTrue();
        await Assert.That(degraded.Passed).IsFalse();
        await Assert.That(degraded.Detail).Contains("3")
            .Because("one flight whose runner was killed is ordinary and every flight for a week is "
                   + "a broken executor, and only the number tells them apart.");

        await Assert.That(degraded.Blocking).IsFalse()
            .Because("a takeover still works on measurements. Calling it blocking would stop the "
                   + "thing the fallback exists to protect.");

        // And it reaches the bundle, because the bundle is built from the
        // doctor's failed checks - one derivation, both surfaces. A degradation
        // visible in only one is one somebody reports and we cannot reproduce.
        var bundle = Bundle.Build(
            DateTimeOffset.UnixEpoch,
            new EnvironmentIdentity
            {
                HostFingerprint = "b8c1f0a9",
                Locks = [],
                Tools = [],
                Provenance = EnvironmentProvenance.Fresh,
            },
            new DoctorReport { Checks = [degraded] },
            flightLog: null);

        await Assert.That(bundle.Degradations.Any(d => d.Name == DoctorChecks.HandoffAccount))
            .IsTrue();
    }

    private static string SourceOf(string file)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Gg.sln")))
        {
            dir = dir.Parent;
        }

        var root = dir ?? throw new InvalidOperationException("Gg.sln not found");

        return Directory
            .EnumerateFiles(root.FullName, file, SearchOption.AllDirectories)
            .First(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
                            StringComparison.Ordinal)
                     && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}",
                            StringComparison.Ordinal));
    }

    private sealed class RecordingClipboard : IClipboard
    {
        internal string? Copied { get; private set; }

        public string? Copy(string text)
        {
            Copied = text;
            return null;
        }
    }

    private sealed class RefusingClipboard : IClipboard
    {
        public string? Copy(string text) => "no clipboard here (headless)";
    }
}
