using Gg.Contracts;
using Gg.Local;

namespace Gg.Client;

/// <summary>A configuration change gg would not make, and why.</summary>
/// <remarks>
/// <b>Named, for the reason the envelope verbs already have named refusals.</b>
/// The command line catches it and prints the sentence; an
/// <c>ArgumentOutOfRangeException</c> reaching the top prints a stack trace and
/// the parameter's name, which is a crash wearing a diagnosis. Found by running
/// the verb rather than by a test, because the test asserted the throw and the
/// harness above it was what turned one into an answer.
/// </remarks>
public sealed class ConfigurationRefused(string why) : Exception(why);

/// <summary>What is in force on this machine, and where the file is.</summary>
/// <remarks>
/// <b>The path is part of the answer.</b> A person asking what is configured is
/// usually one step from asking where to change it, and computing the path
/// themselves means knowing the <c>XDG_CONFIG_HOME</c> rule.
/// </remarks>
public sealed record ConfigurationView
{
    public required string Path { get; init; }

    public required IReadOnlyList<EnvironmentSetting> Settings { get; init; }

    /// <summary>Whether a control plane may change what this machine does.</summary>
    /// <remarks>
    /// <b>On the view rather than in the settings list, because it is not one of
    /// them.</b> It has no environment variable by design — a variable would be
    /// a second way to turn it on, and one a container image could carry — so it
    /// never reaches the page that lists variables, and without this it reached
    /// no surface at all.
    /// </remarks>
    public bool AcceptsOffered { get; init; }

}

/// <summary>One offered setting, beside what it would replace.</summary>
/// <remarks>
/// <b><see cref="Current"/> is the half a person cannot get anywhere else</b>,
/// and it is why the directed keys are offerable at all: the argument for
/// letting a control plane repoint a forge host is that somebody sees which
/// forge it was. Null means nothing is being replaced, so the rendering can say
/// so rather than print an empty column.
/// </remarks>
public sealed record OfferedChange
{
    public required string Key { get; init; }

    public required string Offered { get; init; }

    public string? Current { get; init; }

    /// <summary>Whether taking it would move this value.</summary>
    /// <remarks>
    /// Said per setting, because a list where every line looks like a change is
    /// one nobody can find the change in.
    /// </remarks>
    public bool Changes { get; init; }
}

/// <summary>What is offered here, and what this machine has done about it.</summary>
/// <remarks>
/// <para>
/// <b>One document for both verbs.</b> <c>gg config offered</c> and
/// <c>gg config accept</c> answer the same question — what is offered, against
/// what is in force — and differ only in whether this run wrote it. Two shapes
/// would be two renderings to keep in agreement about the same facts.
/// </para>
/// <para>
/// <b>The posture is on it even when the answer is no</b>, for the reason
/// <see cref="ConfigurationView.AcceptsOffered"/> exists: a person finding out
/// there is an offer to look at needs the answer to "then why would nothing
/// happen" in the same breath.
/// </para>
/// </remarks>
public sealed record OfferedView
{
    public required string Path { get; init; }

    /// <summary>The offer's version, or null when nothing is offered.</summary>
    public string? Version { get; init; }

    public DateTimeOffset? OfferedAt { get; init; }

    public required IReadOnlyList<OfferedChange> Changes { get; init; }

    /// <summary>Whether this machine accepts offers at all.</summary>
    public bool AcceptsOffered { get; init; }

    /// <summary>Whether this offer may only be taken by a person.</summary>
    public bool NeedsAPerson { get; init; }

    /// <summary>Whether this exact offer is already in force here.</summary>
    public bool AlreadyAccepted { get; init; }

    /// <summary>Whether THIS run wrote it.</summary>
    /// <remarks>
    /// <b>What separates the two verbs, and the only thing that does.</b>
    /// <c>offered</c> never sets it; <c>accept</c> sets it when the file moved.
    /// </remarks>
    public bool Accepted { get; init; }

    /// <summary>Why this offer cannot be taken, or null when it can.</summary>
    public string? Refused { get; init; }
}

/// <summary>Whether a document is one, and what it looks like written out.</summary>
/// <remarks>
/// <b>The same three parts <c>EnvelopeValidation</c> carries</b>, for the same
/// reasons: whether it is valid, what is wrong when it is not, and what gg
/// would write — so a person can see before applying what their file is about
/// to become.
/// </remarks>
public sealed record ConfigurationValidation
{
    public required bool Valid { get; init; }

    public string? Diagnosis { get; init; }

    public string? Canonical { get; init; }
}

