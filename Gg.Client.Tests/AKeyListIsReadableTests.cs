using Gg.Contracts;

namespace Gg.Client.Tests;

/// <summary>
/// `gg key list` prints every key this tenant's people have registered, and says
/// which of them are retired.
/// </summary>
/// <remarks>
/// <para>
/// <b>S60.1-02, and the verb is overdue by a whole slice.</b>
/// <c>GET /v1/auth/keys</c> was declared in slice fifty-nine and the control
/// plane has served it since. <c>ControlPlaneClient.ListKeysAsync</c> exists
/// because <c>EveryDeveloperReadHasACallerTests</c> demanded it the moment the
/// endpoint was declared — and then sat with one reference in the whole
/// repository, its own declaration. The door is open, gg knows how to walk
/// through it, and no person can.
/// </para>
/// <para>
/// <b>Why anybody reads this.</b> A credential is sealed to the people who may
/// open it, so sealing one to somebody means finding their key, and a key nobody
/// can look up is a key nobody can seal to. The list is also how a person checks
/// their own registered — `gg key create` prints a fingerprint and then its
/// registration can fail without failing the verb, which leaves "did mine
/// arrive" a question only this answers.
/// </para>
/// <para>
/// <b>A retired key is listed and said to be retired</b>, which the contract
/// already argues for: <i>"A credential sealed to a key last year is still
/// sealed to it, so a row that vanished would leave an envelope naming a holder
/// nobody can account for."</i> Hiding it would make a sealed credential's
/// holder list unexplainable.
/// </para>
/// <para>
/// <b>The fingerprint is what a person reads, and the whole key is what
/// --json carries.</b> A public key is about 120 characters of base64 and means
/// nothing to anybody at a glance; the fingerprint is the short name derived once
/// on the contract so that `gg key create` and this list spell it identically.
/// Neither is a secret, so the choice is about reading rather than safety.
/// </para>
/// </remarks>
public class AKeyListIsReadableTests
{
    private static readonly DateTimeOffset Registered = new(2026, 9, 14, 22, 49, 40, TimeSpan.Zero);

    /// <summary>A P-256 public key, spelled the way a runner and a person register one.</summary>
    private static string APublicKey() =>
        Disposed(System.Security.Cryptography.ECDiffieHellman.Create(
            System.Security.Cryptography.ECCurve.NamedCurves.nistP256));

    /// <summary>
    /// The public half, with the key handle released.
    /// </summary>
    /// <remarks>
    /// <b>Disposed because these tests mint dozens.</b> An undisposed
    /// <c>ECDiffieHellman</c> holds a platform key handle until a finalizer runs,
    /// and a suite that leaks them under parallel execution is a suite whose
    /// failures depend on timing.
    /// </remarks>
    private static string Disposed(System.Security.Cryptography.ECDiffieHellman key)
    {
        using (key)
        {
            return Convert.ToBase64String(key.PublicKey.ExportSubjectPublicKeyInfo());
        }
    }

    private static PrincipalKeySummary AKey(
        string principal, DateTimeOffset? retiredAt = null)
    {
        var publicKey = APublicKey();

        return new PrincipalKeySummary
        {
            KeyId = Guid.NewGuid().ToString(),
            Principal = principal,
            PublicKey = publicKey,
            Fingerprint = PrincipalKeyFingerprint.Of(publicKey),
            RegisteredAt = Registered,
            RetiredAt = retiredAt,
        };
    }

    /// <summary>The stub's shape, as CredentialCommandTests already builds it.</summary>
    private static CredentialCommands Build(StubControlPlane stub, FileCredentialStore store) =>
        new(new ControlPlaneClient(new HttpClient { BaseAddress = new Uri(stub.BaseAddress) }),
            new HeldSessionStore(new StoredSession
            {
                SessionToken = StubControlPlane.IssuedSessionToken,
                ExpiresAt = DateTimeOffset.UtcNow.AddHours(12),
                TenantId = "019fe062-d000-730c-a37d-7247342cd810",
                PrincipalDisplay = "stub-principal",
            }),
            store,
            new ScriptedPrompt("unused"));

    [Test]
    public async Task Every_registered_key_comes_back_with_whose_it_is()
    {
        await using var stub = new StubControlPlane();
        using var temporary = new TemporaryStore();

        stub.Keys.Add(AKey("ada"));
        stub.Keys.Add(AKey("grace"));

        var listed = ((VerbResult.Keys)await Build(stub, temporary.Store).ListKeysAsync()).Value;

        await Assert.That(listed.Keys.Count).IsEqualTo(2);
        await Assert.That(listed.Keys.Select(k => k.Principal)).Contains("ada");
        await Assert.That(listed.Keys.Select(k => k.Principal)).Contains("grace")
            .Because("sealing a credential to somebody means finding THEIR key, so a list that "
                   + "did not say whose each one is would answer the wrong question.");
    }

