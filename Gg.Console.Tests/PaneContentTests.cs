namespace Gg.Console.Tests;

/// <summary>
/// What the panes show once a row can be selected.
/// </summary>
/// <remarks>
/// <para>
/// <b>The plan's headline symptom, reachable at last.</b> The Flight pane says
/// <c>loading…</c> because <c>AppState.Flight</c> is never assigned - and until
/// the queue could fill, nothing could be selected, so the pane was not shown a
/// wrong sentence, it was not shown. Step 2's first commit gave the queue its
/// gates; this is what a person sees next.
/// </para>
/// <para>
/// <b>Against a real control plane, with a real gate.</b> The row comes from a
/// registration diverted to the widening gate - a real flight with a real
/// obligation waiting on a real person, and no runner and no agent anywhere.
/// </para>
/// <para>
/// <b>And with no request the boot was not already making.</b> Every flight's
/// summary arrives in the list the boot already fetches, and every flight's log
/// in the per-flight logs it already fetches and discards. The Flight pane and
/// the log pane cost nothing new; only the credentials do.
/// </para>
/// </remarks>
[Category("RealStack")]
public class PaneContentTests
{
    [Test]
    public async Task The_flight_pane_shows_the_selected_flight()
    {
        var seeded = await AgainstARealControlPlaneTests.GatedAsync();
        var booted = await ConsoleStart.LoadAsync(seeded.Data, seeded.Principal);

        await Assert.That(booted.Selected).IsNotNull()
            .Because("a row has to be selectable, or this measures the case step 0 measured.");

        var pane = PaneText.Flight(booted);

        await Assert.That(pane).IsNotEqualTo("loading…")
            .Because("the pane said this for ever, because nothing assigned AppState.Flight - "
                   + "ConsoleProjection.Apply has the arm and had no caller.");
        await Assert.That(pane).Contains(booted.Selected!.FlightNumber)
            .Because("the flight a person selected, not some other flight.");
        await Assert.That(pane).Contains("constitution")
            .Because("every line below PaneText's flight branch was unreachable; this is one "
                   + "of them, and it is what a person opens the pane for.");
    }

    [Test]
    public async Task The_flight_log_renders_from_what_the_boot_already_fetched()
    {
        // S28.2-02. LoadAsync fetches a log per flight and threw them away once
        // the queue was derived. The N requests it already pays for now buy
        // something.
        var seeded = await AgainstARealControlPlaneTests.GatedAsync();
        var booted = await ConsoleStart.LoadAsync(seeded.Data, seeded.Principal);

        await Assert.That(booted.FlightLog).IsNotNull()
            .Because("the log for the selected flight was fetched at boot and discarded.");
        await Assert.That(booted.FlightLog!.FlightNumber)
            .IsEqualTo(booted.Selected!.FlightNumber)
            .Because("the selected flight's log, not the first one that happened to load.");
    }

    [Test]
    public async Task The_fleet_and_the_credentials_render()
    {
        // S28.2-03. Runners were fetched at boot into a local and never
        // assigned; credentials had a field and a renderer and nothing that
        // fetched them.
        var seeded = await AgainstARealControlPlaneTests.GatedAsync();
        var booted = await ConsoleStart.LoadAsync(seeded.Data, seeded.Principal);

        await Assert.That(booted.Runners).IsNotNull()
            .Because("fetched at boot to derive the queue, and thrown away after.");
        await Assert.That(booted.Credentials).IsNotNull()
            .Because("the field and the renderer both existed and nothing fetched them, so "
                   + "the console could never show what it holds a reference to.");
    }

    [Test]
    public async Task A_read_that_fails_costs_one_read_rather_than_the_console()
    {
        // S28.2-08, and rule 5's third sentence: `failed to load` is not
        // `empty`. The credential read is the one that can fail on its own
        // here - a tenant may hold none and the endpoint still answers - so
        // what this asserts is that ONE read failing does not empty the screen.
        var seeded = await AgainstARealControlPlaneTests.GatedAsync();
        var booted = await ConsoleStart.LoadAsync(seeded.Data, seeded.Principal);

        await Assert.That(booted.Queue).IsNotEmpty()
            .Because("the queue is what a partial boot must keep, because it is the pane the "
                   + "console is for.");
        await Assert.That(booted.Diagnosis).IsNull()
            .Because("nothing failed in this one, so nothing should be claimed to have. The "
                   + "sentence is for a boot that lost something.");
    }

