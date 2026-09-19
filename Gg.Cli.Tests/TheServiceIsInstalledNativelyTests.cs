namespace Gg.Cli.Tests;

/// <summary>
/// <c>gg service install</c> writes the platform's own service definition, a
/// seed only the service user can read, and nothing of gg; <c>gg service
/// uninstall</c> removes exactly what it wrote.
/// </summary>
/// <remarks>
/// <para>
/// <b>Slice forty-three, rule 21 (S43.5-01).</b> A runner became a service by a
/// person following a runbook: create a user, write a seed, copy a unit, edit
/// two lines of it with sed, reload, enable. Every step was a place for one
/// machine to differ from the next, and nothing could say afterwards what had
/// been written - so nothing could take it away again.
/// </para>
/// <para>
/// <b>Against a fake, and that is the design rather than a shortcut.</b> Every
/// side effect is behind <see cref="IServiceHost"/>, so what is written, as
/// whom and with which mode is asserted here without a test ever touching this
/// machine's users or its service manager.
/// </para>
/// </remarks>
public class TheServiceIsInstalledNativelyTests
{
    private const string ControlPlane = "https://cp.example";

    private static FakeServiceHost Machine(ServicePlatform platform = ServicePlatform.Systemd) =>
        new(platform) { Files = { [ServiceInstaller.Binary] = new("(gg)", "root", Mode(0x1ED)) } };

    private static UnixFileMode Mode(int bits) => (UnixFileMode)bits;

    private static readonly UnixFileMode RootReadable = Mode(0x1A4);   // 0644
    private static readonly UnixFileMode OwnerOnly = Mode(0x180);      // 0600
    private static readonly UnixFileMode OwnerOnlyDirectory = Mode(0x1C0); // 0700

    private static ServiceOutcome Install(
        FakeServiceHost host, string? controlPlane = ControlPlane, bool enroll = false,
        string? user = null, string token = "enroll-abc") =>
        ServiceInstaller.Install(host, new ServiceRequest(controlPlane, enroll, user), () => token);

    [Test]
    public async Task A_systemd_machine_gets_a_root_owned_unit_that_runs_the_runner_as_its_own_user()
    {
        var host = Machine();

        var outcome = Install(host);

        await Assert.That(outcome.Done).IsTrue().Because(string.Join(" ", outcome.Said));

        var unit = host.Files["/etc/systemd/system/gg-runner-up.service"];
        await Assert.That(unit.Owner).IsEqualTo("root");
        await Assert.That(unit.Mode).IsEqualTo(RootReadable);
        await Assert.That(unit.Content).Contains("User=gg");
        await Assert.That(unit.Content).Contains($"ExecStart={ServiceInstaller.Binary} runner up");
        await Assert.That(unit.Content).Contains("Restart=always")
            .Because("the runner exits cleanly to take an offer, and on-failure would leave it stopped.");

        // THE UNIT CARRIES NO MACHINE'S CONFIGURATION, for the reason the runbook's
        // own unit gives: an Environment= line beats the file, so it would put the
        // fleet's configuration back on this host.
        await Assert.That(unit.Content).DoesNotContain("Environment=GG_");

        await Assert.That(host.UsersCreated).IsEquivalentTo((string[])["gg"]);
        await Assert.That(host.Started).IsEquivalentTo((string[])["Systemd gg-runner-up"]);
    }

    [Test]
    public async Task A_mac_gets_a_root_owned_launch_daemon_that_runs_the_runner_as_its_own_user()
    {
        var host = Machine(ServicePlatform.Launchd);

        var outcome = Install(host);

        await Assert.That(outcome.Done).IsTrue().Because(string.Join(" ", outcome.Said));

        var plist = host.Files[$"/Library/LaunchDaemons/{ServiceInstaller.LaunchdLabel}.plist"];
        await Assert.That(plist.Owner).IsEqualTo("root");
        await Assert.That(plist.Mode).IsEqualTo(RootReadable);
        await Assert.That(plist.Content).Contains("<key>UserName</key>");
        await Assert.That(plist.Content).Contains("<string>_gg</string>");
        await Assert.That(plist.Content).Contains($"<string>{ServiceInstaller.Binary}</string>");
        await Assert.That(plist.Content).Contains("<string>runner</string>");
        await Assert.That(plist.Content).Contains("<string>up</string>");
        await Assert.That(plist.Content).Contains("<key>KeepAlive</key>");
        await Assert.That(host.Started)
            .IsEquivalentTo((string[])[$"Launchd {ServiceInstaller.LaunchdLabel}"]);
    }

