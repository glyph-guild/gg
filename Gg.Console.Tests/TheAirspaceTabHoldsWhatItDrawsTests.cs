using Gg.Client;
using Gg.Console;
using Gg.Contracts;

namespace Gg.Console.Tests;

/// <summary>
/// The airspace tab holds every document it draws, so moving the cursor costs
/// nothing.
/// </summary>
/// <remarks>
/// <para>
/// <b>THE PANE IS THE POINT AND THE RULE WAS IN THE WAY.</b> The right-hand
/// pane shows the selected document three ways — on disk, applied, effective —
/// and <c>PaneText</c> is pure, so all three have to be on the model. Fetching
/// them per row would be a request on every arrow key, which
/// <c>ConsoleStart</c> refuses in as many words: <i>"reading on the arrow key
/// would be I/O inside a UI session"</i>.
/// </para>
/// <para>
/// <b>So <c>EstateOnThisMachine</c> carries bodies now, and the rule it
/// replaces was written against something that is no longer true.</b> That
/// rule said <i>"AppState is written to GG_STATE_DUMP and handed to
/// ConsoleData.BundleFrom, so a member able to carry envelope text would put a
/// tenant's governance documents in a file they send us"</i>. Measured:
/// <c>BundleFrom</c> takes the state and IGNORES it — it calls
/// <c>Bundle.Build(takenAt, environment, doctor, flightLog)</c> — and
/// <c>GG_STATE_DUMP</c> is an opt-in environment variable described in
/// <c>Program.cs</c> as a <i>"Demo/verification hook"</i>, written once on
/// exit. Governance text on the model reaches a debug dump somebody switched
/// on, not a bundle a customer sends.
/// </para>
/// <para>
/// <b>What stays true is the SHAPE of the old rule's worry</b>, so this holds
/// what the tab draws and nothing else: the documents the airspace applied and
/// the text of the files on disk. Not logs, not evidence, not every read the
/// console makes.
/// </para>
/// </remarks>
public class TheAirspaceTabHoldsWhatItDrawsTests
{
    [Test]
    public async Task The_applied_documents_are_on_the_model()
    {
        var state = new AppState
        {
            Estate = new EstateOnThisMachine
            {
                Root = "/home/someone/airspace",
                Uncommitted = [],
                Applied =
                [
                    new NamedEnvelopeState
                    {
                        Name = "score-hal",
                        Role = Roles.WorkKind,
                        Version = "score-hal@v1",
                        UpdatedAt = DateTimeOffset.UnixEpoch,
                        UpdatedBy = "Kevin Deenanauth",
                    },
                ],
            },
        };

        await Assert.That(state.Estate!.Applied.Select(d => d.Name)).Contains("score-hal")
            .Because("the pane draws this without asking anybody, which is the whole "
                   + "reason it is here rather than fetched per row.");
    }

    [Test]
    public async Task The_file_on_disk_is_held_as_text()
    {
        // THE THIRD TAB. A session MAY read a local file whose path the console
        // holds - that exception is stated - but PaneText is pure and cannot
        // read one, so the text rides on the model like everything else the
        // views draw.
        var file = new AirspaceFile(
            "work-kind", "score-hal", "airspace/work-kinds/score-hal.yaml", null)
        {
            Text = "context:\n  scope: \"**\"\n",
        };

        await Assert.That(file.Text).IsNotNull();
        await Assert.That(file.Text!).Contains("scope", StringComparison.Ordinal)
            .Because("what is actually in the file, which is what somebody comparing "
                   + "against the applied version needs to see.");
    }

    [Test]
    public async Task An_absent_read_is_an_empty_list_rather_than_null()
    {
        // ABSENT JSON KEYS NULL A DEFAULTED COLLECTION - the discriminator is
        // init-only versus required, and this suite has been bitten by it. The
        // pane iterates this, so null would be a crash on a model that
        // round-tripped through the dump.
        var round = AppStateJson.Deserialize(AppStateJson.Serialize(new AppState
        {
            Estate = new EstateOnThisMachine { Root = "/somewhere", Uncommitted = [] },
        }));

        await Assert.That(round.Estate!.Applied).IsNotNull()
            .Because("a collection that deserialises to null is the defect this repository "
                   + "records against absent keys, and the pane walks this one.");
    }
}
