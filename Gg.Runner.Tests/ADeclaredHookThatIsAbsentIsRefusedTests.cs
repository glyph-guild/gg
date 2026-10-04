using Gg.Contracts;
using Gg.Runner.Environments;

namespace Gg.Runner.Tests;

/// <summary>
/// A declared hook that is not there is refused, not read as having none.
/// </summary>
/// <remarks>
/// <para>
/// <b>S58.5-01, and the defect is a silence.</b> A declared path that does not
/// resolve to a file returns null from the invoker, and null is read as
/// <i>"this environment has no hooks"</i> — which is every environment in the
/// field, so it cannot be an error there. The consequence is that an
/// environment declaring <c>hooks:</c> whose repository lacks the file proceeds
/// with <b>no stack</b>, and the agent finds an empty environment.
/// </para>
/// <para>
/// <b>ONE executable, not five files, which is what the first draft of this
/// test got wrong.</b> <c>hooks:</c> is a path to a single program invoked with
/// the point as its argument — <c>stack:</c>'s own shape, kept for its own
/// reason: <i>"two members would be two things to keep in sync."</i> So there is
/// nothing to check per point; there is one file, and either it is there or it
/// is not.
/// </para>
/// <para>
/// <b>The runner attests it, because nothing else can.</b> Not before the GRANT
/// — that is taken at claim, and the path is in the tree the flight checks out,
/// which does not exist yet. Not by the control plane, which reads no customer
/// bytes at any point. So the check is here, after checkout and before any
/// point runs.
/// </para>
/// </remarks>
public class ADeclaredHookThatIsAbsentIsRefusedTests
{
    private static string ATree(string? withFile = null)
    {
        var tree = Directory.CreateDirectory(Path.Combine(
            Path.GetTempPath(), "gg-hooks-" + Guid.NewGuid().ToString("n"))).FullName;

        if (withFile is { Length: > 0 })
        {
            var full = Path.Combine(tree, withFile);
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllText(full, "#!/bin/sh\nexit 0\n");
        }

        return tree;
    }

    [Test]
    public async Task A_declared_hook_that_is_absent_is_refused_naming_the_path()
    {
        var tree = ATree();

        try
        {
            var refusal = StackScript.Missing(tree, ".goodgrief/environments/ui/hooks.sh");

            await Assert.That(refusal).IsNotNull()
                .Because("an absent file reads as an environment with no hooks today, so the "
                       + "flight proceeds with no stack and the agent finds an empty "
                       + "environment - in silence, which is the whole defect.");

            await Assert.That(refusal!).Contains(".goodgrief/environments/ui/hooks.sh")
                .Because("named as the author wrote it, because that is the string they have "
                       + "to correct - not the absolute path it resolved to under /srv/env, "
                       + "which describes a pool host's layout they never see.");
        }
        finally
        {
            Directory.Delete(tree, recursive: true);
        }
    }

    [Test]
    public async Task A_declared_hook_that_is_there_is_taken()
    {
        var tree = ATree(".goodgrief/environments/ui/hooks.sh");

        try
        {
            await Assert.That(StackScript.Missing(tree, ".goodgrief/environments/ui/hooks.sh"))
                .IsNull()
                .Because("one executable is the whole of what this declaration implies - the "
                       + "point is its argument, so there is nothing to check per point.");
        }
        finally
        {
            Directory.Delete(tree, recursive: true);
        }
    }

    [Test]
    public async Task An_environment_with_no_hooks_is_asked_nothing()
    {
        // EVERY ENVIRONMENT IN THE FIELD. The absence of a DECLARATION is not
        // the absence of a FILE, and conflating them would fail every flight
        // that flies today.
        var tree = ATree();

        try
        {
            foreach (var none in (string?[]) [null, "", "   "])
            {
                await Assert.That(StackScript.Missing(tree, none)).IsNull()
                    .Because("an environment that declared no hooks has none to be missing.");
            }
        }
        finally
        {
            Directory.Delete(tree, recursive: true);
        }
    }

    [Test]
    public async Task A_path_that_climbs_out_of_the_tree_is_refused_as_that_rather_than_as_absent()
    {
        // S58.5-02, REPURPOSED. `Within` already refuses a path that escapes
        // the checkout - "an absolute path is a file on the pool host rather
        // than in the repository; a path that climbs out reaches the same place
        // by another route". But it refuses by returning NULL, which is the same
        // silence as a missing file: the flight runs no stack and says nothing.
        //
        // The two have to be told apart, because they are different mistakes. A
        // missing file is one somebody has not written yet. A path that climbs
        // out is one aimed at the host, and reporting it as "not found" would
        // send its author looking for a file that is exactly where they put it.
        var tree = ATree();

        try
        {
            foreach (var outside in (string[]) ["../hooks.sh", "/etc/hooks.sh", "a/../../x.sh"])
            {
                var refusal = StackScript.Missing(tree, outside);

                await Assert.That(refusal).IsNotNull()
                    .Because($"'{outside}' leaves the checkout, and today that is the same "
                           + "null as a file nobody wrote.");

                await Assert.That(refusal!).Contains("outside")
                    .Because("and it says which mistake this is, because somebody told 'not "
                           + "found' would go looking for a file that is where they put it.");
            }
        }
        finally
        {
            Directory.Delete(tree, recursive: true);
        }
    }
}
