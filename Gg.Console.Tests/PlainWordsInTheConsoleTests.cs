using System.Reflection;
using System.Text.RegularExpressions;
using Gg.Client;
using Gg.Console;
using Gg.Contracts;

namespace Gg.Console.Tests;

/// <summary>
/// What a person reads in the console, in the voice the owner chose.
/// </summary>
/// <remarks>
/// Neutral and formal: no "you", no internal words, no contractions, and an
/// empty pane or a refusal says what to run next. The ratchet renders every
/// modal and every key description rather than scanning source, so a comment
/// cannot fail it and a string built at run time cannot slip past it.
/// </remarks>
public class PlainWordsInTheConsoleTests
{
    private static readonly Regex RuledOut = new(
        @"\b(you|your|yours|yourself|tenant|tenants|principal|nonce|mint|mints|minted|redeem|redeemed|somebody|nobody|everybody)\b|n't\b",
        RegexOptions.IgnoreCase);

    [Test]
    public async Task An_organisation_with_no_repositories_is_told_what_to_run()
    {
        var empty = new AppState { Repositories = new RegisteredRepositories { Repositories = [] } };

        await Assert.That(PaneText.Repositories(empty))
            .IsEqualTo("No repositories are registered for this organisation yet. A flight can "
                     + "still run without one. To register a repository, run gg airspace "
                     + "repositories add.");
    }

    [Test]
    public async Task An_organisation_with_no_runners_is_told_what_to_run()
    {
        var empty = new AppState { Runners = new RunnerList { Runners = [] } };

        await Assert.That(PaneText.Runners(empty)).Contains("run gg runner up on a machine");
    }

    [Test]
    public async Task A_takeover_nobody_is_named_for_ends_in_a_command()
    {
        var take = new TakeSession(claim: _ => new TakeoverClaim.Refused(null));

        var result = take.Take(new TakeRequest
        {
            FlightId = "019ff8aa-0000-7000-8000-000000000042",
            FlightNumber = "GG-42",
            TreePath = "/nowhere",
            Seed = new TakeSeed
            {
                Revision = 1,
                FlightNumber = "GG-42",
                FlightId = "019ff8aa-0000-7000-8000-000000000042",
                Measurements = new TakeMeasurements
                {
                    FilesEdited = [],
                    FilesReadNotEdited = [],
                    Searches = [],
                    Errors = [],
                    UndeclaredMovesUsed = [],
                    Attempts = 1,
                    StopReason = "landed",
                },
                AccountState = "none",
                TranscriptState = "none",
            },
        });

        await Assert.That(result.Diagnosis)
            .IsEqualTo("This flight has already been taken over, and the control plane cannot say "
                     + "by whom. Nothing was started. Run gg show GG-42 to see its current state.");
    }

    [Test]
    public async Task A_reader_answer_the_console_cannot_show_names_no_type()
    {
        var source = Sources.Read("Gg.Console", "State", "Reducer.cs");

        await Assert.That(source).DoesNotContain("outcome.GetType().Name")
            .Because("a type name tells a person nothing they can act on.");
        await Assert.That(source).Contains("answered in a way this console cannot ");
    }

    [Test]
    public async Task No_modal_uses_words_the_voice_rules_out()
    {
        var offending = new List<string>();
        var rendered = 0;

        foreach (var mode in Enum.GetValues<UiMode>())
        {
            var state = new AppState { Mode = mode };

            string text;
            try
            {
                text = PaneText.ModalTitle(state) + "\n" + PaneText.Modal(state);
            }
            catch (Exception)
            {
                // A modal that needs more state than an empty model to draw is
                // checked by its own tests; this walk reads what it can.
                continue;
            }

            rendered++;

            if (RuledOut.Match(text) is { Success: true } match)
            {
                offending.Add($"{mode}: '{match.Value}' in {text}");
            }
        }

        await Assert.That(rendered).IsGreaterThan(20)
            .Because("a walk that rendered nothing would pass on nothing.");
        await Assert.That(offending).IsEmpty();
    }

