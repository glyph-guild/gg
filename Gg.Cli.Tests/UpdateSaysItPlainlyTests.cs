using Gg.Client;
using Gg.Local;

namespace Gg.Cli.Tests;

/// <summary>
/// The words an update gives a person when it will not, or does not need to, act.
/// </summary>
/// <remarks>
/// These are read by people at a terminal who are not thinking in terms of
/// control planes or install shapes. Each message says what happened, what
/// gg did about it (usually nothing), and what to do next. The banned phrases
/// are the ones this text used to carry.
/// </remarks>
public class UpdateSaysItPlainlyTests
{
    private const string Installer = "https://example.invalid/install.sh";

    private static readonly string[] Jargon =
    [
        "control plane", "could not be established", "unit of change", "that this machine chose",
        "request this machine", "layout it does not understand",
    ];

    [Test]
    public async Task An_unknown_latest_version_is_said_in_plain_words()
    {
        var plan = UpdatePlans.For(new InstallShape(InstallKind.ToolPath, "/usr/local/lib/gg"),
            "0.88.20", target: null, Installer, toolPathWritable: true, scratch: "/tmp/x");

        var said = plan.Refusal ?? plan.Summary;

        await Assert.That(said).DoesNotContain("could not be established")
            .Because("'could not be established' is the phrase a person stops reading at.");
        await Assert.That(said).DoesNotContain("This may already be the newest.")
            .Because("the sentence with the answer should say so in its own words.");
        await Assert.That(said).Contains("You may already have the newest version")
            .Because("the one thing a person wants to know is whether they need to do anything.");
    }

    [Test]
    public async Task A_machine_ahead_of_latest_is_said_without_jargon()
    {
        var plan = UpdatePlans.For(new InstallShape(InstallKind.ToolPath, "/usr/local/lib/gg"),
            "0.90.0", "0.89.0", Installer, toolPathWritable: true, scratch: "/tmp/x");

        await Assert.That(plan.Summary).Contains("ahead")
            .Because("the test that pins this state reads the word ahead, and it stays.");
        await Assert.That(plan.Summary).DoesNotContain("control plane")
            .Because("the latest version is the latest one gg has been told about, in a person's words.");
        await Assert.That(plan.Summary).DoesNotContain("Nothing was moved")
            .Because("'nothing was moved' is the wrong comfort here: nothing was touched, and that is all.");
    }

    [Test]
    public async Task A_target_that_is_not_a_version_is_said_without_jargon()
    {
        var plan = UpdatePlans.For(new InstallShape(InstallKind.ToolPath, "/usr/local/lib/gg"),
            "0.88.20", "latest", Installer, toolPathWritable: true, scratch: "/tmp/x");

        await Assert.That(plan.Refusal).IsNotNull();
        await Assert.That(plan.Refusal!).DoesNotContain("request this machine")
            .Because("a machine is not making a request; it is declining to guess.");
        await Assert.That(plan.Refusal!).Contains("0.88.20")
            .Because("the example is the thing a person can recognise as a version.");
    }

    [Test]
    public async Task An_unknown_install_method_is_said_without_jargon()
    {
        var plan = UpdatePlans.For(new InstallShape(InstallKind.Native, null),
            "0.88.20", "0.88.21", installer: null, toolPathWritable: true, scratch: "/tmp/x");

        var said = plan.Refusal ?? plan.Summary;

        await Assert.That(said).DoesNotContain("could not be established");
        await Assert.That(said).Contains("reinstall")
            .Because("a person who cannot update in place needs the thing to do instead.");
    }

    [Test]
    public async Task A_container_is_told_what_to_do_in_plain_words()
    {
        var plan = UpdatePlans.For(new InstallShape(InstallKind.Container, null),
            "0.88.20", "0.88.21", Installer, toolPathWritable: true, scratch: "/tmp/x");

        var said = plan.Refusal ?? plan.Summary;

        await Assert.That(said).DoesNotContain("unit of change");
        await Assert.That(said).Contains("rebuild");
    }

    [Test]
    public async Task No_update_message_carries_jargon()
    {
        var messages = new List<string>();
        foreach (var (kind, installed, target, installer) in new (InstallKind, string?, string?, string?)[]
                 {
                     (InstallKind.ToolPath, "0.88.20", null, Installer),
                     (InstallKind.ToolPath, "0.90.0", "0.89.0", Installer),
                     (InstallKind.ToolPath, "0.88.20", "latest", Installer),
                     (InstallKind.Native, "0.88.20", "0.88.21", null),
                     (InstallKind.Native, "0.88.20", "0.88.21", Installer),
                     (InstallKind.Container, "0.88.20", "0.88.21", Installer),
                     (InstallKind.Unknown, "0.88.20", "0.88.21", Installer),
                 })
        {
            var plan = UpdatePlans.For(new InstallShape(kind, "/usr/local/lib/gg"),
                installed, target, installer, toolPathWritable: true, scratch: "/tmp/x");
            messages.Add(plan.Refusal ?? plan.Summary);
        }

        foreach (var word in Jargon)
        {
            await Assert.That(messages.Any(m => m.Contains(word, StringComparison.Ordinal))).IsFalse()
                .Because($"'{word}' is a term of this codebase, not a sentence a person reads.");
        }
    }
}
