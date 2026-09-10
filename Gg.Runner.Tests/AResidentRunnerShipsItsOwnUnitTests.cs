namespace Gg.Runner.Tests;

/// <summary>
/// The unit and the seed a resident runner is brought up with.
/// </summary>
/// <remarks>
/// <para>
/// <b>Because the one running the fleet today ships nowhere.</b>
/// <c>gg-runner-maintain.service</c> is in this repository;
/// <c>gg-runner-up.service</c> on the resident host is not, and its
/// <c>Environment=</c> lines are the only copy of that machine's
/// configuration. A rebuilt host loses <c>GG_CONTROL_PLANE</c>,
/// <c>GG_RUNNER_LABELS</c>, <c>GG_EXECUTOR_BINARY</c> and
/// <c>GG_STUN_SERVERS</c> together — and the last of those carries a measured
/// decision about which relays cross two particular networks.
/// </para>
/// <para>
/// <b>Committing it as it stands was rejected once, and rightly.</b> It would
/// enshrine the <c>Environment=</c> lines the configuration slice exists to
/// remove. What makes it committable now is that most of them no longer need to
/// be there: a runner takes its relays and labels from what the control plane
/// offers, so the unit carries the two values that cannot be offered and the
/// seed carries the two that cannot be inherited.
/// </para>
/// <para>
/// <b><c>Restart=always</c>, and that is the line that makes the feature
/// work.</b> A runner that meets a new offer ends its own process so the next
/// start takes it — a clean exit, code zero. Its sibling unit says
/// <c>Restart=on-failure</c>, under which that exit is final and the machine
/// simply stops. The two units differ here for a reason, so the reason is
/// asserted rather than left to whoever copies one into the other.
/// </para>
/// </remarks>
public class AResidentRunnerShipsItsOwnUnitTests
{
    private static string Resident(string file) =>
        Path.Combine(RepoRoot(), "deploy", "resident-runner", file);

    private static string Unit() => File.ReadAllText(Resident("gg-runner-up.service"));

    [Test]
    public async Task The_unit_is_in_the_repository_at_all()
    {
        await Assert.That(File.Exists(Resident("gg-runner-up.service"))).IsTrue()
            .Because("a rebuilt host loses everything this file says, and nothing anywhere "
                   + "else says it.");
    }

    [Test]
    public async Task It_restarts_on_a_clean_exit_or_an_offer_can_never_be_taken()
    {
        var unit = Unit();

        await Assert.That(unit).Contains("Restart=always", StringComparison.Ordinal)
            .Because("taking an offer means ending the process so the next start applies "
                   + "it. Under Restart=on-failure - which the pool-host unit beside this "
                   + "one uses - that clean exit is the machine going away for good.");

        await Assert.That(unit).DoesNotContain("Restart=on-failure", StringComparison.Ordinal)
            .Because("both lines present would leave which one wins to systemd's parse "
                   + "order rather than to anybody's decision.");
    }

    [Test]
    public async Task It_carries_only_what_cannot_be_offered()
    {
        // THE WHOLE POINT OF COMMITTING IT. Relays and labels are the unwatched
        // tier: a runner takes them from the control plane, so a unit that
        // pinned them would put the fleet's configuration back on the host and
        // silently win, because the environment beats the file.
        var unit = Unit();

        foreach (var offered in (string[])["GG_STUN_SERVERS", "GG_RUNNER_LABELS"])
        {
            await Assert.That(unit).DoesNotContain(offered, StringComparison.Ordinal)
                .Because($"{offered} is offered by the control plane, and an Environment= "
                       + "line for it beats the file this runner just wrote - the fleet's "
                       + "configuration back on one host, quietly.");
        }
    }

    [Test]
    public async Task The_seed_is_the_two_values_that_cannot_arrive_any_other_way()
    {
        // control-plane, because nothing can tell you where the control plane
        // is except the control plane. accept-offered, because it has no
        // environment variable BY DESIGN - the recorded reason is that a
        // variable is one a container image or a systemd unit could carry
        // without anybody reading it - so a machine that accepts offers is one
        // where somebody wrote a file.
        var seed = File.ReadAllText(Resident("config.json"));

        await Assert.That(seed).Contains("\"control-plane\"", StringComparison.Ordinal);
        await Assert.That(seed).Contains("\"accept-offered\": true", StringComparison.Ordinal)
            .Because("without it the runner takes nothing offered, and the unit is back to "
                   + "carrying everything.");

        var parsed = Gg.Local.ConfigurationFile.Parse(seed);

        await Assert.That(parsed.Diagnosis).IsNull()
            .Because("a seed gg will not read is a host that comes up on defaults and says "
                   + "so once, on a machine nobody is watching.");
        await Assert.That(parsed.Configuration!.AcceptOffered).IsTrue();
    }

    [Test]
    public async Task The_seed_pins_nothing_the_control_plane_should_be_deciding()
    {
        // A seed is a bootstrap, not a configuration. Every value in it is one
        // that could not have arrived any other way.
        var parsed = Gg.Local.ConfigurationFile.Parse(
            File.ReadAllText(Resident("config.json")));

        var pinned = Gg.Local.Configuration.Members
            .Where(m => m.Get(parsed.Configuration!) is { Length: > 0 })
            .Select(m => m.Key)
            .Where(key => !string.Equals(key, "control-plane", StringComparison.Ordinal))
            .ToList();

        await Assert.That(pinned).IsEmpty()
            .Because("anything else here is a value the fleet is supposed to decide, pinned "
                   + "on one host where it will win over what that host is offered. Found: "
                   + string.Join(", ", pinned));
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
