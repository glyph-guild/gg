using Gg.Client;
using Gg.Local;

namespace Gg.Cli.Tests;

/// <summary>
/// `gg config`: show what is in force, check a document, set one value.
/// </summary>
/// <remarks>
/// <para>
/// <b><c>set</c> is the ordinary path here, not the escape hatch</b> — which is
/// the measurement's doing. Four settings are set on any machine in this
/// product, so a person changing one is changing a quarter of their
/// configuration, and asking them to hand-edit JSON for that would be asking
/// them to open a file to change one word.
/// </para>
/// <para>
/// <b><c>validate</c> contacts nothing</b>, the property <c>gg envelope
/// validate</c> already has: a document that is wrong should cost no round trip,
/// and somebody working on a machine with no network should still be able to
/// check their own file.
/// </para>
/// </remarks>
public class ConfigIsAVerbTests
{
    private static string ATempFile() =>
        Path.Combine(Path.GetTempPath(), $"gg-config-verb-{Guid.NewGuid():N}.json");

    [Test]
    public async Task Validate_needs_no_session_and_no_client()
    {
        // Constructed with nothing, because it needs nothing. A signature that
        // asked for a client would make the offline case unreachable.
        var result = ConfigCommands.Validate("{ \"editor\": \"hx\" }");

        await Assert.That(result).IsTypeOf<VerbResult.ConfigValidated>();
        await Assert.That(((VerbResult.ConfigValidated)result).Value.Valid).IsTrue();
    }

    [Test]
    public async Task An_invalid_document_is_refused_and_still_says_everything_it_knows()
    {
        // A validator that reports success on a document it just refused is one
        // nobody can put in a pipeline - and one that refuses without saying
        // what is wrong is one nobody can act on.
        var result = (VerbResult.ConfigValidated)
            ConfigCommands.Validate("{ \"runner-hold-seconds\": 0 }");

        await Assert.That(result.Value.Valid).IsFalse();
        await Assert.That(result.Value.Diagnosis).IsNotNull();
        await Assert.That(result.Value.Diagnosis!).Contains("0", StringComparison.Ordinal);
    }

    [Test]
    public async Task A_valid_document_comes_back_in_the_form_gg_would_write_it()
    {
        // What `show` after `set` will print, so a person can see now what their
        // file is about to look like - the property `gg envelope validate`
        // already offers through Canonical.
        var result = (VerbResult.ConfigValidated)
            ConfigCommands.Validate("{ \"editor\": \"hx\" }");

        await Assert.That(result.Value.Canonical).IsNotNull();
        await Assert.That(result.Value.Canonical!).Contains("editor", StringComparison.Ordinal);
    }

    [Test]
    public async Task Set_refuses_a_key_nobody_declared_and_says_what_is_settable()
    {
        var path = ATempFile();

        try
        {
            var refused = Assert.Throws<ArgumentOutOfRangeException>(
                () => ConfigCommands.Set(path, "controlplane", "https://example.invalid"));

            await Assert.That(refused!.Message).Contains("control-plane", StringComparison.Ordinal)
                .Because("a refusal that does not say what IS settable leaves somebody "
                       + "guessing at the spelling, which is how they got here.");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public async Task Set_changes_one_value_and_leaves_the_others_where_they_were()
    {
        var path = ATempFile();

        try
        {
            ConfigurationFile.Write(
                new Configuration { ControlPlane = "https://kept.invalid", Editor = "vi" }, path);

            ConfigCommands.Set(path, "editor", "hx");

            var after = ConfigurationFile.Read(path).Configuration!;

            await Assert.That(after.Editor).IsEqualTo("hx");
            await Assert.That(after.ControlPlane).IsEqualTo("https://kept.invalid")
                .Because("setting one value rewrote the file, and everything else in it has "
                       + "to survive that.");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public async Task Set_refuses_a_value_the_document_would_not_accept()
    {
        // The file on disk stays valid, always. A set that wrote something
        // Validate refuses would leave a document the next run cannot read.
        var path = ATempFile();

        try
        {
            var refused = Assert.Throws<ArgumentOutOfRangeException>(
                () => ConfigCommands.Set(path, "control-plane", "localhost:5199"));

            await Assert.That(refused).IsNotNull();
            await Assert.That(File.Exists(path)).IsFalse()
                .Because("nothing was written, so a refused set leaves no file behind.");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public async Task Show_says_where_every_value_came_from()
    {
        var shown = (VerbResult.ConfigShown)ConfigCommands.Show(
            ConsoleEnvironment.Read(
                new Configuration { Editor = "hx" }, environment: _ => null),
            path: "/somewhere/config.json");

        await Assert.That(shown.Value.Path).IsEqualTo("/somewhere/config.json")
            .Because("a person asking what is in force is often asking where it lives.");

        var editor = shown.Value.Settings.Single(s => s.Name == "EDITOR");

        await Assert.That(editor.Source).IsEqualTo(SettingSources.File);
    }
}
