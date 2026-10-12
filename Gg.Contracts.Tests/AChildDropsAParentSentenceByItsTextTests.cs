using Gg.Contracts;

namespace Gg.Contracts.Tests;

/// <summary>
/// S77.1-03: a child removes a parent's instruction by its exact text.
/// </summary>
/// <remarks>
/// <b>By text, not by position</b>, because an instruction has no id and a
/// position moves whenever the parent gains a sentence. A removal that matches
/// nothing is refused: a parent reworded under its child would otherwise leave
/// two contradicting sentences in one prompt, and the child's author would
/// never hear of it.
/// </remarks>
public class AChildDropsAParentSentenceByItsTextTests
{
    [Test]
    public async Task The_named_sentence_is_gone()
    {
        var effective = WorkKindFixtures.Resolve(WorkKindFixtures.Child).Effective!;

        await Assert.That(effective.Instructions.Select(i => i.Text))
            .DoesNotContain(WorkKindFixtures.Landing);
    }

    [Test]
    public async Task A_sentence_the_parent_no_longer_says_is_refused_naming_it()
    {
        var resolved = WorkKindFixtures.Resolve(WorkKindFixtures.Child.Replace(
            "    - \"Your whole result is the landing you propose.\"",
            "    - \"Your result is the landing you propose.\""));

        await Assert.That(resolved.Effective).IsNull();
        await Assert.That(resolved.Refused!).Contains("Your result is the landing you propose.");
    }

    [Test]
    public async Task The_match_is_exact()
    {
        var resolved = WorkKindFixtures.Resolve(WorkKindFixtures.Child.Replace(
            "    - \"Your whole result is the landing you propose.\"",
            "    - \"your whole result is the landing you propose.\""));

        await Assert.That(resolved.Refused).IsNotNull()
            .Because("a near match is a guess about which sentence was meant.");
    }
}
