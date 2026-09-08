using System.Security.Cryptography;
using Gg.Client;

namespace Gg.Client.Tests;

/// <summary>
/// A runner makes one key, keeps it, and offers only the public half.
/// </summary>
/// <remarks>
/// <b>At registration, because that is the moment trust collapses to.</b>
/// ADR-0013: a key handed over per-session could be substituted per-session, so
/// pinning at registration means a console has one thing to have got right —
/// and this is the half that makes there be something to pin.
/// </remarks>
public class RunnerIdentityKeyTests
{
    private static string InADirectoryOfItsOwn() =>
        Path.Combine(Directory.CreateTempSubdirectory("gg-key-").FullName, "key.json");

    [Test]
    public async Task A_key_made_once_is_the_same_key_next_time()
    {
        // THE WHOLE POINT OF PINNING. A key regenerated per start would make
        // every runner look substituted on its second registration, and a
        // console would refuse a fleet that was behaving perfectly.
        var path = InADirectoryOfItsOwn();

        var first = RunnerIdentityKey.LoadOrCreate(path).PublicKey;
        var again = RunnerIdentityKey.LoadOrCreate(path).PublicKey;

        await Assert.That(again).IsEqualTo(first);
    }

    [Test]
    public async Task Two_runner_names_on_one_host_get_two_keys()
    {
        // A pool host runs `gg runner up` as itself and `runner maintain` as
        // <machine>:maintain. Sharing a key would make a console pinning "this
        // runner" actually pin "this host" - the defect FileRunnerStore.PathFor
        // exists to have fixed, arriving again wearing cryptography.
        var root = Directory.CreateTempSubdirectory("gg-keys-").FullName;

        var up = RunnerIdentityKey.LoadOrCreate(Path.Combine(root, "a.json")).PublicKey;
        var maintain = RunnerIdentityKey.LoadOrCreate(Path.Combine(root, "b.json")).PublicKey;

        await Assert.That(maintain).IsNotEqualTo(up);
    }

    [Test]
    public async Task The_paths_for_two_names_differ_and_survive_a_colon()
    {
        // The maintain name carries a ':', which is not a filename character
        // EVERYWHERE - and on this platform it is one, which is why the
        // assertion is about invalid characters rather than about the colon. The
        // first version of this test asserted the colon was gone and was wrong
        // on macOS and Linux, where GetInvalidFileNameChars is '/' and NUL.
        //
        // The sanitiser is FileRunnerStore's, reused rather than written twice:
        // writing it twice is how two names flatten to one path, which is the
        // defect that store's own remark exists to have fixed.
        var up = RunnerIdentityKey.PathFor("laptop-7");
        var maintain = RunnerIdentityKey.PathFor("laptop-7:maintain");

        await Assert.That(maintain).IsNotEqualTo(up);
        await Assert.That(
                Path.GetFileName(maintain).IndexOfAny(Path.GetInvalidFileNameChars()))
            .IsEqualTo(-1)
            .Because("whatever this platform refuses in a file name has to be gone from it.");
        await Assert.That(Path.GetDirectoryName(maintain))
            .IsEqualTo(Path.GetDirectoryName(FileRunnerStore.PathFor("laptop-7:maintain")))
            .Because("a private key and a thirty-day token are the same kind of thing at "
                   + "rest; two directories would be two places to get permissions right.");
    }

    [Test]
    public async Task What_is_offered_is_a_public_key_and_nothing_else()
    {
        // THE FILE HOLDS THE PRIVATE HALF AND THE WIRE CARRIES THE PUBLIC ONE.
        // A test that only checked the string was non-empty would pass on a
        // PKCS#8 blob going out over the wire.
        var path = InADirectoryOfItsOwn();
        var offered = RunnerIdentityKey.LoadOrCreate(path).PublicKey;

        var reading = ECDiffieHellman.Create();
        reading.ImportSubjectPublicKeyInfo(Convert.FromBase64String(offered), out var read);

        await Assert.That(read).IsGreaterThan(0)
            .Because("it has to parse as a SubjectPublicKeyInfo, or nothing can seal to it.");

        await Assert.That(File.ReadAllText(path)).IsNotEqualTo(offered)
            .Because("what is kept and what is offered must not be the same bytes.");

        // And the kept half really is private material: it imports as one.
        var kept = ECDiffieHellman.Create();
        kept.ImportPkcs8PrivateKey(Convert.FromBase64String(File.ReadAllText(path)), out _);
        await Assert.That(kept.ExportSubjectPublicKeyInfo()).IsEquivalentTo(
            Convert.FromBase64String(offered))
            .Because("the public half offered has to be the public half OF the key kept.");
    }

    [Test]
    public async Task The_kept_half_is_readable_by_nobody_else_where_that_can_be_said()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var path = InADirectoryOfItsOwn();
        RunnerIdentityKey.LoadOrCreate(path);

        var mode = File.GetUnixFileMode(path);

        await Assert.That(mode.HasFlag(UnixFileMode.GroupRead)).IsFalse();
        await Assert.That(mode.HasFlag(UnixFileMode.OtherRead)).IsFalse();
    }
}
