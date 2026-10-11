using Gg.Contracts;

namespace Gg.Cli.Tests;

/// <summary>
/// <c>gg credential send --tracker &lt;key&gt;</c> and <c>--analysis &lt;key&gt;</c> —
/// every credential <c>gg credential add</c> can register can also be sent.
/// </summary>
/// <remarks>
/// <para>
/// <b>The gap this closes.</b> <c>add</c> took four subjects and <c>send</c> took two,
/// so a SonarCloud token registered through the wizard could reach no machine but the
/// one it was typed on: the broadcast found nobody because no profile declared it, and
/// <c>--runner</c> would not take <c>--analysis</c>.
/// </para>
/// <para>
/// <b>One record for both, carrying the locator.</b> A tracker's and an analysis
/// service's locators are derived here by the contract's own <c>For…</c> rule, which is
/// the derivation <c>add</c> stored under - so the handler moves a locator and derives
/// nothing.
/// </para>
/// </remarks>
public class AnyCredentialCanBeSentTests
{
    private static CliAction.LocatorCredentialSend SentToOne(params string[] args)
    {
        var action = CliArgs.Parse(args);
        return action as CliAction.LocatorCredentialSend ?? throw new InvalidOperationException(
            $"'{string.Join(" ", args)}' did not parse as a send to one machine but as {action.GetType().Name}"
          + (action is CliAction.Unknown unknown ? $": {unknown.Message}" : "."));
    }

    private static CliAction.CredentialSendWhereNeeded Broadcast(params string[] args)
    {
        var action = CliArgs.Parse(args);
        return action as CliAction.CredentialSendWhereNeeded ?? throw new InvalidOperationException(
            $"'{string.Join(" ", args)}' did not parse as a broadcast but as {action.GetType().Name}"
          + (action is CliAction.Unknown unknown ? $": {unknown.Message}" : "."));
    }

    [Test]
    public async Task An_analysis_service_s_credential_goes_to_one_machine()
    {
        var sent = SentToOne("credential", "send", "--runner", "vmlinux002", "--analysis", "sonarcloud");

        await Assert.That(sent.RunnerId).IsEqualTo("vmlinux002");
        await Assert.That(sent.Locator).IsEqualTo(CredentialLocator.ForAnalysis("sonarcloud"));
    }

    [Test]
    public async Task A_tracker_s_credential_goes_to_one_machine()
    {
        var sent = SentToOne("credential", "send", "--tracker", "ado", "--runner", "vmlinux002");

        await Assert.That(sent.Locator).IsEqualTo(CredentialLocator.ForTracker("ado"));
    }

    [Test]
    public async Task Either_goes_wherever_it_is_needed_when_no_machine_is_named()
    {
        await Assert.That(Broadcast("credential", "send", "--analysis", "sonarcloud").Locator)
            .IsEqualTo(CredentialLocator.ForAnalysis("sonarcloud"));
        await Assert.That(Broadcast("credential", "send", "--tracker", "ado").Locator)
            .IsEqualTo(CredentialLocator.ForTracker("ado"));
    }

    [Test]
    public async Task One_send_names_one_credential()
    {
        foreach (var args in (string[][])
            [
                ["credential", "send", "--runner", "r", "--repo", "acme/widgets", "--analysis", "sonarcloud"],
                ["credential", "send", "--tracker", "ado", "--analysis", "sonarcloud"],
                ["credential", "send", "--runner", "r", "--agent", "claude", "--tracker", "ado"],
            ])
        {
            await Assert.That(CliArgs.Parse(args)).IsTypeOf<CliAction.Unknown>()
                .Because($"'{string.Join(" ", args)}' names two credentials, and one send puts "
                       + "one credential under one locator.");
        }
    }

    [Test]
    public async Task A_key_no_locator_can_carry_is_refused_before_any_network()
    {
        var refused = CliArgs.Parse(["credential", "send", "--runner", "r", "--analysis", "sonar cloud"]);

        await Assert.That(refused).IsTypeOf<CliAction.Unknown>();
    }

    [Test]
    public async Task Nothing_it_parses_to_could_hold_a_secret()
    {
        var members = typeof(CliAction.LocatorCredentialSend).GetProperties();

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
            .Because("a runner, a locator and --json.");
    }

    [Test]
    public async Task The_help_names_both()
    {
        var help = ((CliAction.Unknown)CliArgs.Parse(["nonsense"])).Message;
        var sendLines = help.Split('\n')
            .Where(line => line.Contains("gg credential send", StringComparison.Ordinal))
            .ToList();

        await Assert.That(sendLines).IsNotEmpty();
        foreach (var line in sendLines)
        {
            await Assert.That(line).Contains("--tracker <key>", StringComparison.Ordinal);
            await Assert.That(line).Contains("--analysis <key>", StringComparison.Ordinal);
        }
    }
}
