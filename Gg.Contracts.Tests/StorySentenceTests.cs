namespace Gg.Contracts.Tests;

/// <summary>
/// One grammar for a flight's story, contract-side.
/// </summary>
/// <remarks>
/// <para>
/// <b><see cref="Reason"/>'s arrangement, for <see cref="Reason"/>'s reason.</b>
/// The flight log's <c>Detail</c> is a serialized dictionary with different keys
/// for each of fourteen kinds, shipped in a member whose own doc says <i>"a
/// rendered string, not a nested object… a shape that varied per kind would make
/// both of those parse a union"</i>. Every consumer parses that union anyway,
/// and the line a person reads is <c>lease-granted {"generation":1}</c>.
/// </para>
/// <para>
/// A closed kind, flat positional params, and one <see cref="FlightStory.Sentence"/>
/// both repositories compile against removes the union entirely: there is
/// nothing to branch on to parse, and no surface can reword what another surface
/// asserted.
/// </para>
/// </remarks>
public class StorySentenceTests
{
    // ---- S32.1-01 ----

    [Test]
    public async Task Every_kind_this_build_declares_renders_a_sentence()
    {
        // OVER THE VOCABULARY, not over the kinds a test happened to write. The
        // sweep is what makes a kind added next month fail here rather than
        // render blank on somebody's terminal.
        foreach (var kind in StoryKinds.All)
        {
            var sentence = FlightStory.Sentence(kind, ["one", "two"]);

            await Assert.That(sentence).IsNotEmpty()
                .Because($"'{kind}' is declared, so a reader is entitled to a sentence for it.");
            await Assert.That(sentence).IsNotEqualTo(kind)
                .Because($"'{kind}' rendered as itself is the raw kind wearing a renderer, "
                       + "which is what the log does today.");
        }
    }

    [Test]
    public async Task A_kind_nobody_declared_throws_and_names_itself()
    {
        // ARTICLE XI'S SHAPE. A renderer that shrugs at a kind it does not know
        // turns a governed record into silence, and silence reads as health. It
        // fails a build or a render; never an audit.
        var thrown = Assert.Throws<InvalidOperationException>(
            () => FlightStory.Sentence("not-a-kind", []));

        await Assert.That(thrown!.Message).Contains("not-a-kind")
            .Because("a refusal that does not name what it refused sends somebody to read "
                   + "the switch to find out which value reached it.");
    }

    [Test]
    public async Task A_sentence_reads_without_the_params_it_was_given_none_of()
    {
        // THE HALF THAT MUST NOT THROW. A kind whose params are missing is a
        // record written by an older writer, and a reader that threw on it would
        // make one absent value cost the whole story.
        foreach (var kind in StoryKinds.All)
        {
            await Assert.That(FlightStory.Sentence(kind, [])).IsNotEmpty()
                .Because($"'{kind}' with no params is an older record, not a broken one.");
        }
    }
}
