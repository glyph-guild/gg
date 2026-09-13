using Gg.Contracts;

namespace Gg.Runner;

/// <summary>Where a credential this runner holds is destroyed.</summary>
/// <remarks>
/// <para>
/// <b>Separate from <see cref="IKeepACredential"/> on purpose, and the
/// separation IS the policy.</b> That port exists only when a machine's own
/// file says <c>accept-configured</c>: putting a secret on a machine is
/// something it has to have agreed to. Taking one away is not, and a machine
/// that would not forget when told is a liability. Two ports means wiring one
/// cannot wire the other by accident, which a single interface with two methods
/// could not promise.
/// </para>
/// <para>
/// <b>It answers rather than throws</b>, for its sibling's reason one layer
/// over: what calls it is a heartbeat, and an exception there costs a runner
/// its liveness over a file that was already gone.
/// </para>
/// </remarks>
public interface IForgetACredential
{
    /// <summary>Destroys the secret at this locator. Whether one was there.</summary>
    bool Forget(string locator);
}

/// <summary>
/// What a runner does with the credentials a beat told it to drop.
/// </summary>
/// <remarks>
/// <para>
/// <b>Its own type rather than a few lines in <c>BeatAsync</c>, because the
/// rules are worth asserting.</b> Every locator is validated before it becomes
/// a path, one that will not drop does not stop the rest, and absence is
/// nothing to do. None of those is obvious from a <c>foreach</c>, and all three
/// are the difference between revocation and a delete primitive pointed at a
/// disk.
/// </para>
/// <para>
/// <b>Nothing here reports what it dropped.</b>
/// <c>AnAcceptedOfferIsRecordedTests</c> settled the shape for the other
/// direction - <i>a machine that told its control plane what it accepted would
/// turn a carried offer into a tracked instruction</i> - and it holds here: the
/// control plane knows what it asked for, and a confirmation would make a
/// machine's local disk into state the far side believes it owns.
/// </para>
/// </remarks>
public static class ForgetsWhatItIsTold
{
    /// <summary>Drops every locator a beat named, or none.</summary>
    public static void Apply(IReadOnlyList<string>? forget, IForgetACredential? store)
    {
        // NOTHING TO DO IS THE ORDINARY CASE, on every beat of every runner in
        // a fleet where nothing was revoked. A null store is ordinary too: a
        // runner composed without one simply has nothing to destroy.
        if (forget is null || store is null)
        {
            return;
        }

        foreach (var locator in forget)
        {
            // VALIDATED BEFORE IT BECOMES A PATH, which is the case
            // CredentialStore's own remark was written about - "by the time a
            // runner sees a locator it is data that came back from the control
            // plane, and a path it could steer is a path it could steer
            // anywhere". Deleting is the direction where steering one costs the
            // most, so it is refused rather than sanitised.
            if (CredentialLocator.Validate(locator) is not null)
            {
                continue;
            }

            try
            {
                store.Forget(locator);
            }
            catch (ArgumentException)
            {
                // ONE THAT WILL NOT DROP DOES NOT STOP THE REST. A control
                // plane repeats a revocation until it is sure, so a locator
                // that is already gone is the ordinary case rather than the
                // rare one - and stopping here would leave later credentials
                // held because an earlier one had already been dealt with.
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
