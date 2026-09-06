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
/// <b>Nothing here throws.</b> The console is drawn over the terminal, so an
/// exception out of this ends the process and takes the screen with it: a
/// person would be left looking at a stack trace where their queue was. Every
/// failure comes back as the sentence the modal draws.
/// </para>
/// </remarks>
public sealed class SignInSession(
    Func<DeviceAuthorizationStarted> start,
    Func<DeviceAuthorizationStarted, SignInResult> wait,
    IConsoleWriter? output = null) : ISignInSession
{
    /// <summary>The handle, held here and never handed out.</summary>
    private DeviceAuthorizationStarted? _started;

    /// <summary>
    /// Where the code goes when the modal cannot draw it.
    /// </summary>
    /// <remarks>
    /// Writing to the terminal from the console's own assembly is allowed here
    /// for the reason <c>TakeSession</c> already writes to it: this runs
    /// BETWEEN UI sessions, with the screen provably nobody's.
    /// </remarks>
    private readonly IConsoleWriter _output = output ?? new StandardConsoleWriter();

    public SignInStep Start()
    {
        try
        {
            _started = start();
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
                UserCode = ControlText.Strip(_started.UserCode),
                VerificationUri = ControlText.Strip(_started.VerificationUri),
                ExpiresAt = _started.ExpiresAt,
            },
            Said = "Waiting for you to approve it.",
        };
    }

    public SignInStep Wait()
    {
        // The loop tracks whether something is pending to choose a key; this
        // tracks it to hold a handle. They should agree, and a disagreement is
        // a sentence in the modal rather than a crash in the shell.
        if (_started is not { } started)
        {
            return new SignInStep { Said = "Nothing has been started here yet." };
        }

        // LET GO OF BEFORE THE WAIT, not after it. A device code is spent once
        // the authorization resolves either way, and every path out of here -
        // approved, declined, expired, unreachable - returns to the offer, so
        // the next press has to start a fresh one.
        _started = null;

        // SAID BEFORE THE WAIT, because the modal that was drawing this is
        // already gone: the UI session ended to get here, and what follows can
        // block until the code expires. Somebody who pressed approve a moment
        // early would otherwise be looking at a blank terminal with nothing on
        // it to approve. TakeSession's rule - once the screen stops being ours,
        // nothing of ours is read again until it comes back.
        AuthCommands.ShowCode(_output, started);
        _output.WriteLine("Waiting for you to approve it. The console comes back after that.");

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
