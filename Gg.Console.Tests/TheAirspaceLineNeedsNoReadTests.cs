using System.Text.RegularExpressions;
using Gg.Console;

namespace Gg.Console.Tests;

/// <summary>
/// Where the airspace is, on screen before anything has been asked.
/// </summary>
/// <remarks>
/// <para>
/// <b>The whole airspace half of the tab was hidden behind an unrelated
/// read.</b> <c>PaneText.Envelope</c> returned early when
/// <c>state.Envelope</c> was null — so the <c>airspace:</c> line, the document
/// list and the <i>press w</i> that answers all of it rendered only once the
/// composed envelope had come back from the control plane. On a machine with no
/// control plane that read never succeeds, so the tab said one sentence about
/// the envelope and nothing at all about the airspace, for ever.
/// </para>
/// <para>
/// <b>Found by using it, which is the second time on this feature.</b> Every
/// assertion behind that pane passed: <c>PaneText.Estate</c> renders the line
/// correctly and is tested doing so, the key resolves, the hint is on the hint
/// line. What no test held was the composition — one function's early return
/// swallowing another function's whole output.
/// </para>
/// <para>
/// <b>And where the airspace is was never a question for the control
/// plane.</b> It is a path in a local file, like the machine's name and the
/// settings page beside it, so it belongs in the local fold that runs at boot
/// and on every refresh — not in a projection that depends on a session, a
/// network and a tenant having applied a document.
/// </para>
/// </remarks>
public class TheAirspaceLineNeedsNoReadTests
{
    [Test]
    public async Task The_airspace_line_shows_with_no_envelope_read()
    {
        // THE DEFECT. The estate is known - the read that fills it caught its
        // own failure and kept the root - and the pane showed none of it
        // because a different document had not arrived.
        var state = new AppState
        {
            Envelope = null,
            Estate = new EstateOnThisMachine
            {
                Uncommitted = [],
                Root = "/home/someone/policy",
                IsRepository = true,
                Diagnosis = "The airspace could not be read: no control plane.",
            },
        };

        await Assert.That(PaneText.Envelope(state))
            .Contains("documents", StringComparison.Ordinal)
            .Because("a person cannot be shown their documents only on the condition that a "
                   + "tenant has already applied an envelope.");

        // AND THE PATH NEEDS NO READ AT ALL, being a local fact: it is a pure
        // function of the model, so it cannot be gated by anything.
        await Assert.That(PaneText.AirspacePath(state)).IsEqualTo("/home/someone/policy");
    }

    [Test]
    public async Task The_airspace_section_shows_with_no_envelope_read()
    {
        // WAS ABOUT `w`, WHICH IS GONE: the path is typed into a field on the
        // tab now rather than collected through $EDITOR. What the assertion was
        // always for survives - the airspace section renders whatever the
        // envelope read did, because the one state that most needs it is the
        // machine that has configured nothing.
        await Assert.That(PaneText.Envelope(new AppState { Envelope = null }))
            .Contains("documents", StringComparison.Ordinal)
            .Because("an unconfigured machine cannot make the read that used to gate this, "
                   + "so gating it hid the section from exactly the person who needed it.");
    }

    [Test]
    public async Task The_envelope_still_says_it_has_not_been_read()
    {
        // THE CONTROL. Fixing the gating must not lose the envelope's own
        // absence: `e` is still what fetches it, and a pane that silently
        // showed nothing where the rules go would be worse than one that says
        // it has not asked.
        var text = PaneText.Envelope(new AppState { Envelope = null });

        await Assert.That(text).Contains("e", StringComparison.Ordinal);
        await Assert.That(text).Contains("not read", StringComparison.OrdinalIgnoreCase)
            .Because("an unread envelope and a tenant with no envelope are different facts, "
                   + "and this pane already distinguishes them.");
    }

    [Test]
    public async Task The_envelope_still_renders_when_it_is_there()
    {
        // THE OTHER CONTROL, on the same fix: the composed rendering is the
        // reason this pane exists, and a restructure that dropped it would
        // pass both tests above.
        var state = new AppState
        {
            Envelope = new Gg.Contracts.EnvelopeState
            {
                Version = "v7",
                UpdatedAt = DateTimeOffset.UnixEpoch,
                UpdatedBy = "an-architect",
                Envelope = new Gg.Contracts.Envelope
                {
                    Context = new Gg.Contracts.ContextBinding
                    {
                        Scope = "src/**", Constitution = "1.0.0",
                    },
                    Obligations = [],
                    Loops = [],
                    Destinations = [],
                },
            },
        };

        await Assert.That(PaneText.Envelope(state))
            .Contains("scope", StringComparison.OrdinalIgnoreCase)
            .Because("the rules in force are what somebody pressed `e' for.");
    }

    [Test]
    public async Task Where_the_airspace_is_comes_from_the_local_fold()
    {
        // A LOCAL FACT, IN THE LOCAL FOLD. The machine's name and the settings
        // page are read there because they are files this machine already has;
        // a path in a configuration file is the same kind of thing, and putting
        // it behind a projection made it depend on a session, a network and a
        // tenant having applied a document.
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Gg.sln")))
        {
            root = root.Parent;
        }

        var source = File.ReadAllText(Path.Combine(root!.FullName, "Gg.Cli", "Program.cs"));
        var fold = source[source.IndexOf("static AppState LocalFacts(", StringComparison.Ordinal)..];
        fold = fold[..fold.IndexOf("\n}", StringComparison.Ordinal)];

        await Assert.That(Regex.IsMatch(fold, @"Airspace\(\)")).IsTrue()
            .Because("boot and refresh both run this fold, so the airspace line is on the "
                   + "first frame rather than after a key nobody knew to press. Found: "
                   + fold[..Math.Min(120, fold.Length)]);
    }
}
