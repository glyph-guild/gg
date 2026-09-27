using System.Text.Json;

namespace Gg.Cli.Tests;

/// <summary>
/// A flight hands back what it LEARNED, not a document it wrote.
/// </summary>
/// <remarks>
/// <para>
/// <b>Measured on GG-330.</b> The rehearsal's carrier was fixed — it loaded
/// <c>propose_document</c> in its first <c>ToolSearch</c> and wrote no scratch file
/// — and the tool then refused it 32 times out of 35 attempts. Four faults, and
/// three of them are in this file's subject:
/// </para>
/// <para>
/// <b>1. The tool validated a WHOLE envelope when the flight had one field.</b> To
/// say what it had learned, the agent had to author <c>context</c>,
/// <c>obligations</c>, <c>loops</c> and <c>destinations</c> for a document it does
/// not administer — <see cref="Gg.Contracts.Envelope"/> has four <c>required</c>
/// members — and every required key it did not have is a key it has to invent. It
/// invented <c>obligations: none: {check: …}</c>, which is a policy statement, to get
/// past a validator.
/// </para>
/// <para>
/// <b>2. <c>PlatformToolServer.Refused</c> dispatched two of the parser's six role
/// arms</b> — <c>ParseNarrowing</c> for narrowing, <c>Parse</c> (the WORK-KIND
/// envelope) for everything else. So GG-330 said <c>role: watch</c> and was told
/// <i>"'obligations' is missing, and an envelope without it governs nothing"</i>, a
/// key a watch document does not have. The refusal path exists to teach the schema
/// and taught it the wrong one, authoritatively, eight times.
/// </para>
/// <para>
/// <b>3. And the control plane holds <c>work-kind</c> only</b> — every other role
/// is logged and dropped — so the three proposals the tool DID accept were all
/// <c>narrowing</c> and all discarded. The tool offered seven roles, two were
/// judged correctly and one is consumed.
/// </para>
/// <para>
/// <b>So the surface narrows to what exists: a flight amends the learned context of
/// a work kind.</b> That is a call made rather than a bug fixed, and it removes
/// something: a flight can no longer hand back a complete replacement envelope, as
/// <c>ARefusalTeachesTheSchemaTests</c> used to assert it could. The argument is
/// that authoring governance was never a flight's standing — the kind's own
/// instruction says <i>"say nothing about what an agent is permitted to do"</i> —
/// and leaving the door open is what produced two <c>check: human</c> gates on
/// <c>src/JDX.Web/**</c> from a flight that was asked for advice.
/// </para>
/// <para>
/// <b>Fabrication becomes structurally impossible rather than discouraged</b>,
/// which is the property worth having: there is no required key left to invent,
/// because the only key is the one the flight actually has.
/// </para>
/// </remarks>
public class AFlightAmendsRatherThanAuthorsTests
{
    /// <summary>What GG-330 should have been able to hand back on its first call.</summary>
    private const string Amendment = """
        learned:
          against:
            commit: "a1b2c3d"
          advice:
            - "node_modules is absent at checkout; npm install takes about fifty seconds."
        """;

    private static string Call(string document, string role = "work-kind") =>
        JsonSerializer.Serialize(new
        {
            jsonrpc = "2.0",
            id = 7,
            method = "tools/call",
            @params = new
            {
                name = "propose_document",
                arguments = new Dictionary<string, string>
                {
                    ["role"] = role,
                    ["name"] = "ui-preview",
                    ["document"] = document,
                },
            },
        });

    private static async Task<(bool IsError, string Text)> AnsweredAsync(
        string document, string role = "work-kind")
    {
        var answers = await PlatformToolServerTests.ExchangeAsync(Call(document, role));
        var result = answers[0].RootElement.GetProperty("result");

        return (result.TryGetProperty("isError", out var flag) && flag.GetBoolean(),
                result.GetProperty("content")[0].GetProperty("text").GetString() ?? "");
    }

    [Test]
    public async Task What_a_flight_learned_is_enough_on_its_own()
    {
        // THE WHOLE POINT. Thirty-five calls bought GG-330 nothing because every
        // one of them had to carry a governing document; this is the document it
        // actually had to write.
        var (isError, text) = await AnsweredAsync(Amendment);

        await Assert.That(isError).IsFalse()
            .Because($"'learned' alone is what a flight has to say, and it was answered: {text}");

        await Assert.That(text).Contains("Recorded")
            .Because("and the receipt says so in the word the accepting path uses.");
    }

