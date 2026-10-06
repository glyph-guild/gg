using Gg.Client;
using Gg.Contracts;

namespace Gg.Client.Tests;

/// <summary>
/// A credential for an agent, or a tracker, can be registered — not only pushed.
/// </summary>
/// <remarks>
/// <para>
/// <b>S64.4-02, and the asymmetry it closes.</b> <c>gg credential send</c> takes
/// <c>--repo</c> <b>or</b> <c>--agent</c>; <c>gg credential add</c> takes only
/// <c>--repo</c>. So a credential for an agent could be pushed to a machine and could
/// not be registered on the one it came from, which is how this fleet's tracker
/// credential ended up as <c>local:jdx/jdnext</c> — a repository-shaped locator for a
/// hosted tracker, because that was the only shape <c>add</c> could make.
/// </para>
/// <para>
/// <b>The wire member stops claiming to be a repository.</b>
/// <c>CredentialRegistrationRequest.Repo</c> and <c>CredentialSummary.Repo</c> are
/// both <c>required string</c>, documented as <i>"the repository this credential is
/// for"</i>, so until this step every registered credential asserted it was for a
/// repository whether or not it was. The member becomes <c>For</c> and carries what a
/// person named — a repository slug, an agent, a tracker — as they spelled it.
/// </para>
/// <para>
/// <b>Kept rather than deleted, deliberately, and it was close.</b> Nothing decides
/// anything on this value: the control plane never reads it (the lease grant keys on
/// the locator), and gg prints it. Deleting it would have been cheaper. What keeps it
/// is spelling — <c>ForRepo</c> lowercases and reduces, so the locator is lossy, and a
/// list that showed <c>acme/widgets</c> for a repository the forge calls
/// <c>Acme/Widgets</c> is a list somebody has to translate. The member's job is to say
/// what a person would say.
/// </para>
/// </remarks>
public class AnAgentCredentialCanBeRegisteredTests
{
    private const string Secret = "sk-ant-not-a-real-token";
    private const string Passphrase = "correct horse battery staple";

    private static string ATempDir(string tag) =>
        Path.Combine(Path.GetTempPath(), $"gg-{tag}-" + Guid.NewGuid().ToString("N"));

    private static FileCredentialStore AStore() =>
        new(ATempDir("store"),
            MachineKey.LoadOrCreate(Path.Combine(ATempDir("mk"), "machine-key")));

    private static string APersonsKey()
    {
        var path = Path.Combine(ATempDir("pk"), "person-key");
        PersonKey.Create(path, Passphrase);
        return path;
    }

    [Test]
    public async Task An_agents_credential_lands_under_the_agents_own_locator()
    {
        var store = AStore();
        var locator = CredentialLocator.For(CredentialSubjects.Agent, "claude");

        store.Register(locator, Secret, PersonKey.PublicHalfOf(APersonsKey()));

        await Assert.That(store.Holds(locator)).IsTrue()
            .Because("an agent's token is registered on the machine that minted it, which is the "
                   + "half `gg credential send --agent` could already do and `add` could not.");

        await Assert.That(locator).Contains(CredentialLocator.AgentSegment)
            .Because("and it lands in the agents' namespace rather than under a repository name "
                   + "somebody invented to get past the only verb that existed.");
    }

    [Test]
    public async Task A_trackers_credential_does_too()
    {
        var store = AStore();
        var locator = CredentialLocator.For(CredentialSubjects.Tracker, "jdnext");

        store.Register(locator, Secret, PersonKey.PublicHalfOf(APersonsKey()));

        await Assert.That(store.Holds(locator)).IsTrue();

        await Assert.That(locator).Contains(CredentialLocator.TrackerSegment)
            .Because("this fleet's tracker credential is local:jdx/jdnext today, which says "
                   + "repository and means tracker.");
    }

    [Test]
    public async Task A_registration_request_says_what_it_is_for_without_claiming_a_repository()
    {
        // THE WIRE MEMBER, asserted on the type rather than through a round trip,
        // because the claim is about what the contract SAYS. A member called Repo on
        // a request registering an agent's token is a lie whatever value it carries.
        var request = new CredentialRegistrationRequest
        {
            For = "claude",
            Reference = new CredentialReference
            {
                Kind = CredentialKinds.Local,
                Locator = CredentialLocator.For(CredentialSubjects.Agent, "claude"),
                Identity = "kdeenanauth",
                Scopes = [CredentialScopes.Read],
            },
        };

        await Assert.That(CredentialReference.Validate(request.Reference)).IsNull()
            .Because("an agent's reference is a well-formed reference, and nothing about "
                   + "registering one needed a repository.");

        await Assert.That(request.For).IsEqualTo("claude");
    }

    [Test]
    public async Task What_a_person_named_survives_as_they_spelled_it()
    {
        // WHY THE MEMBER IS KEPT RATHER THAN DELETED. ForRepo lowercases and reduces,
        // so the locator cannot answer this - and a list showing acme/widgets for a
        // repository the forge calls Acme/Widgets is one somebody has to translate.
        var request = new CredentialRegistrationRequest
        {
            For = "Acme/Widgets",
            Reference = new CredentialReference
            {
                Kind = CredentialKinds.Local,
                Locator = CredentialLocator.ForRepo("Acme/Widgets"),
                Identity = "kdeenanauth",
                Scopes = [CredentialScopes.Read],
            },
        };

        await Assert.That(request.Reference.Locator).IsEqualTo("local:acme/widgets")
            .Because("the locator is reduced so one repository spelled two ways is one credential.");

        await Assert.That(request.For).IsEqualTo("Acme/Widgets")
            .Because("and the member carries what a person would say, which is the only thing the "
                   + "locator cannot give back.");
    }

    [Test]
    public async Task The_summary_says_the_same_thing_the_request_did()
    {
        // TWO TYPES, ONE MEANING. The request and the summary both carried `Repo`; a
        // rename that moved one and not the other would leave a reader deriving the
        // same fact from two members that disagree about what it is.
        var summary = new CredentialSummary
        {
            CredentialId = "01a0632b-e971-7000-8000-000000000000",
            For = "jdnext",
            Reference = new CredentialReference
            {
                Kind = CredentialKinds.Local,
                Locator = CredentialLocator.For(CredentialSubjects.Tracker, "jdnext"),
                Identity = "kdeenanauth",
                Scopes = [CredentialScopes.Read],
            },
            ReferencedBySubject = "kevin@example.test",
            AddedAt = DateTimeOffset.UnixEpoch,
        };

        await Assert.That(summary.For).IsEqualTo("jdnext");

        await Assert.That(CredentialLocator.SubjectOf(summary.Reference.Locator))
            .IsEqualTo(CredentialSubjects.Tracker)
            .Because("and the subject is readable from the locator, so a reader never has to "
                   + "guess what the name beside it names.");
    }
}
