namespace Gg.Runner.Tests;

/// <summary>
/// What a machine must be for a resident runner to live on it.
/// </summary>
/// <remarks>
/// <para>
/// <b>The proxy had no home.</b> Its nginx configuration has existed since
/// slice twelve and the only way to run it was a <c>docker run</c> in a
/// comment — which is fine for a walk on a laptop and is not a way to stand up
/// a host. Everything below exists so that "the runner acts only through the
/// scope-enforcing proxy" is something a machine is BUILT to satisfy rather
/// than something an operator is trusted to remember.
/// </para>
/// <para>
/// <b>The socket is the whole risk.</b> The Docker socket is host root: anything
/// that can reach it can start a privileged container and own the machine. The
/// proxy exists to be the only thing that sees it, so an artefact that mounts
/// it anywhere else has not weakened the control — it has removed it, while
/// leaving every test about the control passing.
/// </para>
/// <para>
/// <b>And a runner pointed at the socket is the same failure wearing the
/// configuration's clothes.</b> <c>GG_POOL_ENDPOINT</c> is refused when unset,
/// loudly, naming the variable — but nothing refuses one that names the socket
/// directly, and that reaches everything the proxy was built to refuse.
/// </para>
/// </remarks>
public class PoolHostTests
{
    private static string Host(string file) =>
        Path.Combine(RepoRoot(), "deploy", "pool-host", file);

    private static IReadOnlyList<string> Artefacts() =>
        Directory.Exists(Path.Combine(RepoRoot(), "deploy", "pool-host"))
            ? Directory.EnumerateFiles(Path.Combine(RepoRoot(), "deploy", "pool-host"))
                .Select(Path.GetFileName).OfType<string>().Order(StringComparer.Ordinal).ToList()
            : [];

    [Test]
    public async Task A_machine_can_be_stood_up_from_something_other_than_a_comment()
    {
        await Assert.That(File.Exists(Host("compose.yaml"))).IsTrue()
            .Because("the proxy's only launch instructions were a docker run inside an nginx "
                   + "comment, which is not a way to stand up a host.");
        await Assert.That(File.Exists(Host("README.md"))).IsTrue()
            .Because("what a machine must provide is deployment knowledge, and it belongs beside "
                   + "the thing that provides it.");
    }

    [Test]
    public async Task Only_the_proxy_is_given_the_socket()
    {
        // The Docker socket is host root. The proxy exists to be the ONLY thing
        // that sees it; an artefact that mounts it elsewhere removes the
        // control rather than weakening it, and every test about the control
        // keeps passing.
        var mounting = Artefacts()
            .Where(f => File.ReadAllText(Host(f)).Contains("docker.sock", StringComparison.Ordinal))
            .ToList();

        await Assert.That(mounting).IsEquivalentTo((string[])["compose.yaml"])
            .Because("exactly one artefact may name the socket, and it is the one that puts it "
                   + "inside the proxy. Found: " + string.Join(", ", mounting));
    }

    [Test]
    public async Task Nothing_a_machine_is_built_from_carries_a_secret()
    {
        // A runner's credential is `local` - a file on the machine that
        // registered it - and a session is a bearer token with a person's
        // authority. Neither may be baked into provisioning: cloud-init is
        // readable from instance metadata, and a compose file is committed.
        var carrying = new List<string>();

        foreach (var file in Artefacts())
        {
            var text = File.ReadAllText(Host(file));
            foreach (var smell in (string[])["PAT=", "TOKEN=", "SessionToken", "-----BEGIN", "password:"])
            {
                if (text.Contains(smell, StringComparison.OrdinalIgnoreCase))
                {
                    carrying.Add($"{file}: '{smell}'");
                }
            }
        }

        await Assert.That(carrying).IsEmpty()
            .Because("cloud-init is readable from instance metadata and a compose file is "
                   + "committed. Found: " + string.Join(", ", carrying));
    }

    [Test]
    public async Task The_endpoint_a_host_is_given_is_the_proxy_and_never_the_socket()
    {
        // GG_POOL_ENDPOINT is refused when UNSET, loudly, naming the variable.
        // Nothing refuses one that names the socket directly - and that reaches
        // everything the proxy was built to refuse, from a variable that looks
        // configured.
        var endpoints = Artefacts()
            .SelectMany(f => File.ReadAllLines(Host(f)))
            .Where(l => l.Contains("GG_POOL_ENDPOINT", StringComparison.Ordinal))
            .ToList();

        await Assert.That(endpoints).IsNotEmpty()
            .Because("a host that is never given the endpoint cannot run a resident runner at all.");
        await Assert.That(endpoints.Where(l => l.Contains(".sock", StringComparison.Ordinal)))
            .IsEmpty()
            .Because("pointing it at the socket is the reach the proxy exists to refuse, arriving "
                   + "through the variable that was supposed to prevent it.");
    }

    [Test]
    public async Task The_host_is_told_which_control_plane_it_answers_to()
    {
        // GG_CONTROL_PLANE falls back to http://localhost:5199 - a laptop
        // default, and the right one there. A provisioned host inherits it
        // silently: nothing is unset, nothing is refused, and the machine
        // answers to itself. Same shape as the endpoint above, one variable
        // over: it does not fail, it succeeds against the wrong thing.
        // ASSIGNMENTS, not mentions. A first formulation matched the bare name
        // and fired on the README sentence documenting the fallback - which is
        // the opposite of the defect, since a host that is told about localhost
        // in prose is not a host configured with it.
        var addresses = Artefacts()
            .SelectMany(f => File.ReadAllLines(Host(f)))
            .Where(l => l.Contains("GG_CONTROL_PLANE=", StringComparison.Ordinal))
            .ToList();

        await Assert.That(addresses).IsNotEmpty()
            .Because("a host that is never told the control plane does not refuse - it falls back "
                   + "to localhost and looks configured while answering to itself.");
        await Assert.That(addresses.Where(l => l.Contains("localhost", StringComparison.Ordinal)))
            .IsEmpty()
            .Because("the fallback written down explicitly is the fallback shipped, and a pool "
                   + "host is by definition not the machine the control plane runs on.");
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
