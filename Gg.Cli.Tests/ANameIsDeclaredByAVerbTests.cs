using System.Text.RegularExpressions;

namespace Gg.Cli.Tests;

/// <summary>
/// `gg airspace name` — the act that has never had a verb.
/// </summary>
/// <remarks>
/// <para>
/// <b>A name must be declared before any document can be applied to it, and gg
/// could not declare one.</b> The control plane's own refusal spells out the
/// missing step — <i>"Declare the name - POST /v1/airspace/names - and apply
/// again"</i> — and <c>DeclareNameRequest</c> has sat in the contract with no
/// caller in this binary. So creating a new narrowing meant calling an HTTP
/// endpoint by hand, which is not a thing to tell a customer.
/// </para>
/// <para>
/// <b>A declaration is always a widening, so it always rides a gate.</b>
/// ADR-0016 § 6: a new name <i>"has no prior version to sit below; it is reach
/// that did not exist a moment ago"</i>, so registrations take the widening
/// path unconditionally. The verb therefore has two success shapes and must
/// render both — a 200 when the name is already live, a 202 when it is riding
/// a flight.
/// </para>
/// <para>
/// <b>The sub-verb ratchet is new, and it is why the last one shipped
/// unadvertised.</b> <c>EveryVerbIsDiscoverableTests</c> walks the FIRST word of
/// each match arm, so <c>gg airspace</c> being in the usage satisfies it for
/// every sub-verb the family will ever have. That granularity is wrong here:
/// <c>airspace</c> already has four sub-verbs and this adds a fifth, and none of
/// them is guarded by the walk.
/// </para>
/// </remarks>
public class ANameIsDeclaredByAVerbTests
{
    private static string SourcePath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Gg.sln")))
        {
            directory = directory.Parent;
        }

        return directory is null
            ? throw new InvalidOperationException(
                "The repository root was not found above the test binary, so the parser's "
              + "source cannot be read - and this guard reads the source deliberately.")
            : Path.Combine(directory.FullName, "Gg.Cli", "CliArgs.cs");
    }

    /// <summary>Every second word an `airspace` arm dispatches on.</summary>
    private static IReadOnlyList<string> SubVerbsTheParserAccepts()
    {
        var source = File.ReadAllText(SourcePath());

        return [.. Regex.Matches(source, @"^\s{12}\[""airspace"",\s*""(?<sub>[a-z][a-z-]*)""",
                    RegexOptions.Multiline)
                .Select(m => m.Groups["sub"].Value)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(v => v, StringComparer.Ordinal)];
    }

    [Test]
    public async Task The_walk_actually_finds_the_sub_verbs()
    {
        // THE LIVENESS ANCHOR. A regex that matched nothing would make the
        // assertion below vacuously true, and this guard would report success
        // while reading an empty set - which is the failure mode the walk in
        // EveryVerbIsDiscoverableTests already carries an anchor for.
        var subs = SubVerbsTheParserAccepts();

        await Assert.That(subs).Contains("pull");
        await Assert.That(subs).Contains("apply");
        await Assert.That(subs.Count).IsGreaterThanOrEqualTo(4)
            .Because("show, pull, diff and apply were all there before this verb was, so a "
                   + "walk finding fewer than four has stopped reading the arms.");
    }

    [Test]
    public async Task Every_airspace_sub_verb_appears_in_the_usage()
    {
        var usage = ((CliAction.Unknown)CliArgs.Parse(["nonsense"])).Message;

        var undiscoverable = SubVerbsTheParserAccepts()
            .Where(sub => !usage.Contains(sub, StringComparison.Ordinal))
            .ToList();

        await Assert.That(undiscoverable).IsEmpty()
            .Because("the verb walk checks first words only, so `gg airspace` in the usage "
                   + "advertises this family for ever whatever is added to it. A sub-verb "
                   + "that works and is not named is a feature only somebody reading the "
                   + "source can find. Found: " + string.Join(", ", undiscoverable));
    }

    [Test]
    public async Task A_role_and_a_name_are_both_taken()
    {
        var parsed = CliArgs.Parse(["airspace", "name", "narrowing", "pci"]);

        var declaring = await Assert.That(parsed).IsTypeOf<CliAction.AirspaceName>();

        await Assert.That(declaring!.Role).IsEqualTo("narrowing");
        await Assert.That(declaring.Name).IsEqualTo("pci");
    }

    [Test]
    public async Task The_parent_defaults_to_root_rather_than_to_nothing()
    {
        // NOT BLANK. The door treats an empty parent as "no parent" and its own
        // refusal for a parent that does not exist says why that is wrong - "a
        // child of nothing is unreachable by construction". Root is in every
        // tenant's topology by synthesis, so it is the one default that is
        // always valid, and it is what the request type's own remark names.
        var parsed = (CliAction.AirspaceName)CliArgs.Parse(
            ["airspace", "name", "work-kind", "migrate-data"]);

        await Assert.That(parsed.Parent).IsEqualTo("root")
            .Because("a declaration gg sent with no parent would be a name nothing can "
                   + "reach, accepted by the door and useless afterwards.");
    }

    [Test]
    public async Task A_parent_can_be_named()
    {
        var parsed = (CliAction.AirspaceName)CliArgs.Parse(
            ["airspace", "name", "narrowing", "pci", "--under", "migrate-data"]);

        await Assert.That(parsed.Parent).IsEqualTo("migrate-data");
        await Assert.That(parsed.Name).IsEqualTo("pci");
    }

    [Test]
    public async Task A_declaration_missing_its_name_says_what_the_verb_takes()
    {
        var parsed = CliArgs.Parse(["airspace", "name", "narrowing"]);

        var unknown = await Assert.That(parsed).IsTypeOf<CliAction.Unknown>();

        await Assert.That(unknown!.Message).Contains("role", StringComparison.OrdinalIgnoreCase)
            .Because("the refusal names both arguments, or somebody who guessed the order "
                   + "has to guess again.");
    }

    [Test]
    public async Task The_family_refusal_names_the_new_sub_verb_too()
    {
        var parsed = CliArgs.Parse(["airspace", "banana"]);

        var unknown = await Assert.That(parsed).IsTypeOf<CliAction.Unknown>();

        await Assert.That(unknown!.Message).Contains("name", StringComparison.Ordinal)
            .Because("`gg airspace takes show, pull, diff or apply` is the sentence a person "
                   + "gets when they mistype, and it is the second place the family is "
                   + "advertised.");
    }
}
