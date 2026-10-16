using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace Gg.Runner.Tests;

/// <summary>
/// The words a person reads from a runner: what <c>gg runner up</c> prints,
/// the diagnoses a flight's story and <c>gg why</c> carry, and how a
/// runner's credential ended.
/// </summary>
/// <remarks>
/// <para>
/// <b>Rendered where a test can reach the sentence</b> - how a credential
/// ended is a pure function, so its three sentences are produced and read.
/// </para>
/// <para>
/// <b>Scanned from source for the rest.</b> Most of the runner's sentences are
/// built deep inside a loop or written straight to the console, so every
/// string literal under <c>Gg.Runner</c> is read, with interpolation holes
/// left out (a hole is code, not prose).
/// </para>
/// <para>
/// <b>The agent is addressed as "you", and that is correct.</b> A scan cannot
/// tell a prompt from a person's message, so the files that build what is
/// sent to the agent are named in <see cref="AddressesTheAgent"/>, each with
/// its reason, and only "you" and "your" are allowed there. Every other word
/// on the list is ruled out in those files too.
/// </para>
/// </remarks>
public class PlainWordsInTheRunnerTests
{
    private static readonly Regex RuledOut = new(
        @"\b(you|your|yours|yourself|somebody|somebody's|nobody|nobody's|tenant|tenants|tenant's"
      + @"|principal|principals|nonce|nonces|mint|mints|minted|redeem|redeems|redeemed)\b"
      + @"|\b\w+n't\b|\b(it|that|there|what|who)'s\b|\b\w+'(re|ve|ll)\b|\bI'm\b",
        RegexOptions.IgnoreCase);

    private static readonly Regex AddressesTheReader = new(
        @"\b(you|your|yours|yourself)\b", RegexOptions.IgnoreCase);

    /// <summary>
    /// Files whose string literals are the agent's prompt or task, where "you"
    /// is the agent and is the right word.
    /// </summary>
    private static readonly Dictionary<string, string> AddressesTheAgent = new()
    {
        ["Execution/ClaudeCodeExecutor.cs"] =
            "builds the agent's prompt and appended system prompt: the opening, the landing "
          + "instructions, fenced notes and a person's feedback, all addressed to the agent",
        ["Sweeps/SweepLauncher.cs"] =
            "builds the sweep agent's task and frames the watch's instructions for it",
    };

    private static string RunnerRoot()
    {
        var here = new DirectoryInfo(AppContext.BaseDirectory);
        while (here is not null && !File.Exists(Path.Combine(here.FullName, "Gg.sln")))
        {
            here = here.Parent;
        }

        return Path.Combine(
            here?.FullName ?? throw new InvalidOperationException("Gg.sln not found"),
            "Gg.Runner");
    }

    private static HttpRequestException Unauthorized() =>
        new("Response status code does not indicate success: 401 (Unauthorized).",
            inner: null, HttpStatusCode.Unauthorized);

    public static IEnumerable<Func<CredentialEnding>> Endings()
    {
        var now = new DateTimeOffset(2026, 10, 16, 12, 0, 0, TimeSpan.Zero);

        yield return () => CredentialEnding.For(now.AddMinutes(-1), now, Unauthorized());
        yield return () => CredentialEnding.For(now.AddDays(20), now, Unauthorized());
        yield return () => CredentialEnding.For(null, now, Unauthorized());
    }

    [Test]
    [MethodDataSource(nameof(Endings))]
    public async Task How_a_credential_ended_reads_as_sentences(CredentialEnding ending)
    {
        await Assert.That(ending.Said).DoesNotMatch(RuledOut);
        await Assert.That(char.IsUpper(ending.Said[0])).IsTrue()
            .Because($"'{ending.Said}' is a sentence, so it starts with a capital letter.");
        await Assert.That(ending.Said).EndsWith(".");
        await Assert.That(ending.Said).DoesNotContain("401")
            .Because("a status code and exception text are internals, not what happened.");
    }

    [Test]
    public async Task A_credential_taken_away_says_what_to_check()
    {
        var now = new DateTimeOffset(2026, 10, 16, 12, 0, 0, TimeSpan.Zero);

        await Assert.That(CredentialEnding.For(now.AddDays(20), now, Unauthorized()).Said)
            .Contains("Run gg runners to check this machine's status.");
        await Assert.That(CredentialEnding.For(null, now, Unauthorized()).Said)
            .Contains("Run gg runners to check this machine's status.");
    }

    [Test]
    public async Task No_runner_sentence_uses_words_the_voice_rules_out()
    {
        var root = RunnerRoot();
        var offending = new List<string>();

        foreach (var file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(root, file).Replace('\\', '/');
            if (relative.StartsWith("bin/", StringComparison.Ordinal)
                || relative.StartsWith("obj/", StringComparison.Ordinal))
            {
                continue;
            }

            var agentFacing = AddressesTheAgent.ContainsKey(relative);

            foreach (var (line, text) in Literals(File.ReadAllText(file)))
            {
                foreach (Match match in RuledOut.Matches(text))
                {
                    if (agentFacing && AddressesTheReader.IsMatch(match.Value))
                    {
                        continue;
                    }

                    offending.Add($"{relative}:{line}: '{match.Value}' in \"{text}\"");
                }
            }
        }

        await Assert.That(offending).IsEmpty()
            .Because("a person reading a runner's output is not addressed as you, and reads "
                   + "someone, no one, person and organisation rather than the words the "
                   + "platform's builders use.");
    }

