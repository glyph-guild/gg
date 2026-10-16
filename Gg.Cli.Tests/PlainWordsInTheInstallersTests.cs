using System.Text.RegularExpressions;

namespace Gg.Cli.Tests;

/// <summary>
/// Every line the two installers print: their step lines, their refusals and
/// what they say when they finish.
/// </summary>
/// <remarks>
/// <para>
/// <b>Read from the scripts, by the helper each line goes through.</b>
/// install.sh prints through <c>refuse</c>, <c>step</c> and <c>printf
/// 'install.sh: …'</c>; install.ps1 through <c>throw</c> and <c>Write-Host
/// "install.ps1: …"</c>. The hint lines that follow the closing sentence are
/// command, then comment - fragments - and are not read here.
/// </para>
/// <para>
/// <b>A sentence may end in a command to copy</b>, after "run:" or after a
/// colon that ends the line, because a full stop after it would be typed too.
/// </para>
/// </remarks>
public class PlainWordsInTheInstallersTests
{
    private static readonly Regex RuledOut = new(
        @"\b(you|your|yours|yourself|somebody|nobody|tenant|principal|nonce|mint|redeem)\b"
      + @"|\b\w+n't\b|\be\.g\.",
        RegexOptions.IgnoreCase);

    private static readonly Regex EndsInACommand = new(@"run: |:\\n  ");

    private static string Script(string name)
    {
        var here = new DirectoryInfo(AppContext.BaseDirectory);
        while (here is not null && !File.Exists(Path.Combine(here.FullName, "Gg.sln")))
        {
            here = here.Parent;
        }

        return File.ReadAllText(Path.Combine(
            here?.FullName ?? throw new InvalidOperationException("Gg.sln not found"),
            "deploy", name));
    }

    /// <summary>The messages install.sh prints, without the script's own prefix.</summary>
    internal static IReadOnlyList<string> ShellMessages()
    {
        var script = Script("install.sh");
        var messages = new List<string>();

        foreach (Match m in Regex.Matches(script, @"\b(?:refuse|step) ""((?:[^""\\]|\\.)*)"""))
        {
            messages.Add(m.Groups[1].Value);
        }

        foreach (Match m in Regex.Matches(script, @"printf '(install\.sh: (?:[^'\\]|'\\''|\\.)*)'"))
        {
            if (m.Groups[1].Value == "install.sh: %s\\n")
            {
                continue; // refuse's and step's own printf: the messages are read above.
            }

            messages.Add(m.Groups[1].Value["install.sh: ".Length..]);
        }

        return messages;
    }

    /// <summary>The messages install.ps1 prints or throws, without its prefix.</summary>
    internal static IReadOnlyList<string> PowerShellMessages()
    {
        var script = Script("install.ps1");
        var messages = new List<string>();

        foreach (Match m in Regex.Matches(script, @"\bthrow ""([^""]*)"""))
        {
            messages.Add(m.Groups[1].Value);
        }

        foreach (Match m in Regex.Matches(script, @"Write-Host (?:""install\.ps1: ([^""]*)""|'install\.ps1: ([^']*)')"))
        {
            messages.Add(m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value);
        }

        return messages;
    }

    public static IEnumerable<Func<(string Script, string Message)>> Messages()
    {
        foreach (var message in ShellMessages())
        {
            yield return () => ("install.sh", message);
        }

        foreach (var message in PowerShellMessages())
        {
            yield return () => ("install.ps1", message);
        }
    }

    [Test]
    public async Task The_scan_finds_the_installers_messages()
    {
        await Assert.That(ShellMessages().Count).IsGreaterThan(25);
        await Assert.That(PowerShellMessages().Count).IsGreaterThan(12);
    }

    [Test]
    [MethodDataSource(nameof(Messages))]
    public async Task Each_message_is_a_sentence_in_plain_words(string script, string message)
    {
        await Assert.That(RuledOut.IsMatch(message)).IsFalse()
            .Because($"{script} says '{message}', and the voice rules out "
                   + $"'{RuledOut.Match(message).Value}'.");

        await Assert.That(char.IsLower(message[0])).IsFalse()
            .Because($"{script} says '{message}', which is a sentence and starts with a capital "
                   + "letter (a value such as --keep, a path or a quoted name may start it).");

        var end = message.EndsWith("\\n", StringComparison.Ordinal) ? message[..^2] : message;
        await Assert.That(end.EndsWith('.') || EndsInACommand.IsMatch(message)).IsTrue()
            .Because($"{script} says '{message}', which is a sentence and ends with a full stop.");
    }

    [Test]
    public async Task A_refusal_says_what_was_left_and_what_to_do()
    {
        var shell = Script("install.sh");
        var powershell = Script("install.ps1");

        await Assert.That(shell).Contains("This machine is $rid, and gg is released only for linux-x64, linux-arm64, osx-arm64 and osx-x64. Nothing was installed.");
        await Assert.That(shell).Contains("Remove or rename that program, then run this again.");
        await Assert.That(shell).Contains("report it at https://github.com/$repo/issues.");
        await Assert.That(powershell).Contains("Report it at https://github.com/$repo/issues.");
        await Assert.That(powershell).Contains("This machine is not a runner. Nothing was started and no service was made.");
    }

    [Test]
    public async Task The_script_still_runs_only_from_its_last_line()
    {
        var lines = Script("install.sh").TrimEnd().Split('\n');

        await Assert.That(lines[^1]).IsEqualTo("main \"$@\"");
    }
}
