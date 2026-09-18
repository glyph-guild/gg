using Gg.Contracts;
using Gg.Runner.Vcs;

namespace Gg.Runner.Pools;

/// <summary>What fetching a build's recipe produced.</summary>
public abstract record RecipeFetch
{
    private RecipeFetch()
    {
    }

    /// <summary>The recipe directory on disk - the whole build context - and the commit it came from.</summary>
    public sealed record Fetched(string Directory, string Commit) : RecipeFetch;

    /// <summary>Why nothing was fetched, in our words; never a url or an exception's own text.</summary>
    public sealed record Refused(string Diagnosis) : RecipeFetch;
}

/// <summary>
/// Where a build's recipe comes from (slice forty-one).
/// </summary>
/// <remarks>
/// A port, so the maintainer can be told what a fetch produced without a
/// repository - and so a runner started without one says so rather than
/// building nothing in silence.
/// </remarks>
public interface IRecipeSource
{
    /// <summary>The recipe at the commit its ref resolves to, inside <paramref name="scratchDirectory"/>.</summary>
    Task<RecipeFetch> FetchAsync(
        PoolRecipe recipe, string scratchDirectory, CancellationToken cancellationToken = default);
}

/// <summary>
/// Fetches a recipe through the clone every flight already uses.
/// </summary>
/// <remarks>
/// <para>
/// <b>The clone, not a new read on the port</b> - found building step 3. The
/// VCS port reads one file, and a build needs a directory on disk; the clone
/// makes exactly that, and it already reports the commit its ref landed on,
/// which is the record rule 3 asks for.
/// </para>
/// <para>
/// <b>The context is the recipe directory and nothing else</b> (rule 10). The
/// path is refused before anything is fetched when it could climb out, and the
/// directory handed back is checked to be inside the clone once it exists.
/// </para>
/// <para>
/// <b>A credential only where the operator declared the host</b> (rule 16): the
/// adapters are this machine's declared ones, so a provider with none is refused
/// by name, and the credential is asked for by the repository's own key.
/// </para>
/// </remarks>
/// <param name="adapters">This machine's VCS adapters, keyed by the provider each serves.</param>
/// <param name="secretFor">The machine's credential for a repository, or null where it needs none.</param>
public sealed class GitRecipeSource(
    IReadOnlyList<IVcsAdapter> adapters,
    Func<RepoTarget, string?> secretFor) : IRecipeSource
{
    private readonly IReadOnlyList<IVcsAdapter> _adapters = adapters;
    private readonly Func<RepoTarget, string?> _secretFor = secretFor;

    public async Task<RecipeFetch> FetchAsync(
        PoolRecipe recipe, string scratchDirectory, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        var repository = recipe.Repository;
        var dockerfile = recipe.Dockerfile ?? "Dockerfile";

        // BEFORE ANYTHING IS FETCHED. Validated at authoring too, and checked
        // again here: this machine does not trust that the document it was
        // handed passed a gate.
        if (Climbs(recipe.Path) || Climbs(dockerfile))
        {
            return new RecipeFetch.Refused(
                $"the recipe's path '{recipe.Path}' or Dockerfile '{dockerfile}' could leave the "
              + "repository, so nothing was fetched.");
        }

        if (_adapters.FirstOrDefault(a =>
                string.Equals(a.Provider, repository.Provider, StringComparison.Ordinal)) is not { } adapter)
        {
            return new RecipeFetch.Refused(
                $"this runner serves no repositories from '{repository.Provider}', so it cannot "
              + $"fetch the recipe in {repository.Slug}. The provider is declared in GG_VCS_HOSTS.");
        }

        var target = new RepoTarget
        {
            Provider = repository.Provider,
            Slug = repository.Slug,
            PinnedRef = repository.PinnedRef,
        };

        if (adapter.Resolve(target.PinnedRef) is not RefResolution.Ref resolved)
        {
            return new RecipeFetch.Refused(
                $"'{repository.PinnedRef}' is not a ref this runner can fetch from {repository.Slug}.");
        }

        var clone = Path.Combine(scratchDirectory, "repository");
        CloneOutcome cloned;

        try
        {
            Directory.CreateDirectory(scratchDirectory);
            cloned = await adapter.CloneAsync(
                target, resolved.Value, clone, _secretFor(target), cancellationToken);
        }
        catch (Exception failed) when (failed is InvalidOperationException
                                          or VcsCapabilityException
                                          or IOException)
        {
            // OUR WORDS, NOT THE EXCEPTION'S - a git error can name a url, and a
            // url can carry a credential.
            return new RecipeFetch.Refused(
                $"'{repository.PinnedRef}' could not be fetched from {repository.Slug}.");
        }

        var directory = Path.GetFullPath(Path.Combine(clone, recipe.Path));
        var root = Path.GetFullPath(clone) + Path.DirectorySeparatorChar;

        if (!directory.StartsWith(root, StringComparison.Ordinal) || !Directory.Exists(directory))
        {
            return new RecipeFetch.Refused(
                $"{repository.Slug} holds no directory '{recipe.Path}' at {cloned.HeadCommit}, so "
              + "there is no recipe to build.");
        }

        if (!File.Exists(Path.Combine(directory, dockerfile)))
        {
            return new RecipeFetch.Refused(
                $"'{recipe.Path}' in {repository.Slug} at {cloned.HeadCommit} has no {dockerfile}, "
              + "so there is nothing to build.");
        }

        return new RecipeFetch.Fetched(directory, cloned.HeadCommit);
    }

    private static bool Climbs(string path) =>
        path.StartsWith('/')
        || path.Contains('\\', StringComparison.Ordinal)
        || path.Split('/').Contains("..", StringComparer.Ordinal);
}

