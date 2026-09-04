using Gg.Contracts;
using Gg.Runner.Execution;

namespace Gg.Runner.Tests;

/// <summary>
/// Reading a nominated work kind out of what the agent actually did.
/// </summary>
/// <remarks>
/// <para>
/// <b>From the tool call, never from prose.</b> A classifier's closing summary
/// mentions a work kind because it just nominated one, and a sentence is
/// something an agent can be TOLD to write - by a file in a customer's tree,
/// among other things. A tool call is a thing the agent chose to make, in a
/// shape read mechanically, and it is a narrower thing to trust.
/// </para>
/// <para>
/// <b>From the transcript and only the transcript, which is a security
/// property rather than a convenience.</b> The alternative - the server writing
/// a sidecar file the runner reads - is forgeable: <c>--allowedTools</c> does
/// not bind the shell, so any envelope granting <c>run-tests</c> lets the agent
/// write the fact directly. The channel's integrity would then depend on the
/// envelope, which is exactly inverted.
/// </para>
/// <para>
/// <b>The happy path is asserted against a real transcript.</b>
/// <c>agent-nominated-a-kind.ndjson</c> is a genuine Claude Code session
/// against the real server: it read a work item through an operator's tracker
/// and then nominated through the platform's own. Step 0 measured the shape of
/// these blocks precisely so this could be written against reality rather than
/// against an approximation of it.
/// </para>
/// </remarks>
public class NominationExtractionTests
{
    private static string Fixture(string name)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Gg.sln")))
        {
            dir = dir.Parent;
        }

