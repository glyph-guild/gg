namespace Gg.Console.Tests;

/// <summary>
/// The boot's model is folded over what a person did, except for the one
/// thing only the boot can know.
/// </summary>
/// <remarks>
/// <para>
/// <b>The sign-in modal could not appear, on any version.</b>
/// <c>ConsoleStart</c> answers a <c>NotSignedInException</c> with
/// <c>Mode = SignIn</c> and a diagnosis beside it; the fold that applies the
/// boot carried the person's own mode over it unconditionally, so the
/// diagnosis arrived and the mode did not. The console drew "Nobody is signed
/// in here, so nothing could be read." in the flight pane and offered no way
/// to do anything about it — the modal that exists for exactly this never
/// opened.
/// </para>
/// <para>
/// <b>Reproduced before it was believed:</b> with an expired session the
/// console drew that sentence while its hint line read
/// <c>a actions · n new flight · y fly by hand</c> — Normal mode's keys.
/// </para>
/// </remarks>
public class TheBootCanSayNobodyIsSignedInTests
{
    private static AppState Boot(UiMode mode) => new()
    {
        Mode = mode,
        Diagnosis = "Nobody is signed in here, so nothing could be read.",
        ActiveTab = TabId.Queue,
    };

    [Test]
    public async Task A_boot_that_found_nobody_signed_in_opens_the_modal()
    {
        var folded = Reducer.Booted(Boot(UiMode.SignIn), new AppState());

        await Assert.That(folded.Mode).IsEqualTo(UiMode.SignIn)
            .Because("the boot is the ONLY thing that can discover this, and a fold that "
                   + "keeps the person's Normal over it throws away the one field that "
                   + "says so - leaving a console that reports being signed out in a pane "
                   + "and offers no way to sign in.");

        await Assert.That(folded.Diagnosis).IsNotNull()
            .Because("the reason stays whatever happens to the remedy.");
    }

    [Test]
    public async Task And_what_a_person_opened_meanwhile_still_wins()
    {
        // THE HALF THE FIX MUST NOT BREAK. The boot takes seconds; somebody
        // who opened help in that window keeps it, which is what carrying the
        // mode over was for in the first place.
        var folded = Reducer.Booted(
            Boot(UiMode.SignIn), new AppState { Mode = UiMode.Help });

        await Assert.That(folded.Mode).IsEqualTo(UiMode.Help)
            .Because("Normal is nobody having opened anything, and only then may the boot "
                   + "speak into it.");
    }

    [Test]
    public async Task A_boot_that_found_a_session_leaves_normal_alone()
    {
        var folded = Reducer.Booted(Boot(UiMode.Normal), new AppState());

        await Assert.That(folded.Mode).IsEqualTo(UiMode.Normal)
            .Because("the ordinary boot opens no modal, and this must not invent one.");
    }

    [Test]
    public async Task The_tab_and_the_look_are_still_the_persons()
    {
        // The rest of the carrying-over, unchanged: the boot fills the tab it
        // opens on and nothing else, so a person who turned elsewhere keeps
        // their tab and is asked for its read.
        var moved = new AppState { ActiveTab = TabId.Itineraries };
        var folded = Reducer.Booted(Boot(UiMode.Normal), moved);

        await Assert.That(folded.ActiveTab).IsEqualTo(TabId.Itineraries);
        await Assert.That(folded.Refresh.Wanted).IsTrue()
            .Because("the boot filled the queue, so the tab they actually moved to is "
                   + "empty until it is asked for.");
    }

    [Test]
    public async Task And_staying_put_asks_for_nothing()
    {
        var folded = Reducer.Booted(Boot(UiMode.Normal), new AppState());

        await Assert.That(folded.Refresh.Wanted).IsFalse()
            .Because("a second identical round of the heaviest read the console makes, at "
                   + "the one moment it has just finished.");
    }
}
