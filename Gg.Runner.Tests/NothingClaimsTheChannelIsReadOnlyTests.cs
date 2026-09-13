namespace Gg.Runner.Tests;

/// <summary>
/// No remark on the runner's side of the channel still calls it read-only.
/// </summary>
/// <remarks>
/// <para>
/// <b><i>Prose asserts what the code lacks</i>, and this is the inverse: prose
/// asserting a restraint the code no longer has.</b> The channel carried two
/// read-only verbs, that was written down in the places where the security
/// argument is actually made, and then a third verb arrived that writes. Every
/// one of those sentences is now a reviewer being told the wrong thing in the
/// one file they would go to for the right thing.
/// </para>
/// <para>
/// <b>A phrase match, and narrow on purpose.</b> Its job is to fail once, force
/// the four edits, and then stay quiet - so a reworded remark passes, which is
/// correct, because what it guards against is the specific claim rather than a
/// specific spelling. It reads source because the claim only exists in source.
/// </para>
/// <para>
/// <b>Comment lines are not stripped here, unlike the guards that scan for
/// code.</b> Those had to strip them because a comment explaining a removed
/// shape contains that shape. This one's subject IS the comment, so stripping
/// would leave it asserting over nothing.
/// </para>
/// </remarks>
public class NothingClaimsTheChannelIsReadOnlyTests
{
    /// <summary>Where the channel's security argument is made.</summary>
    /// <remarks>
    /// Named rather than scanned across the project, because a list is a claim
    /// about where the argument lives and a walk is a claim about where it does
    /// not - and the second is the one that quietly returns no offenders.
    /// </remarks>
    private static readonly string[] Arguing =
        ["AttendedSession.cs", "RunnerLoop.cs", "AskDispatch.cs"];

    [Test]
    public async Task Nothing_still_calls_it_read_only()
    {
        foreach (var file in Arguing)
        {
            var text = File.ReadAllText(Path.Combine(RunnerRoot(), file));

            foreach (var claim in (string[])
                ["read-only verbs", "two read-only", "read-only constraint"])
            {
                await Assert.That(text.Contains(claim, StringComparison.OrdinalIgnoreCase))
                    .IsFalse()
                    .Because($"{file} says '{claim}', and the channel now carries a verb that "
                           + "writes a credential to this machine's disk. A reviewer reading "
                           + "for the restraint finds a sentence that was true when it was "
                           + "written and is not now.");
            }
        }
    }

    [Test]
    public async Task The_files_it_reads_are_the_files_it_thinks_they_are()
    {
        // THE POISON TWIN. A path that stopped resolving would make every
        // assertion above pass by reading an empty string, which is the shape
        // this repository writes ratchets to avoid.
        foreach (var file in Arguing)
        {
            var text = File.ReadAllText(Path.Combine(RunnerRoot(), file));

            await Assert.That(text).IsNotEmpty();
            await Assert.That(text).Contains("namespace Gg.Runner", StringComparison.Ordinal)
                .Because($"{file} was found and is what it claims to be, so the silence "
                       + "above is silence about the claim rather than about everything.");
        }
    }

    private static string RunnerRoot()
    {
        var here = new DirectoryInfo(AppContext.BaseDirectory);

        while (here is not null
            && !File.Exists(Path.Combine(here.FullName, "Gg.Runner", "AskDispatch.cs")))
        {
            here = here.Parent;
        }

        if (here is null)
        {
            throw new InvalidOperationException(
                "Gg.Runner is not above this test's output directory any more, so this walk "
              + "would assert over nothing.");
        }

        return Path.Combine(here.FullName, "Gg.Runner");
    }
}
