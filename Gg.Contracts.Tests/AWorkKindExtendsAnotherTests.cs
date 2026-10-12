using Gg.Contracts;

namespace Gg.Contracts.Tests;

/// <summary>
/// S77.1-01: a work kind file with <c>extends:</c> is read as an extension.
/// </summary>
/// <remarks>
/// <para>
/// <b>A door of its own, the narrowing's rule.</b> An extension is not an
/// envelope: every member but its parent is optional, and the language refuses
/// a partial <see cref="Envelope"/>, which has four required members. So the
/// full door refuses <c>extends:</c> by name and points at this one, rather
/// than reading a child as a broken envelope.
/// </para>
/// <para>
/// <b>A child inherits its parent's context.</b> It restates no root-governed
/// member - context, the bounds, the floor's servers, learned advice - and one
/// that tries is refused by name, because a child that names its own context
/// has misunderstood what it inherits.
/// </para>
/// </remarks>
public class AWorkKindExtendsAnotherTests
{
    [Test]
    public async Task A_child_is_read_as_its_parent_and_what_it_changes()
    {
        var read = EnvelopeYaml.ParseExtension(WorkKindFixtures.Child);

        await Assert.That(read.Diagnosis).IsNull();
        var child = read.Extension!;
        await Assert.That(child.Extends).IsEqualTo("investigate");
        await Assert.That(child.Brief).IsEqualTo("Investigate the work item and write the plan to its tracker.");
        await Assert.That(child.Description).IsNull()
            .Because("absent stays absent: a child that says nothing about its description inherits it.");
        await Assert.That(child.Loops.Single().Moves).IsEquivalentTo(new[] { "propose-work-item", "read" });
        await Assert.That(child.Destinations.Single().Id).IsEqualTo("ado");
        await Assert.That(child.Remove!.Destinations).IsEquivalentTo(new[] { "none" });
        await Assert.That(child.Remove.Instructions).IsEquivalentTo(new[] { WorkKindFixtures.Landing });
    }

    [Test]
    public async Task A_child_renders_back_to_what_it_says()
    {
        var once = EnvelopeText.Render(WorkKindFixtures.Extension(WorkKindFixtures.Child));
        var twice = EnvelopeText.Render(WorkKindFixtures.Extension(once));

        await Assert.That(twice).IsEqualTo(once)
            .Because("pull writes back what the author wrote, and a render that drifts on its own "
                   + "output mints a version per pull.");
        await Assert.That(once).StartsWith("extends: investigate\n");
        await Assert.That(once).DoesNotContain("description:")
            .Because("a child that inherits its description must not gain one by being written back.");
        await Assert.That(once).DoesNotContain("context:");
    }

    [Test]
    public async Task The_full_door_refuses_a_child_by_name()
    {
        var read = EnvelopeYaml.Parse(WorkKindFixtures.Child);

        await Assert.That(read.Envelope).IsNull();
        await Assert.That(read.Diagnosis!).Contains("extends")
            .Because("a child read as an envelope is a document missing its context, which is the "
                   + "wrong diagnosis for the right file.");
    }

    [Test]
    public async Task A_child_names_its_parent()
    {
        foreach (var (what, text) in ((string, string)[])
            [("no extends", "brief: \"a child of nobody\"\n"),
             ("a blank extends", "extends: \"  \"\nbrief: \"a child of nobody\"\n")])
        {
            await Assert.That(EnvelopeYaml.ParseExtension(text).Diagnosis).IsNotNull()
                .Because($"{what} is a delta against nothing.");
        }
    }

    [Test]
    public async Task A_child_refuses_what_it_inherits_and_what_nobody_declared()
    {
        foreach (var key in new[] { "context", "learned", "models", "repositories", "colour" })
        {
            var read = EnvelopeYaml.ParseExtension($"extends: investigate\n{key}: x\n");

            await Assert.That(read.Diagnosis).IsNotNull();
            await Assert.That(read.Diagnosis!).Contains(key)
                .Because("a refusal names the key, or the author is left guessing which line.");
        }

        await Assert.That(EnvelopeYaml.ParseExtension(
                "extends: investigate\nremove:\n  colours:\n    - red\n").Diagnosis!)
            .Contains("colours");
    }

    [Test]
    public async Task Based_on_is_consumed_and_is_not_part_of_the_child()
    {
        var read = EnvelopeYaml.ParseExtension("based-on: investigate-instructions@v2\n" + WorkKindFixtures.Child);

        await Assert.That(read.BasedOn).IsEqualTo("investigate-instructions@v2");
        await Assert.That(EnvelopeText.Render(read.Extension!)).DoesNotContain("based-on");
    }
}