    [Test]
    public async Task No_empty_pane_uses_words_the_voice_rules_out()
    {
        AppState[] states =
        [
            new AppState(),
            new AppState
            {
                Repositories = new RegisteredRepositories { Repositories = [] },
                Credentials = new CredentialList { Credentials = [] },
                Runners = new RunnerList { Runners = [] },
            },
        ];

        var offending = states
            .SelectMany(state => (string[])
            [
                PaneText.Repositories(state),
                PaneText.CredentialsPane(state),
                PaneText.Runners(state),
                PaneText.FlyingWithAbsence(state),
                PaneText.Board(state),
                PaneText.Flights(state),
                PaneText.HelpDoctorText(state),
                .. PaneText.QueueRows(state),
            ])
            .Where(text => RuledOut.IsMatch(text))
            .ToList();

        await Assert.That(offending).IsEmpty();
    }

    [Test]
    public async Task No_key_description_uses_words_the_voice_rules_out()
    {
        var offending = Keymap.Catalogue()
            .SelectMany(entry => (string?[]) [entry.Binding.Description, entry.Binding.When])
            .OfType<string>()
            .Where(text => RuledOut.IsMatch(text))
            .Distinct()
            .ToList();

        await Assert.That(offending).IsEmpty()
            .Because("the hint line and the help page are generated from these.");
    }

    [Test]
    public async Task The_queue_order_carries_no_note_to_developers()
    {
        await Assert.That(QueueSort.Default.Name).IsEqualTo("oldest first");
    }

    // ---- columns, labels, the help page and the look page ----

    /// <summary>
    /// Words a label, heading or cell may not carry even though a sentence might
    /// explain them: the heartbeat's verb, the API's door, a spike, a ceremony.
    /// </summary>
    private static readonly Regex Jargon = new(
        @"\b(beating|door|spike|ceremony|ask)\b|agent-login|\.cs\b|AppState",
        RegexOptions.IgnoreCase);

    private static IEnumerable<(string Table, IReadOnlyList<string> Columns)> EveryTable() =>
    [
        ("flights", Rows.FlightColumns),
        ("work list", Rows.BrowseColumns),
        ("log", Rows.LogColumns),
        ("recorded", Rows.FactColumns),
        ("nominations", Rows.NominationColumns),
        ("sweeps", Rows.SweepColumns),
        ("itineraries", Rows.ItineraryColumns),
        ("legs", Rows.ItineraryLegColumns),
        ("runners", Rows.RunnerColumns),
        ("repositories", Rows.RepositoryColumns),
        ("credentials", Rows.CredentialColumns),
        ("work item history", Rows.WorkItemColumns),
        ("work kinds", Rows.WorkKindColumns),
        ("audience", Rows.AudienceColumns),
        ("flying with", Rows.FlyingWithColumns),
        ("airspace", AirspaceRows.AirspaceColumns),
        ("environments", EnvironmentRows.EnvironmentColumns),
        ("members", EnvironmentRows.MemberColumns),
        ("runner flights", RunnerActivity.FlightColumns),
        ("runner sessions", RunnerActivity.SessionColumns),
        ("credential choice", CredentialRepositories.Columns),
        ("work item fields", WorkItemDetails.FieldColumns),
        ("look", Looks.Columns),
        .. Enum.GetValues<BrowseFacet>().Select(facet => ($"filter {facet}", Rows.FilterColumns(facet))),
    ];

    [Test]
    public async Task Every_column_heading_is_a_plain_lower_case_fragment()
    {
        var offending = EveryTable()
            .SelectMany(table => table.Columns.Select(heading => (table.Table, Heading: heading)))
            .Where(c => RuledOut.IsMatch(c.Heading)
                     || Jargon.IsMatch(c.Heading)
                     || c.Heading != c.Heading.ToLowerInvariant()
                     || c.Heading.EndsWith('.'))
            .Select(c => $"{c.Table}: '{c.Heading}'")
            .ToList();

        await Assert.That(offending).IsEmpty();
    }