        return File.ReadAllText(Path.Combine(
            dir!.FullName, "Gg.Runner.Tests", "Fixtures", name));
    }

    /// <summary>One assistant turn calling the tool, and its paired result.</summary>
    private static string Called(
        string id, string workKind, string reason, bool? failed = null, bool paired = true)
    {
        var call =
            "{\"type\":\"assistant\",\"message\":{\"content\":[{\"type\":\"tool_use\","
          + "\"id\":\"" + id + "\",\"name\":\"" + NominationTool.Qualified + "\","
          + "\"input\":{\"work_kind\":\"" + workKind + "\",\"reason\":\"" + reason + "\"}}]}}";

        if (!paired)
        {
            return call;
        }

        var error = failed == true ? ",\"is_error\":true" : "";
        var result =
            "{\"type\":\"user\",\"message\":{\"content\":[{\"type\":\"tool_result\","
          + "\"tool_use_id\":\"" + id + "\"" + error
          + ",\"content\":[{\"type\":\"text\",\"text\":\"Recorded\"}]}]}}";

        return call + "\n" + result;
    }

    [Test]
    public async Task A_real_session_yields_the_kind_and_the_reason_the_agent_gave()
    {
        var nomination = TranscriptDigest.Nomination(Fixture("agent-nominated-a-kind.ndjson"));

        await Assert.That(nomination).IsNotNull()
            .Because("this is a genuine session against the real server, so a null here means "
                   + "the extractor and the client disagree about the shape of a tool call.");
        await Assert.That(nomination!.WorkKind).IsEqualTo("implement");
        await Assert.That(nomination.Reason).IsNotEmpty();
        await Assert.That(FlightNomination.Validate(nomination)).IsNull()
            .Because("what is extracted has to be shippable, or the flight produces a fact "
                   + "ingress refuses and the classification is lost at the door.");
    }

    [Test]
    public async Task The_nomination_is_not_the_first_tool_call_and_that_is_expected()
    {
        // MEASURED IN STEP 0. At CLI 2.1.260 the agent resolves a deferred tool
        // with a search call first, and in the real session below it also read
        // the work item through a tracker server. An extractor that assumed the
        // nomination was the first tool call would find nothing.
        //
        // ASSERTED OVER THE CALLS, not over where the name appears in the text:
        // the qualified name occurs first in the session's own list of granted
        // tools, hundreds of characters before anything is called, so a
        // position comparison answers a different question and answers it
        // wrongly. Found by this test failing on exactly that.
        var names = new List<string>();

        foreach (var line in Fixture("agent-nominated-a-kind.ndjson").Split('\n'))
        {
            if (line.Length == 0)
            {
                continue;
            }

            using var document = System.Text.Json.JsonDocument.Parse(line);
            if (!document.RootElement.TryGetProperty("message", out var message)
                || !message.TryGetProperty("content", out var content)
                || content.ValueKind != System.Text.Json.JsonValueKind.Array)
            {
                continue;
            }

            names.AddRange(content.EnumerateArray()
                .Where(b => b.ValueKind == System.Text.Json.JsonValueKind.Object
                         && b.TryGetProperty("type", out var type)
                         && type.GetString() == "tool_use")
                .Select(b => b.GetProperty("name").GetString() ?? ""));
        }

        await Assert.That(names).Contains(NominationTool.Qualified);
        await Assert.That(names[0]).IsNotEqualTo(NominationTool.Qualified)
            .Because("something else called a tool before this one did, which is the ordinary "
                   + $"case rather than the exception. The calls were: {string.Join(", ", names)}");
    }

    [Test]
    public async Task A_transcript_with_no_call_yields_nothing()
    {
        // RULE 8's HALF ON THIS SIDE. A classifier that declined called no
        // tool, so there is nothing to extract - and nothing is the answer,
        // never an empty nomination that ingress would refuse or, worse,
        // accept.
        await Assert.That(TranscriptDigest.Nomination(
            Fixture("agent-considered.ndjson"))).IsNull();
        await Assert.That(TranscriptDigest.Nomination("")).IsNull();
    }

    [Test]
    public async Task The_last_successful_call_wins()
    {
        // THE RULE THE SEED COMPOSER ALREADY FOLLOWS, and for its reason: an
        // agent that nominated twice changed its mind, and the newest answer is
        // the one. Taking the first would act on something it had withdrawn.
        var nomination = TranscriptDigest.Nomination(
            Called("a", "research", "no diagnosis yet") + "\n"
          + Called("b", "implement", "the item names the fix"));

        await Assert.That(nomination!.WorkKind).IsEqualTo("implement");
    }

    [Test]
    public async Task A_call_with_no_result_is_not_a_nomination()
    {
        // THE RUN WAS CUT OFF MID-CALL. The agent asked and nothing came back,
        // so nobody knows whether the tool recorded it - and a value taken from
        // a call that may never have completed is a value the runner invented
        // the completion of.
        await Assert.That(TranscriptDigest.Nomination(
            Called("a", "research", "no diagnosis yet", paired: false))).IsNull();
    }

    [Test]
    public async Task A_call_whose_result_is_an_error_is_not_a_nomination()
    {
        // THE TOOL REFUSED IT - a missing argument, most likely. The agent can
        // read that and try again; what must not happen is the runner shipping
        // a fact for a call the tool rejected.
        await Assert.That(TranscriptDigest.Nomination(
            Called("a", "research", "no diagnosis yet", failed: true))).IsNull();
    }

    [Test]
    public async Task An_earlier_success_survives_a_later_failure()
    {
        // THE PAIR OF THE RULE ABOVE, so "last one wins" does not become "last
        // one attempted wins": a refused second call leaves the first standing,
        // because the agent's last SUCCESSFUL answer is still what it said.
        var nomination = TranscriptDigest.Nomination(
            Called("a", "research", "no diagnosis yet") + "\n"
          + Called("b", "", "nothing named", failed: true));

        await Assert.That(nomination!.WorkKind).IsEqualTo("research");
    }

    [Test]
    public async Task A_call_missing_either_field_is_not_a_nomination()
    {
        // HALF A NOMINATION IS NOT ONE. The server refuses these, so this arm
        // is defence against a transcript that came from somewhere else - and
        // the extractor may not invent the missing half.
        var noKind =
            "{\"type\":\"assistant\",\"message\":{\"content\":[{\"type\":\"tool_use\","
          + "\"id\":\"a\",\"name\":\"" + NominationTool.Qualified + "\","
          + "\"input\":{\"reason\":\"only a reason\"}}]}}\n"
          + "{\"type\":\"user\",\"message\":{\"content\":[{\"type\":\"tool_result\","
          + "\"tool_use_id\":\"a\",\"content\":[]}]}}";

        await Assert.That(TranscriptDigest.Nomination(noKind)).IsNull();
    }

    [Test]
    public async Task Another_servers_tool_of_the_same_name_is_not_ours()
    {
        // THE COLLISION, from the other side. An operator reader keyed `gg` is
        // refused at configuration, and this is what that refusal is protecting:
        // the extractor matches the WHOLE qualified name, so a tool called
        // `nominate_work_kind` on somebody else's server is not a nomination
        // this runner served.
        var theirs =
            "{\"type\":\"assistant\",\"message\":{\"content\":[{\"type\":\"tool_use\","
          + "\"id\":\"a\",\"name\":\"mcp__tracker__" + NominationTool.Name + "\","
          + "\"input\":{\"work_kind\":\"implement\",\"reason\":\"theirs\"}}]}}\n"
          + "{\"type\":\"user\",\"message\":{\"content\":[{\"type\":\"tool_result\","
          + "\"tool_use_id\":\"a\",\"content\":[]}]}}";

        await Assert.That(TranscriptDigest.Nomination(theirs)).IsNull();
    }

    [Test]
    public async Task A_line_that_will_not_parse_does_not_lose_the_nomination()
    {
        // The digest's own rule beside it: a half-written last line is ordinary
        // while a file is still being appended to, and throwing would lose
        // every signal before it.
        var nomination = TranscriptDigest.Nomination(
            "{not json\n" + Called("a", "research", "no diagnosis yet"));

        await Assert.That(nomination!.WorkKind).IsEqualTo("research");
    }

    [Test]
    public async Task What_crosses_is_bounded()
    {
        // The reason is prose an agent wrote, and an agent asked for a reason
        // can write a document. Bounded here, on this machine, before it
        // crosses - the same place every other extracted value is bounded.
        var nomination = TranscriptDigest.Nomination(
            Called("a", "research", new string('x', FlightNomination.MaxReason * 2)));

        await Assert.That(nomination).IsNotNull();
        await Assert.That(FlightNomination.Validate(nomination!)).IsNull()
            .Because("an unbounded reason would be refused at ingress, which loses the "
                   + "classification for a reason that has nothing to do with the work.");
    }

    [Test]
    public async Task It_reaches_the_control_plane_as_a_fact_of_its_own_kind()
    {
        // THE LEG BETWEEN EXTRACTION AND INGRESS. A value the extractor found
        // and the pipeline dropped is a classification that happened and never
        // arrived - and the pipeline's own switch throws on an unhandled
        // payload, so this is what proves the arm exists rather than that
        // nothing reached it.
        var nomination = TranscriptDigest.Nomination(Fixture("agent-nominated-a-kind.ndjson"))!;

        var digested = Gg.Runner.Facts.FactPipeline.Digest(
            new Gg.Runner.Facts.CleanFacts([new Gg.Runner.Facts.FactPayload.Nomination(nomination)]),
            "flight-1",
            DateTimeOffset.UnixEpoch);

        var fact = digested.Items.Single();

        await Assert.That(fact.Kind).IsEqualTo(FactKinds.FlightNomination);
        await Assert.That(fact.Nomination).IsNotNull();
        await Assert.That(fact.Nomination!.WorkKind).IsEqualTo("implement");
        await Assert.That(FactEnvelope.Validate(fact)).IsNull()
            .Because("what the pipeline produces has to be a fact ingress accepts, or the "
                   + "classification is lost at the door for a reason that has nothing to do "
                   + "with the work.");
    }
}
