using Gg.Client;
using Gg.Contracts;

namespace Gg.Console;

/// <summary>
/// What one step of signing in produced.
/// </summary>
/// <remarks>
/// One type for both steps, because both answer the same three questions: is
/// there a session now, is there still something for a person to do, and what
/// do they read about it. Two types would make the loop's arm dispatch on which
/// half it called, which is the thing it is trying not to care about.
/// </remarks>
public sealed record SignInStep
{
    /// <summary>What a person must still do, or null when nothing is waiting.</summary>
    public PendingSignIn? Pending { get; init; }

    /// <summary>Whether this machine now holds a session.</summary>
    public bool SignedIn { get; init; }

    /// <summary>
    /// The sentence a person reads.
    /// </summary>
    /// <remarks>
    /// What HAPPENED, never what it becomes. Expired, declined and unreachable
    /// are three sentences and one fact to the person: it did not work, and
    /// they are somewhere they can try again.
    /// </remarks>
    public required string Said { get; init; }
}

/// <summary>
/// Signing in, with the terminal free and the modal still up.
/// </summary>
/// <remarks>
/// <para>
/// <b>Asking for the code happens between UI lifetimes; waiting for the
/// approval does not.</b> <c>ConsoleLoop</c> runs <see cref="Start"/> with the
/// screen provably nobody's, which is what makes a network call and, later, a
/// credential write allowed at all. The wait then runs on a task owned outside
/// every UI lifetime and <see cref="Arrived"/> asks only whether it has
/// finished — <see cref="AutoRefresh"/>'s exception, argued there, and for the
/// same reason: what the rule forbids is a session that BLOCKS, and every call
/// here returns.
/// </para>
/// <para>
/// <b>Why it is not a second keypress.</b> It was one, and the press meant "I
/// have approved it in a browser". Approving is the whole of signing in, so the
/// press could only ever tell the console something it was about to find out;
/// what it actually did was make a person who had already approved sit in front
/// of a console that showed no sign of it. There is nothing for a person to
/// confirm here that the control plane has not already been asked.
/// </para>
/// <para>
/// <b>The device code lives on the implementation and nowhere else.</b> It is
/// the one value in this flow that is a credential — see
/// <see cref="PendingSignIn"/> — which is why <see cref="Arrived"/> takes no
/// argument: there is nothing for the model to hand back.
/// </para>
/// </remarks>
public interface ISignInSession
{
    /// <summary>
    /// Begins a device authorization, says what a person must do, and starts
    /// watching for them to do it.
    /// </summary>
    SignInStep Start();

    /// <summary>
    /// What the wait produced, or null while it is still outstanding.
    /// </summary>
    /// <remarks>
    /// <b>Never waits.</b> This is asked once a second from inside a UI session,
    /// so an answer that blocked would freeze the keyboard for as long as a
    /// person takes to find their browser. Null is the ordinary answer and it
    /// means "not yet", not "nothing was started".
    /// </remarks>
    SignInStep? Arrived();
}

/// <summary>
/// Signs this machine in through the same two halves <c>gg login</c> uses.
/// </summary>
/// <remarks>
/// <para>
/// <b>The device code lives in the field below and goes nowhere else.</b>
/// Whoever holds it polls once the authorization is approved and is handed the
/// session token, so it is a bearer capability - and this object is outside
/// every UI lifetime, like the live tails, rather than in a record that is
/// serialized to disk under <c>GG_STATE_DUMP</c> and mailed to us in a bundle.
/// </para>
/// <para>
/// <b>Delegates rather than the commands themselves</b>, the shape
/// <c>TakeSession</c>'s claim already has: the verbs are async and this is
/// called from a synchronous shell, so the composition root owns the bridge -
/// and a test can drive both halves without a control plane.
/// </para>
/// <para>
/// <b>Nothing here throws, and nothing here prints.</b> The console is drawn
/// over the terminal for the whole of the wait now, so an exception out of this
/// ends the process and takes the screen with it, and a line written to stdout
/// goes through the middle of what Terminal.Gui is painting. Every failure comes
/// back as the sentence the modal draws.
/// </para>
/// </remarks>
public sealed class SignInSession(
    Func<DeviceAuthorizationStarted> start,
    Func<DeviceAuthorizationStarted, SignInResult> wait,
    Func<Func<SignInStep>, Task<SignInStep>>? run = null) : ISignInSession
{
    /// <summary>The wait, in flight or finished. Null before anything started.</summary>
    private Task<SignInStep>? _waiting;

    /// <summary>
    /// How the wait is got off this thread.
    /// </summary>
    /// <remarks>
    /// Injected for the reason time is: a test that let a real task run would be
    /// asserting "asking does not block" BY waiting, and waiting in a test is
    /// the one thing this repo does not do.
    /// </remarks>
    private readonly Func<Func<SignInStep>, Task<SignInStep>> _run =
        run ?? (work => Task.Run(work));

    public SignInStep Start()
    {
        DeviceAuthorizationStarted started;

        try
        {
            started = start();
        }
        catch (Exception failure) when (failure is HttpRequestException
                                            or ProtocolTooOldException
                                            or TaskCanceledException)
        {
            return new SignInStep
            {
                Said = $"Could not ask the control plane for a code: {failure.Message}",
            };
        }

        // OFF THIS THREAD BEFORE THE MODAL IS DRAWN. The loop returns from here
        // straight into a new UI session, so by the time a person is reading the
        // code the control plane is already being asked about it - which is what
        // makes approving in a browser the only thing left to do.
        _waiting = _run(() => Waited(started));

        return new SignInStep
        {
            // FOUR VALUES ARRIVE AND THREE CROSS. This is the line the whole
            // arrangement exists for: DeviceCode is not among them.
            //
            // AND THE THREE ARE CLEANED ON THE WAY THROUGH. Both strings are
            // composed by a control plane, and this doorway is their ingress -
            // the console's rule is that external text is stripped before
            // STORAGE rather than at render, because the model is written to
            // disk, handed to the diagnostics bundle, and read back by things
            // that are not PaneText.
            Pending = new PendingSignIn
            {
                UserCode = ControlText.Strip(started.UserCode),
                VerificationUri = ControlText.Strip(started.VerificationUri),
                ExpiresAt = started.ExpiresAt,
            },
            Said = "Waiting for you to approve it.",
        };
    }

    public SignInStep? Arrived()
    {
        if (_waiting is not { IsCompleted: true } finished)
        {
            return null;
        }

        // LET GO OF ONCE IT HAS RESOLVED. A device code is spent whichever way
        // the authorization went, and every path out of here returns to the
        // offer, so the next press has to start a fresh one. The result is
        // handed back rather than held: the model is where it lives after this.
        _waiting = null;

        // A TASK THAT FAULTED IS STILL A SENTENCE. Waited catches what the
        // client is documented to throw; anything else would otherwise surface
        // as an exception out of a UI timer, which ends the process and takes
        // the screen with it.
        return finished.IsCompletedSuccessfully
            ? finished.Result
            : new SignInStep { Said = "Lost the control plane while waiting." };
    }

    /// <summary>The wait itself, with every way out turned into a sentence.</summary>
    private SignInStep Waited(DeviceAuthorizationStarted started)
    {
        try
        {
            var result = wait(started);

            return new SignInStep { SignedIn = result.SignedIn, Said = result.Said };
        }
        catch (Exception failure) when (failure is HttpRequestException
                                            or ProtocolTooOldException
                                            or TaskCanceledException)
        {
            return new SignInStep { Said = $"Lost the control plane while waiting: {failure.Message}" };
        }
    }
}
