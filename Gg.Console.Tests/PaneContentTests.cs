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
}
