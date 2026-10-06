using Gg.Contracts;

namespace Gg.Client.Tests;

/// <summary>
/// <b>S63.1-02</b> - a draft file that no longer parses is refused with the parser's sentence and
/// left byte-for-byte as it was.
/// </summary>
/// <remarks>
/// A person may edit the draft by hand (slice sixty-three rule 4). Their mistake is answered,
/// and their file is theirs: a tool that "repaired" it would throw away what they typed.
/// </remarks>
public class AHandEditedDraftIsNeverOverwrittenTests
{
    [Test]
    public async Task A_change_to_an_unreadable_draft_writes_nothing()
    {
        var root = Directory.CreateTempSubdirectory("gg-drafts-").FullName;
        try
        {
            var drafts = new ItineraryDrafts(root);
            var mangled = "intent: [unclosed\nlegs:\n  - subject: x\n";
            Directory.CreateDirectory(Path.GetDirectoryName(drafts.PathOf("draft"))!);
            await File.WriteAllTextAsync(drafts.PathOf("draft"), mangled);

            var changed = drafts.Change("draft", d => d with { Intent = FlightIntent.Of("new words") });

            var refused = await Assert.That(changed).IsTypeOf<DraftChange.Refused>();
            await Assert.That(refused!.Diagnosis).Contains("YAML")
                .Because("the parser's own sentence, which says where the file went wrong.");
            await Assert.That(await File.ReadAllTextAsync(drafts.PathOf("draft"))).IsEqualTo(mangled);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public async Task A_change_the_draft_refuses_writes_nothing()
    {
        var root = Directory.CreateTempSubdirectory("gg-drafts-").FullName;
        try
        {
            var drafts = new ItineraryDrafts(root);
            _ = drafts.Change("draft", d => d with { Intent = FlightIntent.Of("kept") });
            var before = await File.ReadAllTextAsync(drafts.PathOf("draft"));

            var changed = drafts.Change("draft", _ => DraftChange.Refuse("no such leg"));

            await Assert.That(changed).IsTypeOf<DraftChange.Refused>();
            await Assert.That(await File.ReadAllTextAsync(drafts.PathOf("draft"))).IsEqualTo(before);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
