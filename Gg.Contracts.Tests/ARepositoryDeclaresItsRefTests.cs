using Gg.Contracts;

namespace Gg.Contracts.Tests;

/// <summary>
/// A registered repository says which ref a flight is pinned to when the
/// flight's intent names none.
/// </summary>
/// <remarks>
/// <para>
/// <b>The hole is a flight about a work item.</b> A flight's repositories are
/// resolved from its intent URI — the URI contributes the slug and the ref, and
/// the registry contributes who the repository is. A ticket intent is a provider
/// and an id: it names no repository and therefore no ref, so the flight opens
/// with an empty working tree and the agent reports, correctly, that there is
/// nothing here to work on.
/// </para>
/// <para>
/// <b>The selection already exists and cannot close it alone.</b>
/// <c>FlightLaunchRequest.Repository</c> says which repository a flight is
/// about; what it cannot say is at which ref, and <c>FlightRepo.PinnedRef</c> is
/// required. Defaulting to <c>refs/heads/main</c> control-plane-side is exactly
/// the guess <c>FlightRepo</c> refuses in writing — <i>"a uri naming no ref
/// produces no repository rather than a guessed default branch"</i> — and it is
/// wrong on every repository whose trunk is called something else.
/// </para>
/// <para>
/// <b>So the registry declares it, once, where the rest of a repository's
/// identity already lives.</b> The entry is already the authority on provider,
/// forge id and credential — on WHO a repository is — and "which ref work starts
/// from" is the same kind of fact, known to whoever registers it and to nobody
/// downstream.
/// </para>
/// <para>
/// <b>Optional, because every registered repository predates it.</b> A registry
/// that declares no ref keeps behaving exactly as it does now: a flight whose
/// intent names a ref is unaffected, and a flight whose intent names none is
/// refused rather than silently given a branch somebody guessed.
/// </para>
/// </remarks>
public class ARepositoryDeclaresItsRefTests
{
    private static RegisterRepositoryRequest ARequest(string? declared) => new()
    {
        Name = "payments",
        Provider = "tracker",
        Id = "b7a1",
        Path = "acme/payments-service",
        Credential = "required",
        Ref = declared,
    };

    [Test]
    public async Task A_registration_can_declare_the_ref_work_starts_from()
    {
        // THE DEFECT, as a shape: there was nowhere to put this, so a ticket
        // flight had no ref and therefore no repository.
        await Assert.That(ARequest("refs/heads/main").Ref).IsEqualTo("refs/heads/main");
    }

    [Test]
    public async Task A_registration_that_declares_none_is_still_a_registration()
    {
        // THE ANCHOR, and it is most of the registry. Every repository
        // registered before this member existed carries no ref, and a required
        // member here would make them all unreadable at once.
        await Assert.That(ARequest(null).Ref).IsNull();
    }

    [Test]
    public async Task The_ref_reads_back_on_the_registered_repository()
    {
        // A declaration nothing can read is a declaration that silently does
        // nothing - the exact registered-not-invoked shape this repository
        // keeps finding. The control plane resolves flights from the read
        // model, so the read model has to carry it.
        var registered = new RepositoryRegistered
        {
            Name = "payments",
            Provider = "tracker",
            Id = "b7a1",
            Path = "acme/payments-service",
            Credential = "required",
            Ref = "refs/heads/trunk",
            RegisteredBy = "somebody",
            RegisteredAt = DateTimeOffset.UnixEpoch,
        };

        await Assert.That(registered.Ref).IsEqualTo("refs/heads/trunk");
    }

    [Test]
    public async Task A_bare_branch_name_is_refused_where_it_is_written()
    {
        // "main" is not a ref, it is a guess about which namespace somebody
        // meant - and a tag and a branch of one name are two different commits.
        // Refused HERE, by the author who can still fix it, rather than by a
        // clone six steps later that reports a ref this repository invented.
        await Assert.That(RepositoryRefs.Validate("main"))
            .IsNotNull()
            .Because("resolving a bare name means choosing a namespace on the author's behalf, "
                   + "which is the guess this whole member exists to remove.");
    }

    [Test]
    public async Task A_fully_qualified_ref_is_accepted_in_every_namespace_a_flight_uses()
    {
        // Branches and pull refs both, because a flight already pins
        // refs/pull/n/head on the BASE repository - that is what makes a fork
        // need no credential of its own, and a validator that knew only about
        // branches would break it.
        await Assert.That(RepositoryRefs.Validate("refs/heads/main")).IsNull();
        await Assert.That(RepositoryRefs.Validate("refs/pull/12/head")).IsNull();
        await Assert.That(RepositoryRefs.Validate("refs/tags/v1.0.0")).IsNull();
    }

    [Test]
    public async Task Declaring_nothing_is_not_an_error()
    {
        // Silence is not a malformed value. A registry that says nothing about
        // its ref has not said something wrong, and diagnosing it here would
        // refuse every repository registered to date.
        await Assert.That(RepositoryRefs.Validate(null)).IsNull();
    }
}
