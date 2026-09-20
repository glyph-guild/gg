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

        // OVER A WINDOW, NOT A LINE. This first demanded Settings.Value on the
        // same line as the argument, which is a claim about where somebody
        // wrapped a call rather than about where the value came from - and it
        // failed on the fix. The property is that each argument resolves; how
        // it is formatted is nobody's business.
        var arguments = Regex.Matches(source, @"stunServers:", RegexOptions.None)
            .Select(m => source.Substring(m.Index, Math.Min(220, source.Length - m.Index)))
            .ToList();

        await Assert.That(arguments).IsNotEmpty();

        foreach (var argument in arguments)
        {
            await Assert.That(argument.Contains("Settings.Value(", StringComparison.Ordinal))
                .IsTrue()
                .Because("every stunServers argument in the root must be a resolved value, or "
                       + "the file reaches the check that most looks like a network problem. "
                       + "Passed: " + argument.Split('\n')[0].Trim());
        }
    }

    [Test]
    public async Task No_read_of_a_setting_is_left_to_answer_from_the_environment_alone()
    {
        // THE HOLE THE NEIGHBOURING GUARD LEFT, and the reason this defect came
        // back after being fixed once. That test demands `Settings.Value(`
        // appear near every `stunServers:`, which asks whether the call is
        // THERE and not whether it was given anything to read. All three of
        //
        //     Settings.Value(Gg.Runner.StunConfiguration.Variable)
        //
        // satisfied it while resolving from the environment only, because
        // `Settings.Resolve` reads `file is null ? null : Of(file, variable)` -
        // so the argument nobody passed is the whole difference between reading
        // the file and not.
        //
        // WHAT IT COST: `gg doctor` said "this machine has no STUN server
        // configured" about a machine with two in its file, and the remedy it
        // printed - `gg config set stun-servers ...` - writes that same file.
        // Following the advice could never clear the warning.
        //
        // AN OPTIONAL PARAMETER WHOSE OMISSION SILENTLY CHANGES THE ANSWER is
        // the shape, so this counts arguments rather than matching text, and it
        // reads every source file rather than the root alone.
        var offenders = new List<string>();

        foreach (var file in Sources())
        {
            var text = File.ReadAllText(file);

            foreach (var call in new[] { "Settings.Value(", "Settings.Resolve(" })
            {
                for (var at = text.IndexOf(call, StringComparison.Ordinal); at >= 0;
                     at = text.IndexOf(call, at + 1, StringComparison.Ordinal))
                {
                    // A MENTION IS NOT A CALL: this file names both in its own
                    // prose, and a scan that cannot tell them apart would fail
                    // on the sentence describing the fix.
                    if (Quoted(text, at))
                    {
                        continue;
                    }

                    if (Arguments(text, at + call.Length) < 2)
                    {
                        offenders.Add(
                            $"{Path.GetFileName(file)}: {text.Substring(at, 60).Split('\n')[0].Trim()}");
                    }
                }
            }
        }

        await Assert.That(offenders).IsEmpty()
            .Because("a setting read with no configuration answers from the environment and the "
                   + "built-ins, so anything in the file is invisible to it - and nothing says "
                   + "so at the call. Reading from the environment alone: "
                   + string.Join(" / ", offenders));
    }

    /// <summary>Every C# source file of the product, tests excluded.</summary>
    private static IEnumerable<string> Sources()
    {
        var here = new DirectoryInfo(AppContext.BaseDirectory);
        while (here is not null && !Directory.Exists(Path.Combine(here.FullName, "Gg.Cli")))
        {
            here = here.Parent;
        }

        return Directory
            .EnumerateFiles(here!.FullName, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}",
                            StringComparison.Ordinal)
                     && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
                            StringComparison.Ordinal)
                     && !f.Contains(".Tests", StringComparison.Ordinal));
    }

    /// <summary>Whether the text at <paramref name="at"/> sits inside a string.</summary>
    private static bool Quoted(string text, int at)
    {
        var line = text.LastIndexOf('\n', at) + 1;

        return text.AsSpan(line, at - line).Count('"') % 2 == 1;
    }

    /// <summary>
    /// How many arguments the call whose parenthesis just opened is given,
    /// counting commas at the call's own depth so a nested call counts as one.
    /// </summary>
    private static int Arguments(string text, int after)
    {
        var depth = 1;
        var arguments = 1;

        for (var i = after; i < text.Length && depth > 0; i++)
        {
            switch (text[i])
            {
                case '(' or '[':
                    depth++;
                    break;
                case ')' or ']':
                    depth--;
                    if (depth == 0 && text.AsSpan(after, i - after).Trim().IsEmpty)
                    {
                        return 0;
                    }

                    break;
                case ',' when depth == 1:
                    arguments++;
                    break;
                default:
                    break;
            }
        }

        return arguments;
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
