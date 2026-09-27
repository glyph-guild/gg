using System.Text.Json;

namespace Gg.Cli.Tests;

/// <summary>
/// A document handed back is parsed before it is acknowledged, so a refusal
/// teaches the schema.
/// </summary>
/// <remarks>
/// <para>
/// <b>Measured on GG-327.</b> The rehearsal handed back a thoughtful document in a
/// schema it invented — <c>summary:</c>, <c>learned_against:</c>,
/// <c>advice: [{point, basis}]</c>, <c>outcome:</c> — because nothing had shown it
/// what a work-kind document looks like. The tool answered "recorded", the runner
/// shipped it, and the parser would have refused it at the far end where nobody
/// could act on the refusal.
/// </para>
/// <para>
/// <b>`describe_airspace` explains the keys and a fleet flight is not offered
/// it</b>, deliberately: it belongs to the console's drafting session. So the only
/// thing that can teach a flight the schema is the tool it hands the document to.
/// </para>
/// <para>
/// <b>Which is <c>DocumentTool</c>'s own stated virtue, and the one this tool was
/// missing:</b> <i>"the document is VALIDATED before it lands, so a refusal
/// teaches the schema."</i> Validating the role and not the document was half of
/// that.
/// </para>
/// <para>
/// <b>And it is load-bearing rather than kind.</b> The extractor reads only calls
/// whose result came back without an error, so a refusal here is a document that
/// never becomes a fact — and an agent that is told why can fix it and call again,
/// which the description already promises.
/// </para>
/// </remarks>
public class ARefusalTeachesTheSchemaTests
{
    private static string Call(string document) => JsonSerializer.Serialize(new
    {
        jsonrpc = "2.0",
        id = 7,
        method = "tools/call",
        @params = new
        {
            name = "propose_document",
            arguments = new Dictionary<string, string>
            {
                ["role"] = "work-kind",
                ["name"] = "ui-preview",
                ["document"] = document,
            },
        },
    });

    private static async Task<(bool IsError, string Text)> AnsweredAsync(string document)
    {
        var answers = await PlatformToolServerTests.ExchangeAsync(Call(document));
        var result = answers[0].RootElement.GetProperty("result");

        return (result.TryGetProperty("isError", out var flag) && flag.GetBoolean(),
                result.GetProperty("content")[0].GetProperty("text").GetString() ?? "");
    }

    [Test]
    public async Task A_document_in_an_invented_schema_is_refused_and_told_why()
    {
        // THE SHAPE GG-327 REALLY HANDED BACK, trimmed. Every key in it is
        // plausible and none of them is the schema.
        var (isError, text) = await AnsweredAsync(
            "summary: >\n  Rehearsal of ADO 19069\nlearned_against:\n  repository: JDX/JDNext\n"
          + "advice:\n  - point: npm ci takes about 50 seconds\n    basis: ran it and timed it\n");

        await Assert.That(isError).IsTrue()
            .Because("acknowledging it sends the runner a fact the far end will refuse, where "
                   + "nobody can act on the refusal.");

        await Assert.That(text).Contains("Refused")
            .Because("and it has to say so in the word the other refusals use.");
    }

    // A_document_that_reads_as_the_role_is_recorded LIVED HERE, and it asserted
    // that a complete work-kind envelope handed back by a flight is recorded.
    // GG-330 is why it does not any more: the completeness it required is what
    // made a rehearsal invent `obligations` to satisfy a validator, and a flight
    // authoring governance was never its standing. The accepting case now lives in
    // `AFlightAmendsRatherThanAuthorsTests`, where the document is the one thing
    // the flight actually has - what it learned - and the refusing case for a
    // governing document lives beside it.
}