/// <summary>What a build produced.</summary>
public abstract record ImageBuilt
{
    private ImageBuilt()
    {
    }

    /// <summary>The image the daemon made.</summary>
    public sealed record Built(string ImageId) : ImageBuilt;

    /// <summary>
    /// Why it did not build: a sentence that may cross, and the daemon's own
    /// detail, which may not.
    /// </summary>
    /// <param name="Diagnosis">Ours, and safe to attest.</param>
    /// <param name="Detail">
    /// The daemon's words, which echo the recipe's own lines. For this machine's
    /// log and nowhere else - the control plane never holds a recipe's words.
    /// </param>
    public sealed record Failed(string Diagnosis, string? Detail) : ImageBuilt;
}

/// <summary>What pushing a built image produced.</summary>
public abstract record ImagePushed
{
    private ImagePushed()
    {
    }

    /// <summary>The digest the registry answered with, which is what a pin can name.</summary>
    public sealed record Pushed(string Digest) : ImagePushed;

    /// <summary>Why it did not push, in the same two halves as a failed build.</summary>
    public sealed record Failed(string Diagnosis, string? Detail) : ImagePushed;
}

/// <summary>
/// Builds an image from a directory and pushes it (slice forty-one).
/// </summary>
/// <remarks>
/// Its own port rather than two more members on <see cref="IPoolAdapter"/>, which
/// every pool double would then have to implement for an act none of them is
/// about. <see cref="DockerPoolAdapter"/> is both, through the same proxy.
/// </remarks>
public interface IImageBuilder
{
    Task<ImageBuilt> BuildAsync(
        string context, string dockerfile, string tag, IReadOnlyDictionary<string, string> labels,
        CancellationToken cancellationToken = default);

    /// <remarks>
    /// <b>Not <c>PushAsync</c>, and the name is load-bearing.</b> That name is the
    /// destination port's - writing a customer's repository - which one test
    /// holds to one declaration, its implementations and one caller. This pushes
    /// an image to the pool host's own registry, a different act behind a
    /// different gate, and sharing the name would hide a second repository write
    /// among these.
    /// </remarks>
    Task<ImagePushed> PushImageAsync(string repository, string tag, CancellationToken cancellationToken = default);
}
