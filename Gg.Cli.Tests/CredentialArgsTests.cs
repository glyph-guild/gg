using System.Reflection;
using System.Text.RegularExpressions;

namespace Gg.Cli.Tests;

/// <summary>
/// <c>gg credential add</c> has no flag that could carry a secret, and could
/// not grow one without failing here.
/// </summary>
/// <remarks>
/// <para>
/// Both halves of "the secret never enters a request body, and the command has
/// no flag that would let it" are assertions rather than intentions. This is
/// the second half.
/// </para>
/// <para>
/// It is the one that gets added later "for scripting", which is exactly the
/// reason to assert it now: an argument is in shell history and in <c>ps</c>
/// output before any code of ours has run, and neither of those is somewhere a
/// later fix can reach.
/// </para>
/// </remarks>
public class CredentialArgsTests
{
    /// <summary>Words that name secret material rather than a fact about one.</summary>
    private static readonly string[] SecretShapedWords =
        ["token", "secret", "password", "passphrase", "bearer", "apikey", "privatekey", "accesskey"];

    /// <summary>Every <c>--flag</c> literal the parser mentions.</summary>
    /// <remarks>
    /// Read out of the source, because that is where a flag is added. A test
    /// over <see cref="CliArgs.Parse"/>'s behaviour would have to guess the
    /// name of the flag it is hunting for, which is the one thing it cannot
    /// know.
    /// </remarks>
    internal static IReadOnlyList<string> FlagsIn(string source) =>
        [.. Regex.Matches(source, @"--[a-z][a-z0-9-]*", RegexOptions.None, TimeSpan.FromSeconds(5))
            .Select(m => m.Value)
            .Distinct(StringComparer.Ordinal)];

    private static string ParserSource()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Gg.sln")))
        {
            dir = dir.Parent;
        }
        var root = dir ?? throw new InvalidOperationException("Gg.sln not found above " + AppContext.BaseDirectory);
        return File.ReadAllText(Path.Combine(root.FullName, "Gg.Cli", "CliArgs.cs"));
    }

    [Test]
    public async Task No_flag_gg_accepts_could_carry_a_secret()
    {
        var offenders = FlagsIn(ParserSource())
            .Where(flag => SecretShapedWords.Any(w => flag.Contains(w, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        await Assert.That(offenders).IsEmpty()
            .Because("an argument is in shell history and ps output before any code of ours runs. Found: "
                   + string.Join(", ", offenders));
    }

    [Test]
    public async Task The_poison_twin_the_scanner_really_would_see_such_a_flag()
    {
        // An absence over a source file passes on an empty read, a bad path and
        // a regex that matches nothing. So the scanner is pointed at a planted
        // parser first.
        const string Planted = """
            ["credential", "add", "--repo", var repo, "--token", var token] => new CredentialAdd(repo, token),
            """;

        var found = FlagsIn(Planted);

        await Assert.That(found).Contains("--token");
        await Assert.That(found.Any(f => SecretShapedWords.Any(w => f.Contains(w, StringComparison.OrdinalIgnoreCase))))
            .IsTrue()
            .Because("if the scanner cannot see this, the assertion above proves nothing.");
    }

    [Test]
    public async Task The_scanner_is_reading_the_real_parser()
    {
        // The other empty-scan failure: a source file that was found but is not
        // the one with the flags in it.
        var flags = FlagsIn(ParserSource());

        await Assert.That(flags).Contains("--json");
        await Assert.That(flags).Contains("--repo");
    }

    [Test]
    public async Task No_member_of_the_credential_action_could_hold_a_secret()
    {
        // The parse RESULT, as well as the flags: a positional argument would
        // reach a member without ever being spelled with dashes.
        var members = typeof(CliAction.CredentialAdd)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.Name != "EqualityContract")
            .Select(p => p.Name)
            .ToList();

        await Assert.That(members).IsNotEmpty();

        var offenders = members
            .Where(name => SecretShapedWords.Any(w => name.Contains(w, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        await Assert.That(offenders).IsEmpty()
            .Because("Found: " + string.Join(", ", offenders));
    }

    [Test]
    public async Task Credential_add_takes_a_repository_and_defaults_to_read()
    {
        var parsed = CliArgs.Parse(["credential", "add", "--repo", "github/acme-widgets"]);

        var action = (CliAction.CredentialAdd)parsed;
        await Assert.That(action.Repo).IsEqualTo("github/acme-widgets");
        await Assert.That(action.Scopes).IsEquivalentTo((string[])["read"]);
    }

    [Test]
    public async Task Credential_add_without_a_repository_is_refused_with_the_usage()
    {
        var parsed = CliArgs.Parse(["credential", "add"]);

        await Assert.That(parsed).IsTypeOf<CliAction.Unknown>();
        await Assert.That(((CliAction.Unknown)parsed).Message).Contains("--repo");
    }

    [Test]
    public async Task A_flag_the_parser_does_not_know_is_refused_rather_than_ignored()
    {
        // Article XI at the command line. Ignoring an unknown flag is how
        // `--token` would appear to work while being silently dropped, which
        // is worse than refusing it: somebody would believe it did something.
        foreach (var hostile in (string[])["--token", "--secret", "--password"])
        {
            var parsed = CliArgs.Parse(["credential", "add", "--repo", "github/acme-widgets", hostile, "value"]);

            await Assert.That(parsed).IsTypeOf<CliAction.Unknown>()
                .Because($"{hostile} must be refused, not absorbed.");
        }
    }

    [Test]
    public async Task Credential_list_and_rm_parse_and_take_json()
    {
        await Assert.That(CliArgs.Parse(["credential", "list", "--json"]))
            .IsTypeOf<CliAction.CredentialList>();
        await Assert.That(((CliAction.CredentialList)CliArgs.Parse(["credential", "list", "--json"])).Json)
            .IsTrue();

        var removed = (CliAction.CredentialRemove)CliArgs.Parse(["credential", "rm", "019fe815"]);
        await Assert.That(removed.CredentialId).IsEqualTo("019fe815");
    }

    [Test]
    public async Task Credential_rm_without_an_id_says_what_it_wanted()
    {
        var parsed = CliArgs.Parse(["credential", "rm"]);

        await Assert.That(parsed).IsTypeOf<CliAction.Unknown>();
        await Assert.That(((CliAction.Unknown)parsed).Message).Contains("credential rm");
    }

    [Test]
    public async Task The_usage_now_promises_the_credential_verbs()
    {
        // A usage string is a promise, and the reverse holds too: a verb that
        // works and is not listed is one nobody finds.
        var usage = ((CliAction.Unknown)CliArgs.Parse(["nonsense"])).Message;

        await Assert.That(usage).Contains("gg credential add");
        await Assert.That(usage).Contains("gg credential list");
    }
}
