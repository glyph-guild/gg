using System.Diagnostics;
using System.Formats.Tar;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace Gg.Cli.Tests;

/// <summary>
/// <c>install.sh</c> installs only bytes an attestation vouches for, installs
/// them by rename beside the last version, and a second run is the update.
/// </summary>
/// <remarks>
/// <para>
/// <b>Slice forty-three, rule 22 (S43.5-02).</b> The release notes told a
/// person to pipe a download straight into <c>tar</c>, and said themselves that
/// this extracts before anything could have checked it - "not on a machine
/// that will hold credentials". A runner is exactly that machine. So the
/// installer checks first, and these tests hold the order.
/// </para>
/// <para>
/// <b>The real script, run.</b> Reading it for the words would pass a script
/// that names the check and skips it. So each test runs it under <c>sh</c> with
/// a stubbed download, a stubbed verifier and a stubbed gg, on a PATH with no
/// real verifier on it - the machine this is for has none either.
/// </para>
/// <para>
/// <b>Never beside a real handshake, and one at a time.</b> Each test here runs
/// a shell that starts a dozen more processes, and a handshake starved of CPU on
/// a two-core runner reports <c>NoRouteBetweenUs</c> - the flake
/// <c>AConsoleReachesARunnerTests</c> records and serialises its own peers
/// against. Run beside these, <c>The_runner_knows_the_console_arrived</c> failed
/// that way on CI three runs in a row, where it had passed on every recent main.
/// The key is that class's, because the reason is: CPU a handshake is waiting on.
/// </para>
/// </remarks>
[NotInParallel("a-real-webrtc-handshake")]
public class TheInstallerVerifiesWhatItInstallsTests
{
    private const string ControlPlane = "https://cp.example";

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Gg.sln")))
        {
            directory = directory.Parent;
        }

