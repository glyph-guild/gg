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
    /// What HAPPENED, never what it becomes. Expired, declined and pressed-too-
    /// early are three sentences and one fact to the person: it did not work,
    /// and they are somewhere they can try again.
    /// </remarks>
    public required string Said { get; init; }
}

/// <summary>
/// Signing in, with the terminal free.
/// </summary>
/// <remarks>
/// <para>
/// Only ever called while no UI session is running. <c>ConsoleLoop</c> runs
/// between UI lifetimes, which is what makes the two network calls and the
/// credential write allowed at all — a session may read a local file and
/// nothing else.
/// </para>
/// <para>
/// <b>Two calls rather than one, and the split is the feature.</b> A single
/// <c>SignIn()</c> would fetch the code and block on approval in the same
/// breath, so the code would only ever appear in whatever the shell printed
/// before the console redrew over it. Starting hands the code back for the
/// model to hold and the modal to draw; waiting is the second press.
/// </para>
/// <para>
/// <b>The device code lives on the implementation and nowhere else.</b> It is
/// the one value in this flow that is a credential — see
/// <see cref="PendingSignIn"/> — which is why <see cref="Wait"/> takes no
/// argument: there is nothing for the model to hand back.
/// </para>
/// </remarks>
public interface ISignInSession
{
    /// <summary>Begins a device authorization and says what a person must do.</summary>
    SignInStep Start();

    /// <summary>
    /// Waits for the person to approve what was started, and stores the session.
    /// </summary>
    /// <remarks>
    /// Bounded by the authorization's own expiry rather than by a timeout
    /// invented here, so a person who walks away gets the console back with a
    /// sentence rather than a terminal that never returns.
    /// </remarks>
    SignInStep Wait();
}
