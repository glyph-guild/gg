namespace Gg.Cli.Tests;

/// <summary>
/// The two long-lived runners - the resident and the pool maintainer - state
/// their machine every time they start.
/// </summary>
/// <remarks>
/// <para>
/// <b>They are the ones that never register again.</b> Both reuse a stored
/// credential, so a machine added to registration never reaches them, and
/// they are exactly the two a person wants a host's members grouped under.
/// </para>
/// <para>
/// <b>Not members.</b> A member's machine is written by the mint from its
/// maintainer's row; a member stating its own would be a runner declaring a
/// fact this control plane deliberately takes from somewhere it trusts more.
/// </para>
/// </remarks>
public class LongLivedRunnersOfferTheirMachineTests
{
    private static string ProgramText()
    {
        var here = new DirectoryInfo(AppContext.BaseDirectory);
        while (here is not null && !Directory.Exists(Path.Combine(here.FullName, "Gg.Cli")))
        {
            here = here.Parent;
        }

        return File.ReadAllText(Path.Combine(here!.FullName, "Gg.Cli", "Program.cs"));
    }

    private static string Body(string text, string signature)
    {
        var start = text.IndexOf(signature, StringComparison.Ordinal);
        if (start < 0)
        {
            return "";
        }

        var next = text.IndexOf("\nstatic ", start + signature.Length, StringComparison.Ordinal);
        return next < 0 ? text[start..] : text[start..next];
    }

    [Test]
    public async Task The_resident_offers_its_machine()
    {
        var body = Body(ProgramText(), "static async Task<int> RunnerUpAsync(");

        await Assert.That(body.Length).IsGreaterThan(0)
            .Because("the scan has to find `gg runner up` before it can say anything about it.");
        await Assert.That(body).Contains("OfferMachineAsync(")
            .Because("the resident reuses its stored credential, so this is the only way its "
                   + "machine is ever recorded.");
    }

    [Test]
    public async Task The_maintainer_offers_its_machine()
    {
        var body = Body(ProgramText(), "static async Task<int> RunnerMaintainAsync(");

        await Assert.That(body.Length).IsGreaterThan(0);
        await Assert.That(body).Contains("OfferMachineAsync(")
            .Because("the maintainer is what a host's members were wrongly grouped under, and "
                   + "it too never registers again.");
    }
}
