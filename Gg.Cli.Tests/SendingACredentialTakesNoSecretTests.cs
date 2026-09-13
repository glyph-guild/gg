using Gg.Contracts;

namespace Gg.Cli.Tests;

/// <summary>
/// <c>gg credential send</c> names a runner and a repository, and has nowhere a
/// secret could be typed.
/// </summary>
/// <remarks>
/// <para>
/// <b>Beside <c>add</c>, because that is where the mental model already is.</b>
/// A person registers a credential, and then a machine needs it. Putting this
/// under <c>gg runner</c> would file it by which component is being changed
/// rather than by what is being moved, and the thing being moved is the reason
/// anybody is here.
/// </para>
/// <para>
/// <b>The locator is DERIVED, never typed.</b> <c>CredentialLocator.ForRepo</c>
/// is the same derivation <c>gg credential add</c> used to store the secret and
/// the runner uses to find it - three places, one rule, which is what that
/// type's own remark exists to guarantee: <i>two derivations that agree today is
/// how a runner ends up looking for a file the CLI never wrote.</i>
/// </para>
/// <para>
/// <b>And no flag carries the secret.</b> <c>CredentialArgsTests</c> holds this
/// for <c>add</c> and the argument transfers unchanged: an argument is in shell
/// history and in <c>ps</c> output before any code of ours has run, and neither
/// of those is somewhere a later fix can reach. It is the flag that gets added
/// later "for scripting", which is exactly the reason to assert it now.
/// </para>
/// </remarks>
public class SendingACredentialTakesNoSecretTests
{
    private static CliAction.CredentialSend Sent(params string[] args)
    {
        var action = CliArgs.Parse(args);

        return action as CliAction.CredentialSend ?? throw new InvalidOperationException(
            $"'{string.Join(" ", args)}' did not parse as a send but as {action.GetType().Name}"
          + (action is CliAction.Unknown unknown ? $": {unknown.Message}" : "."));
    }

    [Test]
    public async Task It_names_a_runner_and_a_repository()
    {
        var sent = Sent("credential", "send", "--runner", "runner-1", "--repo", "acme/widgets");

        await Assert.That(sent.RunnerId).IsEqualTo("runner-1");
        await Assert.That(sent.Repo).IsEqualTo("acme/widgets");
    }

    [Test]
    public async Task Both_are_required_and_the_refusal_says_which_is_missing()
    {
        foreach (var (line, wanted) in new[]
        {
            ((string[])["credential", "send", "--repo", "acme/widgets"], "--runner"),
            ((string[])["credential", "send", "--runner", "runner-1"], "--repo"),
        })
        {
            var refused = CliArgs.Parse(line);

            await Assert.That(refused).IsTypeOf<CliAction.Unknown>();
            await Assert.That(((CliAction.Unknown)refused).Message)
                .Contains(wanted, StringComparison.Ordinal)
                .Because("a refusal that names a problem and hides which half of the line "
                       + "was wrong is half a sentence.");
        }
    }

    [Test]
    public async Task No_flag_could_carry_the_secret()
    {
        // CredentialArgsTests' ASSERTION, FOR THE VERB THAT MOVES ONE. It is the
        // flag somebody adds later "for scripting", and by then the value is in
        // shell history and in ps output - neither of which a later fix reaches.
        foreach (var flag in (string[])
            ["--secret", "--token", "--password", "--value", "--pat"])
        {
            var refused = CliArgs.Parse(
                ["credential", "send", "--runner", "runner-1", "--repo", "acme/widgets",
                 flag, "ghp-not-a-real-token"]);

            await Assert.That(refused).IsTypeOf<CliAction.Unknown>()
                .Because($"{flag} would put a credential in shell history before any code "
                       + "of ours ran.");
        }
    }

    [Test]
    public async Task Nothing_it_parses_to_could_hold_one_either()
    {
        // THE OTHER HALF, which the flag check alone would miss: a positional
        // argument reaches a member without ever being spelled with dashes.
        // Asserted over the SHAPE, so a member added later fails here.
        var members = typeof(CliAction.CredentialSend).GetProperties();

        foreach (var member in members)
        {
            foreach (var word in (string[])
                ["secret", "token", "password", "passphrase", "bearer", "apikey"])
            {
                await Assert.That(member.Name.Contains(word, StringComparison.OrdinalIgnoreCase))
                    .IsFalse()
                    .Because($"'{member.Name}' is named for secret material on a verb whose "
                           + "whole job is to move one without it being typed.");
            }
        }

        await Assert.That(members.Length).IsLessThanOrEqualTo(3)
            .Because("a runner, a repository and --json. A fourth member is a decision "
                   + "somebody makes here rather than one that arrives.");
    }

    [Test]
    public async Task The_locator_is_the_one_the_store_already_uses()
    {
        // THREE PLACES, ONE RULE. gg credential add wrote the file under this
        // name, the runner looks for it under this name, and this verb has to
        // name the same thing or it places a secret nothing will ever read.
        await Assert.That(CredentialLocator.ForRepo("acme/widgets"))
            .IsEqualTo("local:acme/widgets");
    }

    [Test]
    public async Task The_help_says_it_exists()
    {
        // A VERB NOBODY CAN FIND IS A VERB NOBODY HAS, and this one is the only
        // way to put a credential on a machine that has no filesystem anybody
        // can reach.
        var help = ((CliAction.Unknown)CliArgs.Parse(["nonsense"])).Message;

        await Assert.That(help).Contains("credential send", StringComparison.Ordinal);
    }
}
