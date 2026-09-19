namespace Gg.Runner.Tests;

/// <summary>
/// A contract release never takes "latest" from gg.
/// </summary>
/// <remarks>
/// <para>
/// <b>Slice forty-three, S43.1-02.</b> Both workflows publish GitHub releases on
/// this repository, and GitHub marks the newest one <i>latest</i> unless told
/// not to. The contracts workflow publishes on most pushes to main, so
/// <c>releases/latest</c> resolved to <c>contracts-v0.198.0</c> - which carries a
/// <c>.nupkg</c> for the control plane and no gg binary - and every install link
/// written against <i>latest</i> 404'd.
/// </para>
/// <para>
/// <b>Fixed where the release is made</b>, not only in the links: a contract
/// release is an artefact the control plane pins by version, and it has no use
/// for the word <i>latest</i> at all.
/// </para>
/// </remarks>
public class ContractsAreNeverLatestTests
{
    /// <summary>The whole `gh release create` command, continuation lines and all.</summary>
    private static string ReleaseCommand(string workflow)
    {
        var lines = File.ReadAllLines(Path.Combine(RepoRoot(), ".github", "workflows", workflow));
        var start = Array.FindIndex(lines, l => l.Contains("gh release create", StringComparison.Ordinal));

        if (start < 0)
        {
            return "";
        }

        var command = new List<string>();

        for (var i = start; i < lines.Length; i++)
        {
            command.Add(lines[i]);

            if (!lines[i].TrimEnd().EndsWith('\\'))
            {
                break;
            }
        }

        return string.Join('\n', command);
    }

    [Test]
    public async Task Both_workflows_create_a_release()
    {
        // THE ANCHOR, so the two below cannot pass over a command that moved.
        await Assert.That(ReleaseCommand("publish-contracts.yml")).IsNotEmpty();
        await Assert.That(ReleaseCommand("publish-cli.yml")).IsNotEmpty();
    }

    [Test]
    public async Task A_contract_release_is_published_not_latest()
    {
        await Assert.That(ReleaseCommand("publish-contracts.yml")).Contains("--latest=false")
            .Because("a contract release marked latest is what `releases/latest` resolves to, and it "
                   + "carries no gg binary.");
    }

    [Test]
    public async Task A_gg_release_is_left_to_be_latest()
    {
        await Assert.That(ReleaseCommand("publish-cli.yml")).DoesNotContain("--latest=false")
            .Because("the gg release is the one a person asking for the newest gg means.");
    }

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null
            && !File.Exists(Path.Combine(directory.FullName, "Gg.Contracts", "fact-vocabulary.json")))
        {
            directory = directory.Parent;
        }

        return (directory ?? throw new InvalidOperationException("repository root not found")).FullName;
    }
}
