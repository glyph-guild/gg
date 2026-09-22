using Gg.Client;
using Gg.Contracts;
using Gg.Local;

namespace Gg.Cli;

/// <summary>
/// What a runner does with an offer, decided before anything is composed.
/// </summary>
/// <remarks>
/// <para>
/// <b>At startup, and deliberately not on the beat that carries it.</b> The
/// runner reads its labels, its hold, its relays and its executor once, into
/// locals handed to the loop — so a file written mid-run changes nothing until
/// the process restarts. Applying here means a machine brought up fresh is
/// configured before it claims anything, and it needs no mechanism for mutating
/// a live loop: a flight cannot start under one configuration and land under
/// another, because nothing moves while one is flying.
/// </para>
/// <para>
/// <b>Unattended, so only the unwatched tier can land.</b> It asks
/// <see cref="OfferedConfigurations.Accept"/> with <c>attended: false</c>. That
/// is the guard written when nothing called it that way, and this is the first
/// caller that does — relay addresses and runner labels apply, and a key that
/// changes where code is fetched from or sent to still waits for a person.
/// </para>
/// <para>
/// <b>Pure, because the network belongs to the caller.</b> The composition root
/// makes one heartbeat and hands the answer here. That split is what lets every
/// case be a unit test rather than a scan of a file of top-level statements —
/// and it is why a control plane that cannot be reached costs the runner
/// nothing: the caller catches, passes null, and this returns what was already
/// in force.
/// </para>
/// </remarks>
public static class OfferedAtStartup
{
    /// <summary>What to write, what to say, and what to compose from.</summary>
    public sealed record Outcome
    {
        /// <summary>
        /// The configuration the rest of this run should read.
        /// </summary>
        /// <remarks>
        /// <b>The whole reason this returns anything at all.</b>
        /// <c>InForce.Configuration</c> is memoized for the process, so a root
        /// that wrote a file and then went on reading the memo would compose
        /// from the document it had just replaced — the feature doing nothing,
        /// quietly, on every machine.
        /// </remarks>
        public Configuration? InForce { get; init; }

        /// <summary>The document to write, or null when there is nothing to write.</summary>
        public Configuration? Write { get; init; }

        /// <summary>
        /// What to say about an offer that was not taken, or null.
        /// </summary>
        /// <remarks>
        /// <b>Null on the steady state, and that is deliberate.</b> Every boot
        /// after the first meets an offer already in force; a line about it
        /// each time is a line in every machine's log for ever, which is how a
        /// log stops being read. What earns a sentence is an offer that could
        /// not be taken — nobody is here to notice it any other way.
        /// </remarks>
        public string? Note { get; init; }
    }

    /// <summary>What a RUNNING loop should do with the offer it was handed.</summary>
    /// <param name="alreadySaid">
    /// The offer version this loop has already spoken about, so the same
    /// refusal is said once rather than every beat.
    /// </param>
    /// <remarks>
    /// <para>
    /// <b>A function because the root cannot be tested.</b> This was a lambda
    /// that read only <see cref="Outcome.Write"/> and returned - so an offer
    /// that could not be taken produced a <see cref="Outcome.Note"/> which
    /// nothing printed, in the one place where nobody is present to notice it
    /// any other way.
    /// </para>
    /// <para>
    /// <b>Measured on slice forty-seven's walk:</b> a runner one version behind
    /// sat on the old configuration for four minutes with a new offer in force
    /// and wrote nothing at all. It was behaving correctly - a machine refuses a
    /// key it does not know - and the silence is what sent the walk looking at
    /// the control plane instead.
    /// </para>
    /// <para>
    /// <b>ONCE PER OFFER, NOT PER BEAT.</b> A loop beats every few seconds, and
    /// the same refusal repeated is how a log stops being read - which is the
    /// failure the note's own remark warns about.
    /// </para>
    /// </remarks>
    public static Beat OnABeat(
        OfferedConfiguration? offered, Configuration? file, string path, string? alreadySaid)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (offered is null)
        {
            return new Beat();
        }

        var decided = Decide(offered, file, path);

        // TAKEN BY STOPPING, which is the behaviour that already worked: nothing
        // is applied to a running loop, so the next start is what takes it.
        if (decided.Write is not null)
        {
            return new Beat
            {
                Say = $"offered configuration {offered.Version} is not what this runner is "
                    + "running on, and nothing is applied to a running loop. Stopping so the "
                    + "next start takes it.",
                Stop = true,
                Said = offered.Version,
            };
        }

        // AND SAID ONCE. Restarting would not help - this build will not take
        // this offer however often it starts - so the sentence is all there is.
        if (decided.Note is { Length: > 0 } note
            && !string.Equals(alreadySaid, offered.Version, StringComparison.Ordinal))
        {
            return new Beat { Say = note, Said = offered.Version };
        }

        return new Beat { Said = alreadySaid };
    }

    /// <summary>What a running loop says and does about one beat's offer.</summary>
    public sealed record Beat
    {
        /// <summary>The line to write, or null for the steady state.</summary>
        public string? Say { get; init; }

        /// <summary>Whether to stop, so the next start takes what was offered.</summary>
        public bool Stop { get; init; }

        /// <summary>The offer version spoken about, to hand back on the next beat.</summary>
        public string? Said { get; init; }
    }

    /// <summary>What this machine should do with the offer it was handed.</summary>
    public static Outcome Decide(OfferedConfiguration? offered, Configuration? file, string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (offered is null)
        {
            return new Outcome { InForce = file };
        }

        // NO FILE IS STILL NO FILE. Handing Accept an invented empty document
        // would be right about the answer - nothing is opted in - and wrong
        // about what to compose from afterwards, so the original is what goes
        // back either way.
        var taken = OfferedConfigurations.Accept(offered, file ?? new Configuration(), attended: false);

        if (taken.Refused is { } refused)
        {
            return new Outcome
            {
                InForce = file,
                Note = $"the control plane is offering configuration this gg will not take, "
                     + $"so nothing changed: {refused}",
            };
        }

        if (taken.Configuration is not { } applied)
        {
            // WAITING, WHICH IS TWO DIFFERENT SENTENCES. Either this machine
            // accepts nothing offered, or the offer repoints something and only
            // a person may take it. An operator reading one host's log to find
            // out why it ignored what the fleet took needs to know which.
            return new Outcome
            {
                InForce = file,
                Note = taken.Waiting
                    ? file?.AcceptOffered is true
                        ? $"offer {offered.Version} repoints something, so this machine will "
                        + "not take it with nobody watching. Somebody runs "
                        + $"`gg config accept {offered.Version}`."
                        : $"offer {offered.Version} is waiting and this machine accepts nothing "
                        + $"offered. Set 'accept-offered' to true in {path} - it has no "
                        + "environment variable and cannot itself be offered, so opening the "
                        + "file is the only way."
                    : null,
            };
        }

        // ALREADY IN FORCE, and nothing to do. Writing the file anyway would
        // churn a document a person owns on every boot of every machine.
        if (taken.AlreadyAccepted)
        {
            return new Outcome { InForce = file };
        }

        return new Outcome { InForce = applied, Write = applied };
    }
}
