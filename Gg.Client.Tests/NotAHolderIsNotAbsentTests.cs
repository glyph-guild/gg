using Gg.Client;

namespace Gg.Client.Tests;

/// <summary>
/// A credential this machine holds no copy of is refused by naming the act that
/// would fix it — never reported as one that is not there.
/// </summary>
/// <remarks>
/// <para>
/// <b>S64.2-03, and the reason it is a criterion rather than a nicety.</b> After
/// step 2 the normal state of a credential on the machine that registered it is
/// <i>present and unopenable here</i>, which is a state nothing in gg had to
/// describe before. The two readings send a person to opposite places: "it is not
/// here" sends them to register it again, which would overwrite the one they
/// have; "this machine is not a holder" sends them to one command.
/// </para>
/// <para>
/// <b>The distinction already exists and is already written down</b> —
/// <c>SaidWhenNoHolder</c>'s remark calls collapsing the two <i>"an afternoon
/// spent on a file that was always fine"</i>, and <c>Read</c> returning
/// <c>null</c> is reserved for a credential that genuinely is not on the disk.
/// What is new is that the refusal must now name a LOCAL act, because the thing
/// to do about it is no longer only "ask somebody to push it".
/// </para>
/// <para>
/// <b>And <c>Holds</c> must keep saying yes.</b> It answers from the extension and
/// opens nothing, so it is the one thing on this path that is unaffected by who a
/// credential is sealed to — and a <c>Holds</c> that started saying no would make
/// <c>gg doctor</c> report a missing credential that is sitting right there.
/// </para>
/// </remarks>
public class NotAHolderIsNotAbsentTests
{
    private const string Locator = "local:acme/widgets";
    private const string Absent = "local:acme/never-registered";
    private const string Secret = "ghp-not-a-real-token";
    private const string Passphrase = "correct horse battery staple";

    private static string ATempDir(string tag) =>
        Path.Combine(Path.GetTempPath(), $"gg-{tag}-" + Guid.NewGuid().ToString("N"));

    private static FileCredentialStore ARegisteredCredential()
    {
        var store = new FileCredentialStore(
            ATempDir("store"),
            MachineKey.LoadOrCreate(Path.Combine(ATempDir("mk"), "machine-key")));

        var keyPath = Path.Combine(ATempDir("pk"), "person-key");
        PersonKey.Create(keyPath, Passphrase);
        store.Register(Locator, Secret, PersonKey.PublicHalfOf(keyPath));

        return store;
    }

    [Test]
    public async Task A_credential_sealed_to_its_person_is_refused_rather_than_missing()
    {
        var store = ARegisteredCredential();

        await Assert.That(() => store.Read(Locator)).Throws<CredentialUnavailableException>()
            .Because("null means 'not on this disk', and this one is on this disk. Returning null "
                   + "here would make a caller register it again over the top of itself.");
    }

    [Test]
    public async Task The_refusal_names_the_act_that_would_fix_it()
    {
        var store = ARegisteredCredential();

        var refused = Assert.Throws<CredentialUnavailableException>(() => store.Read(Locator));

        await Assert.That(refused!.Message).Contains("trust-this-machine")
            .Because("the act is one command and nobody can guess its name from a sentence about "
                   + "holders.");

        await Assert.That(refused.Message).DoesNotContain(Secret)
            .Because("a sentence about a credential is never a place to print one.");
    }

    [Test]
    public async Task And_does_not_say_the_credential_is_absent()
    {
        var store = ARegisteredCredential();

        var refused = Assert.Throws<CredentialUnavailableException>(() => store.Read(Locator));

        foreach (var wrong in (string[])["No secret", "not registered", "no credential for"])
        {
            await Assert.That(refused!.Message).DoesNotContain(wrong, StringComparison.OrdinalIgnoreCase)
                .Because($"'{wrong}' is what is said about a credential that is not there, and this "
                       + "one is. Said: " + refused.Message);
        }
    }

    [Test]
    public async Task Holds_still_says_yes()
    {
        var store = ARegisteredCredential();

        await Assert.That(store.Holds(Locator)).IsTrue()
            .Because("Holds answers from the extension and opens nothing, so who it is sealed to "
                   + "cannot change its answer - and gg doctor leans on that.");
    }

    [Test]
    public async Task A_credential_that_really_is_not_there_is_still_null()
    {
        // THE OTHER HALF, and the one a change like this quietly breaks: making
        // every unopenable credential throw is easy, and making a genuinely
        // absent one throw too would turn "you have not set this up yet" into an
        // error a person reads as a fault.
        var store = ARegisteredCredential();

        await Assert.That(store.Read(Absent)).IsNull()
            .Because("a missing secret is a diagnosis the caller makes - doctor reports it, the "
                   + "runner turns it into a flight-log event - not an exception.");
    }

    [Test]
    public async Task And_the_refusal_goes_away_when_the_act_is_performed()
    {
        // THE SENTENCE IS ACTIONABLE OR IT IS AN EXCUSE. A diagnosis naming a
        // command is worth nothing unless running that command silences it, and
        // this is the only test that checks the advice was true.
        var store = new FileCredentialStore(
            ATempDir("store"),
            MachineKey.LoadOrCreate(Path.Combine(ATempDir("mk"), "machine-key")));

        var keyPath = Path.Combine(ATempDir("pk"), "person-key");
        PersonKey.Create(keyPath, Passphrase);
        store.Register(Locator, Secret, PersonKey.PublicHalfOf(keyPath));

        var refused = Assert.Throws<CredentialUnavailableException>(() => store.Read(Locator));
        await Assert.That(refused!.Message).Contains("trust-this-machine");

        store.TrustThisMachine(Locator, PersonKey.Unlock(keyPath, Passphrase));

        await Assert.That(store.Read(Locator)).IsEqualTo(Secret)
            .Because("the refusal named an act; doing it has to be what makes the read work, or "
                   + "the sentence sent somebody somewhere that did not help.");
    }
}