    [Test]
    public async Task A_governing_document_is_refused_because_a_flight_does_not_author_one()
    {
        // THE CAPABILITY THIS REMOVES, asserted rather than left implicit. The
        // envelope below is valid and was recorded before GG-330; it is refused
        // now, and the refusal is the design rather than a regression.
        var (isError, text) = await AnsweredAsync("""
            context:
              scope: "**"
              constitution: "1.0.0"
            obligations:
              in-scope:
                check: machine
                rule: no-file-outside-scope
            loops:
              implement:
                executor: frontier
                discharges: [in-scope]
                moves: [read, edit]
                budget:
                  wall-clock: "20m"
                on-exhaustion: handoff-to-human
            destinations:
              forge:
                kind: pull-request
                requires: [in-scope]
            """);

        await Assert.That(isError).IsTrue()
            .Because("a flight amends learned context; authoring governance was never its standing.");

        await Assert.That(text).Contains("learned")
            .Because("a refusal that does not name the one key it wants teaches nothing, which "
                   + "is the fault that cost GG-330 thirty-five calls.");
    }

    [Test]
    public async Task An_invented_obligation_is_refused_rather_than_recorded()
    {
        // GG-330's attempt 27, verbatim in shape. It named itself a probe, and it
        // was RECORDED as a governance proposal - because the channel costs
        // nothing and marks nothing as experimental.
        var (isError, text) = await AnsweredAsync("""
            based-on: learn-work-kind
            obligations:
              probe-obligation:
                check: human
                approver: root
            """, role: "narrowing");

        await Assert.That(isError).IsTrue()
            .Because("this was accepted three times on GG-330 and dropped by the control plane "
                   + "each time, so the flight was told 'recorded' about nothing.");
    }

    [Test]
    [MethodDataSource(nameof(RolesTheControlPlaneDoesNotHold))]
    public async Task A_role_nothing_consumes_is_refused_by_name(string role)
    {
        // ONE CASE PER ROLE, which is the coverage that was missing: the old file
        // had two tests over one role, so four arms judging documents against the
        // work-kind schema shipped invisibly.
        var (isError, text) = await AnsweredAsync(Amendment, role);

        await Assert.That(isError).IsTrue()
            .Because($"'{role}' is logged and dropped by the receptor, so accepting it here "
                   + "tells the flight its work was recorded when nothing was.");

        await Assert.That(text).Contains(Gg.Contracts.Roles.WorkKind)
            .Because("and it names the role that IS held, rather than only refusing.");
    }

    public static IEnumerable<Func<string>> RolesTheControlPlaneDoesNotHold() =>
        [.. Gg.Contracts.Roles.All
            .Where(role => !string.Equals(role, Gg.Contracts.Roles.WorkKind, StringComparison.Ordinal))
            .Select<string, Func<string>>(role => () => role)];

    [Test]
    public async Task Advice_in_the_wrong_shape_says_which_shape_it_wants()
    {
        // THE CONTRADICTION, PINNED. GG-330's attempts 7 and 8 were told
        // "'learned.advice' should be a single value" and then "'learned.advice'
        // should be a list" - both from `Strings`, which passes the PARENT path
        // into RequireScalar for each item, so a list-of-blocks and a bare scalar
        // blame the same path with opposite advice. Neither message ever said
        // that advice is a list OF SINGLE VALUES, which is the one fact that
        // would have ended it.
        var (_, listOfBlocks) = await AnsweredAsync("""
            learned:
              against:
                commit: "a1b2c3d"
              advice:
                - point: "npm install takes about fifty seconds"
                  basis: "ran it and timed it"
            """);

        var (_, bareScalar) = await AnsweredAsync("""
            learned:
              against:
                commit: "a1b2c3d"
              advice: |
                npm install takes about fifty seconds
            """);

        await Assert.That(listOfBlocks).IsNotEqualTo(bareScalar)
            .Because("two different mistakes that produce the same sentence is a diagnosis "
                   + "an author cannot act on.");

        // BOTH have to name the container AND the items, because naming one of the
        // two is what let an author swap between them for two calls running.
        await Assert.That(listOfBlocks).Contains("single value")
            .Because("a list of blocks needs telling that the ITEMS are single values.");

        await Assert.That(bareScalar).Contains("list")
            .Because("a bare scalar needs telling that the CONTAINER is a list.");

        await Assert.That(bareScalar).Contains("single value")
            .Because("and telling it the items are single values in the same breath, which "
                   + "is the half that was missing from both of GG-330's messages.");
    }
}
