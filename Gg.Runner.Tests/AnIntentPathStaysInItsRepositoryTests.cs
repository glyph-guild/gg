using Gg.Contracts;
using Gg.Runner.Vcs;

namespace Gg.Runner.Tests;

/// <summary>
/// <b>S62.2-02</b> - a path that could leave the repository is refused before anything is
/// fetched, for an intent as for a skill.
/// </summary>
/// <remarks>
/// <b>Before anything is fetched</b> is proven by pointing the reader at a repository that does
/// not exist: had it asked the adapter anything, the refusal would be about resolving a ref, not
/// about the path.
/// </remarks>
public class AnIntentPathStaysInItsRepositoryTests
{
    private static RepositoryFileReader Reader() => new(
        [new LocalVcsAdapter(Path.Combine(Path.GetTempPath(), "gg-nowhere", Guid.NewGuid().ToString("n")))],
        Path.Combine(Path.GetTempPath(), "gg-file-cache", Guid.NewGuid().ToString("n")),
        secretFor: _ => Task.FromResult<string?>(null));

    private static RepoTarget Nowhere() => new()
    {
        Provider = LocalVcsAdapter.ProviderKey,
        Slug = "nowhere",
        PinnedRef = "main",
    };

    [Test]
    [Arguments("../outside.md")]
    [Arguments("/etc/passwd")]
    [Arguments("docs/./plan.md")]
    [Arguments("docs/\u0007plan.md")]
    public async Task A_path_outside_is_refused_in_the_contracts_words_before_any_fetch(string path)
    {
        var read = await Reader().ReadAsync(Nowhere(), path, "this flight's intent");

        await Assert.That(read).IsTypeOf<FileRead.Unreadable>();
        await Assert.That(((FileRead.Unreadable)read).Diagnosis)
            .StartsWith(RepositoryPaths.Refused(path)!)
            .Because("the sentence is the contract's, so a person sees the same words whether the "
                   + "wire or the reader refused it.");
    }

    [Test]
    public async Task A_control_character_is_refused_on_the_wire_too()
    {
        await Assert.That(RepositoryPaths.Refused("docs/\u0007plan.md")).IsNotNull()
            .Because("the reader refused control characters before the rules were shared, and "
                   + "the shared rule is the stricter of the two, not the looser.");
    }

    [Test]
    public async Task The_sentence_names_what_was_being_read()
    {
        var read = await Reader().ReadAsync(Nowhere(), "docs/plan.md", "this flight's intent");

        await Assert.That(((FileRead.Unreadable)read).Diagnosis).Contains("this flight's intent")
            .Because("a reader shared by skills and intents must still say which one failed.");
    }
}