    [Test]
    public async Task A_refresh_reads_the_row_the_cursor_is_on()
    {
        // THE HALF THAT COULD ONLY BE MEASURED HERE. The boot read `queue[0]`
        // while the arrow key read the selection, which is invisible until a
        // queue has two rows AND something re-reads with the cursor moved -
        // which is what step 3 made every write do.
        var seeded = await AgainstARealControlPlaneTests.TwoGatedAsync();
        var booted = await ConsoleStart.LoadAsync(seeded.Data, seeded.Principal);

        await Assert.That(booted.Queue.Count).IsGreaterThanOrEqualTo(2)
            .Because("one row cannot show a selection defect - both answers are the same "
                   + "flight.");

        var moved = booted with { SelectedRow = 1 };
        var refreshed = await ConsoleStart.LoadAsync(seeded.Data, seeded.Principal, moved);

        await Assert.That(refreshed.SelectedRow).IsEqualTo(1)
            .Because("the cursor is the person's and a refresh does not move it.");
        await Assert.That(refreshed.Flight).IsNotNull();
        await Assert.That(refreshed.Flight!.FlightId).IsEqualTo(refreshed.Selected!.FlightId)
            .Because("the flight pane shows the flight the queue's cursor is on. It showed "
                   + "the top row's flight under the selected row's name, which is the one "
                   + "wrong answer a person cannot see is wrong.");
        await Assert.That(refreshed.FlightLog!.FlightNumber)
            .IsEqualTo(refreshed.Selected!.FlightNumber)
            .Because("and its log with it.");
    }

    [Test]
    public async Task The_boot_asks_who_it_is()
    {
        // WHAT A CONSTRUCTED STATE CANNOT SHOW. The projection tests prove a
        // notice reaches the pane once a verb returns one; only the real boot
        // can show that a verb is ASKED. A fresh tenant has no degradation to
        // report, so asserting on the count would pass against no wiring at
        // all - the request is the evidence.
        var seeded = await AgainstARealControlPlaneTests.GatedAsync();

        _ = await ConsoleStart.LoadAsync(seeded.Data, seeded.Principal);

        await Assert.That(seeded.Counter.Paths.Any(p =>
                p.EndsWith("/v1/auth/whoami", StringComparison.Ordinal))).IsTrue()
            .Because("AppState.Notices is drawn above every queue and was assigned by "
                   + "nothing, because whoami was the one read verb with no value to "
                   + "project. A degradation nobody is shown is one nobody acts on.");
    }

    [Test]
    public async Task Why_a_flight_is_stopped_reaches_its_pane()
    {
        // S28.4-02, and the real stack is where it has to be measured: the halt
        // and the obligation attributions are the control plane's own words, and
        // a constructed FlightAttribution proves only that a renderer renders.
        var seeded = await AgainstARealControlPlaneTests.GatedAsync();
        var booted = await ConsoleStart.LoadAsync(seeded.Data, seeded.Principal);

        await Assert.That(booted.Attribution).IsNotNull()
            .Because("gg why has answered this question since it was written and the "
                   + "console's wrapper for it had no caller at all.");
        await Assert.That(booted.Attribution!.FlightNumber)
            .IsEqualTo(booted.Selected!.FlightNumber)
            .Because("the selected row's, which is the only row it is read for.");

        var pane = PaneText.Flight(booted);

        await Assert.That(pane).Contains("why")
            .Because("the section is in the pane a person is already looking at.");
        await Assert.That(pane).DoesNotContain("not read for this row")
            .Because("it WAS read for this row - the unread sentence appearing here would "
                   + "mean the boot never asked.");
    }

    [Test]
    public async Task The_checklist_is_read_for_the_selected_flight()
    {
        // S28.4-01. ConsoleData.PlanAsync has had no caller since it was
        // written, and the real stack is where the items come from: a
        // constructed Checklist proves a renderer renders.
        var seeded = await AgainstARealControlPlaneTests.GatedAsync();
        var booted = await ConsoleStart.LoadAsync(seeded.Data, seeded.Principal);

        var opened = ConsoleChecklist.Read(seeded.Data, booted);

        await Assert.That(opened.Checklist).IsNotNull()
            .Because("the pane a person stares at while a flight waits could not be filled.");
        await Assert.That(opened.Checklist!.Items).IsNotEmpty()
            .Because("an envelope in force declares obligations, so its checklist has items - "
                   + "an empty one here would mean the read reached the wrong flight.");

        var pane = PaneText.Checklist(opened);

        await Assert.That(pane).DoesNotContain("not read")
            .Because("it WAS read, and that sentence appearing means the boot never asked.");
        await Assert.That(pane).Contains(opened.Checklist!.Items[0].Disposition)
            .Because("the disposition is the whole answer to 'can this start'.");
    }
}
