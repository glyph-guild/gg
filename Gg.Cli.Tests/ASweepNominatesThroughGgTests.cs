using System.Text.Json;
using Gg.Local;

namespace Gg.Cli.Tests;

/// <summary>
/// A sweep's executor nominates through gg's own server, once per item worth a
/// flight.
/// </summary>
/// <remarks>
/// <para>
/// <b>The owner's decision of 2026-09-16:</b> <i>"the agent and/or script should
/// be doing that. that way it can be dynamic if necessary."</i> The executor
/// chooses what to nominate, and it does so through the tool it already has -
/// ADR-0023 § 2: a nomination passes through <c>gg</c> by construction, so the
/// server's record is the record of what the sweep nominated.
/// </para>
/// <para>
/// <b>A sweep mode, not a second tool.</b> A flight's classifier nominates a
/// kind for the one item it is about, once, and then stops. A sweep is about
/// many items and nominates each one worth a flight - so in sweep mode the tool
/// names the subject, its version and its intent key, leaves the kind to the
/// bound when the agent does not choose, may be called many times, and does
/// not tell the agent to stop. It offers no environment or repository to
/// select, because nothing on a sweep's path selects.
/// </para>
/// </remarks>
public class ASweepNominatesThroughGgTests
{
    private static async Task<IReadOnlyList<JsonDocument>> ExchangeAsync(
        bool sweep, params string[] lines)
    {
        var output = new StringWriter();
        await PlatformToolServer.RunAsync(
            new StringReader(string.Join('\n', lines)), output, sweep: sweep);

        return output.ToString()
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => JsonDocument.Parse(line))
            .ToList();
    }

    private const string List = "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"tools/list\"}";

    private static string Nominate(string arguments) =>
        "{\"jsonrpc\":\"2.0\",\"id\":2,\"method\":\"tools/call\",\"params\":{\"name\":\""
      + NominationTool.Name + "\",\"arguments\":" + arguments + "}}";

    private static JsonElement NominateTool(JsonDocument listed) =>
        listed.RootElement.GetProperty("result").GetProperty("tools").EnumerateArray()
            .Single(t => t.GetProperty("name").GetString() == NominationTool.Name);

    private static (bool IsError, string Text) Answer(JsonDocument answer)
    {
        // ABSENT IS FALSE: the server writes the flag only on a refusal.
        var result = answer.RootElement.GetProperty("result");
        return (result.TryGetProperty("isError", out var flag) && flag.GetBoolean(),
                result.GetProperty("content")[0].GetProperty("text").GetString()!);
    }

    [Test]
    public async Task The_runner_starts_the_server_in_sweep_mode_by_asking_for_it()
    {
        await Assert.That(((CliAction.RunnerTools)CliArgs.Parse(["runner", "tools", "--sweep"]))
            .Sweep).IsTrue();
        await Assert.That(((CliAction.RunnerTools)CliArgs.Parse(["runner", "tools"])).Sweep)
            .IsFalse()
            .Because("a flight's server is started exactly as it always was.");
    }

    [Test]
    public async Task In_sweep_mode_a_nomination_names_its_subject_and_version()
    {
        var tool = NominateTool((await ExchangeAsync(sweep: true, List))[0]);
        var schema = tool.GetProperty("inputSchema");

        var required = schema.GetProperty("required").EnumerateArray()
            .Select(r => r.GetString()!).ToList();
        var properties = schema.GetProperty("properties").EnumerateObject()
            .Select(p => p.Name).ToList();

        await Assert.That(required).IsEquivalentTo(
            (string[])[NominationTool.Sweep.Subject, NominationTool.Sweep.Version, "reason"])
            .Because("a sweep is about many items, so each nomination says which one, and at "
                   + "which version - the key the board dedupes on.");
        await Assert.That(properties).Contains(NominationTool.Sweep.IntentKey);
        await Assert.That(properties).Contains("work_kind");
        await Assert.That(properties).DoesNotContain("environment")
            .Because("nothing on a sweep's path selects, so offering a selection would be a "
                   + "field an agent fills and nothing reads.");
        await Assert.That(properties).DoesNotContain("repository");
    }

    [Test]
    public async Task A_flights_nomination_is_unchanged()
    {
        var schema = NominateTool((await ExchangeAsync(sweep: false, List))[0])
            .GetProperty("inputSchema");

        await Assert.That(schema.GetProperty("required").EnumerateArray()
                .Select(r => r.GetString()!))
            .IsEquivalentTo((string[])["work_kind", "reason"]);
    }

    [Test]
    public async Task A_sweep_nomination_is_recorded_and_the_agent_goes_on()
    {
        var (isError, text) = Answer((await ExchangeAsync(sweep: true, Nominate(
            "{\"subject\":\"4242\",\"version\":\"7\",\"intent_key\":\"https://t.example/4242\","
          + "\"reason\":\"needs a person\"}")))[0]);

        await Assert.That(isError).IsFalse();
        await Assert.That(text).Contains("4242");
        await Assert.That(text).DoesNotContain("stop now")
            .Because("a sweep nominates every item worth a flight, so telling it to stop after "
                   + "the first would make every sweep nominate one.");
    }

    [Test]
    [Arguments("{\"version\":\"7\",\"reason\":\"why\"}")]
    [Arguments("{\"subject\":\"4242\",\"reason\":\"why\"}")]
    [Arguments("{\"subject\":\"4242\",\"version\":\"7\"}")]
    public async Task A_sweep_nomination_missing_a_required_part_is_refused(string arguments)
    {
        var (isError, text) = Answer((await ExchangeAsync(sweep: true, Nominate(arguments)))[0]);

        await Assert.That(isError).IsTrue();
        await Assert.That(text).Contains("Nothing was recorded");
    }

    [Test]
    public async Task A_sweep_nomination_past_an_identitys_length_is_refused()
    {
        var subject = new string('x', Gg.Contracts.SweepNomination.MaxSubject + 1);

        var (isError, _) = Answer((await ExchangeAsync(sweep: true, Nominate(
            "{\"subject\":\"" + subject + "\",\"version\":\"7\",\"reason\":\"why\"}")))[0]);

        await Assert.That(isError).IsTrue()
            .Because("a subject is an identity, and the report the runner sends refuses a longer "
                   + "one - refusing it here is what lets the agent fix it.");

        var key = new string('k', Gg.Contracts.SweepNomination.MaxIntentKey + 1);
        var (keyError, _) = Answer((await ExchangeAsync(sweep: true, Nominate(
            "{\"subject\":\"4242\",\"version\":\"7\",\"intent_key\":\"" + key
          + "\",\"reason\":\"why\"}")))[0]);

        await Assert.That(keyError).IsTrue();
    }
}
