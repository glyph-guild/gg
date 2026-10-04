using System.Text.RegularExpressions;

namespace Gg.Runner.Tests;

/// <summary>
/// The pool host's registry can be tidied, held by reading the four files that
/// decide it rather than by trusting their comments.
/// </summary>
/// <remarks>
/// <para>
/// <b>Starting <c>registry:2</c> with no config is what filled the host.</b>
/// The image's default carries no <c>storage.delete.enabled</c>, so
/// <c>DELETE /v2/&lt;name&gt;/manifests/&lt;digest&gt;</c> answers 405
/// UNSUPPORTED whatever asks. Measured on this fleet's host: 12.05 GB over 25
/// tags, two of them pinned.
/// </para>
/// <para>
/// <b>And a delete frees nothing by itself.</b> Measured on the same host,
/// deleting one manifest returned 213 bytes of a 12 GB store: it unlinks, and
/// the layers stay until <c>registry garbage-collect</c> walks the filesystem,
/// which the HTTP API does not expose. So this is four files - a config, the
/// mount that carries it, a collector, and the timer that runs it - and any
/// one of them missing is a fix that does not work. That is what these hold.
/// </para>
/// </remarks>
public class ThePoolRegistryCanBeTidiedTests
{
    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Gg.sln")))
        {
            directory = directory.Parent;
        }

