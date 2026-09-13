namespace Gg.Cli.Tests;

/// <summary>
/// `gg airspace repositories add` — the door three refusals already point at.
/// </summary>
/// <remarks>
/// <para>
/// <b>A port nothing calls, again.</b>
/// <c>POST /v1/airspace/repositories</c> has been served since slice ten and
/// <c>RegisterRepositoryRequest</c> has been a pinned, serializable contract
/// type; <c>FlightIngress</c> refuses an intent naming an unregistered
/// repository by pointing a person at that door, and nothing in this binary
/// could knock on it. So the answer to <i>"my envelope names a repository the
/// console does not list"</i> was to go and use something that is not gg — the
/// same shape <c>configure-this-runner</c> had.
/// </para>
/// <para>
/// <b>Four values and no shorthand, because a registry entry is four different
/// facts.</b> The provider is a key the registrar chose and a runner resolves to
/// a host of its own; the id is the forge's immutable identifier, which flight
/// identity resolves through; the path is a display label that may drift; the
/// name is what envelopes and flights say. The request type's own remark is
/// explicit that none of them is derived from a URI — <i>"which host a
/// customer's credential goes to must never be a policy edit here"</i> — so a
/// verb that split one argument into four would be guessing at the one fact
/// that cannot be re-derived afterwards.
/// </para>
/// <para>
/// <b>Who MAY register is answered by the control plane and never by a flag
/// this side read.</b> <c>WhoAmI.IsAdmin</c> is a hint about what a surface
/// would be allowed to show, never a permission — every route checks the
/// principal itself — so the refusal arrives on the wire as a status and is
/// rendered, and this parser knows nothing about roles.
/// </para>
/// </remarks>
public class ARepositoryIsRegisteredByAVerbTests
{
    private static CliAction.RepositoryRegister Parse(params string[] arguments) =>
        (CliAction.RepositoryRegister)CliArgs.Parse(arguments);

    [Test]
    public async Task The_four_facts_a_registry_entry_is_made_of_all_reach_the_action()
    {
        var parsed = Parse(
            "airspace", "repositories", "add",
            "--name", "payments",
            "--provider", "github",
            "--id", "R_123",
            "--path", "acme/payments");

        await Assert.That(parsed.Name).IsEqualTo("payments");
        await Assert.That(parsed.Provider).IsEqualTo("github");
        await Assert.That(parsed.Id).IsEqualTo("R_123");
        await Assert.That(parsed.Path).IsEqualTo("acme/payments")
            .Because("the path is the display label an intent is matched against, and it is "
                   + "the one of the four that may drift - so it is taken rather than read "
                   + "back off the id.");
    }

    [Test]
    public async Task The_optional_members_of_a_registration_are_reachable_too()
    {
        // A HALF-DOOR IS THE DEFECT THIS VERB EXISTS TO FIX. `--ref` decides
        // whether a TICKET flight - one whose intent names no repository - has
        // anywhere to start work; without it such a flight materializes an
        // empty tree. `--credential none` is what makes a file:// mirror
        // flyable at all. A verb reaching only the required four would register
        // repositories that certain flights still cannot use, which is the same
        // "it is there and it does not reach" shape one level down.
        var parsed = Parse(
            "airspace", "repositories", "add",
            "--name", "mirror",
            "--provider", "local",
            "--id", "M_1",
            "--path", "acme/mirror",
            "--ref", "refs/heads/trunk",
            "--credential", "none",
            "--narrowings", "policy");

        await Assert.That(parsed.Ref).IsEqualTo("refs/heads/trunk");
        await Assert.That(parsed.Credential).IsEqualTo("none");
        await Assert.That(parsed.Narrowings).IsEqualTo("policy");
    }