    [Test]
    public async Task The_seed_is_readable_by_the_service_user_and_nobody_else()
    {
        var host = Machine();

        var outcome = Install(host, enroll: true, token: "enroll-abc");

        await Assert.That(outcome.Done).IsTrue().Because(string.Join(" ", outcome.Said));

        var home = host.Homes["gg"];
        var directory = host.Files[$"{home}/.config/good-grief"];
        await Assert.That(directory.Owner).IsEqualTo("gg");
        await Assert.That(directory.Mode).IsEqualTo(OwnerOnlyDirectory);

        var seed = host.Files[$"{home}/.config/good-grief/config.json"];
        await Assert.That(seed.Owner).IsEqualTo("gg");
        await Assert.That(seed.Mode).IsEqualTo(OwnerOnly);

        var parsed = Gg.Local.ConfigurationFile.Parse(seed.Content).Configuration;
        await Assert.That(parsed).IsNotNull().Because(seed.Content);
        await Assert.That(parsed!.ControlPlane).IsEqualTo(ControlPlane);

        var token = host.Files[$"{home}/.config/good-grief/enrollment"];
        await Assert.That(token.Owner).IsEqualTo("gg");
        await Assert.That(token.Mode).IsEqualTo(OwnerOnly);
        await Assert.That(token.Content.Trim()).IsEqualTo("enroll-abc");

        // AND NOWHERE ELSE: not the unit, not the manifest, not a sentence.
        await Assert.That(host.Files
                .Where(f => !f.Key.EndsWith("/enrollment", StringComparison.Ordinal))
                .Any(f => f.Value.Content.Contains("enroll-abc", StringComparison.Ordinal)))
            .IsFalse();
        await Assert.That(string.Join(" ", outcome.Said)).DoesNotContain("enroll-abc");
    }

    [Test]
    public async Task Without_elevation_nothing_is_written_and_it_says_why()
    {
        var host = Machine();
        host.IsElevated = false;
        var before = host.Snapshot();

        var outcome = Install(host);

        await Assert.That(outcome.Done).IsFalse();
        await Assert.That(string.Join(" ", outcome.Said)).Contains("sudo");
        await Assert.That(host.Snapshot()).IsEquivalentTo(before);
        await Assert.That(host.UsersCreated).IsEmpty();
        await Assert.That(host.Started).IsEmpty();
    }

    [Test]
    public async Task Uninstall_removes_exactly_what_install_wrote()
    {
        var host = Machine();
        // SOMETHING ALREADY THERE, which is what "exactly" is about: an
        // uninstall that removed every file under /etc/gg, or the whole of the
        // user's config directory, would pass a test that started empty.
        host.Files["/etc/gg"] = new("", "root", Mode(0x1ED), Directory: true);
        host.Files["/etc/gg/somebody-elses.conf"] = new("keep", "root", RootReadable);
        var before = host.Snapshot();

        await Assert.That(Install(host, enroll: true).Done).IsTrue();
        await Assert.That(host.Snapshot()).IsNotEquivalentTo(before);

        var outcome = ServiceInstaller.Uninstall(host);

        await Assert.That(outcome.Done).IsTrue().Because(string.Join(" ", outcome.Said));

        // The user's home is the one thing left, and it is said: it holds the
        // runner's identity and credentials, which install did not write.
        var after = host.Snapshot()
            .Where(path => path != host.Homes["gg"])
            .ToList();
        await Assert.That(after).IsEquivalentTo(before);
        await Assert.That(host.Stopped).IsEquivalentTo((string[])["Systemd gg-runner-up"]);
        await Assert.That(string.Join(" ", outcome.Said)).Contains("gg")
            .Because("the user that was left is named, so a person knows it is theirs to remove.");
    }

    [Test]
    public async Task A_second_uninstall_finds_nothing_to_remove()
    {
        var host = Machine();
        await Assert.That(Install(host).Done).IsTrue();
        await Assert.That(ServiceInstaller.Uninstall(host).Done).IsTrue();
        var before = host.Snapshot();

        var again = ServiceInstaller.Uninstall(host);

        await Assert.That(again.Done).IsTrue();
        await Assert.That(host.Snapshot()).IsEquivalentTo(before);
    }

    [Test]
    public async Task A_file_it_did_not_write_is_never_overwritten()
    {
        // A RUNNER INSTALLED BY HAND FROM THE RUNBOOK, which is how every
        // resident host so far was made. Two runners on one machine is not
        // something this makes.
        var host = Machine();
        host.Files["/etc/systemd/system/gg-runner-up.service"] = new("by hand", "root", RootReadable);
        var before = host.Snapshot();

        var outcome = Install(host);

        await Assert.That(outcome.Done).IsFalse();
        await Assert.That(host.Files["/etc/systemd/system/gg-runner-up.service"].Content)
            .IsEqualTo("by hand");
        await Assert.That(host.Snapshot()).IsEquivalentTo(before);
        await Assert.That(host.Started).IsEmpty();
    }

