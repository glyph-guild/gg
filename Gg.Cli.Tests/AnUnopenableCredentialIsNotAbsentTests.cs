using System.Security.Cryptography;
using Gg.Client;
using Gg.Contracts;
using Gg.Runner;

namespace Gg.Cli.Tests;

/// <summary>
/// A credential this machine holds and cannot open reaches a flight as a
/// diagnosis, and never as "there is none".
/// </summary>
/// <remarks>
/// <para>
/// <b>Rule 9, end to end rather than at the store.</b> Step 2 made
/// <c>FileCredentialStore.Read</c> throw instead of answering null; what this
/// pins is that the throw survives the whole chain — store, resolver,
/// <c>CredentialResolution</c> — and arrives as something a person can act on.
/// A diagnosis that stops one layer short of the flight log is a diagnosis
/// nobody reads.
/// </para>
/// <para>
/// <b>The assertion that matters is that the two sentences DIFFER.</b> Absent
/// and unopenable both end with a flight that has no credential, and they send
/// a person to opposite places: one to add it, one to ask whoever holds it to
/// push it here. If both said "no secret stored at X", nothing upstream could
/// tell them apart and the honest half of rule 9 would be lost while every
/// other assertion still passed.
/// </para>
/// <para>
/// <b>And the worse failure is silent.</b> Null means "no credential for that
/// locator", and a runner handed that clones ANONYMOUSLY — so a store carried
/// to another machine would quietly become a flight with no permissions rather
/// than a flight that stopped and said why.
/// </para>
/// </remarks>
public class AnUnopenableCredentialIsNotAbsentTests
{
    private const string Locator = "local:acme/widgets";
    private const string Value = "ghp-a-token-nobody-should-find";

    private static string ATempDirectory() =>
        Path.Combine(Path.GetTempPath(), "gg-unopenable-" + Guid.NewGuid().ToString("N"));

    private static CredentialReference AReference() => new()
    {
        Kind = CredentialKinds.Local,
        Locator = Locator,
        Identity = "acme-bot",
        Scopes = [CredentialScopes.Read],
    };

    /// <summary>A store holding a credential sealed to somebody else's key.</summary>
    /// <remarks>
    /// Exactly the directory ADR-0037's falsifier describes: the files of one
    /// machine, read by another.
    /// </remarks>
    private static FileCredentialStore ACarriedStore()
    {
        var root = ATempDirectory();

        new FileCredentialStore(root, MachineKey.LoadOrCreate(ATempDirectory() + "/one"))
            .Write(Locator, Value);

        return new FileCredentialStore(root, MachineKey.LoadOrCreate(ATempDirectory() + "/another"));
    }

    private static async Task<CredentialResolution> ResolvedThrough(ICredentialStore store) =>
        await new LocalCredentialResolver(store).ResolveAsync(AReference());

    [Test]
    public async Task It_is_unresolvable_rather_than_resolved()
    {
        await Assert.That(await ResolvedThrough(ACarriedStore()))
            .IsTypeOf<CredentialResolution.Unresolvable>();
    }

    [Test]
    public async Task The_diagnosis_names_the_locator()
    {
        var resolution = (CredentialResolution.Unresolvable)await ResolvedThrough(ACarriedStore());

        await Assert.That(resolution.Problem).Contains(Locator)
            .Because("which credential stopped the flight is the first thing somebody needs.");
    }

    [Test]
    public async Task The_diagnosis_says_what_to_do_about_it()
    {
        var resolution = (CredentialResolution.Unresolvable)await ResolvedThrough(ACarriedStore());

        // A REMEDY RATHER THAN A VERDICT. Article XI asks for a diagnosis, and
        // "could not open" names a state without naming an action - which is
        // how somebody ends up checking a disk that is fine.
        await Assert.That(resolution.Problem.Contains("push", StringComparison.OrdinalIgnoreCase))
            .IsTrue()
            .Because("the remedy is that somebody who holds it sends it here. Said: "
                   + resolution.Problem);
    }

    [Test]
    public async Task Absent_and_unopenable_do_not_say_the_same_thing()
    {
        // THE HEART OF RULE 9. Both end with a flight that has no credential and
        // they send a person to opposite places.
        var unopenable = (CredentialResolution.Unresolvable)await ResolvedThrough(ACarriedStore());

        var empty = (CredentialResolution.Unresolvable)await ResolvedThrough(
            new FileCredentialStore(ATempDirectory(), MachineKey.LoadOrCreate(ATempDirectory() + "/k")));

        await Assert.That(unopenable.Problem).IsNotEqualTo(empty.Problem);

        await Assert.That(empty.Problem).Contains("credential add")
            .Because("nothing is here, so the remedy is to add one.");

        await Assert.That(unopenable.Problem).DoesNotContain("credential add")
            .Because("something IS here; telling somebody to add it again is the wrong act, "
                   + "and `gg credential add` would seal a second copy beside one that was "
                   + "never the problem.");
    }

    [Test]
    public async Task The_diagnosis_carries_none_of_the_file()
    {
        var store = ACarriedStore();
        var resolution = (CredentialResolution.Unresolvable)await ResolvedThrough(store);

        await Assert.That(resolution.Problem).DoesNotContain(Value);

        foreach (var file in Directory.EnumerateFiles(store.Root, "*", SearchOption.AllDirectories))
        {
            foreach (var run in File.ReadAllText(file).Split('"', ',', ':', '{', '}'))
            {
                if (run.Trim().Length > 20)
                {
                    await Assert.That(resolution.Problem).DoesNotContain(run.Trim())
                        .Because("rule 8: a refusal names the place, never what it found there.");
                }
            }
        }
    }

    [Test]
    public async Task A_damaged_envelope_is_also_not_absent()
    {
        var store = ACarriedStore();
        File.WriteAllText(store.SealedPathFor(Locator), "not an envelope at all");

        var resolution = await ResolvedThrough(store);

        await Assert.That(resolution).IsTypeOf<CredentialResolution.Unresolvable>();
        await Assert.That(((CredentialResolution.Unresolvable)resolution).Problem)
            .DoesNotContain("credential add")
            .Because("a damaged file is not a missing one, and the remedy differs.");
    }

    [Test]
    public async Task The_machine_store_says_why_rather_than_going_quiet()
    {
        // THE ONE PLACE "CANNOT OPEN" STILL COLLAPSES TO NULL, deliberately and
        // with its reason written down: `secretFor` answers a string or nothing
        // and cannot carry a sentence, so the sentence goes to the runner's log
        // instead. What must not happen is that it goes nowhere - a preview that
        // silently serves nothing is the failure that path was built around.
        var store = ACarriedStore();
        var said = new StringWriter();

        var answered = MachineCredentialStore.SecretFor(store, Locator, said);

        await Assert.That(answered).IsNull();
        await Assert.That(said.ToString().Length).IsGreaterThan(0)
            .Because("null with no sentence anywhere is the silent failure rule 9 exists for.");
    }
}
