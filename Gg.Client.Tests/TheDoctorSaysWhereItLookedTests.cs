using System.Text.RegularExpressions;

namespace Gg.Client.Tests;

/// <summary>
/// The doctor, run from a shell on a machine with a gg service installed, says
/// which of its checks describe that shell and not the service.
/// </summary>
/// <remarks>
/// <para>
/// <b>A healthy pool host read as one configured for nothing.</b> Run from a
/// person's shell on vmlinux001, the runner check read that person's session
/// and the pool check read <c>GG_POOL_ENDPOINT</c> from that shell and its
/// <c>config.json</c> - while the maintainer ran under its own user, with its
/// own runner identity and the endpoint in its unit's <c>Environment=</c>. Both
/// checks were true about the shell and reported as if they were about the host.
/// </para>
/// <para>
/// <b>Said, not guessed.</b> The doctor cannot read a unit's environment
/// without privileges a doctor should not need, so it does not pretend to: it
/// names the unit it found, says the check describes this shell, and reports
/// that as a disclosure - how this works - rather than as a failure of a host
/// that is fine.
/// </para>
/// </remarks>
public class TheDoctorSaysWhereItLookedTests
{
    private static readonly IReadOnlyList<string> AMaintainer = ["gg-runner-maintain.service"];

    [Test]
    public async Task A_pool_check_beside_an_installed_unit_says_it_read_this_shell()
    {
        var check = Doctor.PoolCheck(new MachineRole { InstalledUnits = AMaintainer });

        await Assert.That(check.Outcome).IsEqualTo(DoctorOutcome.Disclosure)
            .Because("the host may well maintain a pool; this shell cannot see the unit's "
                   + "environment, and a failure here sent a person to reconfigure a healthy host.");
        await Assert.That(check.Detail).Contains("this shell, not the unit");
        await Assert.That(check.Detail).Contains("gg-runner-maintain.service");
    }

    [Test]
    public async Task A_pool_check_with_no_unit_installed_is_unchanged()
    {
        var check = Doctor.PoolCheck(MachineRole.None);

        await Assert.That(check.Outcome).IsEqualTo(DoctorOutcome.Fail);
        await Assert.That(check.Detail).Contains("this host maintains no pool");
    }

    [Test]
    public async Task A_runner_check_beside_an_installed_unit_says_the_unit_has_its_own_identity()
    {
        var check = Doctor.RunnerCheck(stored: null, honoured: null, units: AMaintainer);

        await Assert.That(check.Outcome).IsEqualTo(DoctorOutcome.Disclosure);
        await Assert.That(check.Detail).Contains("this shell, not the unit");
        await Assert.That(check.Detail).Contains("gg-runner-maintain.service");
    }

    [Test]
    public async Task A_runner_check_with_no_unit_installed_is_unchanged()
    {
        var check = Doctor.RunnerCheck(stored: null, honoured: null);

        await Assert.That(check.Outcome).IsEqualTo(DoctorOutcome.Fail);
        await Assert.That(check.Fix).IsEqualTo("gg login");
    }

    [Test]
    public async Task The_units_found_are_gg_services_and_nothing_else()
    {
        var root = Directory.CreateTempSubdirectory("gg-units-");
        try
        {
            var systemd = Directory.CreateDirectory(Path.Combine(root.FullName, "systemd")).FullName;
            var launchd = Directory.CreateDirectory(Path.Combine(root.FullName, "launchd")).FullName;

            foreach (var name in new[] { "gg-runner-up.service", "gg-runner-maintain.service", "sshd.service", "gg-runner-up.service.d" })
            {
                File.WriteAllText(Path.Combine(systemd, name), "");
            }

            foreach (var name in new[] { "dev.glyphguild.gg-runner-up.plist", "com.apple.thing.plist" })
            {
                File.WriteAllText(Path.Combine(launchd, name), "");
            }

            var found = InstalledUnits.Find(systemd, launchd);

            await Assert.That(found).IsEquivalentTo(
                ["dev.glyphguild.gg-runner-up.plist", "gg-runner-maintain.service", "gg-runner-up.service"]);
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    [Test]
    public async Task A_machine_with_neither_directory_has_no_units()
    {
        var found = InstalledUnits.Find(
            Path.Combine(Path.GetTempPath(), "gg-nowhere-" + Guid.NewGuid()),
            Path.Combine(Path.GetTempPath(), "gg-nowhere-" + Guid.NewGuid()));

        await Assert.That(found).IsEmpty();
    }

    [Test]
    public async Task The_doctor_the_root_composes_is_told_what_is_installed()
    {
        var here = new DirectoryInfo(AppContext.BaseDirectory);
        while (here is not null && !Directory.Exists(Path.Combine(here.FullName, "Gg.Cli")))
        {
            here = here.Parent;
        }

        var program = File.ReadAllText(Path.Combine(here!.FullName, "Gg.Cli", "Program.cs"));
        var role = Regex.Match(
            program,
            @"new MachineRole\s*\{(?<body>(?>[^{}]+|\{(?<depth>)|\}(?<-depth>))*(?(depth)(?!)))\}",
            RegexOptions.Singleline);

        await Assert.That(role.Success).IsTrue()
            .Because("the scan must find the doctor's MachineRole, or it proves nothing.");
        await Assert.That(role.Groups["body"].Value).Contains("InstalledUnits =")
            .Because("a doctor never told what is installed says nothing about it, however "
                   + "right its checks are.");
    }
}
