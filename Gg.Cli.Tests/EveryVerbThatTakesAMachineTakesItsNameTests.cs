using System.Reflection;
using Gg.Cli;

namespace Gg.Cli.Tests;

/// <summary>
/// A verb that acts on a machine takes the machine's name, not only its id.
/// </summary>
/// <remarks>
/// <para>
/// <b>The ratchet, because the cost of missing one is not a build error.</b>
/// A verb added later with a <c>RunnerId</c> and no interface compiles, runs,
/// and refuses a name that every other verb accepts - and somebody who has
/// learned that names work meets the one place they do not, on the verb they
/// happened to need.
/// </para>
/// <para>
/// <b><c>Fly</c> is exempt and named here rather than excluded quietly.</b> Its
/// machine is optional and the member is called <c>Runner</c>, so it cannot
/// carry the interface; <c>Program.ByName</c> handles it by name, and this test
/// asserts that it is still the only exception.
/// </para>
/// </remarks>
public class EveryVerbThatTakesAMachineTakesItsNameTests
{
    /// <summary>Verbs whose machine is handled outside the interface.</summary>
    private static readonly string[] Exempt = ["Fly"];

    private static IEnumerable<Type> Verbs() =>
        typeof(CliAction).GetNestedTypes(BindingFlags.Public)
            .Where(type => type.IsAssignableTo(typeof(CliAction)));

    [Test]
    public async Task Every_action_holding_a_runner_id_can_be_given_one_by_name()
    {
        var missing = Verbs()
            .Where(verb => verb.GetProperty("RunnerId")?.PropertyType == typeof(string))
            .Where(verb => !verb.IsAssignableTo(typeof(CliAction.INameAMachine)))
            .Select(verb => verb.Name)
            .Order(StringComparer.Ordinal)
            .ToList();

        await Assert.That(missing).IsEmpty()
            .Because("a verb with a RunnerId and no INameAMachine compiles and then refuses a "
                   + "name every other verb takes. Found: " + string.Join(", ", missing));
    }

    [Test]
    public async Task And_the_sweep_is_reading_real_verbs()
    {
        // GUARDS THE EMPTINESS ABOVE. An empty set satisfies it, and a
        // reflection query that quietly matched nothing is exactly how a
        // ratchet stops ratcheting.
        var carrying = Verbs()
            .Count(verb => verb.IsAssignableTo(typeof(CliAction.INameAMachine)));

        await Assert.That(carrying).IsGreaterThanOrEqualTo(9)
            .Because($"claim, unclaim, reserve, release, retire, repin, ownership, watch, "
                   + $"credential send and agent login all take one. Saw {carrying}.");
    }

    [Test]
    public async Task And_flying_is_still_the_only_verb_handled_apart()
    {
        // NAMED, NOT SKIPPED. Fly's machine is optional and its member is
        // called Runner - so it is resolved by name in Program.ByName instead,
        // and a second exception appearing there without a word here is what
        // this notices.
        var program = File.ReadAllText(Path.Combine(Root(), "Gg.Cli", "Program.cs"));

        await Assert.That(program).Contains("CliAction.Fly { Runner:")
            .Because("the one verb that cannot carry the interface is resolved explicitly, "
                   + "and this is where that is written down.");

        await Assert.That(Exempt).IsEquivalentTo((string[])["Fly"]);
    }

    private static string Root()
    {
        var here = new DirectoryInfo(AppContext.BaseDirectory);
        while (here is not null && !File.Exists(Path.Combine(here.FullName, "Gg.sln")))
        {
            here = here.Parent;
        }

        return here?.FullName ?? throw new InvalidOperationException("Gg.sln not found");
    }
}
