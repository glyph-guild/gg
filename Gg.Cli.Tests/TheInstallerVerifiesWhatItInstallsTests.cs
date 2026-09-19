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
/// </remarks>
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

        var nowhere = await box.RunAsync("--version", "0.38.0");
        await Assert.That(nowhere.Exit).IsNotEqualTo(0);
        await Assert.That(nowhere.Output).Contains("--control-plane");

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

        await Assert.That(await File.ReadAllTextAsync(workflow)).Contains("deploy/install.sh");
    }

    /// <summary>A directory standing in for one machine and one release page.</summary>
    private sealed class Sandbox : IDisposable
    {
        private readonly string _dir = Directory.CreateTempSubdirectory("gg-install-").FullName;

        private string Stubs => Path.Combine(_dir, "stubs");

        private string Log => Path.Combine(_dir, "log");

        public string Root => Path.Combine(_dir, "root");

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

            // THE REAL TOOLS, AND NOT THE REAL VERIFIER. A PATH of symlinks
            // rather than /usr/bin, because a build agent has one installed in
            // /usr/bin and the fallback would never be exercised there.
            foreach (var tool in (string[])
                     ["sh", "uname", "mktemp", "mkdir", "tar", "gzip", "mv", "ln", "rm", "rmdir",
                      "grep", "cut", "sha256sum", "shasum", "chmod", "cat", "stty", "id", "dirname"])
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

            foreach (var rid in (string[])["linux-x64", "osx-arm64"])
            {
                System.IO.File.WriteAllBytes(Path.Combine(directory, $"gg-{rid}.tar.gz"), bytes);
            }

            return Convert.ToHexStringLower(SHA256.HashData(bytes));
        }

        public void Attest(string digest) =>
            System.IO.File.WriteAllText(
                Path.Combine(_dir, "attestations", $"sha256:{digest}"),
                """{"attestations":[{"bundle":{"mediaType":"application/vnd.dev.sigstore.bundle.v0.3+json"}}]}""");

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
            start.Environment["PATH"] = Stubs;
            start.Environment["HOME"] = _dir;
            start.Environment["STUB_DIR"] = _dir;

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
