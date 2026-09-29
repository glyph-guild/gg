using System.Text.RegularExpressions;

namespace Gg.Runner.Tests;

/// <summary>
/// The runner reaches an environment instance's daemon over a socket it is
/// permitted to open — never by becoming another user.
/// </summary>
/// <remarks>
/// <para>
/// <b>The question this settles was asked the wrong way round.</b> Slice
/// fifty-four's S54.3-04 assumed the runner would need <i>"a named, bounded
/// grant"</i> to bring up a daemon owned by another user, because it runs as
/// <c>gg</c> and has no <c>sudo</c>. Measured on the pool host, it needs no grant
/// at all: an instance's daemon is started at boot by <c>loginctl
/// enable-linger</c>, so there is nothing for the runner to start. What it needs
/// is to <i>talk</i> to one, and that is a file permission rather than a
/// privilege.
/// </para>
/// <para>
/// <b>Why the default socket cannot be the one it talks to.</b> Measured:
/// <c>/run/user/1002</c> is <c>drwx------</c> owned by the slot, and
/// <c>/run/user</c> is a tmpfs mounted <c>mode=700</c> and recreated every boot —
/// so <c>gg</c> is refused, and an ACL placed there would not survive a restart.
/// The daemon is given a second listener on a durable path instead
/// (<c>dockerd-rootless.sh</c> passes its arguments through to <c>dockerd</c>,
/// so <c>-H</c> reaches it), group-readable by the runner. See
/// <c>deploy/pool-host/environments.md</c>.
/// </para>
/// <para>
/// <b>So this is a ratchet, and it was written after the property was already
/// true.</b> Nothing in <c>Gg.Runner</c> names an escalation verb today. Its
/// worth is entirely in whether it CAN fail, which is why the predicate is
/// tested against samples in both directions rather than only pointed at the
/// tree — a scan nobody has seen reject anything is a scan that may be reading
/// an empty list.
/// </para>
/// <para>
/// <b>What it protects.</b> The moment the runner may become another user, every
/// argument in ADR-0034 about instances being separated by the kernel is worth
/// less: a process that can cross the boundary on request is a boundary with a
/// door in it. The cheap version of this feature — a <c>sudoers</c> line with a
/// wildcard — is exactly what this exists to make somebody argue for in public
/// rather than add quietly.
/// </para>
/// </remarks>
public class TheRunnerReachesAnInstanceWithoutBecomingAnybodyTests
{
    /// <summary>
    /// Running something as somebody else. Word-bounded, because a comment will
    /// one day contain <c>pseudo</c> and a scan that refuses it is a scan
    /// somebody deletes rather than obeys.
    ///
    /// <para>
    /// <b>The systemd arm is punctuation-tolerant on purpose.</b> A first draft
    /// matched <c>systemctl --user -M</c> as contiguous text and missed
    /// <c>Run("systemctl", "--user", "-M", …)</c>, which is how it would
    /// actually be written in C#. The sample below caught it, which is the
    /// entire argument for testing a predicate rather than pointing it at a tree
    /// and watching it pass.
    /// </para>
    /// </summary>
    private static readonly Regex Escalation = new(
        @"\b(sudo|pkexec|machinectl|runuser|setuid|seteuid)\b"
      + @"|--machine\b|@\.host\b|systemctl[^;]{0,40}\b-M\b|\bsu\s+-",
        RegexOptions.Compiled);

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

    private static IReadOnlyList<string> RunnerSources() =>
    [
        .. Directory.EnumerateFiles(Path.Combine(RepoRoot(), "Gg.Runner"), "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal),
    ];

    [Test]
    public async Task The_scan_really_reads_the_runner()
    {
        // THE LIVENESS ANCHOR. A scan over an empty list reports a clean runner
        // for ever, which is the failure this file would be least able to
        // notice about itself.
        await Assert.That(RunnerSources().Count).IsGreaterThan(20)
            .Because("Gg.Runner is many files; a handful means the enumeration is wrong and "
                   + "every assertion below is passing over nothing.");
    }

    [Test]
    public async Task The_scan_catches_an_escalation_and_lets_ordinary_code_through()
    {
        // BOTH DIRECTIONS, because a ratchet written after its property is
        // already true is only worth what it would catch.
        foreach (var escalating in (string[])
                 [
                     """var psi = new ProcessStartInfo("sudo", "systemctl start docker");""",
                     """Run("pkexec", "--user", slot, "dockerd");""",
                     """Run("machinectl", "shell", $"{slot}@");""",
                     """Run("systemctl", "--user", "-M", $"{slot}@.host", "start", "docker");""",
                     """Run("runuser", "-u", slot, "--", "docker", "ps");""",
                 ])
        {
            await Assert.That(Escalation.IsMatch(escalating)).IsTrue()
                .Because($"'{escalating}' runs something as somebody else and must be caught.");
        }

        foreach (var ordinary in (string[])
                 [
                     """var host = $"unix://{instance.SocketPath}";""",
                     "// the pseudo-terminal keeps the top row for a gg bar",
                     """_ = await _daemon.GetAsync("/v1.48/containers/json", cancellationToken);""",
                     "// a member is a container, and the runner never becomes one",
                 ])
        {
            await Assert.That(Escalation.IsMatch(ordinary)).IsFalse()
                .Because($"'{ordinary}' is ordinary runner code and a scan that refuses it is a "
                       + "scan somebody will delete rather than obey.");
        }
    }

    [Test]
    public async Task The_runner_becomes_nobody()
    {
        var offenders = RunnerSources()
            .Where(f => Escalation.IsMatch(File.ReadAllText(f)))
            .Select(f => Path.GetRelativePath(RepoRoot(), f))
            .ToList();

        await Assert.That(offenders).IsEmpty()
            .Because("an instance's daemon is started at boot by linger, so there is nothing for "
                   + "the runner to start - it opens a socket it is permitted to open. A runner "
                   + "that may become another user turns every kernel boundary in ADR-0034 into "
                   + "one with a door in it. Found: " + string.Join(", ", offenders));
    }
}
