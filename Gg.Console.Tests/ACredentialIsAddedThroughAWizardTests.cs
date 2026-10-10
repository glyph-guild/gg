using Gg.Client;
using Gg.Console;
using Gg.Contracts;
using Gg.Console.Views;

namespace Gg.Console.Tests;

/// <summary>
/// A credential is added inside the console, through a wizard, rather than by
/// tearing the screen down to ask five questions at a bare prompt.
/// </summary>
/// <remarks>
/// <para>
/// <b>Asked for on 2026-10-10</b>, alongside credentials that say what they
/// grant: adding one "puts us back on the commandline". The wizard asks which
/// service, what for, how much access in the service's own words, which
/// account and the token - and says the whole of it back in one sentence
/// before anything is registered.
/// </para>
/// <para>
/// <b>Every step is the model's.</b> The steps, the cursor and the account live
/// in <see cref="AppState.CredentialDraft"/>, so the keymap, the generated key
/// walk and the dump all see the screen. The token does not: it goes to the
/// held secret the composition owns, as the broadcast's passphrase does.
/// </para>
/// </remarks>
public class ACredentialIsAddedThroughAWizardTests
{
    private static RepositoryRegistered ARepository(string path, string provider) => new()
    {
        Name = path.Replace('/', '-'),
        Provider = provider,
        Id = "R_" + path.Length,
        Path = path,
        Credential = RepositoryCredentialModes.Required,
        RegisteredAt = DateTimeOffset.UnixEpoch,
        RegisteredBy = "a-directory:ada",
    };

    private static AppState OnTheCredentialsTab() => new()
    {
        ActiveTab = TabId.Credentials,
        CredentialsVisible = true,
        Repositories = new RegisteredRepositories
        {
            Repositories = [ARepository("JDX/JDNext", "ado"), ARepository("acme/tools", "forge")],
        },
    };

    private static AppState Opened() =>
        Reducer.Reduce(
            OnTheCredentialsTab() with { Mode = UiMode.CredentialActions },
            Command.OpenCredentialWizard);

    private static AppState Choosing(AppState state, CredentialProvider? service)
    {
        var services = CredentialWizard.Services;
        var row = service is null ? services.Count : services.ToList().IndexOf(service);

        return Reducer.Reduce(Reducer.Pointed(state, row), Command.WizardNext);
    }

    private static AppState Next(AppState state) => Reducer.Reduce(state, Command.WizardNext);

    // ---- how it opens ----

    [Test]
    public async Task C_in_the_credential_acts_opens_the_wizard()
    {
        var inside = KeymapContext.For(OnTheCredentialsTab() with { Mode = UiMode.CredentialActions });

        await Assert.That(Keymap.Resolve(KeyStroke.Char('c'), inside))
            .IsEqualTo(Command.OpenCredentialWizard);

        var opened = Opened();
        await Assert.That(opened.Mode).IsEqualTo(UiMode.CredentialWizard);
        await Assert.That(opened.CredentialDraft?.Step).IsEqualTo(CredentialWizardStep.Service);
    }

    [Test]
    public async Task And_the_prompt_at_the_terminal_is_still_one_key_away()
    {
        // FOR WHAT THE WIZARD DOES NOT OFFER: an agent's credential, or a tracker
        // under a key no service in the catalog uses.
        var inside = KeymapContext.For(OnTheCredentialsTab() with { Mode = UiMode.CredentialActions });

        await Assert.That(Keymap.Resolve(KeyStroke.Char('p'), inside))
            .IsEqualTo(Command.AddCredential);
    }

    // ---- the whole of it, for the token the investigate agent needs ----

