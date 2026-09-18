using Gg.Contracts;
using Gg.Runner.Pools;
using Gg.Runner.Vcs;

namespace Gg.Runner.Tests;

/// <summary>
/// A build's recipe is fetched at the commit its ref resolves to, with the
/// machine's own credential for that repository, and the build context is the
/// recipe directory and nothing outside it.
/// </summary>
/// <remarks>
/// <para>
/// <b>Slice forty-one, S41.3-01 and S41.3-03, rules 3, 10 and 16.</b> The runner
/// resolves the ref and reports the commit; the directory it hands the builder
/// is the recipe's own, and one that is missing, or has no Dockerfile, or could
/// climb out of the repository, is refused in our words before anything is
/// built.
/// </para>
/// <para>
/// <b>Through the clone every flight already uses.</b> Found building step 3:
/// the VCS port reads one file, and a build needs a directory on disk - which is
/// exactly what the port's clone makes, and the clone already reports the commit
/// it landed on. So nothing new crosses the port, and the S41.3-01 row is
/// amended from "a directory read on the port" to this.
/// </para>
/// </remarks>
public class ARecipeIsReadAtOneCommitTests
{
    /// <summary>A bare repository holding one recipe, and its commit.</summary>
    private sealed class RecipeRepository : IDisposable
    {
        public string Root { get; }

        public string Bare { get; }

        public string Commit { get; }

        public RecipeRepository()
        {
            Root = Path.Combine(Path.GetTempPath(), "gg-recipe-fixture", Guid.NewGuid().ToString("n"));
            Directory.CreateDirectory(Root);
            Bare = Path.Combine(Root, "recipes.git");
            var work = Path.Combine(Root, "work");

            GitFixture.Run(Root, "init", "--bare", "--initial-branch=main", Bare);
            GitFixture.Run(Root, "clone", Bare, work);

            Directory.CreateDirectory(Path.Combine(work, "images", "gg-member"));
            File.WriteAllText(Path.Combine(work, "images", "gg-member", "Dockerfile"), "FROM ubuntu:24.04\n");
            File.WriteAllText(Path.Combine(work, "images", "gg-member", "setup.sh"), "echo ok\n");
            Directory.CreateDirectory(Path.Combine(work, "images", "empty"));
            File.WriteAllText(Path.Combine(work, "images", "empty", "README.md"), "no recipe here\n");
            File.WriteAllText(Path.Combine(work, "OUTSIDE.md"), "not part of any recipe\n");

            GitFixture.Run(work, "add", ".");
            GitFixture.Run(work, "commit", "-m", "recipes");
            GitFixture.Run(work, "push", "origin", "main");
            Commit = GitFixture.Run(work, "rev-parse", "HEAD").Trim();
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Root, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }

    private static PoolRecipe Recipe(string slug, string path = "images/gg-member", string reference = "main") => new()
    {
        Repository = new LeaseRepoRef { Provider = LocalVcsAdapter.ProviderKey, Slug = slug, PinnedRef = reference },
        Path = path,
    };

    private static string Scratch() =>
        Path.Combine(Path.GetTempPath(), "gg-recipe-scratch", Guid.NewGuid().ToString("n"));

    [Test]
    public async Task The_recipe_directory_is_fetched_at_the_commit_its_ref_resolves_to()
    {
        using var repository = new RecipeRepository();
        var asked = new List<RepoTarget>();
        var source = new GitRecipeSource([new LocalVcsAdapter(repository.Root)], target =>
        {
            asked.Add(target);
            return null;
        });
        var scratch = Scratch();

        var fetched = await source.FetchAsync(Recipe(repository.Bare), scratch);

        var recipe = fetched as RecipeFetch.Fetched;
        await Assert.That(recipe).IsNotNull().Because("answered: " + fetched);
        await Assert.That(recipe!.Commit).IsEqualTo(repository.Commit)
            .Because("the runner resolves the ref and reports the commit - rule 3.");
        await Assert.That(File.Exists(Path.Combine(recipe.Directory, "Dockerfile"))).IsTrue();
        await Assert.That(File.Exists(Path.Combine(recipe.Directory, "setup.sh"))).IsTrue();
        await Assert.That(File.Exists(Path.Combine(recipe.Directory, "OUTSIDE.md"))).IsFalse()
            .Because("the build context is the recipe directory and nothing else - rule 10.");
        await Assert.That(Path.GetFullPath(recipe.Directory))
            .StartsWith(Path.GetFullPath(scratch))
            .Because("everything fetched lives in the scratch the caller will remove.");

        await Assert.That(asked.Single().Slug).IsEqualTo(repository.Bare)
            .Because("the credential is looked up by the repository's own key and nothing else "
                   + "- rule 16.");
    }