        return (directory ?? throw new InvalidOperationException("Gg.sln not found")).FullName;
    }

    private static string Script() => Path.Combine(RepoRoot(), "deploy", "install.sh");

    [Test]
    public async Task A_laptop_takes_the_binary_and_no_service()
    {
        // THE ONE COMMAND A PERSON RUNS ON THEIR OWN MACHINE. Until this, the
        // script always ended in `gg service install` - so the only scripted
        // install made the laptop a runner, with a service user and a second
        // row in the fleet, and the alternative was four manual steps in the
        // README where the second one is silently load-bearing.
        using var box = new Sandbox();
        box.Attest(box.Release("0.42.0"));

        var installed = await box.RunAsync("--version", "0.42.0");

        await Assert.That(installed.Exit).IsEqualTo(0).Because(installed.Output);
        await Assert.That(box.GgArguments()).IsEmpty()
            .Because("nothing on a laptop is a service, and a service install here would "
                   + "make one - a gg user, a unit, and a runner nobody asked for.");
        await Assert.That(installed.Output).Contains("not a runner")
            .Because("said, because the difference between this and a runner install is the "
                   + "whole question somebody is answering when they run it.");
        await Assert.That(installed.Output).Contains("gg config set control-plane")
            .Because("the default control plane is localhost, so a laptop that was told "
                   + "nothing points at nothing - and this is where a person finds that out.");
        await Assert.That(installed.Output).Contains("gg login");
    }

    [Test]
    public async Task A_control_plane_is_what_makes_it_a_runner()
    {
        // THE OTHER HALF, and the reason the laptop case is safe: every
        // existing caller - cloud-init, the runbooks, a machine being enrolled
        // - passes --control-plane, and they still get exactly what they got.
        using var box = new Sandbox();
        box.Attest(box.Release("0.42.0"));

        var installed = await box.RunAsync("--version", "0.42.0", "--control-plane", ControlPlane);

        await Assert.That(installed.Exit).IsEqualTo(0).Because(installed.Output);
        await Assert.That(box.GgArguments())
            .IsEquivalentTo((string[])[$"service install --control-plane {ControlPlane}"]);
    }

    [Test]
    [Arguments("Linux", "x86_64", "gg-linux-x64.tar.gz")]
    [Arguments("Linux", "aarch64", "gg-linux-arm64.tar.gz")]
    [Arguments("Darwin", "arm64", "gg-osx-arm64.tar.gz")]
    [Arguments("Darwin", "x86_64", "gg-osx-x64.tar.gz")]
    public async Task Every_platform_the_release_builds_is_one_this_installs(
        string kernel, string machine, string asset)
    {
        // A PLATFORM THE RELEASE CARRIES AND THE SCRIPT REFUSES is a download
        // somebody does by hand from a page, with the second install line -
        // the one that decides whether the console has its bar - left to them.
        using var box = new Sandbox();
        box.Attest(box.Release("0.42.0"));
        box.Uname(kernel, machine);

        var installed = await box.RunAsync("--version", "0.42.0");

        await Assert.That(installed.Exit).IsEqualTo(0).Because(installed.Output);
        await Assert.That(string.Join(" ", box.Downloads())).Contains(asset);
    }

    [Test]
    public async Task Windows_is_sent_to_the_script_that_can_install_it()
    {
        // NOT "gg is released for Linux and macOS" any more. The release
        // carries win-x64, and a person on Windows running this in git-bash
        // should be told where its installer is rather than that their
        // platform does not exist.
        using var box = new Sandbox();
        box.Attest(box.Release("0.42.0"));
        box.Uname("MINGW64_NT-10.0", "x86_64");

        var refused = await box.RunAsync("--version", "0.42.0");

        await Assert.That(refused.Exit).IsNotEqualTo(0);
        await Assert.That(refused.Output).Contains("install.ps1");
    }

    [Test]
    public async Task A_download_its_attestation_does_not_vouch_for_is_never_installed()
    {
        using var box = new Sandbox();
        box.Release("0.38.0");
        box.Verifier(verifies: false);

        var run = await box.RunAsync("--version", "0.38.0", "--control-plane", ControlPlane);

        await Assert.That(run.Exit).IsNotEqualTo(0).Because(run.Output);
        await Assert.That(Directory.Exists(box.Lib("0.38.0"))).IsFalse();
        await Assert.That(box.Link()).IsNull();
        await Assert.That(box.GgArguments()).IsEmpty()
            .Because("an unverified gg must not even be run.");
        await Assert.That(box.VerifierArguments())
            .Contains(a => a.StartsWith("attestation verify", StringComparison.Ordinal)
                        && a.Contains("--repo glyph-guild/gg", StringComparison.Ordinal));
    }

    [Test]
    public async Task Without_a_verifier_the_downloaded_bytes_are_asked_about_by_their_digest()
    {
        // THE MACHINE THIS IS FOR: a fresh VM with curl and nothing else. The
        // question is asked about the bytes that arrived, by digest, so bytes
        // swapped on the release page have no attestation to be named by.
        using var box = new Sandbox();
        var digest = box.Release("0.38.0");
        box.Attest(digest);

        var run = await box.RunAsync("--version", "0.38.0", "--control-plane", ControlPlane);

        await Assert.That(run.Exit).IsEqualTo(0).Because(run.Output);
        await Assert.That(box.Downloads())
            .Contains(url => url.EndsWith($"/attestations/sha256:{digest}", StringComparison.Ordinal));

        using var tampered = new Sandbox();
        tampered.Release("0.38.0", marker: "somebody else's bytes");
        tampered.Attest(digest);

        var refused = await tampered.RunAsync("--version", "0.38.0", "--control-plane", ControlPlane);

        await Assert.That(refused.Exit).IsNotEqualTo(0).Because(refused.Output);
        await Assert.That(Directory.Exists(tampered.Lib("0.38.0"))).IsFalse();
        await Assert.That(tampered.GgArguments()).IsEmpty();
    }

    [Test]
    public async Task A_version_is_installed_beside_the_last_and_the_link_moves_to_it()
    {
        using var box = new Sandbox();
        box.Attest(box.Release("0.38.0"));
        box.Attest(box.Release("0.39.0", marker: "newer"));

        var first = await box.RunAsync("--version", "0.38.0", "--control-plane", ControlPlane);

        await Assert.That(first.Exit).IsEqualTo(0).Because(first.Output);
        await Assert.That(File.Exists(Path.Combine(box.Lib("0.38.0"), "gg"))).IsTrue();
        await Assert.That(box.Link()).IsEqualTo("/usr/local/lib/gg/0.38.0/gg");
        await Assert.That(box.GgArguments())
            .IsEquivalentTo((string[])[$"service install --control-plane {ControlPlane}"]);

        // INSTALLED, as far as the next run can tell: the manifest gg service
        // install writes is where the installer looks.
        box.Installed();

        var update = await box.RunAsync("--version", "0.39.0");

        await Assert.That(update.Exit).IsEqualTo(0).Because(update.Output);
        await Assert.That(File.Exists(Path.Combine(box.Lib("0.39.0"), "gg"))).IsTrue();
        await Assert.That(File.Exists(Path.Combine(box.Lib("0.38.0"), "gg"))).IsTrue()
            .Because("the last version stays, so going back is one more run.");
        await Assert.That(box.Link()).IsEqualTo("/usr/local/lib/gg/0.39.0/gg");
        await Assert.That(box.GgArguments().Last()).IsEqualTo("service install")
            .Because("an installed machine's service is restarted onto the new gg, not re-seeded.");

        await Assert.That(box.Leftovers()).IsEmpty()
            .Because("nothing half-written is left beside what the link can point at.");
    }

    [Test]
    public async Task A_version_is_always_named_and_a_first_install_names_its_control_plane()
    {
        using var box = new Sandbox();
        box.Attest(box.Release("0.38.0"));

        var unnamed = await box.RunAsync("--control-plane", ControlPlane);
        await Assert.That(unnamed.Exit).IsNotEqualTo(0);
        await Assert.That(unnamed.Output).Contains("--version");

        // AND A FIRST INSTALL NO LONGER REFUSES WITHOUT ONE, because a laptop
        // is a first install too and has no control plane to name at install
        // time. What --control-plane decides now is whether this machine
        // becomes a RUNNER; the test below holds that half.
        await Assert.That(box.Downloads()).IsEmpty()
            .Because("a refusal the script could make before downloading is made before downloading.");
    }

    [Test]
    public async Task An_enrollment_token_reaches_gg_on_stdin_and_never_in_its_arguments()
    {
        using var box = new Sandbox();
        box.Attest(box.Release("0.38.0"));
        var token = box.Write("token", "enroll-secret-123\n");

        var run = await box.RunAsync(
            "--version", "0.38.0", "--control-plane", ControlPlane, "--enroll-file", token);

        await Assert.That(run.Exit).IsEqualTo(0).Because(run.Output);
        await Assert.That(box.GgArguments().Single()).Contains("--enroll");
        await Assert.That(box.GgArguments().Single()).DoesNotContain("enroll-secret-123");
        await Assert.That(box.GgInput().Trim()).IsEqualTo("enroll-secret-123");
        await Assert.That(run.Output).DoesNotContain("enroll-secret-123");
    }

    [Test]
    public async Task The_script_takes_no_ownership_from_the_archive_and_copies_nothing_into_place()
    {
        var text = await File.ReadAllTextAsync(Script());

        // RUN AS ROOT, tar restores the owner the archive recorded - the build
        // machine's user id, which on this machine may be anybody, including the
        // service user. A gg its runner can write is a runner that can replace
        // what the OS starts.
        await Assert.That(text).Contains("--no-same-owner");
        await Assert.That(System.Text.RegularExpressions.Regex.IsMatch(text, @"(^|[\s;|&])cp\s"))
            .IsFalse().Because("bytes copied over a binary macOS has already validated get the next "
                             + "run killed with no output; a rename gives it a new inode.");
        await Assert.That(text).DoesNotContain("latest");
        await Assert.That(text).Contains(ServiceInstaller.Manifest);
    }

    [Test]
    public async Task The_release_carries_the_installer_so_it_is_attested_with_the_binaries()
    {
        var workflow = Directory
            .EnumerateFiles(RepoRoot(), "publish-cli.yml", SearchOption.AllDirectories)
            .Single(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}",
                StringComparison.Ordinal));

        var text = await File.ReadAllTextAsync(workflow);

        await Assert.That(text).Contains("deploy/install.sh");

        // AND THE WINDOWS ONE, because the sh script now refuses a Windows
        // machine by naming a URL - and a refusal pointing at an asset the
        // release does not carry is worse than the refusal it replaced.
        await Assert.That(text).Contains("deploy/install.ps1");

        await Assert.That(File.Exists(Path.Combine(RepoRoot(), "deploy", "install.ps1"))).IsTrue();
    }

    [Test]
    public async Task Every_step_that_runs_a_shell_script_says_which_shell()
    {
        // WHAT THIS COSTS WHEN IT IS MISSING, measured on the first run that
        // built win-x64: the step that fetches the pinned SIPSorcery fork had no
        // `shell:`, so the Windows runner ran it under pwsh, which cannot run a
        // .sh file. It did not fail. PowerShell could not CreateProcess an
        // unknown extension, fell back to ShellExecute, Git for Windows'
        // file association launched it detached, and pwsh exited 0 having waited
        // for nothing. The step reported success in 0.6 seconds, printed not one
        // line, and placed no package; the publish that followed failed with
        // NU1301 on a directory the "successful" step was supposed to create.
        //
        // A step that silently does nothing is worse than one that fails, so the
        // guard is over the shape: a `run:` naming a .sh must say `shell: bash`.
        // The forge's default shell is bash on Unix and pwsh on Windows, so a step
        // without one means a different program on each runner in the matrix -
        // and this workflow's matrix has had a Windows runner in it since 0.43.0.
        var workflow = Directory
            .EnumerateFiles(RepoRoot(), "publish-cli.yml", SearchOption.AllDirectories)
            .Single(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}",
                StringComparison.Ordinal));

        var lines = await File.ReadAllLinesAsync(workflow);

        // Steps, by the `- ` that starts one. Cheaper than a YAML parser and it
        // reads the file a reviewer reads.
        var steps = new List<List<string>>();
        foreach (var line in lines)
        {
            if (System.Text.RegularExpressions.Regex.IsMatch(line, @"^\s+- "))
            {
                steps.Add([]);
            }

            if (steps.Count > 0)
            {
                steps[^1].Add(line);
            }
        }

        var unshelled = steps
            .Where(step => step.Any(l => l.Contains("run:", StringComparison.Ordinal)
                                      && l.Contains(".sh", StringComparison.Ordinal)))
            .Where(step => !step.Any(l => l.Contains("shell: bash", StringComparison.Ordinal)))
            .Select(step => step[0].Trim())
            .ToList();

        await Assert.That(unshelled).IsEmpty()
            .Because("a step running a .sh with no `shell: bash` runs under pwsh on a Windows "
                   + "runner, which launches it detached and exits 0 without it. Found: "
                   + string.Join(" / ", unshelled));
    }

    [Test]
    public async Task No_step_pipes_into_a_grep_that_stops_reading()
    {
        // MEASURED IN ANGER. Adding `shell: bash` to the packaging steps - so
        // the same steps serve the Windows runner - also sets -o pipefail, and
        // `tar -tzf … | grep -q` makes grep close the pipe on its first match.
        // tar then dies of SIGPIPE, the pipeline reports failure, and the step
        // that exists to catch a missing native library reported both of them
        // missing from a tarball that carried them.
        //
        // THE SHAPE, NOT THAT ONE LINE: any producer piped into a grep that
        // stops reading is the same bug waiting. List once into a file and grep
        // the file.
        var workflow = Directory
            .EnumerateFiles(RepoRoot(), "publish-cli.yml", SearchOption.AllDirectories)
            .Single(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}",
                StringComparison.Ordinal));

        var piped = (await File.ReadAllLinesAsync(workflow))
            .Where(line => line.Contains("| grep -q", StringComparison.Ordinal))
            .ToList();

        await Assert.That(piped).IsEmpty()
            .Because("grep -q stops reading at its first match, so under pipefail the producer "
                   + "on its left fails and takes the step with it. Found: "
                   + string.Join(" / ", piped));
    }

    [Test]
    public async Task The_windows_installer_makes_no_runner_and_verifies_what_it_unpacks()
    {
        // ASSERTED OVER ITS SOURCE, because a PowerShell script cannot be run
        // from this suite: the two claims that matter are that it does not make
        // a service - gg on Windows is the command line, and a Windows runner
        // is another slice's - and that it checks the bytes before extracting
        // them, which is the sh script's rule and the same reason.
        var script = await File.ReadAllTextAsync(
            Path.Combine(RepoRoot(), "deploy", "install.ps1"));

        await Assert.That(script).DoesNotContain("service install")
            .Because("nothing on a laptop is a service, and the console UI Windows would need "
                   + "is not written - gg there opens an editor and says so.");
        await Assert.That(script).Contains("attestation verify")
            .Because("whoever can replace an asset on a release page can replace a checksum "
                   + "beside it, so the proof is the build's attestation.");
        await Assert.That(script).Contains("Get-FileHash")
            .Because("without gh the digest is still printed, so somebody can compare it - "
                   + "refusing instead would make the attestation a dependency of installing.");
        await Assert.That(script.IndexOf("Get-FileHash", StringComparison.Ordinal))
            .IsLessThan(script.IndexOf("tar -xzf", StringComparison.Ordinal))
            .Because("checked before anything is extracted, which is the whole point of "
                   + "checking.");
    }

    // ---- gg is on the PATH of whoever ran this ----

    private const string PathLine = "export PATH=\"/usr/local/bin:$PATH\"";

    [Test]
    [Arguments("Linux", "/bin/zsh", ".zshrc")]
    [Arguments("Linux", "/usr/bin/zsh", ".zshrc")]
    [Arguments("Linux", "/bin/bash", ".bashrc")]
    [Arguments("Darwin", "/bin/bash", ".bash_profile")]
    [Arguments("Linux", "/bin/sh", ".profile")]
    [Arguments("Linux", "/usr/bin/dash", ".profile")]
    public async Task The_bin_directory_is_put_on_the_path_of_the_shell_that_ran_it(
        string kernel, string shell, string startup)
    {
        // "gg: command not found" AFTER a successful install is the first thing
        // a person meets on a machine whose PATH lacks /usr/local/bin - a
        // stripped container, a shell whose rc rewrites PATH, a distro with no
        // /etc/environment. The script knew exactly where it put the link and
        // said nothing about whether the shell would find it.
        //
        // THE SHELL THAT RAN IT, not the shell it is running in: this script is
        // /bin/sh, and the person is in zsh. Under sudo $SHELL is root's, so the
        // script reads the invoking user's - but with no SUDO_USER, as here, it
        // is the environment's.
        using var box = new Sandbox();
        box.Attest(box.Release("0.42.0"));
        box.Uname(kernel, "x86_64");
        box.Environment["SHELL"] = shell;

        var installed = await box.RunAsync("--version", "0.42.0");

        await Assert.That(installed.Exit).IsEqualTo(0).Because(installed.Output);

        var file = Path.Combine(box.Home, startup);
        await Assert.That(File.Exists(file)).IsTrue()
            .Because($"{shell} reads {startup} on this platform, and that is where PATH has "
                   + "to be set for the next terminal to find gg.");
        await Assert.That(await File.ReadAllTextAsync(file)).Contains(PathLine);
        await Assert.That(installed.Output).Contains(startup)
            .Because("a file edited in somebody's home is named, so they know what changed "
                   + "and where to look if they would rather it had not.");
    }

    [Test]
    public async Task Fish_is_given_its_own_spelling_in_its_own_directory()
    {
        // fish does not read POSIX `export`, and a line that is a syntax error in
        // the startup file breaks every new shell - worse than the missing PATH
        // it was meant to fix. conf.d is fish's place for exactly this, and
        // fish_add_path is idempotent on its own.
        using var box = new Sandbox();
        box.Attest(box.Release("0.42.0"));
        box.Environment["SHELL"] = "/usr/bin/fish";

        var installed = await box.RunAsync("--version", "0.42.0");

        await Assert.That(installed.Exit).IsEqualTo(0).Because(installed.Output);

        var file = Path.Combine(box.Home, ".config", "fish", "conf.d", "gg.fish");
        await Assert.That(File.Exists(file)).IsTrue();
        await Assert.That(await File.ReadAllTextAsync(file)).Contains("fish_add_path");
        await Assert.That(await File.ReadAllTextAsync(file)).DoesNotContain("export ");
    }

    [Test]
    public async Task A_path_that_already_has_it_is_left_alone_and_said_so()
    {
        // THE COMMON CASE ON A MAC AND MOST DISTROS - path_helper and
        // /etc/environment already put /usr/local/bin there - so the edit must
        // not happen, and the script still says what it found rather than
        // staying silent about the question.
        using var box = new Sandbox();
        box.Attest(box.Release("0.42.0"));
        box.Environment["SHELL"] = "/bin/zsh";
        box.BinAlreadyOnPath = true;

        var installed = await box.RunAsync("--version", "0.42.0");

        await Assert.That(installed.Exit).IsEqualTo(0).Because(installed.Output);
        await Assert.That(File.Exists(Path.Combine(box.Home, ".zshrc"))).IsFalse()
            .Because("a startup file nobody needed edited is a diff in somebody's home they "
                   + "did not ask for.");
        await Assert.That(installed.Output).Contains("on PATH");
    }

    [Test]
    public async Task Running_it_twice_adds_the_line_once()
    {
        // The update path IS running it again, so an rc file that grew a line
        // per release would carry one per version somebody ever installed.
        using var box = new Sandbox();
        box.Attest(box.Release("0.42.0"));
        box.Attest(box.Release("0.43.0"));
        box.Environment["SHELL"] = "/bin/zsh";

        await box.RunAsync("--version", "0.42.0");
        var again = await box.RunAsync("--version", "0.43.0");

        await Assert.That(again.Exit).IsEqualTo(0).Because(again.Output);

        var lines = await File.ReadAllLinesAsync(Path.Combine(box.Home, ".zshrc"));
        await Assert.That(lines.Count(l => l.Contains("/usr/local/bin", StringComparison.Ordinal)))
            .IsEqualTo(1);
    }

    [Test]
    public async Task Without_a_known_shell_it_writes_nothing_and_says_what_to_add()
    {
        // No SHELL and no passwd entry to read one from - a bare container, an
        // unusual login. Guessing a file to edit is how somebody's startup gets
        // a line for a shell they do not use; saying the line is enough.
        using var box = new Sandbox();
        box.Attest(box.Release("0.42.0"));

        var installed = await box.RunAsync("--version", "0.42.0");

        await Assert.That(installed.Exit).IsEqualTo(0).Because(installed.Output);
        await Assert.That(Directory.EnumerateFiles(box.Home, ".*").Select(Path.GetFileName))
            .DoesNotContain(".profile");
        await Assert.That(installed.Output).Contains(PathLine)
            .Because("the line a person adds by hand is the one thing the script can still "
                   + "give them when it cannot tell which file to put it in.");
    }

    [Test]
    public async Task The_windows_installer_puts_its_prefix_on_the_users_path_without_a_leading_separator()
    {
        // The Windows installer already edits PATH - the user's, never the
        // machine's, because a per-user install is the whole point of its
        // default prefix. What it got wrong is the join: on a profile whose
        // user PATH is empty, "$userPath;$Prefix" writes ";C:\...\gg", and a
        // leading empty entry means "the current directory" to cmd.exe - a
        // PATH that runs whatever is in the folder you happen to be in.
        var script = await File.ReadAllTextAsync(
            Path.Combine(RepoRoot(), "deploy", "install.ps1"));

        await Assert.That(script).Contains("'User'")
            .Because("a per-user install goes on the per-user PATH; nothing here has or needs "
                   + "administrator.");
        await Assert.That(script).DoesNotContain("'Machine'");
        await Assert.That(script).DoesNotContain("\"$userPath;$Prefix\"")
            .Because("joined with a separator whether or not there is anything before it, "
                   + "which on an empty user PATH is a leading empty entry.");
    }

    // ---- another program is already called gg ----

    [Test]
    public async Task Another_gg_on_the_path_is_not_overwritten_and_a_name_is_asked_for()
    {
        // github.com/u-quark/gg is a git GUI shipped as a single binary a person
        // puts on their PATH themselves. Installing over it would end with
        // /usr/local/bin/gg pointing at ours and theirs unreachable, or with
        // theirs winning and every line this script prints being false - and
        // the script never looked. A collision is a question for the person,
        // and with no terminal to ask on it is a refusal that names the flag.
        using var box = new Sandbox();
        box.Attest(box.Release("0.42.0"));
        var theirs = box.ForeignGg();

        var refused = await box.RunAsync("--version", "0.42.0");

        await Assert.That(refused.Exit).IsNotEqualTo(0);
        await Assert.That(refused.Output).Contains(theirs)
            .Because("the person is told WHICH gg is in the way, not just that one is.");
        await Assert.That(refused.Output).Contains("--as")
            .Because("and how to install ours under another name without being asked at a "
                   + "prompt this run has no terminal for.");
        await Assert.That(box.Downloads()).IsEmpty()
            .Because("everything that can be refused without a download is refused before one.");
        await Assert.That(box.Exists("gg")).IsFalse();
    }

    [Test]
    public async Task A_file_already_at_the_link_is_never_overwritten()
    {
        // THE WORST CASE: theirs IS /usr/local/bin/gg. The link used to land by
        // `mv -f` over whatever was there, which for a real file is deleting
        // somebody's program without a word. PATH need not even contain the
        // directory for this to matter, so it is checked at the path itself.
        using var box = new Sandbox();
        box.Attest(box.Release("0.42.0"));
        var theirs = box.FileAtTheLink("#!/bin/sh\necho theirs\n");

        var refused = await box.RunAsync("--version", "0.42.0");

        await Assert.That(refused.Exit).IsNotEqualTo(0);
        await Assert.That(await File.ReadAllTextAsync(theirs)).Contains("theirs")
            .Because("a program that was there before this ran is there after it.");
        await Assert.That(box.LinkOf("gg")).IsNull();
    }

    [Test]
    public async Task An_alias_installs_the_link_under_that_name_and_every_hint_uses_it()
    {
        using var box = new Sandbox();
        box.Attest(box.Release("0.42.0"));
        box.ForeignGg();

        var installed = await box.RunAsync("--version", "0.42.0", "--as", "goodgrief");

        await Assert.That(installed.Exit).IsEqualTo(0).Because(installed.Output);
        await Assert.That(box.LinkOf("goodgrief")).IsEqualTo("/usr/local/lib/gg/0.42.0/gg");
        await Assert.That(box.Exists("gg")).IsFalse()
            .Because("theirs keeps the name; ours has the one the person chose.");
        await Assert.That(installed.Output).Contains("goodgrief config set control-plane")
            .Because("every next step this prints is typed by the person, under the name they "
                   + "will actually type.");
        await Assert.That(installed.Output).Contains("goodgrief login");
        await Assert.That(installed.Output).DoesNotContain("  gg config set");
    }

    [Test]
    public async Task The_name_is_remembered_so_an_update_needs_no_flag()
    {
        // The update path is running the script again with a newer version.
        // A person who chose a name once must not have to remember to say it
        // every release, and forgetting must not quietly install a second link
        // called gg beside the program they were avoiding.
        using var box = new Sandbox();
        box.Attest(box.Release("0.42.0"));
        box.Attest(box.Release("0.43.0"));
        box.ForeignGg();

        await box.RunAsync("--version", "0.42.0", "--as", "goodgrief");
        var updated = await box.RunAsync("--version", "0.43.0");

        await Assert.That(updated.Exit).IsEqualTo(0).Because(updated.Output);
        await Assert.That(box.LinkOf("goodgrief")).IsEqualTo("/usr/local/lib/gg/0.43.0/gg");
        await Assert.That(box.Exists("gg")).IsFalse();
    }

    [Test]
    public async Task A_runner_keeps_the_name_the_service_runs()
    {
        // The service unit runs /usr/local/bin/gg, spelled once in
        // ServiceInstaller.Binary. A renamed command on a runner is a service
        // that starts nothing, so the flag is refused there by name.
        using var box = new Sandbox();
        box.Attest(box.Release("0.42.0"));

        var refused = await box.RunAsync(
            "--version", "0.42.0", "--as", "goodgrief", "--control-plane", ControlPlane);

        await Assert.That(refused.Exit).IsNotEqualTo(0);
        await Assert.That(refused.Output).Contains(ServiceInstaller.Binary);
        await Assert.That(box.Downloads()).IsEmpty();
    }

    [Test]
    public async Task A_name_that_is_not_a_command_is_refused()
    {
        using var box = new Sandbox();
        box.Attest(box.Release("0.42.0"));

        var refused = await box.RunAsync("--version", "0.42.0", "--as", "../evil");

        await Assert.That(refused.Exit).IsNotEqualTo(0);
        await Assert.That(box.Downloads()).IsEmpty();
    }

    [Test]
    public async Task The_windows_installer_asks_the_same_question()
    {
        // Asserted over its source, as its sibling is. The three things that
        // matter: it looks for another gg on PATH before writing the shim, it
        // takes the name as a parameter for the run with nobody at the
        // keyboard, and the shim it writes carries that name.
        var script = await File.ReadAllTextAsync(
            Path.Combine(RepoRoot(), "deploy", "install.ps1"));

        await Assert.That(script).Contains("[string] $Alias");
        await Assert.That(script).Contains("Get-Command")
            .Because("a collision is found by asking PowerShell what `gg` resolves to, not by "
                   + "guessing directories.");
        await Assert.That(script).Contains("\"$Alias.cmd\"")
            .Because("the shim is the command a person types, so it carries the name they chose.");
        await Assert.That(script).DoesNotContain("'gg.cmd'");
    }

    /// <summary>A directory standing in for one machine and one release page.</summary>
    private sealed class Sandbox : IDisposable
    {
        private readonly string _dir = Directory.CreateTempSubdirectory("gg-install-").FullName;

        private string Stubs => Path.Combine(_dir, "stubs");

        private string Log => Path.Combine(_dir, "log");

        public string Root => Path.Combine(_dir, "root");

        /// <summary>The home the script is run with - where a startup file would land.</summary>
        public string Home => _dir;

        /// <summary>Extra environment for the run: the shell that ran it, mostly.</summary>
        public Dictionary<string, string> Environment { get; } = new(StringComparer.Ordinal);

        /// <summary>Whether /usr/local/bin is already on the PATH the script sees.</summary>
        public bool BinAlreadyOnPath { get; set; }

        /// <summary>Somebody else's program called gg, earlier on PATH than ours would be.</summary>
        public string ForeignGg()
        {
            Stub("gg", "#!/bin/sh\necho 'gg - git (G)UI'\n");
            return Path.Combine(Stubs, "gg");
        }

        /// <summary>A plain file already sitting where the link would go.</summary>
        public string FileAtTheLink(string content)
        {
            var bin = Path.Combine(Root, "usr", "local", "bin");
            Directory.CreateDirectory(bin);
            var path = Path.Combine(bin, "gg");
            System.IO.File.WriteAllText(path, content);
            return path;
        }

        /// <summary>Where the link named <paramref name="command"/> points, or null for no link.</summary>
        public string? LinkOf(string command) =>
            new FileInfo(Path.Combine(Root, "usr", "local", "bin", command)).LinkTarget;

        public bool Exists(string command) =>
            System.IO.File.Exists(Path.Combine(Root, "usr", "local", "bin", command));

        public Sandbox()
        {
            Directory.CreateDirectory(Stubs);
            Directory.CreateDirectory(Log);
            Directory.CreateDirectory(Root);
            Directory.CreateDirectory(Path.Combine(_dir, "attestations"));

            Stub("curl", """
                #!/bin/sh
                out=""; url=""
                while [ $# -gt 0 ]; do
                  case "$1" in
                    -o) out="$2"; shift 2 ;;
                    -*) shift ;;
                    *) url="$1"; shift ;;
                  esac
                done
                printf '%s\n' "$url" >> "$STUB_DIR/log/curl"
                case "$url" in
                  */attestations/*)
                    f="$STUB_DIR/attestations/${url##*/attestations/}"
                    [ -f "$f" ] || exit 22
                    if [ -n "$out" ]; then cat "$f" > "$out"; else cat "$f"; fi ;;
                  */download/*)
                    f="$STUB_DIR/releases/${url#*/download/}"
                    [ -f "$f" ] || exit 22
                    cat "$f" > "$out" ;;
                  *) exit 6 ;;
                esac
                """);

            // A RELEASED PLATFORM, WHATEVER THIS ONE IS. The script refuses a
            // machine gg is not released for, which is right for a machine and
            // wrong for a test: an arm64 Linux container running this suite
            // would be refused before anything under test ran.
            Stub("uname", """
                #!/bin/sh
                case "$1" in
                  -s) echo Linux ;;
                  -m) echo x86_64 ;;
                  *) echo Linux ;;
                esac
                """);

            // THE REAL TOOLS, AND NOT THE REAL VERIFIER. A PATH of symlinks
            // rather than /usr/bin, because a build agent has one installed in
            // /usr/bin and the fallback would never be exercised there.
            foreach (var tool in (string[])
                     ["sh", "mktemp", "mkdir", "tar", "gzip", "mv", "ln", "rm", "rmdir",
                      "grep", "cut", "sha256sum", "shasum", "chmod", "cat", "stty", "id", "dirname",
                      "readlink"])
            {
                var real = ((string[])["/usr/bin", "/bin", "/usr/sbin", "/sbin"])
                    .Select(d => Path.Combine(d, tool))
                    .FirstOrDefault(File.Exists);

                if (real is not null && !File.Exists(Path.Combine(Stubs, tool)))
                {
                    File.CreateSymbolicLink(Path.Combine(Stubs, tool), real);
                }
            }
        }

        public string Lib(string version) => Path.Combine(Root, "usr", "local", "lib", "gg", version);

        /// <summary>Where the link points, or null for no link.</summary>
        public string? Link()
        {
            var link = new FileInfo(Path.Combine(Root, "usr", "local", "bin", "gg"));
            return link.LinkTarget;
        }

        public void Installed()
        {
            var manifest = Root + ServiceInstaller.Manifest;
            Directory.CreateDirectory(Path.GetDirectoryName(manifest)!);
            System.IO.File.WriteAllText(manifest, "platform systemd\n");
        }

        public string Write(string name, string content)
        {
            var path = Path.Combine(_dir, name);
            System.IO.File.WriteAllText(path, content);
            return path;
        }

        /// <summary>A release's tarball for every platform this ships, returning its digest.</summary>
        public string Release(string version, string marker = "gg")
        {
            var bytes = Tarball(
                "#!/bin/sh\n"
              + $"# {marker} {version}\n"
              + "printf '%s\\n' \"$*\" >> \"$STUB_DIR/log/gg-args\"\n"
              + "cat > \"$STUB_DIR/log/gg-stdin\"\n");

            var directory = Path.Combine(_dir, "releases", $"v{version}");
            Directory.CreateDirectory(directory);

            foreach (var rid in (string[])["linux-x64", "linux-arm64", "osx-arm64", "osx-x64", "win-x64"])
            {
                System.IO.File.WriteAllBytes(Path.Combine(directory, $"gg-{rid}.tar.gz"), bytes);
            }

            return Convert.ToHexStringLower(SHA256.HashData(bytes));
        }

        public void Attest(string digest) =>
            System.IO.File.WriteAllText(
                Path.Combine(_dir, "attestations", $"sha256:{digest}"),
                """{"attestations":[{"bundle":{"mediaType":"application/vnd.dev.sigstore.bundle.v0.3+json"}}]}""");

        /// <summary>What `uname` says this machine is, for one test.</summary>
        /// <remarks>
        /// <b>Stubbed rather than skipped.</b> The script picks its asset from
        /// uname, and a suite that could only test the platform it happens to
        /// run on would cover one of the four the release builds.
        /// </remarks>
        public void Uname(string kernel, string machine) =>
            Stub("uname", $$"""
                #!/bin/sh
                case "$1" in
                  -s) echo {{kernel}} ;;
                  -m) echo {{machine}} ;;
                  *) echo {{kernel}} ;;
                esac
                """);

        public void Verifier(bool verifies) =>
            Stub("gh", $$"""
                #!/bin/sh
                printf '%s\n' "$*" >> "$STUB_DIR/log/gh"
                case "$1 $2" in
                  "auth status") exit 0 ;;
                  "attestation verify") exit {{(verifies ? 0 : 1)}} ;;
                esac
                exit 2
                """);

        public IReadOnlyList<string> Downloads() => Lines("curl");

        public IReadOnlyList<string> GgArguments() => Lines("gg-args");

        public IReadOnlyList<string> VerifierArguments() => Lines("gh");

        public string GgInput() =>
            System.IO.File.Exists(Path.Combine(Log, "gg-stdin"))
                ? System.IO.File.ReadAllText(Path.Combine(Log, "gg-stdin"))
                : "";

        /// <summary>Anything a run left half-done: a staging directory or a link not yet moved.</summary>
        public IReadOnlyList<string> Leftovers() =>
        [
            .. ((string[])[Path.Combine(Root, "usr", "local", "lib", "gg"),
                           Path.Combine(Root, "usr", "local", "bin")])
                .Where(Directory.Exists)
                .SelectMany(d => Directory.EnumerateFileSystemEntries(d))
                .Select(Path.GetFileName)
                .OfType<string>()
                .Where(name => name.StartsWith('.')),
        ];

        public async Task<(int Exit, string Output)> RunAsync(params string[] arguments)
        {
            var start = new ProcessStartInfo("/bin/sh")
            {
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };

            start.ArgumentList.Add(Script());
            start.ArgumentList.Add("--root");
            start.ArgumentList.Add(Root);

            foreach (var argument in arguments)
            {
                start.ArgumentList.Add(argument);
            }

            start.Environment.Clear();
            start.Environment["PATH"] = BinAlreadyOnPath ? Stubs + ":/usr/local/bin" : Stubs;
            start.Environment["HOME"] = _dir;
            start.Environment["STUB_DIR"] = _dir;

            foreach (var (name, value) in Environment)
            {
                start.Environment[name] = value;
            }

            using var process = Process.Start(start)!;
            process.StandardInput.Close();

            var output = process.StandardOutput.ReadToEndAsync();
            var error = process.StandardError.ReadToEndAsync();

            using var patience = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            await process.WaitForExitAsync(patience.Token);

            return (process.ExitCode, await output + await error);
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(_dir, recursive: true);
            }
            catch (IOException)
            {
            }
        }

        private IReadOnlyList<string> Lines(string name)
        {
            var path = Path.Combine(Log, name);
            return System.IO.File.Exists(path)
                ? System.IO.File.ReadAllLines(path)
                : [];
        }

        private void Stub(string name, string script)
        {
            var path = Path.Combine(Stubs, name);
            System.IO.File.WriteAllText(path, script.Replace("\r\n", "\n", StringComparison.Ordinal) + "\n");

            if (!OperatingSystem.IsWindows())
            {
                System.IO.File.SetUnixFileMode(path, (UnixFileMode)0x1ED);
            }
        }

        private static byte[] Tarball(string gg)
        {
            using var buffer = new MemoryStream();

            using (var gzip = new GZipStream(buffer, CompressionLevel.Fastest, leaveOpen: true))
            using (var tar = new TarWriter(gzip, TarEntryFormat.Pax, leaveOpen: true))
            {
                var entry = new PaxTarEntry(TarEntryType.RegularFile, "./gg")
                {
                    Mode = (UnixFileMode)0x1ED,
                    DataStream = new MemoryStream(Encoding.UTF8.GetBytes(gg)),
                };
                tar.WriteEntry(entry);
            }

            return buffer.ToArray();
        }
    }
}
