using Gg.Local;
namespace Gg.Cli.Tests;

/// <summary>
/// The verb the served reader is launched under.
/// </summary>
/// <remarks>
/// <para>
/// <b>A LAUNCH THE BINARY CANNOT PARSE IS THE FAILURE THIS EXISTS FOR.</b>
/// <c>SelfInvocation.For</c> records exactly that shape of defect - a command
/// line assembled by one part of the program and rejected by another, producing
/// a server that never started, an agent that was never offered the tool, and
/// nothing anywhere that said so. <c>IntentConfiguration.Served</c> writes this
/// command line; this file is what makes sure it is one <c>gg</c> answers.
/// </para>
/// <para>
/// <b>Machine-facing, so it is absent from the usage on purpose</b> - the same
/// disposition <c>runner tools</c> has. <c>EveryVerbIsDiscoverableTests</c>
/// walks the FIRST word of each arm, and that word is <c>runner</c>, which the
/// usage already names.
/// </para>
/// </remarks>
public class RunnerReadIsAVerbTests
{
    [Test]
    public async Task The_verb_served_readers_are_launched_under_parses()
    {
        var parsed = CliArgs.Parse(
            ["runner", "read",
             "--provider", "a-tracker",
             "--host", "https://tracker.example/acme",
             "--credential", "local:acme/board"]);

        await Assert.That(parsed).IsTypeOf<CliAction.RunnerRead>();

        var read = (CliAction.RunnerRead)parsed;
        await Assert.That(read.Provider).IsEqualTo("a-tracker");
        await Assert.That(read.Host).IsEqualTo("https://tracker.example/acme");
        await Assert.That(read.Credential).IsEqualTo("local:acme/board");
    }

    [Test]
    public async Task A_tracker_needing_no_credential_still_parses()
    {
        // The declaration half already refuses to make a credential-free
        // tracker invent one; the verb must not undo that by requiring the flag.
        var parsed = CliArgs.Parse(
            ["runner", "read", "--provider", "a-tracker", "--host", "https://tracker.example"]);

        await Assert.That(parsed).IsTypeOf<CliAction.RunnerRead>();
        await Assert.That(((CliAction.RunnerRead)parsed).Credential).IsNull();
    }

    [Test]
    public async Task A_read_with_no_host_is_refused_rather_than_defaulted()
    {
        // ARTICLE XI. A host defaulted here is a reader that quietly reads the
        // wrong tracker, which is worse than one that does not start.
        var parsed = CliArgs.Parse(["runner", "read", "--provider", "a-tracker"]);

        await Assert.That(parsed).IsTypeOf<CliAction.Unknown>();
        await Assert.That(((CliAction.Unknown)parsed).Message).Contains("--host");
    }

    [Test]
    public async Task What_Served_writes_is_what_the_parser_reads()
    {
        // THE TWO HALVES, JOINED. Both sides of this pass their own tests while
        // disagreeing about a flag name; only reading one with the other
        // catches that, and it is the whole defect SelfInvocation.For's remark
        // is warning about.
        var reader = Gg.Local.IntentConfiguration.Served(
            "a-tracker",
            host: "https://tracker.example/acme",
            locator: "local:acme/board",
            self: Gg.Local.SelfInvocation.For("/usr/local/bin/gg", "/usr/local/bin/gg")!);

        var parsed = CliArgs.Parse([.. reader.Arguments]);

        await Assert.That(parsed).IsTypeOf<CliAction.RunnerRead>()
            .Because("the launch writes this command line and this binary has to answer it.");

        var read = (CliAction.RunnerRead)parsed;
        await Assert.That(read.Provider).IsEqualTo("a-tracker");
        await Assert.That(read.Host).IsEqualTo("https://tracker.example/acme");
        await Assert.That(read.Credential).IsEqualTo("local:acme/board");
    }
}
