using System.Globalization;

namespace Gg.Console;

/// <summary>
/// Where the runner on this machine says it is running.
/// </summary>
/// <remarks>
/// <para>
/// <b>A file, because a runner outlives the console that started it.</b> A
/// handle answers "did I start this" and the question is "is one running here"
/// - and a few minutes after you start one and close the window, the answer to
/// the first is no and the second is yes. The runner store beside it is a file
/// for the same reason.
/// </para>
/// <para>
/// <b>Beside the log rather than beside the credentials.</b> This is runtime
/// state, true only while a process is up; the config directory holds things
/// that survive a reboot and one of them is a token.
/// </para>
/// <para>
/// <b>It never throws.</b> It is read on a boot path and written by a runner
/// starting up, and neither is a place to fail over a file - a pid nobody can
/// read is the same answer as no runner, which is the safe one.
/// </para>
/// </remarks>
public sealed class RunnerPidFile(string path)
{
    public string Path => path;

    /// <summary>The pid written there, or null if there is none to be had.</summary>
    public int? Read()
    {
        try
        {
            return File.Exists(path)
                && int.TryParse(
                    File.ReadAllText(path).Trim(),
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out var pid)
                && pid > 0
                    ? pid
                    : null;
        }
        catch (Exception failure) when (failure is IOException
                                            or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public void Write(int pid)
    {
        try
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
            File.WriteAllText(path, pid.ToString(CultureInfo.InvariantCulture));
        }
        catch (Exception failure) when (failure is IOException
                                            or UnauthorizedAccessException)
        {
            // A runner that cannot say where it is still runs. The console then
            // reports what the fleet knows, which is the answer it had before
            // this file existed.
        }
    }

    public void Clear()
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception failure) when (failure is IOException
                                            or UnauthorizedAccessException)
        {
        }
    }
}
