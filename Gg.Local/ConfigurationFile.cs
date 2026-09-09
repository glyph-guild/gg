using System.Text.Json;
using System.Text.Json.Serialization;

namespace Gg.Local;

/// <summary>What reading a configuration produced, or why it could not.</summary>
/// <remarks>
/// <b>The parse and the rule are one door.</b> A configuration that comes back
/// non-null has already been validated, so nothing downstream has to ask again —
/// and a file on disk that <see cref="ConfigurationFile.Read"/> accepted is one
/// the rest of the product can use. The shape is <c>EnvelopeParse</c>'s, one
/// document over.
/// </remarks>
public sealed record ConfigurationParse
{
    /// <summary>The document, or null when there is a diagnosis or no file.</summary>
    public Configuration? Configuration { get; init; }

    /// <summary>What is wrong, or null when nothing is.</summary>
    public string? Diagnosis { get; init; }
}

/// <summary>
/// The configuration file: where it lives, and text to model and back.
/// </summary>
/// <remarks>
/// <para>
/// <b>JSON, because <c>Gg.Local</c> carries no package reference and must
/// not.</b> <c>ProjectReferenceTests</c> holds that — <i>"a local path is not a
/// wire type"</i> — so YamlDotNet, which the envelope uses, is not available
/// here and putting this document in <c>Gg.Contracts</c> to reach it would ship
/// a filesystem path inside the wire contract. <c>System.Text.Json</c> with a
/// source-generated context is BCL and AOT-safe.
/// </para>
/// <para>
/// <b>Keys are kebab-case</b>, like every closed vocabulary in the envelope —
/// <c>pull-request</c>, <c>on-exhaustion</c>, <c>wall-clock</c>. A file a person
/// opens should look like the other documents this product asks them to read.
/// </para>
/// <para>
/// <b>Unmapped members are refused rather than ignored</b>, which is the whole
/// reason this reads through <see cref="Parse"/> rather than
/// <c>JsonSerializer.Deserialize</c> alone. A typo in a hand-edited file is the
/// ordinary way a line comes to look load-bearing and do nothing.
/// </para>
/// </remarks>
public static class ConfigurationFile
{
    /// <summary>The file's name inside the config root.</summary>
    private const string FileName = "config.json";

    /// <summary>
    /// Where the file lives: <c>$XDG_CONFIG_HOME/good-grief/config.json</c>.
    /// </summary>
    /// <param name="configHome">
    /// The root, for a caller that has one. Null reads the environment — and the
    /// override exists because <c>XDG_CONFIG_HOME</c> is process-global and a
    /// suite that runs four-wide cannot have one test setting it while another
    /// reads it. <c>LocalPaths</c> states the same reason.
    /// </param>
    public static string DefaultPath(string? configHome = null)
    {
        var root = configHome
            ?? Environment.GetEnvironmentVariable("XDG_CONFIG_HOME")
            ?? Path.Combine(
                Environment.GetFolderPath(
                    OperatingSystem.IsWindows()
                        ? Environment.SpecialFolder.ApplicationData
                        : Environment.SpecialFolder.UserProfile),
                OperatingSystem.IsWindows() ? "" : ".config");

        return Path.Combine(root, "good-grief", FileName);
    }

    /// <summary>The configuration on disk, or silence when there is no file.</summary>
    /// <remarks>
    /// <b>A missing file is not an error and not an empty configuration.</b> The
    /// first is the ordinary case on a machine nobody has configured; the second
    /// is somebody having written a document that sets nothing, and they are
    /// different facts.
    /// </remarks>
    public static ConfigurationParse Read(string? path = null)
    {
        var at = path ?? DefaultPath();

        if (!File.Exists(at))
        {
            return new ConfigurationParse();
        }

        try
        {
            return Parse(File.ReadAllText(at));
        }
        catch (IOException unreadable)
        {
            return new ConfigurationParse
            {
                Diagnosis = $"'{at}' could not be read: {unreadable.Message}",
            };
        }
        catch (UnauthorizedAccessException unreadable)
        {
            return new ConfigurationParse
            {
                Diagnosis = $"'{at}' could not be read: {unreadable.Message}",
            };
        }
    }

    /// <summary>Writes the configuration, creating the directory if it is absent.</summary>
    public static void Write(Configuration configuration, string? path = null)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var at = path ?? DefaultPath();

        if (Path.GetDirectoryName(at) is { Length: > 0 } directory)
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(at, Render(configuration));
    }

    /// <summary>The configuration as the bytes that go in the file.</summary>
    /// <remarks>
    /// <b>A file, not a fragment</b> — one trailing newline, and <c>'\n'</c>
    /// explicitly rather than the machine's own line ending, which is the rule
    /// <c>EnvelopeText</c> states for the other document a person edits.
    /// </remarks>
    public static string Render(Configuration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var json = JsonSerializer.Serialize(
            configuration, ConfigurationJsonContext.Default.Configuration);

        return json.ReplaceLineEndings("\n") + "\n";
    }

    /// <summary>Text into a configuration, or the sentence that says why not.</summary>
    public static ConfigurationParse Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        Configuration? read;

        try
        {
            read = JsonSerializer.Deserialize(
                text, ConfigurationJsonContext.Default.Configuration);
        }
        catch (JsonException broken)
        {
            // THE LIBRARY'S OWN SENTENCE, kept. It names the line and column,
            // which is what somebody hand-editing wants, and re-wording it here
            // would lose that to say less.
            return new ConfigurationParse { Diagnosis = Said(broken) };
        }

        if (read is null)
        {
            return new ConfigurationParse
            {
                Diagnosis = "This file holds no configuration. An empty document is `{}`; "
                          + "a file with nothing in it at all is more likely a write that "
                          + "did not finish.",
            };
        }

        return Configuration.Validate(read) is { } refused
            ? new ConfigurationParse { Diagnosis = refused }
            : new ConfigurationParse { Configuration = read };
    }

    /// <summary>
    /// What a broken document says, with the unmapped-member case reworded.
    /// </summary>
    /// <remarks>
    /// The library says <i>"The JSON property 'x' could not be mapped to any
    /// .NET member"</i>, which names the offending key — the half that matters —
    /// in the vocabulary of the serializer rather than of the file. The key is
    /// kept and the rest is said in the reader's terms.
    /// </remarks>
    private static string Said(JsonException broken) =>
        broken.Path is { Length: > 0 } path && broken.Message.Contains(
            "could not be mapped", StringComparison.Ordinal)
            ? $"'{path.TrimStart('$', '.')}' is not a setting this version knows. A key "
            + "nothing reads would sit in the file looking load-bearing and do nothing, so "
            + "it is refused rather than ignored."
            : broken.Message;
}

/// <summary>
/// Source-generated, because everything here must stay AOT-publishable.
/// </summary>
/// <remarks>
/// <para>
/// <b><c>WhenWritingNull</c>, so absent stays absent.</b> A key nobody set is
/// not written, which is what keeps it from coming back as an empty string and
/// being read as a declaration where the author left silence.
/// </para>
/// <para>
/// <b><c>Disallow</c>, so a key nobody declared is refused.</b> The default is
/// to skip it, which is the accepted-and-ignored shape this product refuses
/// everywhere else — a fact outside the pinned vocabulary, a document declaring
/// a provenance, a knob on the wrong destination kind.
/// </para>
/// </remarks>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.KebabCaseLower,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow)]
[JsonSerializable(typeof(Configuration))]
internal sealed partial class ConfigurationJsonContext : JsonSerializerContext;
