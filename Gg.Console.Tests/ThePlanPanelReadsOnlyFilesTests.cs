namespace Gg.Console.Tests;

/// <summary>
/// <b>S66.2-03</b> - the panel's <c>p</c> view draws the draft and the last result's verdicts from
/// the two files, and the session's source reaches no control-plane client, credential store or
/// process start beyond the child.
/// </summary>
/// <remarks>
/// A hosted session may not start anything, resolve a credential, or block (CLAUDE.md). The check
/// runs in the tool server's process; the panel reads what it left (slice sixty-six rule 3).
/// </remarks>
public class ThePlanPanelReadsOnlyFilesTests
{
    [Test]
    public async Task The_plan_view_shows_the_legs_and_the_last_verdicts()
    {
        using var session = new PlanSessionFixture();
        session.ThreeLegs(result: "the icon - implement\n     refused: Destination 'the-plan' does not open it.");

        session.Run(PlanSessionFixture.Prefix, PlanSessionFixture.Key('p'));

        var frame = session.Frames[^1];
        await Assert.That(frame).Contains("the icon");
        await Assert.That(frame).Contains("the padding");
        await Assert.That(frame).Contains("does not open it")
            .Because("the verdict the agent was last shown, read from the file the server left.");
    }

    [Test]
    public async Task The_session_source_reaches_nothing_but_files()
    {
        var source = await File.ReadAllTextAsync(Path.Combine(Root(), "Gg.Console", "PtyPlanSession.cs"));

        foreach (var forbidden in (string[])["ControlPlaneClient", "HttpClient", "Process.Start", "SessionStore", "CredentialStore", "IPlanningReads"])
        {
            await Assert.That(source).DoesNotContain(forbidden);
        }
    }

    internal static string Root()
    {
        var at = new DirectoryInfo(AppContext.BaseDirectory);
        while (at is not null && !File.Exists(Path.Combine(at.FullName, "Gg.sln")))
        {
            at = at.Parent;
        }

        return at!.FullName;
    }
}