    [Test]
    public async Task A_service_user_in_the_docker_group_is_refused()
    {
        // THE DOCKER GROUP IS ROOT ON THE MACHINE. A runner holding it could
        // mount the host into a container and write anything, which is the
        // property the pool host's proxy exists to take away.
        var host = Machine();
        host.Homes["gg"] = "/var/lib/gg";
        host.Files["/var/lib/gg"] = new("", "gg", OwnerOnlyDirectory);
        host.Groups["gg"] = ["gg", "docker"];
        var before = host.Snapshot();

        var outcome = Install(host);

        await Assert.That(outcome.Done).IsFalse();
        await Assert.That(string.Join(" ", outcome.Said)).Contains("docker");
        await Assert.That(host.Snapshot()).IsEquivalentTo(before);
    }

    [Test]
    public async Task No_byte_of_gg_is_written_and_a_missing_gg_is_refused()
    {
        var bare = new FakeServiceHost(ServicePlatform.Systemd);

        var refused = Install(bare);

        await Assert.That(refused.Done).IsFalse();
        await Assert.That(string.Join(" ", refused.Said)).Contains("install.sh");
        await Assert.That(bare.Snapshot()).IsEmpty();

        var host = Machine();
        await Assert.That(Install(host).Done).IsTrue();

        await Assert.That(host.Written
                .Where(path => path.StartsWith("/usr/local/", StringComparison.Ordinal))
                .ToList())
            .IsEmpty()
            .Because("gg never moves its own bytes; install.sh does, by rename.");
        await Assert.That(host.Files[ServiceInstaller.Binary].Content).IsEqualTo("(gg)");
    }

    [Test]
    public async Task A_second_install_restarts_the_service_and_writes_nothing()
    {
        // THE UPDATE: install.sh has moved the bytes and the link, and asks
        // again. The seed is the machine's own file by now - the runner writes
        // what it is offered into it - so a second install must not touch it.
        var host = Machine();
        await Assert.That(Install(host).Done).IsTrue();
        var before = host.Snapshot();

        var again = ServiceInstaller.Install(host, new ServiceRequest(null, false, null), () => "");

        await Assert.That(again.Done).IsTrue().Because(string.Join(" ", again.Said));
        await Assert.That(host.Restarted).IsEquivalentTo((string[])["Systemd gg-runner-up"]);
        await Assert.That(host.Snapshot()).IsEquivalentTo(before);
    }

    [Test]
    public async Task Enrolling_an_installed_machine_again_is_refused_rather_than_ignored()
    {
        var host = Machine();
        await Assert.That(Install(host).Done).IsTrue();
        var before = host.Snapshot();
        var asked = false;

        var again = ServiceInstaller.Install(
            host, new ServiceRequest(ControlPlane, true, null), () => { asked = true; return "t"; });

        await Assert.That(again.Done).IsFalse();
        await Assert.That(string.Join(" ", again.Said)).Contains("gg service uninstall");
        await Assert.That(asked).IsFalse().Because("a token nobody will use is not asked for.");
        await Assert.That(host.Snapshot()).IsEquivalentTo(before);
    }

    [Test]
    public async Task A_first_install_needs_a_control_plane_and_a_machine_with_a_service_manager()
    {
        var host = Machine();

        await Assert.That(Install(host, controlPlane: null).Done).IsFalse();
        await Assert.That(Install(host, controlPlane: "not a url").Done).IsFalse();

        var nowhere = new FakeServiceHost(null)
        {
            Files = { [ServiceInstaller.Binary] = new("(gg)", "root", Mode(0x1ED)) },
        };
        var refused = Install(nowhere);
        await Assert.That(refused.Done).IsFalse();
        await Assert.That(string.Join(" ", refused.Said)).Contains("systemd");
    }

    [Test]
    public async Task A_user_name_that_could_break_a_unit_is_refused()
    {
        foreach (var hostile in (string[])["root", "gg\nExecStartPre=/bin/sh", "g g", "../gg", ""])
        {
            var host = Machine();

            var outcome = Install(host, user: hostile);

            await Assert.That(outcome.Done).IsFalse().Because($"'{hostile}' was accepted.");
            await Assert.That(host.Snapshot()).IsEquivalentTo(Machine().Snapshot());
        }
    }

