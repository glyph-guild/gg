using System.Reflection;
using Gg.Client;
using Gg.Console;

namespace Gg.Console.Tests;

/// <summary>
/// A door's refusal reaches the person as a sentence, not as a stack trace
/// (found by slice forty-six's walk).
/// </summary>
/// <remarks>
/// <para>
/// <b>Pressing claim on a tenant-owned machine ended the console.</b>
/// `RunnerOwnershipRefusedException` carries exactly the sentence the pane
/// should have drawn - "an admin has said nobody claims it" - and it was not
/// in the catch, so the whole session died on the one answer the door gives
/// most often.
/// </para>
/// <para>
/// <b>The tests that were supposed to cover this could not.</b> The keymap's
/// own tests use a double whose refusal RETURNS a sentence, which is what a
/// caught exception looks like from outside - so every one of them passed
/// while the real path threw. This asserts over the catch itself, which is the
/// only part a double cannot stand in for.
/// </para>
/// </remarks>
public class ARefusalDoesNotEndTheConsoleTests
{
    private static IEnumerable<Type> Caught()
    {
        // The filter is a `when` clause, so it is not reachable by reflection:
        // this reads the source of the one method every door goes through.
        var source = Sources.Read("Gg.Console", "VerbConsoleActions.cs");
        var body = source[source.IndexOf("private static string Answered(", StringComparison.Ordinal)..];
        body = body[..body.IndexOf("\n    }", StringComparison.Ordinal)];

        return typeof(RunnerOwnershipRefusedException).Assembly
            .GetTypes()
            .Where(t => typeof(Exception).IsAssignableFrom(t))
            .Where(t => body.Contains(t.Name, StringComparison.Ordinal));
    }

    [Test]
    public async Task Every_refusal_these_doors_throw_is_caught_and_said()
    {
        var caught = Caught().Select(t => t.Name).ToList();

        foreach (var refusal in (string[])
                 [
                     nameof(RunnerOwnershipRefusedException),
                     nameof(RunnerNotFoundException),
                     nameof(EnrollmentRefusedException),
                 ])
        {
            await Assert.That(caught).Contains(refusal)
                .Because($"{refusal} is what a door throws when it says no, and a console "
                       + "that did not catch it would end the session on the answer a person "
                       + "is most likely to get.");
        }
    }

    [Test]
    public async Task The_sentence_a_person_reads_is_the_doors_own()
    {
        // What the catch does with it, held here so a later edit cannot turn
        // the message into a console's own paraphrase.
        var refusal = new RunnerOwnershipRefusedException(
            "This runner is the tenant's: an admin has said nobody claims it.");

        await Assert.That(refusal.Message).Contains("an admin has said", StringComparison.Ordinal);

        var source = Sources.Read("Gg.Console", "VerbConsoleActions.cs");
        var body = source[source.IndexOf("private static string Answered(", StringComparison.Ordinal)..];

        await Assert.That(body[..body.IndexOf("\n    }", StringComparison.Ordinal)])
            .Contains("return refusal.Message;", StringComparison.Ordinal)
            .Because("the doors already answer in words a person can act on, and a console "
                   + "that composed its own would be a second vocabulary for one set of rules.");
    }
}