        return (directory ?? throw new InvalidOperationException(
            "Gg.sln not found above the test binary")).FullName;
    }

    private static string Config => File.ReadAllText(
        Path.Combine(Root(), "scripts", "pool-registry", "config.yml"));

    private static string Compose => File.ReadAllText(
        Path.Combine(Root(), "deploy", "pool-host", "compose.yaml"));

    private static string Collector => File.ReadAllText(
        Path.Combine(Root(), "deploy", "pool-host", "gg-registry-gc.service"));

    private static string Timer => File.ReadAllText(
        Path.Combine(Root(), "deploy", "pool-host", "gg-registry-gc.timer"));

    [Test]
    public async Task The_registry_is_configured_to_allow_a_delete()
    {
        // THE ONE LINE THE CONFIG EXISTS FOR. Without it the maintainer's
        // reclaim is refused and the registry grows forever; with it the
        // reclaim decides what goes, because it is the only thing that knows
        // which tag a strategy still pins.
        await Assert.That(Regex.IsMatch(Config, @"storage:(?:.|\n)*?\n  delete:\n    enabled: true"))
            .IsTrue()
            .Because("delete.enabled must sit under storage: - anywhere else it is a key the "
                   + "registry ignores, and the 405 would look exactly the same.");
    }

    [Test]
    public async Task The_registry_still_stores_where_its_volume_is_mounted()
    {
        // A config that moved the root would present an EMPTY registry and make
        // every pinned image unpullable, while looking like a tidy-up that
        // worked. The default path is load-bearing.
        await Assert.That(Config).Contains("rootdirectory: /var/lib/registry");
        await Assert.That(Compose).Contains("registry-data:/var/lib/registry")
            .Because("the config names where the registry writes and compose names what is "
                   + "mounted there; the two have to be the same path or the store is a "
                   + "container layer that a restart throws away.");
    }

    [Test]
    public async Task The_service_actually_mounts_the_config()
    {
        // THE HALF THAT IS EASIEST TO LEAVE OUT, and it fails silently: a
        // registry with no config bind starts perfectly, serves every pull, and
        // refuses every delete. The file would exist and read correctly and
        // nothing would use it.
        await Assert.That(Compose)
            .Contains("../../scripts/pool-registry/config.yml:/etc/docker/registry/config.yml:ro")
            .Because("referenced rather than copied, for the reason the proxy's allowlist is - "
                   + "a second copy is where the halves start disagreeing.");
    }

    [Test]
    public async Task The_collection_deletes_untagged_manifests()
    {
        // NOT OPTIONAL, AND NOT BELT. The containerd image store pushes an OCI
        // index; deleting the index leaves its child manifests behind, still
        // referencing every layer. Without this flag the pass frees almost
        // nothing and the disk looks like the delete never worked.
        await Assert.That(Collector).Contains("--delete-untagged")
            .Because("an index's children are untagged, and they are what holds the layers.");
    }

    [Test]
    public async Task The_collection_stops_the_registry_and_always_starts_it_again()
    {
        await Assert.That(Collector).Contains("docker stop gg-pool-registry")
            .Because("garbage-collect is a walk of the filesystem and is not safe against a "
                   + "concurrent push.");
        await Assert.That(Collector).Contains("ExecStopPost=-/usr/bin/docker start gg-pool-registry")
            .Because("a collection that failed and left the registry stopped would take the "
                   + "pool's pull point with it until somebody noticed. The leading '-' is what "
                   + "makes this run after a failure rather than only after a success.");
    }

    [Test]
    public async Task The_collection_names_the_container_compose_creates()
    {
        // A unit that stopped a container by a name nothing starts would log
        // "No such container", collect an empty store, and report success - the
        // quietest way for this whole change to do nothing. vmlinux001's
        // registry was called gg-registry because a person started it by hand
        // before compose did; the shipped unit follows compose.
        var named = Regex.Matches(Collector, @"gg-[a-z-]*registry[a-z-]*").Select(m => m.Value);

        await Assert.That(named.Distinct()).IsEquivalentTo(["gg-pool-registry"]);
        await Assert.That(Compose).Contains("container_name: gg-pool-registry");
    }

    [Test]
    public async Task The_collection_reads_the_registrys_own_storage()
    {
        // --volumes-from names no volume and no path. A GC pointed at an empty
        // directory reports a clean store and frees nothing, which is
        // indistinguishable from having worked.
        await Assert.That(Collector).Contains("--volumes-from gg-pool-registry");
    }

    [Test]
    public async Task The_collector_is_the_version_that_wrote_the_store()
    {
        // TWO HALVES THAT MUST AGREE. compose pins registry by digest because
        // this holds every image a member runs; a collector from a different
        // registry version walking the store this one wrote is not a default to
        // take silently. Pinning one and floating the other would be the same
        // drift with no line to read it off.
        var pinned = Regex.Match(Compose, @"registry:2@sha256:[0-9a-f]{64}").Value;

        await Assert.That(pinned).IsNotEmpty()
            .Because("the liveness anchor: a compose file that stopped pinning would make the "
                   + "comparison below vacuously true.");
        await Assert.That(Collector).Contains(pinned)
            .Because("the pass that removes layers should be the version that wrote them.");
    }

    [Test]
    public async Task The_collection_binds_no_configuration_path()
    {
        // garbage-collect reads a config only for its storage section, and
        // delete.enabled gates the API rather than the collector - proven equal
        // on vmlinux001, the same 180 blobs with the host's config bound and
        // with the image's taken. So the unit names only a container, and
        // cannot be broken by a checkout moving under it.
        await Assert.That(Collector).DoesNotContain("/opt/")
            .Because("a live unit that depended on which commit a checkout happened to be on "
                   + "would fail in a way that reads as a clean store.");
        await Assert.That(Collector).DoesNotContain("config.yml:/etc/docker/registry/config.yml");
    }

    [Test]
    public async Task The_timer_installs_itself_and_catches_up()
    {
        // A timer with no [Install] enables silently and never fires, which is
        // the quietest way for the collector to do nothing at all.
        await Assert.That(Timer).Contains("WantedBy=timers.target");
        await Assert.That(Timer).Contains("OnCalendar=");
        await Assert.That(Timer).Contains("Persistent=true")
            .Because("a host that was off at 04:00 should collect when it comes back rather "
                   + "than waiting another day - this is the only thing that returns the disk.");
    }

    [Test]
    public async Task A_provisioned_host_gets_all_four_files()
    {
        // THE WHOLE CHANGE SHIPS OR NONE OF IT DOES. The bundle is what a pool
        // host unpacks; a file the tar does not carry is a mount that binds a
        // missing path, which on Linux creates a DIRECTORY over config.yml and
        // starts the registry on its default again.
        // FOUND BY WHAT IT IS, not by its path. gg forbids a source file
        // naming an identity provider, and the directory workflows live in is
        // named after one; assembling that string from pieces would satisfy
        // the rule and not its reason. The workflow that bundles a pool host
        // is the one that makes the tarball - which is also the definition
        // that survives somebody moving it.
        var bundle = Directory.EnumerateFiles(Root(), "*.yml", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
                            StringComparison.Ordinal)
                     && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}",
                            StringComparison.Ordinal))
            .Select(File.ReadAllText)
            .SingleOrDefault(t => t.Contains("gg-pool-host.tar.gz", StringComparison.Ordinal))
            ?? throw new InvalidOperationException(
                "nothing in this repository bundles a pool host, so there is no shipping to "
              + "check - which would make every assertion below vacuously true.");

        foreach (var shipped in (string[])
                 [
                     "scripts/pool-registry/config.yml",
                     "deploy/pool-host/gg-registry-gc.service",
                     "deploy/pool-host/gg-registry-gc.timer",
                 ])
        {
            await Assert.That(bundle).Contains(shipped)
                .Because($"'{shipped}' is mounted or linked by a provisioned host.");
        }

        var cloudInit = File.ReadAllText(
            Path.Combine(Root(), "deploy", "pool-host", "cloud-init.yaml"));

        await Assert.That(cloudInit).Contains("gg-registry-gc.timer")
            .Because("a unit that is shipped and never enabled is a file on a disk. Nothing "
                   + "else on the machine reclaims, and the failure is silent until it is full.");
        await Assert.That(cloudInit).Contains("enable, --now, gg-registry-gc.timer");
    }
}