    [Test]
    public async Task A_SonarCloud_token_is_walked_from_service_to_the_sentence_that_registers_it()
    {
        var service = Choosing(Opened(), CredentialProviders.SonarCloud);

        await Assert.That(service.CredentialDraft?.Step).IsEqualTo(CredentialWizardStep.For);
        await Assert.That(CredentialWizard.Targets(service).Select(t => t.Subject))
            .IsEquivalentTo((string[])[CredentialSubjects.Analysis])
            .Because("a SonarCloud token reads findings, so the only thing it can be for is "
                   + "the analysis service itself.");

        var access = Next(service);
        await Assert.That(access.CredentialDraft?.Step).IsEqualTo(CredentialWizardStep.Access);
        await Assert.That(CredentialWizard.Accesses(access).Select(a => a.Words))
            .IsEquivalentTo((string[])["Browse (read issues and measures)"]);

        var account = Next(access);
        await Assert.That(account.CredentialDraft?.Step).IsEqualTo(CredentialWizardStep.Account);

        // WHAT THE SCREEN DOES ON ENTER: the account goes onto the draft and the
        // token into the held secret, and only then is the command dispatched.
        var review = Next(account with
        {
            CredentialDraft = account.CredentialDraft! with { Identity = "kevin" },
        });

        await Assert.That(review.CredentialDraft?.Step).IsEqualTo(CredentialWizardStep.Review);

        // A CHECKLIST, NOT A SENTENCE (owner, 2026-10-10: "the text in the wizard is not
        // friendly. it should be plain english").
        var said = PaneText.Modal(review);
        foreach (var line in (string[])
                 ["Service    SonarCloud", "Used for   Code analysis results",
                  "Access     Read only", "Account    kevin", "press Enter to save"])
        {
            await Assert.That(said).Contains(line).Because($"the review says '{line}'. Said: {said}");
        }

        await Assert.That(Keymap.Resolve(KeyStroke.EnterKey, KeymapContext.For(review)))
            .IsEqualTo(Command.FinishCredentialWizard);
        await Assert.That(ShellCommands.Handled).Contains(Command.FinishCredentialWizard)
            .Because("registering reaches the control plane, which a UI session may not do; "
                   + "the loop registers after the session ends, as the broadcast sends.");
    }

    [Test]
    public async Task Each_step_before_the_review_goes_on_inside_the_session()
    {
        var account = Next(Next(Choosing(Opened(), CredentialProviders.SonarCloud)));

        await Assert.That(Keymap.Resolve(KeyStroke.EnterKey, KeymapContext.For(account)))
            .IsEqualTo(Command.WizardNext);
        await Assert.That(ShellCommands.Handled).DoesNotContain(Command.WizardNext)
            .Because("tearing the screen down on every step is the blink the credential acts "
                   + "were moved out of the shell to stop.");
    }

    [Test]
    public async Task An_account_is_required_before_the_review()
    {
        var account = Next(Next(Choosing(Opened(), CredentialProviders.SonarCloud)));
        var refused = Next(account);

        await Assert.That(refused.CredentialDraft?.Step).IsEqualTo(CredentialWizardStep.Account)
            .Because("a credential names the account it acts as, or a flight log cannot say "
                   + "who read what it opened.");
    }

    // ---- what a service offers ----

    [Test]
    public async Task A_service_offers_the_repositories_registered_with_it_and_no_others()
    {
        var forJdnext = Choosing(Opened(), CredentialProviders.Find("ado"));

        var repositories = CredentialWizard.Targets(forJdnext)
            .Where(t => t.Subject == CredentialSubjects.Repository)
            .Select(t => t.Named);

        await Assert.That(repositories).IsEquivalentTo((string[])["JDX/JDNext"]);
        await Assert.That(CredentialWizard.Rows(forJdnext))
            .IsEquivalentTo((string[])["The JDX/JDNext repository", "Work items (tickets)"]);
    }

    // ---- in words a person uses ----

    [Test]
    public async Task Every_choice_reads_as_plain_english()
    {
        await Assert.That(CredentialWizard.Rows(Opened()).Last()).IsEqualTo("Other");

        var access = Next(Choosing(Opened(), CredentialProviders.SonarCloud));
        await Assert.That(CredentialWizard.Rows(access)).IsEquivalentTo((string[])
            ["Read only (on SonarCloud: Browse (read issues and measures))"])
            .Because("the plain words first, and the service's own label beside them so a "
                   + "person can find the same box on its token page.");

        var other = Next(Choosing(Opened(), null));
        await Assert.That(CredentialWizard.Rows(other))
            .IsEquivalentTo((string[])["Read only", "Read and make changes"]);
    }