    [Test]
    public async Task A_recipe_directory_with_no_dockerfile_is_refused_before_anything_is_built()
    {
        using var repository = new RecipeRepository();
        var source = new GitRecipeSource([new LocalVcsAdapter(repository.Root)], _ => null);

        var fetched = await source.FetchAsync(Recipe(repository.Bare, "images/empty"), Scratch());

        await Assert.That(fetched).IsTypeOf<RecipeFetch.Refused>();
        await Assert.That(((RecipeFetch.Refused)fetched).Diagnosis).Contains("Dockerfile");
    }

    [Test]
    public async Task A_recipe_directory_the_commit_does_not_hold_is_refused()
    {
        using var repository = new RecipeRepository();
        var source = new GitRecipeSource([new LocalVcsAdapter(repository.Root)], _ => null);

        var fetched = await source.FetchAsync(Recipe(repository.Bare, "images/nothing-here"), Scratch());

        await Assert.That(fetched).IsTypeOf<RecipeFetch.Refused>();
        await Assert.That(((RecipeFetch.Refused)fetched).Diagnosis).Contains("images/nothing-here");
    }

    [Test]
    public async Task A_path_that_could_climb_out_is_refused_before_anything_is_fetched()
    {
        // VALIDATED AT AUTHORING TOO (S41.1-01), and checked here as well: the
        // runner does not trust that the document it was handed passed a gate.
        using var repository = new RecipeRepository();
        var asked = new List<RepoTarget>();
        var source = new GitRecipeSource([new LocalVcsAdapter(repository.Root)], target =>
        {
            asked.Add(target);
            return null;
        });

        var fetched = await source.FetchAsync(Recipe(repository.Bare, "../outside"), Scratch());

        await Assert.That(fetched).IsTypeOf<RecipeFetch.Refused>();
        await Assert.That(asked).IsEmpty()
            .Because("nothing is fetched for a path that could leave the repository.");
    }

    [Test]
    public async Task A_provider_this_machine_does_not_serve_is_refused_naming_it()
    {
        // RULE 16: a credential is presented only where this machine's operator
        // declared the host, so a provider with no adapter is refused - by name,
        // and without asking the credential store anything.
        var asked = new List<RepoTarget>();
        var source = new GitRecipeSource([], target =>
        {
            asked.Add(target);
            return null;
        });

        var fetched = await source.FetchAsync(
            Recipe("JDX/gg-airspace") with
            {
                Repository = new LeaseRepoRef { Provider = "ado", Slug = "JDX/gg-airspace", PinnedRef = "main" },
            },
            Scratch());

        await Assert.That(fetched).IsTypeOf<RecipeFetch.Refused>();
        await Assert.That(((RecipeFetch.Refused)fetched).Diagnosis).Contains("ado");
        await Assert.That(asked).IsEmpty();
    }

    [Test]
    public async Task A_ref_that_resolves_to_nothing_is_refused()
    {
        using var repository = new RecipeRepository();
        var source = new GitRecipeSource([new LocalVcsAdapter(repository.Root)], _ => null);

        var fetched = await source.FetchAsync(Recipe(repository.Bare, reference: "no-such-branch"), Scratch());

        await Assert.That(fetched).IsTypeOf<RecipeFetch.Refused>();
    }
}
