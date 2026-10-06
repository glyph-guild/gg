using Gg.Contracts;

namespace Gg.Contracts.Tests;

/// <summary>
/// A credential is registered for what it is actually for, and a repository is one
/// kind of that rather than the only one.
/// </summary>
/// <remarks>
/// <para>
/// <b>S64.4-01, and the gap the owner found by using the product:</b> <i>"when i go
/// to add a credential, it immediately asks me for the repository. credentials may be
/// used for other things than repositories."</i>
/// </para>
/// <para>
/// <b>The product currently instructs the lie.</b> This fleet's tracker credential is
/// <c>local:jdx/jdnext</c> — a repository-shaped locator for an Azure DevOps tracker
/// — because <c>CredentialLocator.ForRepo</c> is the only producer of a locator and
/// <c>gg credential add</c> is the only verb that registers one. The locator
/// vocabulary already knew better: <c>ForAgent</c> has sat beside <c>ForRepo</c> since
/// slice fifty-nine, and only <c>add</c> could not name one.
/// </para>
/// <para>
/// <b>A CLOSED VOCABULARY, refused loudly when unknown</b> — ADR-0027's rule for
/// <c>kind</c> and <c>StrategyKinds</c>' precedent before it. A subject gg does not
/// know is not narrowed to a repository and is not accepted as a free string: it is
/// refused naming the ones that exist, because a credential filed under a kind
/// nothing resolves is one a flight discovers is missing.
/// </para>
/// <para>
/// <b>And the reserved namespaces are the load-bearing part.</b> A slug reduces
/// through the same alphabet a locator validates, so <c>ForRepo</c> is the only place
/// the derivations can be kept apart — the existing comment says it outright: <i>"A
/// repository's locator named for an agent would read the credential a tracker
/// owns."</i> Written before a tracker existed, and now it does.
/// </para>
/// </remarks>
public class ACredentialSaysWhatItIsForTests
{
    [Test]
    public async Task The_subjects_are_a_closed_vocabulary()
    {
        await Assert.That(CredentialSubjects.All).IsEquivalentTo(new[]
        {
            CredentialSubjects.Repository,
            CredentialSubjects.Agent,
            CredentialSubjects.Tracker,
        })
            .Because("a subject gg cannot resolve is one a flight finds out about. Adding one is a "
                   + "deliberate act with a locator producer beside it, not a string somebody passes.");
    }

    [Test]
    public async Task Each_subject_has_a_locator_producer_and_they_do_not_collide()
    {
        var repo = CredentialLocator.ForRepo("acme/widgets");
        var agent = CredentialLocator.ForAgent("claude");
        var tracker = CredentialLocator.ForTracker("jdnext");

        foreach (var locator in (string[])[repo, agent, tracker])
        {
            await Assert.That(CredentialLocator.Validate(locator)).IsNull()
                .Because($"'{locator}' is produced by this contract, so this contract must accept it.");
        }

        await Assert.That(new[] { repo, agent, tracker }.Distinct().Count()).IsEqualTo(3)
            .Because("three subjects named the same thing must land in three files - one locator "
                   + "serving two of them is one credential overwriting another.");
    }

    [Test]
    public async Task A_repository_cannot_be_named_into_a_reserved_namespace()
    {
        // THE GUARD THAT MATTERS, extended. ForRepo already refused a slug beginning
        // `agent` because a repository under that owner would share a file with an
        // agent's token. A tracker's namespace needs the same refusal on the day it
        // arrives, not on the day somebody registers github.com/tracker/something.
        foreach (var reserved in (string[])[CredentialLocator.AgentSegment, CredentialLocator.TrackerSegment])
        {
            var refused = Assert.Throws<ArgumentException>(
                () => CredentialLocator.ForRepo($"{reserved}/anything"));

            await Assert.That(refused!.Message).Contains(reserved)
                .Because($"'{reserved}' is reserved and the refusal has to say which word is the "
                       + "problem, or somebody renames the repository at random.");
        }
    }

    [Test]
    public async Task And_the_reserved_namespaces_are_reached_through_the_reduced_alphabet_too()
    {
        // A SLUG REDUCES BEFORE IT IS COMPARED, which is the subtle half: `Tracker`
        // and `TRACKER` both lowercase into the reserved word, and a guard that
        // compared the raw input would pass them through.
        foreach (var spelling in (string[])["Tracker/x", "TRACKER/x", "tracker/x"])
        {
            await Assert.That(() => CredentialLocator.ForRepo(spelling))
                .Throws<ArgumentException>()
                .Because($"'{spelling}' reduces into the tracker namespace, and the comparison "
                       + "happens after the reduction or it happens for nothing.");
        }
    }

    [Test]
    public async Task An_unknown_subject_is_refused_naming_the_ones_that_exist()
    {
        var refused = CredentialSubjects.Refuse("vault");

        await Assert.That(refused).IsNotNull()
            .Because("'vault' is not a subject a credential can be registered for, and a subject "
                   + "gg does not know must not be narrowed to a repository.");

        foreach (var known in CredentialSubjects.All)
        {
            await Assert.That(refused!).Contains(known)
                .Because("the refusal names every subject that exists, because a person who "
                       + "guessed wrong has no other way to find out. Said: " + refused);
        }
    }

    [Test]
    public async Task A_known_subject_is_not_refused()
    {
        // THE SILENCE, which a vocabulary check needs as much as the alarm: one that
        // also refused the kinds it knows would be a check people learn to route
        // around.
        foreach (var known in CredentialSubjects.All)
        {
            await Assert.That(CredentialSubjects.Refuse(known)).IsNull()
                .Because($"'{known}' is in All, so refusing it would make the vocabulary a thing "
                       + "to work around rather than a thing to read.");
        }
    }

    [Test]
    public async Task A_subject_says_which_locator_producer_is_its_own()
    {
        // ONE PLACE THAT MAPS THEM, because two would be the hazard Credentials.cs is
        // written to prevent: "two derivations that agree today is how a runner ends
        // up looking for a file the CLI never wrote."
        await Assert.That(CredentialLocator.For(CredentialSubjects.Repository, "acme/widgets"))
            .IsEqualTo(CredentialLocator.ForRepo("acme/widgets"));

        await Assert.That(CredentialLocator.For(CredentialSubjects.Agent, "claude"))
            .IsEqualTo(CredentialLocator.ForAgent("claude"));

        await Assert.That(CredentialLocator.For(CredentialSubjects.Tracker, "jdnext"))
            .IsEqualTo(CredentialLocator.ForTracker("jdnext"));
    }

    [Test]
    public async Task And_the_subject_of_a_locator_can_be_read_back()
    {
        // THE OTHER DIRECTION, which is what a doctor sentence and a credential list
        // need: given a locator somebody registered, which verb names it.
        await Assert.That(CredentialLocator.SubjectOf(CredentialLocator.ForAgent("claude")))
            .IsEqualTo(CredentialSubjects.Agent);

        await Assert.That(CredentialLocator.SubjectOf(CredentialLocator.ForTracker("jdnext")))
            .IsEqualTo(CredentialSubjects.Tracker);

        await Assert.That(CredentialLocator.SubjectOf(CredentialLocator.ForRepo("acme/widgets")))
            .IsEqualTo(CredentialSubjects.Repository)
            .Because("a repository is what is left when no namespace claims it, which is why the "
                   + "reserved words have to be refused on the way in.");
    }
}