    [Test]
    public async Task The_runners_columns_say_whose_what_is_missing_and_what_it_offers_in_plain_words()
    {
        await Assert.That(Rows.RunnerColumns).IsEquivalentTo((string[])
            ["", "runner", "owner", "profile", "state", "working on", "cpu", "memory",
             "missing", "offers", "last heard"]);
    }

    [Test]
    public async Task The_boards_columns_name_what_each_one_holds()
    {
        await Assert.That(Rows.NominationColumns).IsEquivalentTo((string[])
            ["subject", "person", "state", "kind", "age"]);
        await Assert.That(Rows.SweepColumns).IsEquivalentTo((string[])
            ["watch", "state", "run by", "last heard", "next sweep", "opened"]);
    }

    [Test]
    public async Task The_other_renamed_columns_read_plainly()
    {
        await Assert.That(Rows.CredentialColumns).IsEquivalentTo((string[])
            ["credential", "grants", "on this machine", "who can open it"]);
        await Assert.That(Rows.FactColumns).IsEquivalentTo((string[])
            ["at", "kind", "stored as", "what it says"]);
        await Assert.That(EnvironmentRows.EnvironmentColumns).IsEquivalentTo((string[])
            ["environment", "strategy", "pool", "kept ready", "reported", "reported at"]);
    }

    [Test]
    public async Task No_table_row_uses_words_the_voice_rules_out()
    {
        const string Id = "01a06385-322f-7371-93a2-ce35db5c4fbe";

        RunnerSummary Runner(string ownership, string owner = "", bool resident = false) => new()
        {
            RunnerId = Id,
            Label = "vmlinux001",
            State = RunnerStates.Idle,
            Ownership = ownership,
            Owner = owner,
            Resident = resident,
            Reserved = owner.Length > 0,
            Profile = "dev-worker",
        };

        var cells = new List<string>();

        foreach (var runner in (RunnerSummary[])
                 [
                     Runner(RunnerOwnerships.Tenant),
                     Runner(RunnerOwnerships.Open),
                     Runner(RunnerOwnerships.Claimed, "Kevin", resident: true),
                     Runner(RunnerOwnerships.Claimed),
                 ])
        {
            var state = new AppState { RunnerSelected = 0, Runners = new RunnerList { Runners = [runner] } };

            cells.AddRange(Rows.RunnerCells(Rows.Runners(state)[0]));
            cells.AddRange(RunnerDetails.Fields(state).SelectMany(f => (string[])[f.Label, f.Value]));
            cells.Add(PaneText.Runners(state));
        }

        var watches = new AppState
        {
            BoardTable = BoardTable.Sweeps,
            SweepSelected = 0,
            Board = new BoardPage { IncludedEnded = true, Nominations = [] },
            Watches = new WatchStandingList
            {
                Standings =
                [
                    new WatchStanding
                    {
                        Name = "nightly-triage",
                        Version = "3",
                        Window = WatchStanding.DefaultCostWindow,
                        Executor = "instructions",
                        Outcome = WatchOutcomes.Swept,
                        LastHeardAt = DateTimeOffset.UtcNow.AddMinutes(-9),
                    },
                ],
            },
        };

        cells.AddRange(Rows.Sweeps(watches).SelectMany(Strings));
        cells.AddRange(BoardDetails.Fields(watches).SelectMany(f => (string[])[f.Label, f.Value]));

        var offending = cells
            .Where(cell => RuledOut.IsMatch(cell) || Jargon.IsMatch(cell))
            .Distinct()
            .ToList();

        await Assert.That(cells.Count).IsGreaterThan(20)
            .Because("a walk that rendered nothing would pass on nothing.");
        await Assert.That(offending).IsEmpty();
    }

    [Test]
    public async Task Every_tab_and_help_page_is_named_in_lower_case_plain_words()
    {
        var names = Tabs.All.Select(Tabs.Name)
            .Concat(HelpPages.All.Select(HelpPages.Title))
            .ToList();

        await Assert.That(names.Where(n => RuledOut.IsMatch(n) || n != n.ToLowerInvariant()))
            .IsEmpty();
    }

