using System.Text.RegularExpressions;
using Gg.Client;
using Gg.Contracts;
using Gg.Local;

namespace Gg.Cli.Tests;

/// <summary>
/// `gg config offered` and `gg config accept <version>` on the command line.
/// </summary>
/// <remarks>
/// <para>
/// <b>The first two config verbs that contact anything</b>, and the comment
/// above them said none of them did. Configuration is a fact about this machine
/// — which is still true of <c>show</c>, <c>init</c>, <c>set</c> and
/// <c>validate</c>, and is exactly why those four work on a plane. An offer is
/// a fact about somebody else's control plane, so reading one needs a session
/// and a network, and that difference belongs on the surface rather than in a
/// comment that has quietly become false.
/// </para>
/// <para>
/// <b>The version is required, not optional-with-a-default.</b>
/// <c>gg config accept</c> alone would be a person consenting to whatever
/// arrives; the whole reason a directed key may be offered is that somebody saw
/// what was being repointed.
/// </para>
/// </remarks>
public partial class TakingAnOfferIsTwoCommandsTests
{
    /// <summary>The parse's own source, which is where the sub-verbs are.</summary>
    private static string ParseSource()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Gg.sln")))
        {
            directory = directory.Parent;
        }

        var root = (directory ?? throw new InvalidOperationException("Gg.sln not found")).FullName;

        return File.ReadAllText(Path.Combine(root, "Gg.Cli", "CliArgs.cs"));
    }

    /// <summary>Matches a `["config", "verb"` pattern in the parse.</summary>
    [GeneratedRegex("""\["config", "([a-z]+)""")]
    private static partial Regex ConfigVerb();

    [Test]
    public async Task Every_config_verb_the_parse_accepts_is_in_the_usage()
    {
        // FOUND BY RUNNING THE BINARY, and it is the same class of defect as
        // `gg config validate` exiting 0 on a document it had refused.
        // Nineteen assertions passed, two totality guards fired and were
        // answered - and both new verbs were still missing from the one place a
        // person looks for a verb they do not know exists. Nothing in this
        // repository asserted over the usage text at all, so there was no guard
        // to fire.
        //
        // Derived from the parse rather than listed, so the NEXT config verb
        // cannot be the one nobody advertised.
        var accepted = ConfigVerb().Matches(ParseSource())
            .Select(m => m.Groups[1].Value)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();

        await Assert.That(accepted).IsNotEmpty()
            .Because("no config sub-verbs were found, so this ratchet asserted nothing.");

        var usage = ((CliAction.Unknown)CliArgs.Parse(["frobnicate"])).Message;

        var unadvertised = accepted
            .Where(v => !usage.Contains($"gg config {v}", StringComparison.Ordinal))
            .ToList();

        await Assert.That(unadvertised).IsEmpty()
            .Because("a verb absent from gg's own usage is a verb nobody finds. Found: "
                   + string.Join(", ", unadvertised));
    }

    [Test]
    public async Task Offered_is_a_verb()
    {
        await Assert.That(CliArgs.Parse(["config", "offered"]))
            .IsTypeOf<CliAction.ConfigOffered>();
    }

    [Test]
    public async Task Accept_takes_the_version_that_was_read()
    {
        var action = CliArgs.Parse(["config", "accept", "offer@7"]);

        await Assert.That(action).IsTypeOf<CliAction.ConfigAccept>();
        await Assert.That(((CliAction.ConfigAccept)action).Version).IsEqualTo("offer@7");
    }

    [Test]
    public async Task Accept_with_no_version_says_what_to_run_first()
    {
        var action = CliArgs.Parse(["config", "accept"]);

        await Assert.That(action).IsTypeOf<CliAction.Unknown>();

        var message = ((CliAction.Unknown)action).Message;

        await Assert.That(message).Contains("gg config offered", StringComparison.Ordinal)
            .Because("somebody who does not know the version has to be told where it comes "
                   + "from, not merely that one is missing.");
    }

    [Test]
    public async Task The_help_for_config_names_all_six()
    {
        var message = ((CliAction.Unknown)CliArgs.Parse(["config", "nonsense"])).Message;

        foreach (var verb in (string[])["show", "init", "set", "validate", "offered", "accept"])
        {
            await Assert.That(message).Contains(verb, StringComparison.Ordinal)
                .Because($"a verb missing from its own refusal is one nobody finds: {verb}");
        }
    }

    [Test]
    public async Task A_refused_offer_exits_non_zero()
    {
        // THE HAZARD THIS REPOSITORY HAS ALREADY BEEN BITTEN BY. `gg config
        // validate` returned a refusal and exited 0, because the tests asserted
        // the RESULT and the exit code is decided a layer up. A refused offer is
        // the same shape: a document somebody else wrote that this gg will not
        // take, reported as a produced answer rather than thrown.
        var refused = new VerbResult.ConfigOffered(new OfferedView
        {
            Path = "/somewhere/config.json",
            Version = "offer@7",
            Changes = [],
            Refused = "'executor-binary' is not a setting a control plane may offer.",
        });

        await Assert.That(ExitCodes.For(refused)).IsNotEqualTo(ExitCodes.Ok);
    }

    [Test]
    public async Task Seeing_what_is_offered_exits_zero()
    {
        var seen = new VerbResult.ConfigOffered(new OfferedView
        {
            Path = "/somewhere/config.json",
            Version = "offer@7",
            Changes =
            [
                new OfferedChange
                {
                    Key = OfferableKeys.StunServers,
                    Offered = "stun:relay.invalid:3478",
                    Changes = true,
                },
            ],
        });

        await Assert.That(ExitCodes.For(seen)).IsEqualTo(ExitCodes.Ok)
            .Because("an offer waiting is not a failure - it is the verb working.");
    }

    [Test]
    public async Task Both_verbs_are_wired_to_the_control_plane_rather_than_to_EmitLocal()
    {
        // WHERE THE LOCAL COMMENT STOPS BEING TRUE. The four offline config
        // verbs go through EmitLocal, which builds no client and reads no
        // session. These two cannot: an offer is a fact about a control plane.
        // Scanning the composition root because that is the only place the
        // distinction exists - a unit test on the command sees a value handed
        // in either way.
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Gg.sln")))
        {
            directory = directory.Parent;
        }

        var root = (directory ?? throw new InvalidOperationException("Gg.sln not found")).FullName;
        var source = File.ReadAllText(Path.Combine(root, "Gg.Cli", "Program.cs"));

        foreach (var action in (string[])["ConfigOffered", "ConfigAccept"])
        {
            var at = source.IndexOf($"CliAction.{action} ", StringComparison.Ordinal);

            await Assert.That(at).IsGreaterThan(-1)
                .Because($"{action} is not dispatched at all, so the verb parses and does "
                       + "nothing.");

            var arm = source[at..source.IndexOf("\n\n", at, StringComparison.Ordinal)];

            await Assert.That(arm).DoesNotContain("EmitLocal", StringComparison.Ordinal)
                .Because("EmitLocal builds no client and reads no session, so an offer verb "
                       + "wired through it could only ever report that nothing is offered.");
        }
    }
}
