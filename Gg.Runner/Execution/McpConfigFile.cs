using System.Text;

namespace Gg.Runner.Execution;

/// <summary>
/// The MCP configuration an agent is launched with, as a file only this user can
/// read, rather than as an argument every user on the host can.
/// </summary>
/// <remarks>
/// <para>
/// <b>It carries secrets.</b> A tracker reader's credential and each external
/// server's resolved references are written into it, so it cannot be an
/// argument: <c>ps</c> shows those to everything on the host. It was one, and
/// the comment beside it said the opposite.
/// </para>
/// <para>
/// <b>Deleted as soon as the agent has started its servers</b>, which is the
/// first line it writes, and again when the run ends whatever happened. The
/// agent runs as this user and could read the file while it exists, so the
/// shorter it lives the less there is to read.
/// </para>
/// </remarks>
public static class McpConfigFile
{
    /// <summary>The flag whose value is the file's path.</summary>
    public const string Flag = "--mcp-config";

    /// <summary>Writes the configuration and returns where.</summary>
    public static string Write(string json, string? directory)
    {
        ArgumentNullException.ThrowIfNull(json);

        var folder = directory is { Length: > 0 } scratch && Directory.Exists(scratch)
            ? scratch
            : Path.GetTempPath();
        var path = Path.Combine(folder, $"gg-mcp-{Guid.NewGuid():N}.json");

        var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write };

        // OWNER-ONLY FROM THE MOMENT IT EXISTS. Created with the mode, rather than
        // created and then narrowed, so there is no instant when the file is
        // readable by anyone else.
        if (!OperatingSystem.IsWindows())
        {
            options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        }

        using (var stream = new FileStream(path, options))
        {
            stream.Write(Encoding.UTF8.GetBytes(json));
        }

        return path;
    }

    /// <summary>Deletes the file a launch's arguments name, if they name one.</summary>
    public static void Delete(IEnumerable<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        var list = arguments.ToList();
        var at = list.IndexOf(Flag);

        if (at < 0 || at + 1 >= list.Count)
        {
            return;
        }

        try
        {
            File.Delete(list[at + 1]);
        }
        catch (IOException)
        {
            // Already gone, or held for a moment; the run's end tries again.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
