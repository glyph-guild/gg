namespace Gg.Contracts;

/// <summary>
/// How a document names a person: <c>&lt;provider&gt;:&lt;subject&gt;</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>ADR-0024.</b> A watch's <c>for:</c> and an obligation's <c>approver:</c>
/// both name a person, and two spellings of one person drift. This is the one
/// parser both read.
/// </para>
/// <para>
/// <b>The provider subject, because it is the one thing a principal is unique
/// on</b>: a provider, and the subject that provider issued. A display changes
/// when somebody renames themselves and two people may share one; a principal
/// id means nothing to anybody writing a document.
/// </para>
/// <para>
/// <b>Syntax here, and nothing about any particular provider.</b> This binary
/// names no identity provider (<c>ProviderNeutralityTests</c>), so which
/// providers exist and what each one's subjects look like are the control
/// plane's to know. It resolves the pair to a principal of the tenant when the
/// document arrives, and a subject that names nobody there is refused there.
/// </para>
/// </remarks>
public static class PersonSpelling
{
    /// <summary>The longest spelling this reads, provider and subject together.</summary>
    public const int MaxLength = 256;

    /// <summary>Whether this value claims to name a person rather than a role.</summary>
    /// <remarks>
    /// <b>A colon is the claim.</b> Roles have always been bare words, so a
    /// value with a provider prefix is a person and is held to being one - it
    /// is never a role with an odd name.
    /// </remarks>
    public static bool IsPerson(string? value) =>
        value is not null && value.Contains(':', StringComparison.Ordinal);

    /// <summary>Why this value is not a well-formed person, or null when it is one.</summary>
    public static string? Diagnose(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var colon = value.IndexOf(':', StringComparison.Ordinal);
        if (colon < 0)
        {
            return $"'{Shown(value)}' names no provider, so it is a role and not a person. A "
                 + "person is spelled <provider>:<subject> - `gg whoami` prints yours.";
        }

        if (value.Length > MaxLength)
        {
            return $"That person is {value.Length} characters and the limit is {MaxLength}. It "
                 + "is refused rather than trimmed, because half a subject names somebody else.";
        }

        var provider = value[..colon];
        var subject = value[(colon + 1)..];

        if (!IsWord(provider))
        {
            return $"'{Shown(value)}' names its provider as '{Shown(provider)}', which is not a "
                 + "word of lowercase letters, digits and hyphens. A person is spelled "
                 + "<provider>:<subject> - `gg whoami` prints yours.";
        }

        // THE SUBJECT IS THE PROVIDER'S, so what shape it takes is not this
        // binary's to judge - the control plane knows each provider and refuses
        // a subject that names nobody. What IS judged here is what no subject
        // can contain: nothing, whitespace, or a control character, any of
        // which would make one person render as two or as none.
        if (subject.Length == 0)
        {
            return $"'{Shown(value)}' names a provider and no subject, so it names nobody. "
                 + "A person is spelled <provider>:<subject> - `gg whoami` prints yours.";
        }

        return subject.Any(c => char.IsWhiteSpace(c) || char.IsControl(c))
            ? $"'{Shown(value)}' has whitespace or a control character in its subject. A "
              + "subject is one unbroken token, exactly as the provider issued it."
            : null;
    }

    private static bool IsWord(string provider) =>
        provider.Length > 0
        && provider[0] is >= 'a' and <= 'z'
        && provider.All(c => c is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '-');

    // SHOWN, NEVER ECHOED RAW. A control character quoted back into a sentence
    // would reach whatever renders the refusal - a terminal among them.
    private static string Shown(string value) =>
        string.Concat(value.Select(c => char.IsControl(c) ? '?' : c));
}

/// <summary>Whose work a watch's flights are.</summary>
public static class LinesOfWork
{
    /// <summary>The tenant's, and what an absent <c>for:</c> means.</summary>
    public const string Tenant = "tenant";
}
