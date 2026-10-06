using Gg.Contracts;

namespace Gg.Client.Tests;

/// <summary>
/// `gg credential list` says how each credential rests on this machine, and
/// opens none of them to find out.
/// </summary>
/// <remarks>
/// <para>
/// <b>S60.1-01.</b> The list has always printed what the control plane holds —
/// repository, identity, scopes, locator — and nothing about the machine it is
/// running on. So the one question a person asks it, <i>"is this one here, and
/// is any of them still plaintext"</i>, was the one it could not answer, while
/// `gg doctor` answered it for the whole directory at once and for no credential
/// in particular.
/// </para>
/// <para>
/// <b>The word is the store's, not this renderer's</b> (slice sixty, rule 4).
/// <c>ProtectionFor</c> already returns a sentence, and a column cannot hold
/// one — the sealed sentence alone is about 180 characters, and five of those
/// is a wall rather than a list. The temptation is to derive a short word here
/// by looking for "sealed" in the sentence, and that is precisely the
/// <i>"two derivations that agree today"</i> hazard: the day the sentence is
/// reworded, this column starts lying about whether a secret is encrypted.
/// So the store answers both, from one place, and this asks.
/// </para>
/// <para>
/// <b>NOTHING HERE OPENS A CREDENTIAL, and that is asserted rather than
/// intended.</b> `gg doctor`'s own credentials row answers a very similar
/// question by calling <c>Read</c> — which decrypts every one of your
/// credentials and reseals the plaintext ones as a side effect. A list is read
/// far more often than a doctor, and a list that decrypted would pull every
/// secret on the machine into a process whose job is to print four columns.
/// </para>
/// </remarks>
public class CredentialListSaysHowEachRestsTests
{
    private static CredentialSummary ACredential(string repo, string locator) => new()
    {
        CredentialId = "01a0a21c-a32c-76e1-a716-ccbb19dda796",
        For = repo,
        AddedAt = DateTimeOffset.UnixEpoch,
        Reference = new CredentialReference
        {
            Kind = CredentialKinds.Local,
            Locator = locator,
            Identity = "acme-bot",
            Scopes = [CredentialScopes.Read],
        },
    };

    private static CredentialList AList(params CredentialSummary[] credentials) =>
        new() { Credentials = credentials };

    /// <summary>
    /// A store under a scratch root with its own machine key, so nothing here
    /// touches the developer's real store or seals with their real key.
    /// </summary>
    private static FileCredentialStore AScratchStore() =>
        new(Path.Combine(Path.GetTempPath(), "gg-resting-" + Guid.NewGuid().ToString("N")),
            MachineKey.LoadOrCreate(
                Path.Combine(Path.GetTempPath(), "gg-resting-k-" + Guid.NewGuid().ToString("N"), "k")));

