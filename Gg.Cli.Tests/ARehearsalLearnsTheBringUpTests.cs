using System.Text.Json;
using Gg.Cli;

namespace Gg.Cli.Tests;

/// <summary>
/// A rehearsal is asked what it learned about bringing a stack up, bringing it
/// down, and telling whether it is already running.
/// </summary>
/// <remarks>
/// <para>
/// <b>Slice fifty-six step 5, and the asking is the only half that can be
/// tested.</b> Whether the agent answers well is settled by the next flight that
/// follows its advice — step 7 — and advice that is wrong reads exactly like
/// advice that is right until something runs it.
/// </para>
/// <para>
/// <b>The tool's own description, because that is the channel proven to
/// arrive.</b> The tool list is in context before the agent's first token;
/// anything written elsewhere is an instruction the agent has to already be
/// looking for. <c>TheAgentIsToldHowEnvelopesWorkTests</c> settled that for the
/// neighbouring tool and the reasoning is unchanged.
/// </para>
/// <para>
/// <b>THE CHECK IS THE ONE THAT MATTERS</b> (rule 13). A later leg of an
/// itinerary inherits a stack that is already up, and advice that begins by
/// starting one collides with what is running. Nothing on the wire says whether
/// a stack is live — the daemon is the only thing that knows — so the advice has
/// to ask it, and this is where a rehearsal is told to work out how.
/// </para>
/// <para>
/// <b>Asserted on phrases that cannot pass vacuously.</b> The word
/// <c>hosts</c> already appears in this file inside <c>vcs-hosts</c>, which is
/// how the envelope explanation once passed while saying nothing — so these
/// look for wording that could only have been written on purpose.
/// </para>
/// </remarks>
public class ARehearsalLearnsTheBringUpTests
{
    private static async Task<string> LearningToolAsync()
    {
        var output = new StringWriter();

        await PlatformToolServer.RunAsync(
            new StringReader("""{"jsonrpc":"2.0","id":1,"method":"tools/list","params":{}}"""),
            // NO documentRoot AND NO intentPath: propose_document is offered on
            // the FLIGHT branch. Drafting reads the airspace and hands a document
            // back, which is not what a rehearsal does.
            output, intentPath: null, documentRoot: null, inForce: null);

        using var answer = JsonDocument.Parse(output.ToString().Trim().Split('\n')[0]);

        return answer.RootElement.GetProperty("result").GetProperty("tools")
            .EnumerateArray()
            .Single(tool => tool.GetProperty("name").GetString() == "propose_document")
            .GetProperty("description").GetString() ?? "";
    }

    [Test]
    public async Task A_rehearsal_is_asked_how_the_stack_comes_up()
    {
        var asking = await LearningToolAsync();

        await Assert.That(asking).Contains("DOCKER_HOST", StringComparison.Ordinal)
            .Because("an agent that does not know it was given a daemon cannot learn anything "
                   + "about it, and the address is the one thing the platform put in its "
                   + "environment. Description: " + asking);
        await Assert.That(asking).Contains("comes up", StringComparison.OrdinalIgnoreCase);
    }

    [Test]
    public async Task And_how_it_comes_down()
    {
        // BOTH HALVES, because the repository knows both and only the repository
        // does. A rehearsal asked for one would leave the other to be worked out
        // by every flight that follows.
        var asking = await LearningToolAsync();

        await Assert.That(asking).Contains("comes down", StringComparison.OrdinalIgnoreCase);
    }

    [Test]
    public async Task And_how_to_tell_whether_it_is_already_running()
    {
        // RULE 13, AND THE ONE A READER WOULD DROP. A later leg of a plan
        // inherits a stack that is already up; advice that begins by starting one
        // collides with what is running, and nothing on the wire will tell it -
        // the daemon is the only thing that knows.
        var asking = await LearningToolAsync();

        await Assert.That(asking).Contains("already running", StringComparison.OrdinalIgnoreCase)
            .Because("this is the sentence that makes advice written for a lone flight also "
                   + "serve a leg that inherited an instance. Description: " + asking);
    }

    [Test]
    public async Task The_asking_is_scoped_to_flights_that_were_given_one()
    {
        // EVERY FLIGHT IN THE FIELD HOSTS NOTHING, and a rehearsal told
        // unconditionally to describe a stack it never had would hand back
        // invented advice - which is worse than none, because it reads the same.
        var asking = await LearningToolAsync();

        await Assert.That(asking).Contains("if your flight was given", StringComparison.OrdinalIgnoreCase)
            .Because("the condition has to be in the sentence, or an agent with no instance "
                   + "answers anyway. Description: " + asking);
    }
}
