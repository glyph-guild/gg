using Gg.Contracts.Description;
using Gg.Contracts;
using Gg.Runner.Vcs;

namespace Gg.Runner.Tests;

/// <summary>
/// The two doubles a push test needs: a remote that records, and a provider that
/// accepts a credential.
/// </summary>
/// <remarks>
/// <b>Promoted out of <c>TwoGateTests</c> when a second file needed them.</b> They
/// were private there and correctly so while one file used them; two files needing
/// the same double is the signal to have one rather than to reach into another
/// test's internals - and duplicating them would mean two ideas of what a remote
/// does, drifting apart on the first change to either.
/// </remarks>
internal sealed class RecordingDestination(bool pushSucceeds = true, bool proposeSucceeds = true)
    : IDestinationAdapter
{
    public string Provider { get; } = AuthenticatingProvider.Key;

    public List<string> Calls { get; } = [];

    public Task<PushOutcome> PushAsync(
        LandingRequest request, CancellationToken cancellationToken = default)
    {
        Calls.Add($"push:{request.Branch}");

        return Task.FromResult<PushOutcome>(pushSucceeds
            ? new PushOutcome.Pushed(request.Branch, new string('a', 40))
            : new PushOutcome.Refused(request.Slug, "the remote said no"));
    }

    public Task<LandingOutcome> ProposeAsync(
        LandingRequest request, CancellationToken cancellationToken = default)
    {
        Calls.Add($"propose:{request.Branch}");

        return Task.FromResult<LandingOutcome>(proposeSucceeds
            ? new LandingOutcome.Landed(request.Branch, "https://forge.invalid/pr/1", 1)
            : new LandingOutcome.Unsupported("no proposal"));
    }
}

/// <summary>
/// A provider that authenticates, wrapping the local one that does not.
/// </summary>
/// <remarks>
/// <b>Needed because the local adapter refuses a credential outright</b> - file://
/// has nothing to authenticate to, so a secret offered to it is a secret handed
/// to a path. That refusal is correct and it means a lease carrying a credential
/// for a local repository cannot even materialise, which is how the first run of
/// this file reached the adapter zero times and looked like a gating bug.
/// </remarks>
internal sealed class AuthenticatingProvider(LocalVcsAdapter inner) : IVcsAdapter
{
    internal const string Key = "fixture";

    public string Provider => Key;

    public VcsCapabilities Capabilities => inner.Capabilities;

    public RefResolution Resolve(string pinnedRef) => inner.Resolve(pinnedRef);

    // The secret is accepted and dropped: what matters here is that offering one
    // is legitimate for this provider, which is what makes the credential - and
    // therefore the write scope - meaningful.
    public Task<CloneOutcome> CloneAsync(
        RepoTarget target, string resolvedRef, string intoDirectory, string? secret,
        CancellationToken cancellationToken = default) =>
        inner.CloneAsync(target, resolvedRef, intoDirectory, secret: null, cancellationToken);

    public Task<string> FetchAlsoAsync(
        RepoTarget target, string resolvedRef, string intoDirectory, string? secret,
        CancellationToken cancellationToken = default) =>
        inner.FetchAlsoAsync(target, resolvedRef, intoDirectory, secret: null, cancellationToken);
}
