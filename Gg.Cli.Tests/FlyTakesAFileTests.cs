namespace Gg.Cli.Tests;

/// <summary>
/// <b>S62.4-01</b> - <c>gg fly --file repo:path@ref</c> sends a file intent; a missing path or a
/// second payload is refused before anything is sent.
/// </summary>
/// <remarks>
/// <b>One argument, as <c>--ticket</c> is.</b> Three flags would make "a repository and no path"
/// reachable from here, and the contract refuses that with a better sentence than a parser can.
/// The ref follows the last <c>@</c> and is optional: absent is the default branch, as it is for
/// a checkout. The client half - that it is sent as a file intent and nothing the contract
/// refuses leaves this machine - is <c>FlightCommandTests</c>.
/// </remarks>
public class FlyTakesAFileTests
{
    [Test]
    public async Task A_repository_a_path_and_a_ref_is_a_file()
    {
        var fly = (CliAction.Fly)CliArgs.Parse(["fly", "--file", "JDX/JDNext:docs/plans/18291.md@develop"]);

        await Assert.That(fly.FileRepository).IsEqualTo("JDX/JDNext");
        await Assert.That(fly.FilePath).IsEqualTo("docs/plans/18291.md");
        await Assert.That(fly.FileRef).IsEqualTo("develop");
        await Assert.That(fly.Text).IsNull();
        await Assert.That(fly.Uri).IsNull();
    }

    [Test]
    public async Task The_ref_is_optional()
    {
        var fly = (CliAction.Fly)CliArgs.Parse(["fly", "--file", "JDX/JDNext:docs/plans/18291.md"]);

        await Assert.That(fly.FilePath).IsEqualTo("docs/plans/18291.md");
        await Assert.That(fly.FileRef).IsNull()
            .Because("absent means the repository's default branch, as it does for a checkout.");
    }

    [Test]
    [Arguments("JDX/JDNext")]
    [Arguments("JDX/JDNext:")]
    [Arguments(":docs/plan.md")]
    [Arguments("JDX/JDNext:docs/plan.md@")]
    public async Task A_reference_missing_a_half_is_refused_naming_the_shape(string reference)
    {
        var refused = await Assert.That(CliArgs.Parse(["fly", "--file", reference]))
            .IsTypeOf<CliAction.Unknown>();

        await Assert.That(refused!.Message).Contains("<repository>:<path>")
            .Because($"'{reference}' is missing a half, and the sentence says what the whole is.");
    }

    [Test]
    public async Task A_file_beside_text_is_two_payloads()
    {
        await Assert.That(CliArgs.Parse(["fly", "--file", "JDX/JDNext:docs/plan.md", "and also this"]))
            .IsTypeOf<CliAction.Unknown>();
    }

    [Test]
    public async Task A_bare_flag_names_file_among_what_fly_takes()
    {
        var refused = (CliAction.Unknown)CliArgs.Parse(["fly", "--file"]);

        await Assert.That(refused.Message).Contains("--file");
    }
}
