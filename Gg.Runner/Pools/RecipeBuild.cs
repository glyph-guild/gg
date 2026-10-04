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

    /// <summary>
    /// Remove this repository's images that nothing is pinned to and no
    /// container is using. Answers what it freed; never throws.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Nothing reclaims, so the host fills.</b> Measured on this fleet's
    /// pool host three times: 100% of 61 G with 101 MB free, then 22 G
    /// recovered by hand, then back to 2.2 G eleven days later. Each member
    /// image is 1.4 G (dev) or 4.2 G (ui), the bake and roll pipeline leaves
    /// every superseded one behind, and no bound anywhere says how many to
    /// keep. It also bounds what can ever be baked: an image carrying a real
    /// application would be 8-10 G a version, which is two bakes.
    /// </para>
    /// <para>
    /// <b>ON THE BUILDER, NOT THE ROLL, and that is the deployment's
    /// decision rather than a preference.</b> A roll is the moment an image
    /// becomes superseded, so reclaiming there reads as the obvious place -
    /// but <see cref="IPoolAdapter"/> is fenced to <c>/containers/</c> on
    /// purpose. Its own remark says why: "the pull point refuses images,
    /// volumes, build and networks", and a 403 read as drift resets every
    /// member every sweep, which is a bill rather than a bug. The builder
    /// already writes images - it pushes them - so it is the port that may.
    /// </para>
    /// <para>
    /// <b>So a build reclaims what the build before it superseded</b>, which
    /// is one behind and is enough: the steady state is two images per
    /// repository rather than every image ever built. It also runs at a
    /// moment somebody asked for something, where the seconds are affordable.
    /// </para>
    /// <para>
    /// <b>The daemon enforces the important half.</b> A delete without force
    /// is refused for an image a container is using, so a member that is up
    /// cannot lose what it is running even if this is asked wrongly. What
    /// <paramref name="keep"/> adds is the pin itself, which a pool scaled to
    /// nothing would otherwise leave unused and deletable - and a reset would
    /// then have nothing to recreate from.
    /// </para>
    /// <para>
    /// <b>Reversible, which is what makes it safe to do unasked.</b> Every
    /// image here was pushed to the pool host's own registry first, so a
    /// delete is undone by a pull. A refusal is reported and is never a
    /// failure: a host that would not let this happen keeps its disk and its
    /// build.
    /// </para>
    /// </remarks>
    /// <param name="repository">The repository to tidy, e.g. <c>gg-member</c>.</param>
    /// <param name="keep">Digests that must survive whatever else is true.</param>
    Task<ImagesReclaimed> ReclaimImagesAsync(
        string repository,
        IReadOnlyCollection<string> keep,
        CancellationToken cancellationToken = default);
}

/// <summary>What a reclaim freed, or why it freed nothing.</summary>
/// <remarks>
/// <b>An answer rather than a throw, because this is housekeeping beside the
/// act somebody asked for.</b> A build that failed because the tidy-up after
/// it was refused would be a worse console than a host with a full disk.
/// </remarks>
public sealed record ImagesReclaimed
{
    /// <summary>
    /// The tags that were removed, not merely how many.
    /// </summary>
    /// <remarks>
    /// <b>Named rather than counted, because something downstream has to mirror
    /// them.</b> The daemon's images and the registry's blobs are separate
    /// stores and only this list says which of them the daemon actually gave
    /// up - an image a container is still using is refused here, and its
    /// manifest must survive in the registry too or a reset could not recreate
    /// what is running.
    /// </remarks>
    public required IReadOnlyList<string> Removed { get; init; }

    /// <summary>What the daemon said it freed, in bytes.</summary>
    public required long Freed { get; init; }

    /// <summary>Why nothing was removed, when nothing was. Null when it worked.</summary>
    public string? Refused { get; init; }
}

/// <summary>
/// Removing from the host's own registry a manifest the daemon has given up.
/// </summary>
/// <remarks>
/// <para>
/// <b>A second store, and the larger one.</b> Reclaiming the daemon's images
/// leaves the registry holding every one of them: measured on this fleet's
/// pool host, 12.05 GB over 25 tags with two pinned, against 6 GB of images.
/// A push writes both and only one of them was ever read back.
/// </para>
/// <para>
/// <b>Its own port, not another member on <see cref="IImageBuilder"/>.</b> The
/// builder speaks to the Docker daemon through the scope proxy; this speaks
/// the OCI distribution protocol to a registry. One adapter answering both
/// would be one adapter holding two base addresses and two protocols.
/// </para>
/// <para>
/// <b>REACHED DIRECTLY, not through the scope proxy, and that is a smaller
/// thing than it sounds.</b> The proxy exists because the Docker socket is
/// host root - anything that reaches it can start a privileged container and
/// own the machine. A registry is the runner's own output store: this runner
/// already decides everything in it, because everything in it is what this
/// runner pushed. A fence between a process and its own output buys nothing,
/// and the runner is an ordinary process that can already reach any port on
/// its own loopback.
/// </para>
/// <para>
/// <b>The address comes from the pin, so there is nothing to configure.</b> A
/// reference is <c>registry/repository:tag</c> by OCI convention, which makes
/// the bound self-enforcing: the runner reaches only the registry its own
/// strategy pins from, and a new key could name one it does not.
/// </para>
/// </remarks>
public interface IImageRegistry
{
    /// <summary>
    /// Removes the manifest a tag names, if the registry allows it.
    /// </summary>
    /// <param name="reference">
    /// A full reference - <c>127.0.0.1:5000/gg-member:1c26776e2b1f</c>. The
    /// registry's address is read from it rather than configured.
    /// </param>
    Task<ManifestRemoved> RemoveManifestAsync(
        string reference, CancellationToken cancellationToken = default);
}

/// <summary>What removing a manifest did, or why it did nothing.</summary>
/// <remarks>
/// <b>A refusal is an answer here too.</b> A registry started on the image's
/// default config answers 405 UNSUPPORTED to every delete, which is a host
/// that has not been reconfigured rather than a build that went wrong - it
/// must be said out loud and must not fail anything.
/// </remarks>
public sealed record ManifestRemoved
{
    /// <summary>The manifest digest that was removed, or null if none was.</summary>
    public string? Digest { get; init; }

    /// <summary>Why it was not removed. Null when it was.</summary>
    public string? Refused { get; init; }
}
