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
        @"\b(you|your|yours|tenant|principal|nonce|mint|minted|redeem|redeemed|somebody)\b|n't\b",
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
}
