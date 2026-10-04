using Gg.Client;

namespace Gg.Client.Tests;

/// <summary>
/// The derivation's cost and salt are stored beside the wrapped key, so raising
/// the cost later does not orphan what was wrapped under the old one.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is the whole reason the parameters are not a constant.</b> An
/// iteration count compiled into gg can only ever be raised by a release that
/// cannot open any key written before it — which means it is never raised, and
/// the number chosen on the first day is the number in the field for ever.
/// </para>
/// <para>
/// <b>PBKDF2-HMAC-SHA256, measured rather than remembered.</b> The BCL on this
/// SDK has no Argon2 and no scrypt — checked against the assembly, not recalled
/// — and <c>Gg.Contracts</c> takes zero third-party packages while everything
/// must stay AOT-publishable. So PBKDF2 it is, which is weaker against a GPU
/// than a memory-hard function would be. What bounds that is where the file
/// lives: an attacker needs the wrapped key AND the passphrase, and having the
/// file already means they were running as this user, which ADR-0037 says
/// sealing never defended against.
/// </para>
/// </remarks>
public class AWrappedKeyCarriesItsParametersTests
{
    private const string Passphrase = "correct horse battery staple";

    private static string APath() =>
        Path.Combine(Path.GetTempPath(), "gg-params-" + Guid.NewGuid().ToString("N"), "person-key");

    [Test]
    public async Task The_file_names_the_derivation()
    {
        var path = APath();
        PersonKey.Create(path, Passphrase);

        await Assert.That(File.ReadAllText(path)).Contains("pbkdf2")
            .Because("a wrapped key that does not say how it was wrapped can only be opened by "
                   + "guessing, and a wrong guess is indistinguishable from a wrong passphrase.");
    }

    [Test]
    public async Task It_carries_an_iteration_count_and_a_salt()
    {
        var path = APath();
        PersonKey.Create(path, Passphrase);

        var written = File.ReadAllText(path);

        await Assert.That(written).Contains(PersonKey.Iterations.ToString());
        await Assert.That(written).Contains("salt");
    }

    [Test]
    public async Task Two_keys_do_not_share_a_salt()
    {
        var first = APath();
        var second = APath();

        PersonKey.Create(first, Passphrase);
        PersonKey.Create(second, Passphrase);

        await Assert.That(PersonKey.SaltOf(first)).IsNotEqualTo(PersonKey.SaltOf(second))
            .Because("a shared salt makes one precomputation work against every key.");
    }

    [Test]
    public async Task A_key_wrapped_under_a_lower_cost_still_opens()
    {
        // THE ASSERTION THE WHOLE DECISION RESTS ON. gg must read the cost from
        // the FILE rather than from its own constant, or the day somebody raises
        // Iterations is the day every key already in the field stops opening.
        var path = APath();
        PersonKey.CreateAtCostForTesting(path, Passphrase, PersonKey.Iterations / 4);

        await Assert.That(PersonKey.Unlock(path, Passphrase)).IsNotNull();
    }

    [Test]
    public async Task And_one_wrapped_under_a_higher_cost_does_too()
    {
        var path = APath();
        PersonKey.CreateAtCostForTesting(path, Passphrase, PersonKey.Iterations * 2);

        await Assert.That(PersonKey.Unlock(path, Passphrase)).IsNotNull();
    }

    [Test]
    public async Task The_cost_this_build_writes_is_not_trivially_low()
    {
        // A FLOOR RATHER THAN A FIGURE, because the right number moves with
        // hardware and this assertion must not have to move with it. What it
        // catches is the number being dropped to make a test suite faster,
        // which is exactly how a cost ends up at a thousand in production.
        await Assert.That(PersonKey.Iterations).IsGreaterThanOrEqualTo(600_000);
    }

    [Test]
    public async Task An_unknown_derivation_is_refused_by_name()
    {
        // A FILE FROM A LATER GG, met by an earlier one. Naming what it found
        // is what sends somebody to update rather than to suspect their
        // passphrase - the same argument the sealed envelope's version makes.
        var path = APath();
        PersonKey.Create(path, Passphrase);

        File.WriteAllText(path, File.ReadAllText(path).Replace("pbkdf2-hmac-sha256", "argon2id"));

        var said = Assert.Throws<CredentialUnavailableException>(
            () => PersonKey.Unlock(path, Passphrase))!.Message;

        await Assert.That(said).Contains("argon2id");
    }
}
