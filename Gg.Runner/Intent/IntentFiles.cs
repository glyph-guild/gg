using System.Text;
using Gg.Contracts;
using Gg.Runner.Execution;
using Gg.Runner.Vcs;

namespace Gg.Runner.Intent;

/// <summary>What reading a flight's file intent concluded.</summary>
public abstract record IntentFileOutcome
{
    private IntentFileOutcome()
    {
    }

    /// <summary>The flight's intent is not a file, so there was nothing to read.</summary>
    public sealed record None : IntentFileOutcome;

    /// <summary>The file, as the agent will be given it, and the fact recording what was read.</summary>
    public sealed record Read(RequestedIntentFile File, IntentRead Fact) : IntentFileOutcome;

    /// <summary>Why the flight ends before the agent runs, in a sentence a person can act on.</summary>
    public sealed record Refused(string Diagnosis) : IntentFileOutcome;

    /// <summary>The one value for a flight whose intent is not a file.</summary>
    public static IntentFileOutcome Nothing { get; } = new None();
}

/// <summary>
/// Reads a flight's file intent before the loop starts. Slice sixty-two.
/// </summary>
/// <remarks>
/// <para>
/// <b>The runner reads it, at a ref it resolves, with the customer's credential.</b> The
/// control plane reads no repository bytes, so the commit this records is the only answer to
/// which words the flight was given - and a plan's legs are opened against that commit, not the
/// ref.
/// </para>
/// <para>
/// <b>Through the one reader</b>, so the path rule, the cache and the object id are the ones a
/// sweep's skill already gets.
/// </para>
/// </remarks>
public static class IntentFiles
{
    /// <summary>
    /// The most an intent file may be, in bytes.
    /// </summary>
    /// <remarks>
    /// Sized to a long slice (sixty-one is about 17 KiB) and well inside one context. One
    /// constant, so a walk that hits it moves it with a measurement attached.
    /// </remarks>
    public const int MaxIntentBytes = 64 * 1024;

    /// <summary>What reading this lease's file intent concluded.</summary>
    /// <param name="reader">The runner's one reader, or null when it was started without one.</param>
    /// <param name="secretsByLocator">The lease's credentials, resolved - the map a checkout uses.</param>
    public static async Task<IntentFileOutcome> ReadAsync(
        RepositoryFileReader? reader,
        LeaseGranted lease,
        IReadOnlyDictionary<string, string> secretsByLocator,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(lease);
        ArgumentNullException.ThrowIfNull(secretsByLocator);

        if (lease.IntentRepository is not { Length: > 0 } repository
            || lease.IntentPath is not { Length: > 0 } path)
        {
            return IntentFileOutcome.Nothing;
        }

        if (reader is null)
        {
            return new IntentFileOutcome.Refused(
                $"This flight's intent is {path} in {repository}, and this runner was started "
              + "without a reader for repository files, so it was not read.");
        }

        if (lease.IntentRepositoryProvider is not { Length: > 0 } provider)
        {
            return new IntentFileOutcome.Refused(
                $"This flight's intent is {path} in {repository}, and the lease does not say "
              + "which forge that repository is on, so no adapter could read it.");
        }

        // THE CHECKOUT'S OWN LOOKUP: the locator the contract derives from the slug, so the
        // credential that reads this file is the one `gg credential add` registered for it.
        var target = new RepoTarget
        {
            Provider = provider,
            Slug = repository,
            // ABSENT IS THE DEFAULT BRANCH, as it is for a checkout. HEAD is what a remote
            // advertises as its default, and the commit it names is what is recorded.
            PinnedRef = lease.IntentRef is { Length: > 0 } named ? named : "HEAD",
        };

        var read = await reader.WithSecrets(SecretFrom(secretsByLocator))
            .ReadAsync(target, path, "this flight's intent", cancellationToken);

        if (read is not FileRead.Read { File: var file, Commit: var commit })
        {
            return new IntentFileOutcome.Refused(((FileRead.Unreadable)read).Diagnosis);
        }

        var bytes = Encoding.UTF8.GetByteCount(file.Content);

        // REFUSED WHOLE, never cut: an agent reading half a plan cannot tell that it read half,
        // and a plan cut at its middle is one with legs nobody can see.
        if (bytes > MaxIntentBytes)
        {
            return new IntentFileOutcome.Refused(
                $"{path} in {repository} is {bytes:N0} bytes, and an intent is at most "
              + $"{MaxIntentBytes:N0}. It is refused whole rather than cut, because an agent "
              + "reading half of it could not tell that it had.");
        }

        return new IntentFileOutcome.Read(
            new RequestedIntentFile(repository, path, commit, file.Content),
            new IntentRead
            {
                Repository = repository,
                Path = path,
                RequestedRef = lease.IntentRef,
                Commit = commit,
                FileSha = file.BlobSha,
                ByteSize = bytes,
            });
    }

    /// <summary>The secret for a repository, by the locator its credential was registered under.</summary>
    public static Func<RepoTarget, Task<string?>> SecretFrom(
        IReadOnlyDictionary<string, string> secretsByLocator) =>
        target => Task.FromResult(
            secretsByLocator.GetValueOrDefault(CredentialLocator.ForRepo(target.Slug)));
}