    [Test]
    public async Task The_optional_members_are_absent_rather_than_blank_when_unsaid()
    {
        // ABSENCE IS A FACT ON ALL THREE. Null ref means the flight has no
        // repository, null narrowings means off, and null credential means
        // required - so an empty string sent in their place would be a
        // different registration from the one somebody typed.
        var parsed = Parse(
            "airspace", "repositories", "add",
            "--name", "payments", "--provider", "github",
            "--id", "R_123", "--path", "acme/payments");

        await Assert.That(parsed.Ref).IsNull();
        await Assert.That(parsed.Credential).IsNull();
        await Assert.That(parsed.Narrowings).IsNull();
    }

    [Test]
    public async Task A_registration_missing_the_provider_says_what_a_provider_is_for()
    {
        var parsed = CliArgs.Parse([
            "airspace", "repositories", "add",
            "--name", "payments", "--id", "R_123", "--path", "acme/payments"]);

        var unknown = await Assert.That(parsed).IsTypeOf<CliAction.Unknown>();

        await Assert.That(unknown!.Message).Contains("--provider", StringComparison.Ordinal);
        await Assert.That(unknown.Message).Contains("runner", StringComparison.OrdinalIgnoreCase)
            .Because("somebody who left it out did so because they thought it could be read "
                   + "off the path, and the refusal has to say why it cannot: the provider "
                   + "is a key a runner resolves to a host of its own.");
    }

    [Test]
    public async Task Each_of_the_four_is_named_when_it_is_the_missing_one()
    {
        foreach (var flag in (string[])["--name", "--provider", "--id", "--path"])
        {
            var given = new List<string> { "airspace", "repositories", "add" };

            foreach (var (other, otherValue) in ((string, string)[])
                     [("--name", "payments"), ("--provider", "github"),
                      ("--id", "R_123"), ("--path", "acme/payments")])
            {
                if (!string.Equals(other, flag, StringComparison.Ordinal))
                {
                    given.Add(other);
                    given.Add(otherValue);
                }
            }

            var unknown = await Assert.That(CliArgs.Parse([.. given]))
                .IsTypeOf<CliAction.Unknown>();

            await Assert.That(unknown!.Message).Contains(flag, StringComparison.Ordinal)
                .Because($"leaving out {flag} has to be refused by that flag's own name, or "
                       + "a person is told a registry entry is incomplete and left to work "
                       + "out which quarter of it is.");
        }
    }

    [Test]
    public async Task An_option_the_verb_does_not_take_is_refused_by_name()
    {
        // NEVER IGNORED. The option somebody reaches for when a registration is
        // refused is `--url`, and silently dropping it would register the
        // repository with whatever else was typed and leave them convinced they
        // had said which host it is on.
        var parsed = CliArgs.Parse([
            "airspace", "repositories", "add",
            "--name", "payments", "--provider", "github",
            "--id", "R_123", "--path", "acme/payments", "--url", "https://example.test"]);

        var unknown = await Assert.That(parsed).IsTypeOf<CliAction.Unknown>();

        await Assert.That(unknown!.Message).Contains("--url", StringComparison.Ordinal);
        await Assert.That(unknown.Message).Contains("--provider", StringComparison.Ordinal)
            .Because("the refusal names what the verb does take, which is the second half of "
                   + "a useful refusal everywhere else in this parser.");
    }

    [Test]
    public async Task A_flag_given_nothing_to_be_is_refused_rather_than_paired_with_the_next_one()
    {
        var parsed = CliArgs.Parse([
            "airspace", "repositories", "add",
            "--name", "payments", "--provider", "github", "--id", "R_123", "--path"]);

        await Assert.That(parsed).IsTypeOf<CliAction.Unknown>();
    }

    [Test]
    public async Task The_family_refusal_names_repositories_too()
    {
        var parsed = CliArgs.Parse(["airspace", "banana"]);

        var unknown = await Assert.That(parsed).IsTypeOf<CliAction.Unknown>();

        await Assert.That(unknown!.Message).Contains("repositories", StringComparison.Ordinal)
            .Because("`gg airspace takes ...` is the sentence a person gets when they "
                   + "mistype, and it is the second place the family is advertised.");
    }
}