/// <summary>
/// The config verbs: show, validate, init, set, offered, accept.
/// </summary>
/// <remarks>
/// <para>
/// <b>The first four contact nothing.</b> What this machine is configured to do
/// is a fact about this machine, so <c>show</c>, <c>validate</c>, <c>init</c>
/// and <c>set</c> work with no session and no network — the property
/// <c>gg envelope validate</c> already has, and the reason they are the verbs
/// people reach for offline.
/// </para>
/// <para>
/// <b>The last two cannot, and the difference is where the fact lives.</b> An
/// offer is a fact about somebody else's control plane. Neither of them
/// contacts it from here — the offer is handed in, so both are still testable
/// with nothing running — but the composition root has to fetch one, which is
/// why they are dispatched through the async emitter rather than the local one.
/// A note in a comment would have gone stale; <c>TakingAnOfferIsTwoCommands
/// Tests</c> scans the root instead.
/// </para>
/// <para>
/// <b>They return a <see cref="VerbResult"/> like every other verb</b>, so
/// <c>--json</c> and the rendered form are two views of one document rather
/// than two implementations that agree today.
/// </para>
/// </remarks>
public static class ConfigCommands
{
    /// <summary>Everything in force, and the file it would be written to.</summary>
    public static VerbResult Show(
        IReadOnlyList<EnvironmentSetting> settings,
        string? path = null,
        Configuration? file = null)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return new VerbResult.ConfigShown(new ConfigurationView
        {
            Path = path ?? ConfigurationFile.DefaultPath(),
            Settings = settings,
            AcceptsOffered = file?.AcceptOffered is true,
        });
    }

    /// <summary>Whether this text is a configuration. Contacts nothing.</summary>
    public static VerbResult Validate(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var parsed = ConfigurationFile.Parse(text);

        return new VerbResult.ConfigValidated(new ConfigurationValidation
        {
            Valid = parsed.Configuration is not null,
            Diagnosis = parsed.Diagnosis,
            Canonical = parsed.Configuration is { } configuration
                ? ConfigurationFile.Render(configuration)
                : null,
        });
    }

    /// <summary>
    /// Writes a file seeded from what is in force, when there is not one.
    /// </summary>
    /// <remarks>
    /// <b>It refuses to overwrite.</b> A person running this twice is a person
    /// who forgot they had run it, and the second run replacing their edits
    /// would be the worst possible answer to that.
    /// </remarks>
    public static VerbResult Init(Configuration seed, string? path = null)
    {
        ArgumentNullException.ThrowIfNull(seed);

        var at = path ?? ConfigurationFile.DefaultPath();

        if (File.Exists(at))
        {
            throw new ConfigurationRefused(
                $"'{at}' is already there. gg will not overwrite it - read it with `gg config "
              + "show`, or change one value with `gg config set`.");
        }

        if (Configuration.Validate(seed) is { } refused)
        {
            throw new ConfigurationRefused(refused);
        }

        ConfigurationFile.Write(seed, at);

        return new VerbResult.ConfigValidated(new ConfigurationValidation
        {
            Valid = true,
            Canonical = ConfigurationFile.Render(seed),
        });
    }

    /// <summary>
    /// Changes one setting, leaving the rest of the document alone.
    /// </summary>
    /// <remarks>
    /// <b>Validated before anything is written</b>, so the file on disk is
    /// always readable by the next run. A set that wrote a value
    /// <see cref="Configuration.Validate"/> refuses would leave a document gg
    /// then declines to read — and the person who typed it would have no way to
    /// tell that from a file it had never written.
    /// </remarks>
    public static VerbResult Set(string? path, string key, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);

        var at = path ?? ConfigurationFile.DefaultPath();
        var read = ConfigurationFile.Read(at);

        if (read.Diagnosis is { } unreadable)
        {
            throw new ConfigurationRefused(
                $"'{at}' cannot be read, so changing one value in it would lose the rest: "
              + unreadable);
        }

        // BY KEY, WHICH IS WHAT A PERSON READING THE FILE SEES. The variable is
        // the other spelling and is accepted too, because the page and the
        // doctor both name it and somebody will paste one.
        var member = Configuration.Members.FirstOrDefault(
            m => string.Equals(m.Key, key, StringComparison.OrdinalIgnoreCase)
              || string.Equals(m.Variable, key, StringComparison.OrdinalIgnoreCase))
            ?? throw new ConfigurationRefused(
                $"'{key}' is not a setting. These are: "
              + string.Join(", ", Configuration.Members.Select(m => m.Key)) + ".");

        var changed = member.With(read.Configuration ?? new Configuration(), value);

        if (Configuration.Validate(changed) is { } refused)
        {
            throw new ConfigurationRefused(refused);
        }

        ConfigurationFile.Write(changed, at);

        return new VerbResult.ConfigValidated(new ConfigurationValidation
        {
            Valid = true,
            Canonical = ConfigurationFile.Render(changed),
        });
    }

    /// <summary>
    /// What a control plane is offering, against what is in force.
    /// </summary>
    /// <remarks>
    /// <b>It writes nothing and refuses nothing.</b> A control plane offering
    /// something this gg will not take is exactly what a person needs told, so
    /// the refusal is part of the answer — the split <c>Validate</c> above
    /// already draws. Everything here is a read, so it is safe to run on a
    /// machine somebody is trying to understand.
    /// </remarks>
    public static VerbResult Offered(
        OfferedConfiguration? offered, Configuration? file, string? path = null) =>
        new VerbResult.ConfigOffered(View(offered, file, path, accepted: false));

    /// <summary>
    /// Takes the offer whose version was named, and writes it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The version is the point.</b> Between reading an offer and taking it a
    /// control plane can offer something else, and an accept that took whatever
    /// was current would apply a document nobody saw — which removes the one
    /// property that makes a directed key offerable at all.
    /// </para>
    /// <para>
    /// <b>Attended by construction.</b> There is no unattended path any more:
    /// <c>accept-unattended</c> was a switch for a pool member and a pool member
    /// has no file to set it in. Somebody typed this.
    /// </para>
    /// </remarks>
    public static VerbResult Accept(
        OfferedConfiguration? offered, string version, string? path = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(version);

        var at = path ?? ConfigurationFile.DefaultPath();

        if (offered is null)
        {
            throw new ConfigurationRefused(
                $"nothing is offered, so there is no '{version}' to accept. Run `gg config "
              + "offered` to see what your control plane is offering.");
        }

        // NAMED, OR NOTHING HAPPENS. The mismatch says what IS offered, because
        // a refusal that only says "not that one" leaves somebody retyping the
        // version they already had.
        if (!string.Equals(offered.Version, version, StringComparison.Ordinal))
        {
            throw new ConfigurationRefused(
                $"'{version}' is not what is offered now - '{offered.Version}' is. Your "
              + "control plane changed its offer since you looked, so nothing was written. "
              + "Run `gg config offered` and read the new one.");
        }

        var read = ConfigurationFile.Read(at);

        if (read.Diagnosis is { } unreadable)
        {
            throw new ConfigurationRefused(
                $"'{at}' cannot be read, so accepting an offer into it would lose the rest: "
              + unreadable);
        }

        var file = read.Configuration ?? new Configuration();
        var taken = OfferedConfigurations.Accept(offered, file, attended: true);

        if (taken.Refused is { } refused)
        {
            throw new ConfigurationRefused(refused);
        }

        // WAITING HERE MEANS THE LOCAL GATE IS SHUT, and typing a verb is not
        // the same act as opening the file. That friction is deliberate: it is
        // the decision to let something else configure this machine, and the
        // refusal names the key rather than leaving somebody to find it.
        if (taken.Configuration is null && !taken.AlreadyAccepted)
        {
            throw new ConfigurationRefused(
                $"this machine does not accept offered configuration, so '{version}' was not "
              + $"applied. Set 'accept-offered' to true in {at} - it has no environment "
              + "variable and cannot be offered, so opening the file is the only way.");
        }

        // ALREADY IN FORCE: not an error, and not a write either. The file is
        // untouched byte-for-byte, which is what stops re-running this from
        // rewriting a document to say what it already said.
        if (taken.Configuration is { } accepted)
        {
            ConfigurationFile.Write(accepted, at);
        }

        // AGAINST THE FILE AS IT WAS, never the one just written. The value
        // that GOES is the one thing a person cannot recover once the write
        // lands, and it is the whole argument for a redirect being offerable at
        // all - so the answer to "what did I just accept" has to be composed
        // from before. Found by walking the verbs: composed from `after`, every
        // line read "already in force", which was true and useless.
        return new VerbResult.ConfigOffered(
            View(offered, file, at, accepted: taken.Configuration is not null));
    }

    /// <summary>
    /// One offer, read against one file — the answer both verbs give.
    /// </summary>
    /// <remarks>
    /// <b>Composed once</b>, so <c>offered</c> and <c>accept</c> cannot come to
    /// disagree about what an offer would change. The only thing they decide
    /// separately is <paramref name="accepted"/>.
    /// </remarks>
    private static OfferedView View(
        OfferedConfiguration? offered, Configuration? file, string? path, bool accepted)
    {
        var at = path ?? ConfigurationFile.DefaultPath();

        if (offered is null)
        {
            return new OfferedView
            {
                Path = at,
                Changes = [],
                AcceptsOffered = file?.AcceptOffered is true,
            };
        }

        var refused = OfferedConfiguration.Validate(offered);
        var against = file ?? new Configuration();

        return new OfferedView
        {
            Path = at,
            Version = offered.Version,
            OfferedAt = offered.OfferedAt,
            AcceptsOffered = against.AcceptOffered is true,
            NeedsAPerson = OfferedConfiguration.NeedsAPerson(offered),
            AlreadyAccepted = string.Equals(
                against.AcceptedOffer, offered.Version, StringComparison.Ordinal),
            Accepted = accepted,
            Refused = refused,

            // THE VALUES ARE STILL SHOWN ON A REFUSED OFFER. A person told only
            // that their control plane is offering something wrong, without
            // being shown what, has to go and ask somebody.
            Changes = [.. offered.Settings.Select(setting =>
            {
                var member = Configuration.Members.FirstOrDefault(
                    m => string.Equals(m.Key, setting.Key, StringComparison.Ordinal));

                var current = member?.Get(against);

                return new OfferedChange
                {
                    Key = setting.Key,
                    Offered = setting.Value,
                    Current = current,
                    Changes = !string.Equals(current, setting.Value, StringComparison.Ordinal),
                };
            })],
        };
    }
}
