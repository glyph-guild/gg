namespace Gg.Contracts.Tests;

/// <summary>
/// What a registered repository reports about itself.
/// </summary>
/// <remarks>
/// <para>
/// <b>Registered is not invoked.</b> A declaration a tenant can write and cannot
/// read back is one they cannot check, and the whole of ADR-0018 § 2 rests on a
/// person being able to see which repositories may speak. <c>gg airspace pull</c>
/// renders this list.
/// </para>
/// <para>
/// <b>Nullable here and nullable on the request, unlike <c>Credential</c>.</b>
/// That member is required on the way out because an absent declaration and a
/// declared <c>required</c> are the same fact. This one is the opposite: absent
/// and declared are DIFFERENT facts — off versus on — so the absence has to
/// survive the round trip as an absence.
/// </para>
/// </remarks>
public class RepositoryReadBackTests
{
    private static RepositoryRegistered Registered(string? narrowings) => new()
    {
        Name = "payments",
        Provider = "forge.example",
        Id = "F_payments01",
        Path = "acme/payments-service",
        Credential = RepositoryCredentialModes.Required,
        Narrowings = narrowings,
        RegisteredBy = "an-architect",
        RegisteredAt = new DateTimeOffset(2026, 8, 27, 12, 0, 0, TimeSpan.Zero),
    };

    [Test]
    public async Task A_declared_directory_reads_back()
    {
        await Assert.That(Registered(".goodgrief/narrowings/").Narrowings)
            .IsEqualTo(".goodgrief/narrowings/");
    }

    [Test]
    public async Task Off_reads_back_as_off_rather_than_as_a_blank()
    {
        // The distinction the whole member exists to preserve. A reader that
        // cannot tell "declares nothing" from "declares the root of the tree"
        // cannot tell a repository that is not governed from one whose every
        // file is policy.
        await Assert.That(Registered(null).Narrowings).IsNull();
    }

    [Test]
    public async Task The_request_and_the_answer_agree_about_what_off_looks_like()
    {
        // Both nullable, deliberately, and NOT the arrangement Credential has
        // one member up: there an absent declaration and a declared value are
        // the same fact, so the answer is required. Here they are different
        // facts, so the answer is not.
        await Assert.That(typeof(RegisterRepositoryRequest)
            .GetProperty(nameof(RegisterRepositoryRequest.Narrowings))!.PropertyType)
            .IsEqualTo(typeof(string));
        await Assert.That(typeof(RepositoryRegistered)
            .GetProperty(nameof(RepositoryRegistered.Narrowings))!.PropertyType)
            .IsEqualTo(typeof(string));

        await Assert.That(RepositoryNarrowings.Invalid(Registered(null).Narrowings)).IsNull()
            .Because("whatever the answer carries for 'off' must be a legal declaration to "
                   + "send straight back, or a round trip through pull and apply refuses "
                   + "what it was just told.");
    }
}
