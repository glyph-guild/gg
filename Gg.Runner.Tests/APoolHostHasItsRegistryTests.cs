namespace Gg.Runner.Tests;

/// <summary>
/// A pool host brings up the registry its image pins and its proxy's build and
/// push rules already name.
/// </summary>
/// <remarks>
/// <para>
/// <b>Slice forty-three, S43.1-03.</b> Every strategy pins its member image as
/// <c>127.0.0.1:5000/...@sha256:...</c>, and the proxy allows a build to tag and a
/// push to reach exactly <c>127.0.0.1:5000</c> - and nothing in the host's
/// artefacts ever started a registry there. vmlinux001 has one because a person
/// ran it; a second host stood up from these files would build an image and have
/// nowhere to put it.
/// </para>
/// <para>
/// <b>Loopback, like the proxy, and for the proxy's reason.</b> A registry that
/// answered on the network would take pushes from anybody who could reach the
/// port, and the image a member runs is the thing a push decides.
/// </para>
/// </remarks>
public class APoolHostHasItsRegistryTests
{
    private static string Host(string file) =>
        Path.Combine(RepoRoot(), "deploy", "pool-host", file);

    /// <summary>One service's block of compose.yaml, by its key.</summary>
    private static string Service(string name)
    {
        var lines = File.ReadAllLines(Host("compose.yaml"));
        var start = Array.FindIndex(lines, l => l.TrimEnd() == $"  {name}:");

        if (start < 0)
        {
            return "";
        }

        var block = new List<string> { lines[start] };

        for (var i = start + 1; i < lines.Length; i++)
        {
            // THE NEXT SERVICE, or anything back at the top level, ends this one.
            // A blank line does not: it is how the file spaces a block out.
            if (lines[i].Trim().Length > 0 && !lines[i].StartsWith("    ", StringComparison.Ordinal))
            {
                break;
            }

            block.Add(lines[i]);
        }

        return string.Join('\n', block);
    }

    [Test]
    public async Task The_host_starts_a_registry()
    {
        await Assert.That(Service("registry")).IsNotEmpty()
            .Because("the image pins and the proxy's push rule both name 127.0.0.1:5000, and nothing "
                   + "else on a pool host would start it.");
    }

    [Test]
    public async Task The_registry_answers_on_loopback_and_nowhere_else()
    {
        var registry = Service("registry");

        await Assert.That(registry).Contains("\"127.0.0.1:5000:5000\"");

        var exposed = registry.Split('\n')
            .Where(l => l.Contains("5000", StringComparison.Ordinal)
                     && l.TrimStart().StartsWith("- ", StringComparison.Ordinal)
                     && !l.Contains("127.0.0.1:5000", StringComparison.Ordinal))
            .ToList();

        await Assert.That(exposed).IsEmpty()
            .Because("a registry on the network takes pushes from anybody who reaches the port, and "
                   + "a push decides what a member runs. Found: " + string.Join(" | ", exposed));
    }

    [Test]
    public async Task The_registry_image_is_pinned_by_digest()
    {
        await Assert.That(Service("registry")).Contains("image: registry:2@sha256:")
            .Because("the registry holds every image a member runs, so what it is must be a "
                   + "version somebody named, not whatever the tag points at today.");
    }

    [Test]
    public async Task The_registry_is_not_given_the_socket()
    {
        // PoolHostTests holds that exactly one ARTEFACT names the socket, and
        // compose.yaml is that artefact - so a second service in the same file
        // mounting it would pass that test. This is the service-level half.
        await Assert.That(Service("registry")).DoesNotContain("docker.sock")
            .Because("the socket is host root, and the proxy is the only thing built to hold it.");
    }

    [Test]
    public async Task The_proxy_and_the_registry_agree_on_where_images_go()
    {
        // THE OTHER HALF, read rather than assumed: the address the registry
        // listens on is the one the proxy lets a build tag and a push reach.
        var proxy = File.ReadAllText(Path.Combine(RepoRoot(), "scripts", "pool-proxy", "nginx.conf"));

        await Assert.That(proxy).Contains("127\\.0\\.0\\.1(:|%3[Aa])5000");
        await Assert.That(proxy).Contains("/images/127\\.0\\.0\\.1:5000/");
    }

    [Test]
    public async Task The_pool_host_readme_says_it_is_there()
    {
        var readme = File.ReadAllText(Host("README.md"));

        await Assert.That(readme).Contains("127.0.0.1:5000")
            .Because("a person standing a host up by hand reads the README, not compose.yaml.");
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
