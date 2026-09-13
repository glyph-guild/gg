namespace Gg.Cli.Tests;

/// <summary>
/// A person opening a flight can say which regime governs it, and where it runs.
/// </summary>
/// <remarks>
/// <para>
/// <b>BOTH FIELDS HAVE BEEN ON THE WIRE AND UNSENT.</b>
/// <c>FlightLaunchRequest</c> carries <c>workKind</c> and <c>environment</c>;
/// every caller in this repository built a request without them, so a tenant
/// with a work kind defined had no way to open a flight for it. The same
/// <i>port nothing calls</i> shape as the unread chart and pool ledger — on the
/// write side, which is why a ratchet about developer READS did not catch it.
/// </para>
/// <para>
/// <b>The other route decides nothing.</b> <c>FlightNomination</c> carries a
/// work kind, but it is a fact an AGENT ships from inside a flight asking that
/// another be opened, and the control plane refuses anything outside
/// <c>Destination.Opens</c>. So it is not how a person starts one.
/// </para>
/// <para>
/// <b>Choosing wrong grants nothing.</b> The contract's own remark: a work kind
/// "can only narrow root, so choosing wrong grants nothing root withheld". An
/// environment is validated against the composed envelope's bound. Both are
/// refused control-plane-side with a diagnosis, which is why the parser's job
/// stays "did they type it".
/// </para>
/// <para>
/// <b>Position-independent and stripped as a pair</b>, the way <c>--runner</c>
/// already is — an option left in the list is matched as a verb, and
/// <c>fly</c>'s arms are list patterns.
/// </para>
/// </remarks>
public class FlyNamesItsWorkKindTests
{
    private static CliAction.Fly Flown(params string[] args)
    {
        var action = CliArgs.Parse(args);

        return action as CliAction.Fly ?? throw new InvalidOperationException(
            $"'{string.Join(" ", args)}' did not parse as a flight but as "
          + $"{action.GetType().Name}"
          + (action is CliAction.Unknown unknown ? $": {unknown.Message}" : "."));
    }

    [Test]
    public async Task Text_can_name_a_work_kind_and_an_environment()
    {
        var flown = Flown("fly", "score this item", "--work-kind", "hal-score",
                          "--environment", "dev");

        await Assert.That(flown.Text).IsEqualTo("score this item");
        await Assert.That(flown.WorkKind).IsEqualTo("hal-score");
        await Assert.That(flown.Environment).IsEqualTo("dev");
    }

    [Test]
    public async Task So_can_a_uri_and_a_ticket()
    {
        // EVERY LINE gg fly ALREADY TAKES, which is the rule --hand came in on:
        // a flag that worked on one payload and not the others would be a
        // second way to open a flight wearing the first one's name.
        var uri = Flown("fly", "--uri", "https://example.test/1", "--work-kind", "hal-score");

        await Assert.That(uri.Uri).IsEqualTo("https://example.test/1");
        await Assert.That(uri.WorkKind).IsEqualTo("hal-score");

        var ticket = Flown("fly", "--ticket", "jdx#18599", "--environment", "dev");

        await Assert.That(ticket.Provider).IsEqualTo("jdx");
        await Assert.That(ticket.Id).IsEqualTo("18599");
        await Assert.That(ticket.Environment).IsEqualTo("dev");
    }

    [Test]
    public async Task And_so_can_a_line_that_also_names_a_repository()
    {
        var flown = Flown("fly", "--ticket", "jdx#18599", "--repo", "payments",
                          "--work-kind", "hal-score", "--environment", "dev");

        await Assert.That(flown.Repository).IsEqualTo("payments");
        await Assert.That(flown.WorkKind).IsEqualTo("hal-score");
        await Assert.That(flown.Environment).IsEqualTo("dev");
    }

    [Test]
    public async Task They_are_position_independent()
    {
        // AND THE VALUE DOES NOT BECOME THE INTENT, which is what stripping as a
        // PAIR is for: dropping only the name leaves the value in the list, and
        // a value in the list is matched as a verb - how `gg fly --runner abc
        // "do it"' once became a flight whose text was a runner id.
        var flown = Flown("fly", "--work-kind", "hal-score", "score this item");

        await Assert.That(flown.Text).IsEqualTo("score this item");
        await Assert.That(flown.WorkKind).IsEqualTo("hal-score");
    }

    [Test]
    public async Task Neither_is_required()
    {
        var flown = Flown("fly", "score this item");

        await Assert.That(flown.WorkKind).IsNull()
            .Because("null inherits, which is what a flight naming no work kind has always "
                   + "meant - and every flight opened before this flag existed named none.");

        await Assert.That(flown.Environment).IsNull();
    }

    [Test]
    public async Task They_are_refused_on_a_verb_that_opens_nothing()
    {
        // --attended AND --runner'S RULE. Stripping globally would accept
        // `gg flights --work-kind hal-score' and do nothing - a flag that reads
        // as an instruction and is not one.
        foreach (var line in new[]
        {
            (string[])["flights", "--work-kind", "hal-score"],
            ["runners", "--environment", "dev"],
        })
        {
            var refused = CliArgs.Parse(line);

            await Assert.That(refused).IsTypeOf<CliAction.Unknown>()
                .Because($"'{string.Join(" ", line)}' names a flag only gg fly acts on.");
        }
    }

    [Test]
    public async Task A_trailing_flag_says_what_it_needs()
    {
        // THE SHAPE --repo ALREADY REFUSES BY NAME. Falling through would
        // diagnose the wrong half of the line, or open a flight whose text is
        // the word somebody meant as a flag.
        foreach (var option in (string[])["--work-kind", "--environment"])
        {
            var refused = CliArgs.Parse(["fly", "score this item", option]);

            await Assert.That(refused).IsTypeOf<CliAction.Unknown>()
                .Because($"a trailing {option} is somebody who meant to name one.");
        }
    }

    [Test]
    public async Task The_help_says_both()
    {
        // A FLAG NOBODY CAN FIND IS A FLAG NOBODY HAS. The help is the only
        // place a person learns these exist - there is no other surface that
        // opens a flight for a named work kind.
        // THE USAGE AS A PERSON MEETS IT - the message a mistyped line answers
        // with, which is how EveryVerbIsDiscoverableTests reaches it too.
        var help = ((CliAction.Unknown)CliArgs.Parse(["nonsense"])).Message;

        await Assert.That(help).Contains("--work-kind", StringComparison.Ordinal);
        await Assert.That(help).Contains("--environment", StringComparison.Ordinal);
    }
}