    [Test]
    public async Task A_retired_key_is_listed_and_said_to_be_retired()
    {
        await using var stub = new StubControlPlane();
        using var temporary = new TemporaryStore();

        stub.Keys.Add(AKey("ada", retiredAt: Registered.AddDays(30)));

        var text = VerbOutput.ToText(await Build(stub, temporary.Store).ListKeysAsync());

        await Assert.That(text).Contains("ada");
        await Assert.That(text).Contains("retired")
            .Because("a credential sealed to a key last year is still sealed to it, so a row that "
                   + "vanished - or that looked live - would leave an envelope naming a holder "
                   + "nobody can account for.");
    }

    [Test]
    public async Task A_live_key_is_not_called_retired()
    {
        await using var stub = new StubControlPlane();
        using var temporary = new TemporaryStore();

        stub.Keys.Add(AKey("ada"));

        var text = VerbOutput.ToText(await Build(stub, temporary.Store).ListKeysAsync());

        await Assert.That(text).DoesNotContain("retired")
            .Because("the word is the whole signal, so it must appear only where it is true.");
    }

    [Test]
    public async Task The_fingerprint_is_the_one_gg_prints_when_it_mints_a_key()
    {
        // DERIVED ONCE, ON THE CONTRACT. `gg key create` prints a fingerprint and
        // a person looks for that string here; a second derivation on either side
        // is how they come to be unable to find what they just registered.
        await using var stub = new StubControlPlane();
        using var temporary = new TemporaryStore();

        var key = AKey("ada");
        stub.Keys.Add(key);

        var text = VerbOutput.ToText(await Build(stub, temporary.Store).ListKeysAsync());

        await Assert.That(text).Contains(PrincipalKeyFingerprint.Of(key.PublicKey));
    }

    [Test]
    public async Task No_keys_says_how_to_make_one()
    {
        // ARTICLE XI, and the shape `gg credential list` already uses: an empty
        // list is a state with a next step, not a blank screen.
        await using var stub = new StubControlPlane();
        using var temporary = new TemporaryStore();

        var text = VerbOutput.ToText(await Build(stub, temporary.Store).ListKeysAsync());

        await Assert.That(text).Contains("gg key create")
            .Because("a tenant whose people have registered nothing is the ordinary first state, "
                   + "and the reader needs the one command that changes it.");
    }

    [Test]
    public async Task The_json_carries_the_whole_key_even_though_the_text_does_not()
    {
        // THE TEXT IS FOR READING AND THE JSON IS FOR RE-RENDERING. A public key
        // is 120 characters that mean nothing at a glance, so the fingerprint is
        // the column - but a payload somebody sends us has to be enough to
        // reproduce what they saw, which is this result type's own stated rule.
        await using var stub = new StubControlPlane();
        using var temporary = new TemporaryStore();

        var key = AKey("ada");
        stub.Keys.Add(key);

        var result = await Build(stub, temporary.Store).ListKeysAsync();

        // RECOVERED RATHER THAN GREPPED, and the difference is a real trap. gg's
        // --json uses the default JavaScript encoder, which escapes `+` as
        // + and `/` as / - and a base64 public key is full of both. So
        // the key is in the payload and a substring search for it fails. Nothing
        // had met this before because a locator contains neither character.
        // "Carries" means a consumer can get it back, so that is what is asked.
        var carried = ((VerbResult.Keys)VerbOutput.Parse(
            result.Kind, VerbOutput.ToJson(result))).Value;

        await Assert.That(carried.Keys.Single().PublicKey).IsEqualTo(key.PublicKey);

        await Assert.That(VerbOutput.ToText(result)).DoesNotContain(key.PublicKey)
            .Because("120 characters of base64 mean nothing at a glance, so the column is the "
                   + "fingerprint and the key is for whatever reads the payload.");
    }

    [Test]
    public async Task The_result_round_trips_through_json()
    {
        await using var stub = new StubControlPlane();
        using var temporary = new TemporaryStore();

        stub.Keys.Add(AKey("ada"));

        var result = await Build(stub, temporary.Store).ListKeysAsync();
        var parsed = VerbOutput.Parse(result.Kind, VerbOutput.ToJson(result));

        await Assert.That(VerbOutput.ToText(parsed)).IsEqualTo(VerbOutput.ToText(result))
            .Because("the practical form of \"a diagnosis they can send us\": we cannot look at "
                   + "their terminal, so a --json payload has to re-render to what they saw.");
    }
}
