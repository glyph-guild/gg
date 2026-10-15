using System.Text.Json;
using Gg.Client;
using Gg.Console;

namespace Gg.Cli.Tests;

/// <summary>
/// The reference docs.goodgrief.dev publishes is read from this binary, so it
/// lists exactly the commands help lists and the keys the help page lists.
/// </summary>
public class TheReferenceIsTheHelpTests
{
    [Test]
    public async Task ReferenceJsonIsAVerb()
    {
        await Assert.That(CliArgs.Parse(["reference", "--json"])).IsTypeOf<CliAction.PrintReference>();
    }

    [Test]
    public async Task EveryCommandHelpListsIsInTheReference()
    {
        var commands = Reference.Build().Commands;

        var listed = CliArgs.Usage.Count(line => line.StartsWith("gg", StringComparison.Ordinal));

        await Assert.That(commands.Count).IsEqualTo(listed);
    }

    [Test]
    public async Task ACommandIsSplitIntoItsUsageAndItsSummary()
    {
        var fly = Reference.Build().Commands.Single(c => c.Usage.StartsWith("gg fly ", StringComparison.Ordinal));

        await Assert.That(fly.Usage).IsEqualTo("gg fly <text>|--uri <uri>|--ticket <provider>#<id>");
        await Assert.That(fly.Summary).IsEqualTo("open a flight");
        await Assert.That(fly.Options.Select(o => o.Usage)).Contains("--repo <name>");
        await Assert.That(fly.Options.Single(o => o.Usage == "--repo <name>").Summary)
            .IsEqualTo("which repository it is about, by its registered name");
    }

    [Test]
    public async Task ASummaryThatWrapsIsJoinedIntoOneSentence()
    {
        var tenant = Reference.Build().Commands.Single(c => c.Usage == "gg tenant name <name>");

        await Assert.That(tenant.Summary)
            .IsEqualTo("what this tenant is called; only somebody who administers it may say");
    }

    [Test]
    public async Task AUsageThatWrapsKeepsEveryOption()
    {
        var add = Reference.Build().Commands.Single(c =>
            c.Usage.StartsWith("gg airspace repositories add", StringComparison.Ordinal));

        await Assert.That(add.Usage).Contains("[--narrowings <dir>]");
        await Assert.That(add.Summary).IsEqualTo("make a repository nameable - a new one rides a gate");
    }

    [Test]
    public async Task NoCommandIsWithoutASummary()
    {
        var bare = Reference.Build().Commands.Where(c => c.Summary.Length == 0).Select(c => c.Usage).ToList();

        await Assert.That(bare).IsEmpty();
    }

    [Test]
    public async Task EveryKeyTheHelpPageTeachesIsInTheReference()
    {
        var keys = Reference.Build().Keys;

        var taught = Keymap.Catalogue().Count(entry => !entry.Binding.Untaught);

        await Assert.That(keys.Count).IsEqualTo(taught);
        await Assert.That(keys.Any(k => k.Key == "n" && k.Does == "new flight" && k.Heading == "")).IsTrue();
    }

    [Test]
    public async Task TheJsonCarriesTheVersionCommandsAndKeys()
    {
        using var json = JsonDocument.Parse(Reference.ToJson(Reference.Build()));

        await Assert.That(json.RootElement.GetProperty("version").GetString()).IsEqualTo(GgVersions.Binary);
        await Assert.That(json.RootElement.GetProperty("commands").GetArrayLength()).IsGreaterThan(0);
        await Assert.That(json.RootElement.GetProperty("keys").GetArrayLength()).IsGreaterThan(0);
    }
}