    [Test]
    public async Task Service_install_and_uninstall_parse_and_take_no_token_argument()
    {
        await Assert.That(CliArgs.Parse(
                ["service", "install", "--control-plane", ControlPlane, "--enroll", "--user", "runner"]))
            .IsEqualTo(new CliAction.ServiceInstall(ControlPlane, true, "runner"));
        await Assert.That(CliArgs.Parse(["service", "install"]))
            .IsEqualTo(new CliAction.ServiceInstall(null, false, null));
        await Assert.That(CliArgs.Parse(["service", "uninstall"]))
            .IsEqualTo(new CliAction.ServiceUninstall());

        // A TOKEN AFTER --enroll IS A PERSON PASTING IT INTO ARGV, which is the
        // one place it must not be. Refused, not absorbed.
        await Assert.That(CliArgs.Parse(["service", "install", "--enroll", "abc123"]))
            .IsTypeOf<CliAction.Unknown>();
        await Assert.That(CliArgs.Parse(["service", "install", "--control-plane"]))
            .IsTypeOf<CliAction.Unknown>();
    }
}

/// <summary>A machine that exists only in memory: files, users, and a service manager's log.</summary>
internal sealed class FakeServiceHost(ServicePlatform? platform) : IServiceHost
{
    internal sealed record Entry(string Content, string Owner, UnixFileMode Mode, bool Directory = false);

    public ServicePlatform? Platform { get; } = platform;

    public bool IsElevated { get; set; } = true;

    public Dictionary<string, Entry> Files { get; } = new(StringComparer.Ordinal);

    public Dictionary<string, string> Homes { get; } = new(StringComparer.Ordinal);

    public Dictionary<string, string[]> Groups { get; } = new(StringComparer.Ordinal);

    public List<string> UsersCreated { get; } = [];

    public List<string> Written { get; } = [];

    public List<string> Started { get; } = [];

    public List<string> Restarted { get; } = [];

    public List<string> Stopped { get; } = [];

    /// <summary>Every path, so a before and an after can be compared.</summary>
    public List<string> Snapshot() => [.. Files.Keys.Order(StringComparer.Ordinal)];

    public bool Exists(string path) => Files.ContainsKey(path);

    public string? HomeOf(string user) => Homes.GetValueOrDefault(user);

    public IReadOnlyList<string> GroupsOf(string user) => Groups.GetValueOrDefault(user) ?? [user];

    public void CreateUser(string user, string home)
    {
        Require();
        UsersCreated.Add(user);
        Homes[user] = home;
        Groups[user] = [user];
        Files[home] = new("", user, (UnixFileMode)0x1C0, Directory: true);
    }

    public void CreateDirectory(string path, string owner, UnixFileMode mode)
    {
        Require();
        Parent(path);
        Written.Add(path);
        Files[path] = new("", owner, mode, Directory: true);
    }

    public void WriteFile(string path, string content, string owner, UnixFileMode mode)
    {
        Require();
        Parent(path);
        Written.Add(path);
        Files[path] = new(content, owner, mode);
    }

    public string? ReadFile(string path) =>
        Files.TryGetValue(path, out var entry) && !entry.Directory ? entry.Content : null;

    public void Delete(string path)
    {
        Require();

        if (Files.TryGetValue(path, out var entry) && entry.Directory && !IsEmptyDirectory(path))
        {
            throw new ServiceHostException($"{path} is not empty");
        }

        Files.Remove(path);
    }

    public bool IsEmptyDirectory(string path) =>
        Files.TryGetValue(path, out var entry) && entry.Directory
        && !Files.Keys.Any(k => k.StartsWith(path + "/", StringComparison.Ordinal));

    public void Start(ServicePlatform platform, string name, string definition)
    {
        Require();

        if (!Files.ContainsKey(definition))
        {
            throw new ServiceHostException($"{definition} is not there to start");
        }

        Started.Add($"{platform} {name}");
    }

    public void Restart(ServicePlatform platform, string name) => Restarted.Add($"{platform} {name}");

    public void Stop(ServicePlatform platform, string name, string definition) =>
        Stopped.Add($"{platform} {name}");

    public void Forget(ServicePlatform platform, string name)
    {
    }

    private void Require()
    {
        if (!IsElevated)
        {
            throw new ServiceHostException("permission denied");
        }
    }

    private void Parent(string path)
    {
        var parent = path[..path.LastIndexOf('/')];

        // THE FAKE IS STRICT ABOUT PARENTS for the reason a real disk is: a
        // file whose directory nobody made is one the real host would fail to
        // write, and a test that let it would prove an install nothing can run.
        if (parent.Length > 0 && !Files.ContainsKey(parent) && !Implicit(parent))
        {
            throw new ServiceHostException($"{parent} does not exist");
        }
    }

    /// <summary>The directories every machine has, which nothing installs.</summary>
    private static bool Implicit(string directory) =>
        directory is "/etc" or "/etc/systemd" or "/etc/systemd/system"
            or "/Library" or "/Library/LaunchDaemons"
            or "/var" or "/var/lib" or "/private" or "/private/var"
            or "/usr" or "/usr/local" or "/usr/local/bin";
}
