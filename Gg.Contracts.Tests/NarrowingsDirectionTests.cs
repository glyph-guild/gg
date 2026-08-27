namespace Gg.Contracts.Tests;

/// <summary>
/// What a repository may declare as its narrowings directory, refused where an
/// author can still do something about it.
/// </summary>
/// <remarks>
/// <para>
/// <b>The declaration is the tap and the containment at once.</b> ADR-0018 § 2
/// splits the two authorities: Airspace decides whether a repository may speak,
/// git decides what it says. This is the first half's payload — one path, under
/// which every file is a narrowing — and it is also the only thing bounding what
/// the control plane will ever ask a forge for, because a forge has no
/// path-scoped read - an installation token that can read one file can read
/// every file.
/// </para>
/// <para>
/// <b>So a path that could reach outside the directory is refused here.</b> Not
/// at the fetch: a declaration is applied through a gate by a person, and a
/// refusal a person sees while writing it is worth more than one a flight
/// discovers later. Same reason <c>provenance</c> is refused in the parser and
/// not in <c>Validate</c>, one document class over.
/// </para>
/// <para>
/// <b>Null is off, and off is not the same as empty.</b> A repository that
/// declares nothing contributes nothing and never halts. A repository that
/// declares a directory contributes or halts — and never quietly does neither,
/// which is ADR-0018 § 6 read at the moment of authoring rather than at the
/// moment of flight.
/// </para>
/// </remarks>
public class NarrowingsDirectionTests
{
    [Test]
    [Arguments(".goodgrief/narrowings/")]
    [Arguments("policy/")]
    [Arguments("a/deeply/nested/place/")]
    [Arguments(".config/gg/narrowings/")]
    public async Task A_relative_directory_inside_the_repository_is_accepted(string declared)
    {
        await Assert.That(RepositoryNarrowings.Invalid(declared)).IsNull();
    }

    [Test]
    public async Task Null_is_off_and_is_not_a_refusal()
    {
        // The layer being OFF is the normal state and must not read as a
        // malformed declaration - a tenant with no repository-resident
        // narrowings anywhere is every tenant today.
        await Assert.That(RepositoryNarrowings.Invalid(null)).IsNull();
    }

    [Test]
    public async Task Blank_is_a_refusal_rather_than_a_second_spelling_of_off()
    {
        // THE DISTINCTION THAT MATTERS. Null is a decision not to declare;
        // empty string is a declaration of nothing, which would be a repository
        // whose narrowings live at the root of the tree - every file in the
        // repository read as policy. Collapsing them would make a client that
        // sends "" for an absent field turn the tap on.
        await Assert.That(RepositoryNarrowings.Invalid("")).IsNotNull();
        await Assert.That(RepositoryNarrowings.Invalid("   ")).IsNotNull();
    }

    [Test]
    [Arguments("../secrets/")]
    [Arguments(".goodgrief/../../etc/")]
    [Arguments("a/../../b/")]
    public async Task A_path_that_could_climb_out_of_the_repository_is_refused(string declared)
    {
        // The live-probe regression NameRuleTests already carries, one noun
        // over: somebody will try this, and the read this declaration bounds is
        // the only content call the control plane has.
        var refusal = RepositoryNarrowings.Invalid(declared);

        await Assert.That(refusal).IsNotNull();
        await Assert.That(refusal!).Contains("..")
            .Because("a refusal that does not name what it objected to leaves an author "
                   + "guessing which character was the problem.");
    }

    [Test]
    [Arguments("/etc/narrowings/")]
    [Arguments("//host/share/")]
    public async Task An_absolute_path_is_refused(string declared)
    {
        await Assert.That(RepositoryNarrowings.Invalid(declared)).IsNotNull()
            .Because("a declaration names a place inside the repository, and an absolute path "
                   + "names a place on whatever machine reads it.");
    }

    [Test]
    public async Task A_backslash_is_refused_rather_than_normalised()
    {
        // Normalising would make the declaration mean one thing here and
        // another at the forge, which serves paths with forward slashes and no
        // opinion about Windows.
        await Assert.That(RepositoryNarrowings.Invalid(@"a\b\")).IsNotNull();
    }

    [Test]
    public async Task A_file_rather_than_a_directory_is_refused()
    {
        // ADR-0018 § 7 decided a DIRECTORY, and the reason is CODEOWNERS: it
        // grants per path, so two concerns sharing one file share one owner and
        // the enforcement mechanism the whole feature leans on becomes
        // decoration. A declaration that names a file is that decision undone
        // by a missing character.
        var refusal = RepositoryNarrowings.Invalid(".goodgrief/narrowings.yaml");

        await Assert.That(refusal).IsNotNull();
        await Assert.That(refusal!).Contains("directory");
    }

    [Test]
    public async Task A_control_character_is_refused()
    {
        await Assert.That(RepositoryNarrowings.Invalid(".goodgrief/nar\nrowings/")).IsNotNull();
    }

    [Test]
    public async Task Every_refusal_says_something_a_person_can_act_on()
    {
        // A guard whose refusals are all the same sentence is a boolean wearing
        // a string.
        string?[] refusals =
        [
            RepositoryNarrowings.Invalid(""),
            RepositoryNarrowings.Invalid("../x/"),
            RepositoryNarrowings.Invalid("/x/"),
            RepositoryNarrowings.Invalid("x"),
        ];

        foreach (var refusal in refusals)
        {
            await Assert.That(refusal).IsNotNull();
            await Assert.That(refusal!.Length).IsGreaterThan(30);
        }

        await Assert.That(refusals.Distinct(StringComparer.Ordinal).Count())
            .IsEqualTo(refusals.Length)
            .Because("four different objections that read identically send an author to "
                   + "read our source instead of their own declaration.");
    }
}