    [Test]
    public async Task Every_agent_facing_exemption_names_a_file_that_exists()
    {
        var root = RunnerRoot();

        foreach (var file in AddressesTheAgent.Keys)
        {
            await Assert.That(File.Exists(Path.Combine(root, file))).IsTrue()
                .Because($"an exemption for {file} that matches nothing would hide a renamed file.");
        }
    }

    [Test]
    public async Task A_takeover_handed_back_is_told_in_the_third_person()
    {
        var loop = File.ReadAllText(Path.Combine(RunnerRoot(), "RunnerLoop.cs"));

        await Assert.That(loop).Contains("\"The person who took over recorded this flight as finished, and the runner \"");
        await Assert.That(loop).Contains("\" Their note: \"");
        await Assert.That(loop).DoesNotContain("\"You recorded this flight as finished");
    }

    [Test]
    public async Task A_waiting_runner_says_it_stays_connected()
    {
        var host = File.ReadAllText(Path.Combine(RunnerRoot(), "RunnerHost.cs"));

        await Assert.That(host).Contains("stays connected and takes no work.");
        await Assert.That(host).DoesNotContain("keeps beating")
            .Because("a beat is the platform's word; a person reads that the machine is connected.");
        await Assert.That(host).DoesNotContain("\"holding")
            .Because("hold is not a word the voice uses for a machine waiting on something.");
    }

    /// <summary>
    /// Every string literal in a C# file with the line it starts on. Comments
    /// and character literals are skipped; interpolation holes are dropped.
    /// </summary>
    internal static IEnumerable<(int Line, string Text)> Literals(string source)
    {
        var line = 1;
        var i = 0;

        while (i < source.Length)
        {
            var c = source[i];

            if (c == '\n')
            {
                line++;
                i++;
            }
            else if (c == '/' && At(source, i + 1) == '/')
            {
                while (i < source.Length && source[i] != '\n')
                {
                    i++;
                }
            }
            else if (c == '/' && At(source, i + 1) == '*')
            {
                var end = source.IndexOf("*/", i + 2, StringComparison.Ordinal);
                end = end < 0 ? source.Length : end + 2;
                line += Count(source, i, end);
                i = end;
            }
            else if (c == '\'')
            {
                // A character literal: '"', '\'', 'x'.
                i += At(source, i + 1) == '\\' ? 4 : 3;
            }
            else if (c == '"')
            {
                var interpolated = Prefix(source, i).Contains('$');
                var verbatim = Prefix(source, i).Contains('@');
                var start = line;
                var (text, end) = source.AsSpan(i).StartsWith("\"\"\"")
                    ? Raw(source, i, interpolated)
                    : Quoted(source, i, interpolated, verbatim);
                line += Count(source, i, end);
                i = end;
                yield return (start, text);
            }
            else
            {
                i++;
            }
        }
    }

    private static char At(string s, int i) => i < s.Length ? s[i] : '\0';

    private static int Count(string s, int from, int to) =>
        s.AsSpan(from, Math.Min(to, s.Length) - from).Count('\n');

    private static string Prefix(string s, int quote)
    {
        var p = quote;
        while (p > 0 && (s[p - 1] == '$' || s[p - 1] == '@'))
        {
            p--;
        }

        return s[p..quote];
    }

    private static (string Text, int End) Raw(string s, int i, bool interpolated)
    {
        var open = i;
        while (At(s, open) == '"')
        {
            open++;
        }

        var fence = new string('"', open - i);
        var close = s.IndexOf(fence, open, StringComparison.Ordinal);
        close = close < 0 ? s.Length : close;
        var body = s[open..close];
        return (interpolated ? Regex.Replace(body, @"\{[^{}]*\}", " ") : body, close + fence.Length);
    }

    private static (string Text, int End) Quoted(string s, int i, bool interpolated, bool verbatim)
    {
        var text = new StringBuilder();
        var j = i + 1;

        while (j < s.Length)
        {
            var c = s[j];

            if (verbatim && c == '"' && At(s, j + 1) == '"')
            {
                text.Append('"');
                j += 2;
            }
            else if (c == '"' || (!verbatim && c == '\n'))
            {
                return (text.ToString(), j + 1);
            }
            else if (!verbatim && c == '\\')
            {
                text.Append(' ');
                j += 2;
            }
            else if (interpolated && c == '{' && At(s, j + 1) == '{')
            {
                text.Append('{');
                j += 2;
            }
            else if (interpolated && c == '{')
            {
                j = Hole(s, j);
                text.Append(' ');
            }
            else
            {
                text.Append(c);
                j++;
            }
        }

        return (text.ToString(), j);
    }

    /// <summary>Past the hole that opens at <paramref name="i"/>, nested strings included.</summary>
    private static int Hole(string s, int i)
    {
        var depth = 0;

        while (i < s.Length)
        {
            var c = s[i];

            if (c == '{')
            {
                depth++;
            }
            else if (c == '}' && --depth == 0)
            {
                return i + 1;
            }
            else if (c == '"')
            {
                i++;
                while (i < s.Length && s[i] != '"')
                {
                    i += s[i] == '\\' ? 2 : 1;
                }
            }
            else if (c == '\'')
            {
                i += At(s, i + 1) == '\\' ? 3 : 2;
            }

            i++;
        }

        return i;
    }
}
