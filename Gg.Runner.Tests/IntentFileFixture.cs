using Gg.Contracts;
using Gg.Runner.Vcs;

namespace Gg.Runner.Tests;

/// <summary>A lease whose intent is a file in the skill fixture's repository, and a reader for it.</summary>
internal static class IntentFileFixture
{
    internal static RepositoryFileReader Reader(TheRunnerReadsTheSkillAtItsPinTests.SkillRepository repository) => new(
        [new LocalVcsAdapter(repository.Directory)],
        Path.Combine(Path.GetTempPath(), "gg-intent-cache", Guid.NewGuid().ToString("n")),
        secretFor: _ => Task.FromResult<string?>(null));

    internal static LeaseGranted Lease(
        TheRunnerReadsTheSkillAtItsPinTests.SkillRepository repository, string? @ref, string? path = null) => new()
    {
        LeaseId = Guid.NewGuid().ToString(),
        Generation = 1,
        FlightId = Guid.NewGuid().ToString(),
        Repos = [],
        Credentials = [],
        IntentRepository = repository.BarePath,
        IntentRepositoryProvider = LocalVcsAdapter.ProviderKey,
        IntentPath = path ?? TheRunnerReadsTheSkillAtItsPinTests.SkillPath,
        IntentRef = @ref,
    };

    internal static readonly IReadOnlyDictionary<string, string> NoSecrets = new Dictionary<string, string>();
}