    [Test]
    public async Task Every_fold_of_the_help_page_is_titled_in_words_rather_than_a_mode_name()
    {
        var modes = Keymap.Catalogue().Select(entry => entry.Mode).Distinct().ToList();

        var offending = HelpTree.Keys()
            .Select(group => group.Heading)
            .Where(heading => heading.Length == 0
                           || heading != heading.ToLowerInvariant()
                           || RuledOut.IsMatch(heading)
                           // A mode named in two words in the code, run together and
                           // lower cased, is how a missing heading used to read.
                           || modes.Any(mode => mode.ToString().Count(char.IsUpper) > 1
                                             && Regex.IsMatch(heading, $@"\b{mode.ToString().ToLowerInvariant()}\b")))
            .ToList();

        await Assert.That(offending).IsEmpty();
        await Assert.That(modes.Where(mode => PaneText.ModeHeading(mode).Length == 0)).IsEmpty()
            .Because("the copied help page draws an empty line over a mode with no heading.");
    }

    [Test]
    public async Task The_keys_name_the_tabs_they_open_by_the_names_on_the_bar()
    {
        string Said(char key) => Keymap.Catalogue()
            .First(entry => entry.Mode == UiMode.Normal && entry.Binding.Key == KeyStroke.Char(key)
                         && entry.Binding.When is null or "")
            .Binding.Description;

        await Assert.That(Said('r')).IsEqualTo(Tabs.Name(TabId.Credentials));
        await Assert.That(Said('e')).IsEqualTo(Tabs.Name(TabId.Envelope));
    }

    [Test]
    public async Task No_key_description_or_condition_uses_jargon()
    {
        var offending = Keymap.Catalogue()
            .SelectMany(entry => (string?[]) [entry.Binding.Description, entry.Binding.When])
            .OfType<string>()
            .Where(text => Jargon.IsMatch(text) || text.Contains("mark is up", StringComparison.Ordinal))
            .Distinct()
            .ToList();

        await Assert.That(offending).IsEmpty();
    }

    [Test]
    public async Task The_look_page_reads_as_settings_and_hands_back_no_internals()
    {
        var changed = new Look { Palette = Palette.Midnight, TabLine = Edge.Double };

        var texts = Looks.Rows(changed).SelectMany(row => (string[])[row.Setting, row.Value, row.Changed])
            .Concat(Looks.All.Select(Looks.About))
            .Append(Looks.Copyable(changed))
            .Append(Looks.Copyable(new Look()))
            .ToList();

        await Assert.That(texts.Where(t => RuledOut.IsMatch(t) || Jargon.IsMatch(t))).IsEmpty();
        await Assert.That(Looks.Rows(new Look()).Select(row => row.Value)
                .Where(value => value != value.ToLowerInvariant()))
            .IsEmpty()
            .Because("a value is a fragment, and `RoundedDotted' is a name from the code.");
    }

    [Test]
    public async Task The_help_pages_use_no_words_the_voice_rules_out()
    {
        var state = new AppState();

        var texts = new[]
        {
            PaneText.HelpEnvironmentText(state),
            PaneText.HelpDoctorText(state),
            PaneText.Modal(state with { Mode = UiMode.Help }),
        };

        await Assert.That(texts.Where(t => RuledOut.IsMatch(t))).IsEmpty();
    }

    [Test]
    public async Task A_modal_title_is_a_fragment_and_its_sentence_starts_with_a_capital()
    {
        await Assert.That(PaneText.ModalTitle(new AppState { Mode = UiMode.WorkItemDetail }))
            .IsEqualTo("the work item");
        await Assert.That(PaneText.Modal(new AppState { Mode = UiMode.ItineraryDetail }).Trim())
            .IsEqualTo("No plan is open.");
    }

    private static IEnumerable<string> Strings(object row) =>
        row.GetType()
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.PropertyType == typeof(string))
            .Select(p => (string?)p.GetValue(row) ?? "");
}