    /// <summary>A file from before this machine sealed anything.</summary>
    private static void PlaceAPlaintext(FileCredentialStore store, string locator)
    {
        var path = store.PathFor(locator);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "ghp-from-before-sealing");
    }

    /// <summary>
    /// A store that answers how things rest and records every question, so a
    /// test can assert what was NOT asked.
    /// </summary>
    private sealed class AskedStore(Dictionary<string, string> resting) : ICredentialStore
    {
        public List<string> Opened { get; } = [];

        public List<string> AskedHowItRests { get; } = [];

        public string Root => "/nowhere";

        public string Protection => "a test's store";

        public string ProtectionFor(string locator) => "a sentence about " + locator;

        public string RestingOf(string locator)
        {
            AskedHowItRests.Add(locator);

            return resting.TryGetValue(locator, out var answer)
                ? answer
                : throw new ArgumentException($"'{locator}' is not a locator.", nameof(locator));
        }

        /// <summary>No envelope in a double; the holder join has its own tests.</summary>
        public IReadOnlyList<string> HoldersOf(string locator) => [];

        public string PathFor(string locator) => "/nowhere/" + locator;

        public void Write(string locator, string secret) { }

        public void Register(string locator, string secret, string holder) { }

        public void TrustThisMachine(string locator, IAgreeAsAHolder person) { }

        public void WriteSealed(string locator, SealedCredential envelope) { }

        public string? Read(string locator)
        {
            Opened.Add(locator);
            return "a secret nobody should have asked for";
        }

        public bool Holds(string locator) => resting.ContainsKey(locator);

        public bool Remove(string locator) => false;
    }

    [Test]
    public async Task Each_credential_says_how_it_rests_in_the_stores_own_words()
    {
        var store = new AskedStore(new()
        {
            ["local:acme/widgets"] = CredentialResting.Sealed,
            ["local:acme/legacy"] = CredentialResting.Plaintext,
        });

        var resting = CredentialsAtRest.For(
            AList(
                ACredential("acme/widgets", "local:acme/widgets"),
                ACredential("acme/legacy", "local:acme/legacy")).Credentials,
            store.RestingOf,
            store.HoldersOf);

        await Assert.That(CredentialsAtRest.RestingOf(resting, "local:acme/widgets"))
            .IsEqualTo(CredentialResting.Sealed);

        await Assert.That(CredentialsAtRest.RestingOf(resting, "local:acme/legacy"))
            .IsEqualTo(CredentialResting.Plaintext)
            .Because("a machine mid-migration holds both kinds, and the one still readable on "
                   + "disk is the whole reason somebody reads this column.");
    }

    [Test]
    public async Task Finding_out_opens_nothing()
    {
        // THE ASSERTION THIS CLASS EXISTS FOR. `gg doctor`'s credentials row
        // answers a near-identical question with Read, which decrypts every
        // credential on the machine and reseals the plaintext ones on the way
        // past. A list runs far more often and must not.
        var store = new AskedStore(new() { ["local:acme/widgets"] = CredentialResting.Sealed });

        _ = CredentialsAtRest.For(
            AList(ACredential("acme/widgets", "local:acme/widgets")).Credentials,
            store.RestingOf,
            store.HoldersOf);

        await Assert.That(store.Opened).IsEmpty()
            .Because("a column saying how a secret rests must not be produced by decrypting it. "
                   + $"Opened: {string.Join(", ", store.Opened)}");
    }

    [Test]
    public async Task A_locator_this_machine_cannot_place_is_said_rather_than_thrown()
    {
        // THE CONTROL PLANE'S LIST IS NOT THIS MACHINE'S TO VALIDATE. A locator
        // the local store refuses - malformed, or a scheme this build does not
        // know - reaches PathFor and throws ArgumentException. A list that let
        // that out would be a list that one bad row anywhere in the tenant
        // turns into a stack trace, and the other rows were fine.
        var store = new AskedStore(new() { ["local:acme/widgets"] = CredentialResting.Sealed });

        var resting = CredentialsAtRest.For(
            AList(
                ACredential("acme/widgets", "local:acme/widgets"),
                ACredential("acme/odd", "not a locator at all")).Credentials,
            store.RestingOf,
            store.HoldersOf);

        await Assert.That(CredentialsAtRest.RestingOf(resting, "local:acme/widgets"))
            .IsEqualTo(CredentialResting.Sealed)
            .Because("one unplaceable row must not take the readable ones with it.");

        await Assert.That(CredentialsAtRest.RestingOf(resting, "not a locator at all"))
            .IsEqualTo(CredentialResting.Unplaceable);
    }

    [Test]
    public async Task A_credential_nothing_said_about_is_not_said_to_be_here()
    {
        // ABSENCE IS NOT GOOD NEWS, which RepositoryCredentials already states
        // one layer over: a read that half-failed leaves this empty, and a
        // caller rendering that as "here" would be this project's recurring
        // failure in the one list about secrets.
        await Assert.That(CredentialsAtRest.RestingOf([], "local:acme/widgets"))
            .IsEqualTo(CredentialResting.NotKnown);
    }

    [Test]
    public async Task The_rendered_list_carries_the_word_for_every_row()
    {
        var list = AList(
            ACredential("acme/widgets", "local:acme/widgets"),
            ACredential("acme/legacy", "local:acme/legacy"));

        var text = VerbOutput.ToText(new VerbResult.Credentials(
            list,
            [
                new CredentialAtRest("local:acme/widgets", CredentialResting.Sealed, []),
                new CredentialAtRest("local:acme/legacy", CredentialResting.Plaintext, []),
            ]));

        await Assert.That(text).Contains(CredentialResting.Plaintext)
            .Because("the row that is still readable on disk is the one a person is looking for.");

        await Assert.That(text).Contains("acme/legacy");

        // AND THE VALUE IS STILL NOWHERE NEAR IT. The one thing this verb has
        // always been able to promise.
        await Assert.That(text).DoesNotContain("a secret nobody should have asked for");
    }

    [Test]
    public async Task The_store_is_the_authority_and_answers_through_its_interface()
    {
        // WHY THIS IS TYPED TO THE INTERFACE. Without it, green could satisfy
        // every assertion above with a helper that reads ProtectionFor's
        // sentence and looks for the word "sealed" in it - which passes today
        // and starts lying about whether a secret is encrypted the first time
        // somebody rewords the sentence. The word and the sentence have to come
        // out of one place, so the place is the store.
        var sealing = AScratchStore();
        ICredentialStore store = sealing;

        // Write seals, since step 2. So the sealed row is the ordinary one and
        // the plaintext row has to be placed by hand, the way one left over
        // from before the migration is.
        store.Write("local:acme/widgets", "a-secret");

        await Assert.That(store.RestingOf("local:acme/widgets")).IsEqualTo(CredentialResting.Sealed);

        PlaceAPlaintext(sealing, "local:acme/legacy");

        await Assert.That(store.RestingOf("local:acme/legacy"))
            .IsEqualTo(CredentialResting.Plaintext)
            .Because("the file this machine has not resealed yet is the one the migration is "
                   + "still about, and ProtectionFor already tells them apart without opening "
                   + "either.");

        await Assert.That(store.RestingOf("local:acme/never")).IsEqualTo(CredentialResting.NotHere);
    }

    [Test]
    public async Task A_vault_reference_rests_in_a_vault_rather_than_nowhere()
    {
        // THE TRAP THIS COLUMN WOULD OTHERWISE WALK INTO. Holds answers false
        // for every keyvault:// reference on purpose - "the only way to ask a
        // vault whether it has one is to read it" - so a column built on
        // presence reports every working vault credential as absent. And the
        // local store does worse than that: the locator never validates, so
        // PathFor throws and the whole list goes with it.
        ICredentialStore store = new MachineCredentialStore(
            AScratchStore(), new KeyVaultCredentialSource(new HttpClient()));

        await Assert.That(store.RestingOf("keyvault://acme-vault/jdapp-01"))
            .IsEqualTo(CredentialResting.InAVault)
            .Because("a credential that resolves perfectly every flight must not be reported as "
                   + "one nobody ever added.");

        await Assert.That(store.Holds("keyvault://acme-vault/jdapp-01")).IsFalse()
            .Because("this is the behaviour the assertion above exists to survive, not one to "
                   + "change: asking a vault means reading it.");
    }

    [Test]
    public async Task The_words_are_a_closed_set_and_every_one_is_distinct()
    {
        // A VOCABULARY, for CredentialStanding's reason: a column whose values
        // are assembled by whoever wrote the last branch is a column nobody can
        // filter or test against.
        string[] words =
        [
            CredentialResting.Sealed,
            CredentialResting.Plaintext,
            CredentialResting.InAVault,
            CredentialResting.NotHere,

            // ADDED AFTER DRIVING THE PANE, because "not here" read as "it is on
            // another machine" on a row where no credential exists at all - which
            // sends somebody looking for the machine that has it.
            CredentialResting.Nowhere,

            CredentialResting.Unplaceable,
            CredentialResting.NotKnown,
        ];

        await Assert.That(words.Distinct(StringComparer.Ordinal).Count()).IsEqualTo(words.Length);

        await Assert.That(CredentialResting.All.Count).IsEqualTo(words.Length)
            .Because("a word that is not in All is one no reader can enumerate, and this column "
                   + "is about to be rendered in a pane as well as here.");
    }
}
