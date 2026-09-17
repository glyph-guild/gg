using Gg.Contracts;

namespace Gg.Client.Tests;

/// <summary>
/// `gg airspace diff` names the reason a file did not read, and not only that
/// it did not.
/// </summary>
/// <remarks>
/// <para>
/// <b>Found applying a real watch to a real tenant.</b> A hand-written work
/// kind came back as <c>airspace/work-kinds/review.yaml: does not read as a
/// document</c> and nothing else. The cause was one missing block, and the only
/// way to find it was to bisect the file against a known-good one — which is
/// not a thing a person should have to do to an error message.
/// </para>
/// <para>
/// <b>The reason was computed and thrown away.</b> <c>AirspaceTree.Read</c>
/// asks the contract's own parser and keeps what it says on
/// <c>UnreadableDocument.Diagnosis</c>; <c>ApplyEstateAsync</c> puts it in the
/// refusal. The DIFF projected the list down to paths on its way into
/// <c>EstateDiff</c>, so the sentence existed, travelled one method, and was
/// dropped at the last step.
/// </para>
/// <para>
/// <b>And diff is the command that is supposed to tell you.</b> Apply refusing
/// with the reason is the safety net; diff is what somebody runs FIRST,
/// precisely to find out what would happen — so the command with the better
/// message was the one you only reach by trying the thing you were checking.
/// </para>
/// </remarks>
public class TheDiffSaysWhyAFileDidNotReadTests
{
    /// <summary>A tree whose narrowing does not parse, and the reason it does not.</summary>
    private static (string Root, string Path) WithAnUnreadableFile()
    {
        var root = Path.Combine(Path.GetTempPath(), "gg-diff-why-" + Guid.NewGuid().ToString("n"));

        _ = AirspaceTree.Write(root, PullTests.Estate());

        var path = Path.Combine(root, "airspace", "narrowings", "pci.yaml");

        // `banana` IS NOT A CHECK, and the parser says so in its own words.
        File.WriteAllText(path, "obligations:\n  - id: pci-review\n    check: banana\n");

        return (root, "airspace/narrowings/pci.yaml");
    }

    [Test]
    public async Task The_reason_survives_into_the_diff_rather_than_stopping_at_the_read()
    {
        var (root, relative) = WithAnUnreadableFile();

        try
        {
            var read = AirspaceTree.Read(root);
            var said = read.Unreadable.Single().Diagnosis;

            await Assert.That(said).IsNotEmpty()
                .Because("ASK WHY IT PASSES: if the parser said nothing here, the assertion "
                       + "below would be demanding something nobody ever computed.");

            var diff = new EstateDiff
            {
                Changes = [],
                Retiring = [],
                Unreadable = [.. read.Unreadable],
            };

            await Assert.That(diff.Unreadable.Single().Diagnosis).IsEqualTo(said)
                .Because("the diff carries what the parser said rather than a projection down "
                       + "to the path - the sentence is the whole of what a person can act on.");
            await Assert.That(diff.Unreadable.Single().Path).IsEqualTo(relative);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public async Task The_rendered_diff_prints_the_reason_beside_the_path()
    {
        var (root, relative) = WithAnUnreadableFile();

        try
        {
            var read = AirspaceTree.Read(root);
            var said = read.Unreadable.Single().Diagnosis;

            var text = VerbOutput.ToText(new VerbResult.AirspaceDiffed(new EstateDiff
            {
                Changes = [],
                Retiring = [],
                Unreadable = [.. read.Unreadable],
            }));

            await Assert.That(text).Contains(relative);
            await Assert.That(text).Contains(said)
                .Because("a path and a shrug send somebody bisecting their own file against a "
                       + "working one, which is how this was actually found.");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public async Task Apply_and_diff_now_say_the_same_thing_about_the_same_file()
    {
        // THE TWO COMMANDS AGREE, which is the point rather than a bonus. Apply
        // already refused with the reason; diff is the one somebody runs first,
        // so the command with the worse message was the one reached earlier.
        var (root, _) = WithAnUnreadableFile();

        try
        {
            var read = AirspaceTree.Read(root);

            var diffed = VerbOutput.ToText(new VerbResult.AirspaceDiffed(new EstateDiff
            {
                Changes = [],
                Retiring = [],
                Unreadable = [.. read.Unreadable],
            }));

            foreach (var unreadable in read.Unreadable)
            {
                await Assert.That(diffed).Contains(unreadable.Diagnosis)
                    .Because("apply names every file AND its reason, and a person who ran diff "
                           + "first should not have to run the destructive one to learn more.");
            }
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