    [Test]
    public async Task Nothing_the_wizard_says_needs_gg_s_own_vocabulary()
    {
        // THE WORDS THIS CODE USES FOR ITSELF, which a person adding a token should
        // never have to learn: every step's question, its help, and its rows.
        string[] jargon =
            ["register", "subject", "locator", "sealed", "flight", "scope", "narrowest",
             "envelope", "principal", "holder", "esc drops"];

        var states = new List<AppState> { Opened() };
        var service = Choosing(Opened(), CredentialProviders.SonarCloud);
        var access = Next(service);
        var account = Next(access);
        var review = Next(account with
        {
            CredentialDraft = account.CredentialDraft! with { Identity = "kevin" },
        });
        states.AddRange([service, access, account, review, Choosing(Opened(), null)]);

        foreach (var state in states)
        {
            var text = string.Join("\n",
                [CredentialWizard.Said(state),
                 CredentialWizard.Help(state.CredentialDraft!.Step),
                 .. CredentialWizard.Rows(state)]);

            foreach (var word in jargon)
            {
                await Assert.That(text.Contains(word, StringComparison.OrdinalIgnoreCase)).IsFalse()
                    .Because($"'{word}' is gg's word, not a person's. On {state.CredentialDraft.Step}: {text}");
            }
        }
    }

    [Test]
    public async Task Something_else_offers_every_repository_in_gg_s_own_words()
    {
        var other = Choosing(Opened(), null);

        await Assert.That(CredentialWizard.Targets(other).Select(t => t.Named))
            .IsEquivalentTo((string[])["JDX/JDNext", "acme/tools"]);

        var access = Next(other);
        await Assert.That(CredentialWizard.Accesses(access).Select(a => a.Scope))
            .IsEquivalentTo((string[])[CredentialScopes.Read, CredentialScopes.Write]);
    }

    // ---- and the ways back ----

    [Test]
    public async Task Back_returns_one_step_and_escape_drops_the_whole_draft()
    {
        var access = Next(Choosing(Opened(), CredentialProviders.SonarCloud));

        await Assert.That(Keymap.Resolve(KeyStroke.LeftKey, KeymapContext.For(access)))
            .IsEqualTo(Command.WizardBack);
        await Assert.That(Reducer.Reduce(access, Command.WizardBack).CredentialDraft?.Step)
            .IsEqualTo(CredentialWizardStep.For);

        var closed = Reducer.Reduce(access, Command.CloseModal);
        await Assert.That(closed.Mode).IsEqualTo(UiMode.Normal);
        await Assert.That(closed.CredentialDraft).IsNull()
            .Because("a draft left behind would reopen half-answered, naming an account "
                   + "somebody decided not to use.");
    }

    [Test]
    public async Task The_draft_carries_no_secret()
    {
        // THE TOKEN IS HELD, NEVER MODELLED: AppState is written under
        // GG_STATE_DUMP and goes into the diagnostics bundle.
        var members = typeof(CredentialDraft).GetProperties().Select(p => p.Name.ToLowerInvariant());

        await Assert.That(members.Any(n => n.Contains("token", StringComparison.Ordinal)
                                        || n.Contains("secret", StringComparison.Ordinal)
                                        || n.Contains("password", StringComparison.Ordinal)))
            .IsFalse();
    }

    // ---- where the keyboard is ----

    [Test]
    public async Task Focus_goes_to_the_step_and_follows_it_when_the_step_turns()
    {
        // NOT THE MODAL: the wizard is its own dialog and the modal is hidden while it
        // shows, so focusing the modal would put the keyboard on a dialog nobody sees.
        await Assert.That(FocusChange.Wanted(
                UiMode.CredentialWizard, TabId.Credentials, null, modalHasFocus: false,
                wizardStep: CredentialWizardStep.Service))
            .IsEqualTo(FocusTarget.WizardStep);

        await Assert.That(FocusChange.Wanted(
                UiMode.CredentialWizard, TabId.Credentials, null, modalHasFocus: true,
                wizardStep: CredentialWizardStep.For,
                landedWizardStep: CredentialWizardStep.For))
            .IsEqualTo(FocusTarget.LeaveAlone)
            .Because("a render once a second must not pull the keyboard back to the top of "
                   + "a list somebody is moving along.");

        await Assert.That(FocusChange.Wanted(
                UiMode.CredentialWizard, TabId.Credentials, null, modalHasFocus: true,
                wizardStep: CredentialWizardStep.Account,
                landedWizardStep: CredentialWizardStep.For))
            .IsEqualTo(FocusTarget.WizardStep)
            .Because("the step turned while the wizard kept focus, and the keyboard has to "
                   + "follow to the fields the new step asks with.");
    }
}
