using Gg.Contracts;

namespace Gg.Client.Tests;

/// <summary>
/// The doctor asks whether YOUR credentials resolve here.
/// </summary>
/// <remarks>
/// <para>
/// <b>It used to ask about the tenant's.</b> Every registered credential had
/// to resolve on this machine or the check failed <c>Blocking</c>, telling the
/// reader to run <c>gg credential add</c> for each one named. That was already
/// arguable with one developer; the day a second registers one it is simply
/// wrong - their secret is on their laptop, as it should be, and this machine
/// is told it is broken.
/// </para>
/// <para>
/// <b>A credential is a person's, so the question is a person's.</b> Whose a
/// credential is travels as the subject a document spells them with - the pair
/// the board page sends - and never the principal id, which is the control
/// plane's own bookkeeping.
/// </para>
/// </remarks>
public class TheDoctorChecksYourOwnCredentialsTests
{
    private const string Mine = "a-directory:me-4471";
    private const string Theirs = "a-directory:dana-9920";

    private static CredentialSummary ACredential(string repo, string subject) => new()
    {
        CredentialId = Guid.NewGuid().ToString(),
        Repo = repo,
        AddedAt = DateTimeOffset.UnixEpoch,
        ReferencedBySubject = subject,
        Reference = new CredentialReference
        {
            Kind = CredentialKinds.Local,
            Locator = CredentialLocator.ForRepo(repo),
            Identity = "a-bot",
            Scopes = [CredentialScopes.Read],
        },
    };

    [Test]
    public async Task A_colleagues_credential_is_not_this_machines_to_hold()
    {
        // THE DAY A SECOND PERSON REGISTERS ONE. Their secret is on their
        // laptop, which is where it belongs, and nothing about that is a
        // fault on mine.
        var theirs = ACredential("acme/widgets", Theirs);

        await Assert.That(Doctor.IsYours(theirs, Mine)).IsFalse();
    }

    [Test]
    public async Task And_your_own_is()
    {
        await Assert.That(Doctor.IsYours(ACredential("acme/widgets", Mine), Mine)).IsTrue();
    }

    [Test]
    public async Task A_credential_that_says_whose_it_is_not_is_still_yours_to_answer_for()
    {
        // AN OLDER CONTROL PLANE SENDS NO SUBJECT, and every credential in
        // such a tenant was everybody's - so the check must keep asking about
        // it rather than falling silent. Absent is not "somebody else's".
        var older = ACredential("acme/widgets", subject: "");

        await Assert.That(Doctor.IsYours(older, Mine)).IsTrue()
            .Because("a reader that skipped what it was not told about would stop "
                   + "reporting the very thing this check exists for.");
    }
}
