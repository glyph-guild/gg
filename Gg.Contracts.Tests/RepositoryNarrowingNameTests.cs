namespace Gg.Contracts.Tests;

/// <summary>
/// <c>&lt;repository&gt;/&lt;filename&gt;</c>: a third kind of name, composed from
/// two vocabularies with different rules.
/// </summary>
/// <remarks>
/// <para>
/// <b>Namespaced, and ADR-0018 § 7 says why.</b>
/// <c>.goodgrief/narrowings/pci.yaml</c> in <c>payments</c> is
/// <c>payments/pci</c> and never the tenant's <c>pci</c> — because without the
/// namespace a team shadows a compliance regime by choosing a filename, which
/// is a widening performed by a merge, and § 1 and § 2 exist to prevent exactly
/// that.
/// </para>
/// <para>
/// <b>The left half is the registry KEY, not the forge path.</b> A path is a
/// display label that may drift; the key is what envelopes and flights refer
/// to. ADR-0018 § 3 got this right in its own example — named by key, pinned by
/// id, resolved at commit — and a name built from the path would rename every
/// narrowing the day somebody renames a repository on the forge.
/// </para>
/// <para>
/// <b>Slice thirteen mapped name → path; this maps path → name</b>, and needs
/// the same guarantees in the other direction: total, injective, and defined
/// for every legal filename. It is a THIRD computation rather than a reuse,
/// because <c>AirspaceNames</c> governs one path component in a working copy
/// and would refuse every composed name on its first slash.
/// </para>
/// </remarks>
public class RepositoryNarrowingNameTests
{
    [Test]
    [Arguments("payments", "pci", "payments/pci")]
    [Arguments("acme/widgets", "pci", "acme/widgets/pci")]
    [Arguments("a", "b", "a/b")]
    public async Task A_key_and_a_document_compose_to_one_name(
        string repository, string document, string expected)
    {
        await Assert.That(RepositoryNarrowingNames.Compose(repository, document))
            .IsEqualTo(expected);
    }

    [Test]
    [Arguments("payments", "pci")]
    [Arguments("acme/widgets", "pci")]
    [Arguments("acme/widgets", "data-handling")]
    public async Task Every_composed_name_parses_back_to_what_made_it(
        string repository, string document)
    {
        // TOTAL AND INJECTIVE, asserted as a round trip: two different pairs
        // cannot reach one name if every name reaches back to one pair.
        var composed = RepositoryNarrowingNames.Compose(repository, document)!;

        await Assert.That(RepositoryNarrowingNames.TryParse(composed, out var back, out var doc))
            .IsTrue();
        await Assert.That(back).IsEqualTo(repository);
        await Assert.That(doc).IsEqualTo(document);
    }

    [Test]
    public async Task A_slug_shaped_key_parses_on_the_last_slash_rather_than_the_first()
    {
        // THE AMBIGUITY THAT WOULD OTHERWISE BE REAL. A registry key may hold a
        // slash - `acme/widgets` is a slug and the slash is what makes it one -
        // while a document name cannot, because it is one file in one
        // directory. So the LAST slash is the separator, and that single fact
        // is what keeps the mapping injective over keys that contain slashes.
        RepositoryNarrowingNames.TryParse("acme/widgets/pci", out var repository, out var document);

        await Assert.That(repository).IsEqualTo("acme/widgets");
        await Assert.That(document).IsEqualTo("pci");
    }

    [Test]
    [Arguments("pci")]
    [Arguments("root")]
    [Arguments("flight")]
    [Arguments("implement")]
    public async Task A_composed_name_can_never_equal_an_estate_name(string estate)
    {
        // FREE BY CONSTRUCTION, and that is the point of asserting it. An
        // estate name is one path component - AirspaceNames refuses a slash -
        // and a composed name always carries one. So shadowing a compliance
        // regime by choosing a filename is impossible rather than discouraged,
        // and `flight` stays reserved without anybody defending it here.
        await Assert.That(AirspaceNames.Invalid(estate)).IsNull()
            .Because($"'{estate}' has to be a legal estate name, or this proves nothing.");

        foreach (var repository in (string[])["payments", "acme/widgets"])
        {
            var composed = RepositoryNarrowingNames.Compose(repository, estate)!;

            await Assert.That(composed).IsNotEqualTo(estate);
            await Assert.That(AirspaceNames.Invalid(composed)).IsNotNull()
                .Because("a composed name is not a legal estate name, which is what makes the "
                       + "two vocabularies unable to collide at all.");
        }
    }

    [Test]
    [Arguments("pay@ments")]
    [Arguments("payments@v1")]
    public async Task A_key_carrying_the_version_separator_is_refused(string repository)
    {
        // THE ONE THE REGISTRY LETS THROUGH. A repository key refuses only
        // blank and newlines - deliberately, because a slug is not an estate
        // name - so it may hold an `@`. Versions are qualified `name@vN`, so a
        // composed name carrying one is a name nothing can parse a version out
        // of, and `payments@v1/pci@v3` is not a string anybody should have to
        // read twice.
        await Assert.That(RepositoryNarrowingNames.Invalid(repository)).IsNotNull();
        await Assert.That(RepositoryNarrowingNames.Compose(repository, "pci")).IsNull();
    }

    [Test]
    [Arguments("")]
    [Arguments("   ")]
    [Arguments("pay\nments")]
    [Arguments("/payments")]
    [Arguments("payments/")]
    public async Task A_key_that_cannot_form_half_a_name_is_refused(string repository)
    {
        await Assert.That(RepositoryNarrowingNames.Invalid(repository)).IsNotNull();
        await Assert.That(RepositoryNarrowingNames.Compose(repository, "pci")).IsNull();
    }

    [Test]
    [Arguments("pci/extra")]
    [Arguments("")]
    [Arguments("pc i")]
    [Arguments("PCI")]
    public async Task A_document_name_that_is_not_one_path_component_is_refused(string document)
    {
        // The right half IS an estate-shaped name, unlike the left: it is a
        // file stem a person chose, and holding it to the same rule is what
        // stops `payments/PCI` and `payments/pci` being two names for one
        // regime on a case-insensitive filesystem.
        await Assert.That(RepositoryNarrowingNames.Compose("payments", document)).IsNull();
    }

    [Test]
    public async Task Nothing_parses_out_of_a_name_with_no_separator()
    {
        await Assert.That(RepositoryNarrowingNames.TryParse("pci", out _, out _)).IsFalse();
        await Assert.That(RepositoryNarrowingNames.TryParse("", out _, out _)).IsFalse();
    }

    [Test]
    public async Task A_legal_key_says_so()
    {
        // The liveness half: a rule that refused everything would satisfy every
        // refusal above.
        foreach (var repository in (string[])["payments", "acme/widgets", "a"])
        {
            await Assert.That(RepositoryNarrowingNames.Invalid(repository)).IsNull();
        }
    }
}
