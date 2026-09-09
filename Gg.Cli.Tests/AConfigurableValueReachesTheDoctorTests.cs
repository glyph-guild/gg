using System.Text.RegularExpressions;
using Gg.Local;

namespace Gg.Cli.Tests;

/// <summary>
/// A value in the configuration file reaches the doctor, including the one that
/// arrived through a helper rather than through a direct read.
/// </summary>
/// <remarks>
/// <para>
/// <b>The defect this is about was made by two slices meeting.</b> The
/// configuration slice moved six variables behind one reader and asserted the
/// root reads none of them straight from the environment. The watching slice
/// then gave the doctor a STUN check that reads <c>GG_STUN_SERVERS</c> - which
/// is one of those six, and is offerable - through
/// <c>StunConfiguration.FromEnvironment()</c>. So a person who puts
/// <c>stun-servers</c> in the file gets a doctor that cannot see it and says
/// nothing outside could reach this machine.
/// </para>
/// <para>
/// <b>And the existing guard could not catch it, which is the more useful
/// finding.</b> <c>TheRootReadsTheFileAndHandsItOnTests</c> scans the root for
/// <c>Environment.GetEnvironmentVariable("...")</c> and checks the name against
/// <c>Configuration.Members</c>. A call to a <c>FromEnvironment()</c> helper is
/// the same bypass one function deeper, and the regex cannot see it. So the
/// guard is widened here rather than the one defect fixed: the next helper will
/// be added by somebody who has not read this file.
/// </para>
/// </remarks>
public class AConfigurableValueReachesTheDoctorTests
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

    [Test]
    public async Task The_root_reads_no_configurable_variable_through_a_helper_either()
    {
        // ARGUMENTLESS IS THE TELL. Every one of these helpers takes an
        // optional `declared` so a caller can hand it a resolved value; called
        // with nothing, it falls back to the environment and the file reaches
        // it not at all. That fallback exists for a runner in its own process
        // and must not fire in the root, which HAS the file.
        var bypassing = Regex.Matches(ProgramText(), @"(\w+)\.FromEnvironment\(\s*\)")
            .Select(m => m.Groups[1].Value)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        await Assert.That(bypassing).IsEmpty()
            .Because("the root resolves through Settings and hands the value on, or anything "
                   + "in the file is invisible to what it builds. Bypassing: "
                   + string.Join(", ", bypassing));
    }

    [Test]
    public async Task The_stun_servers_the_doctor_asks_with_come_from_the_reader()
    {
        var source = ProgramText();

        await Assert.That(source).Contains("stunServers:", StringComparison.Ordinal)
            .Because("the doctor's STUN check is not built here at all, so this scan judges "
                   + "nothing.");

        // NAMED WHERE IT IS PASSED, so the resolution and the use are one line
        // apart and a reader can see that the file was consulted.
        await Assert.That(Regex.IsMatch(
            source, @"stunServers:\s*[^)\n]*Settings\.Value\("))
            .IsTrue()
            .Because("every stunServers argument in the root must be a resolved value. Found: "
                   + string.Join(" | ", Regex.Matches(source, @"stunServers:[^\n]*")
                       .Select(m => m.Value.Trim())));
    }

    [Test]
    public async Task A_value_in_the_file_is_what_the_stun_check_would_use()
    {
        // AGAINST A STATED ENVIRONMENT, the neighbouring guard's own lesson:
        // whether GG_STUN_SERVERS happens to be exported in the shell running
        // this suite must not decide whether the file gets to answer.
        var resolved = Settings.Value(
            Gg.Runner.StunConfiguration.Variable,
            new Configuration { StunServers = "stun:relay.example:3478" },
            environment: _ => null);

        await Assert.That(Gg.Runner.StunConfiguration.FromEnvironment(resolved))
            .Contains("stun:relay.example:3478")
            .Because("this is the whole thread: a file value, through the one reader, into the "
                   + "thing that asks. If it breaks anywhere along it, the doctor reports that "
                   + "nothing outside could reach this machine and the reason is a file it "
                   + "never opened.");
    }
}
