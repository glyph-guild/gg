using Gg.Local;

namespace Gg.Cli;

/// <summary>
/// The configuration on this machine, read once per boot.
/// </summary>
/// <remarks>
/// <para>
/// <b>Once, because the root touches it from a dozen places</b> — the control
/// plane address, the doctor's picture of the machine, the labels, the hold, the
/// sessions the console is handed. Reading the file at each would be a dozen
/// opens and, worse, a dozen chances to be handed a different answer part-way
/// through one operation.
/// </para>
/// <para>
/// <b>PER BOOT AND NOT PER PROCESS, because one process here writes this file
/// and keeps running.</b> A verb reads the file and exits in milliseconds, and
/// for that one "read once" and "read once per process" are the same sentence.
/// The console is neither: it runs for hours, it is the surface that sets the
/// airspace path and edits the configuration, and it was answering afterwards
/// from the copy it took at boot — so a path somebody had just set came back as
/// the one it replaced, and pull and apply went on acting on the old tree.
/// <c>LocalFacts</c> is what says the boot is over, because that is the one
/// function boot and every refresh both apply, and everything this file
/// answers is folded there.
/// </para>
/// <para>
/// <b>A broken file is said out loud and then stepped over.</b> Silence would
/// leave a person with a document they believe is in force and a machine running
/// on defaults — the exact confusion the source column exists to prevent. Halting
/// would be worse: <c>gg config validate</c> is the verb that explains the
/// problem, and it would be unreachable behind the problem it explains.
/// </para>
/// </remarks>
internal static class InForce
{
    private static Configuration? _read;

    /// <summary>Whether <see cref="_read"/> holds this boot's answer.</summary>
    private static bool _have;

    /// <summary>
    /// Whether a broken file has already been said out loud.
    /// </summary>
    /// <remarks>
    /// <b>A SECOND FLAG, because what is cached and what has been said are
    /// different facts.</b> One flag for both would make every refresh re-print
    /// the diagnosis — and a refresh runs with Terminal.Gui torn down, so that
    /// line lands across the screen it is about to rebuild. Said once for the
    /// life of the process, re-read as often as the file is rewritten.
    /// </remarks>
    private static bool _reported;

    /// <summary>What the file says, or null when there is none or it is broken.</summary>
    internal static Configuration? Configuration
    {
        get
        {
            if (_have)
            {
                return _read;
            }

            _have = true;
            var parse = ConfigurationFile.Read();

            if (parse.Diagnosis is { } refused && !_reported)
            {
                _reported = true;

                // STDERR, so it cannot land in the middle of a --json document
                // somebody is piping. The same split every refusal here uses.
                System.Console.Error.WriteLine(
                    $"gg: {ConfigurationFile.DefaultPath()} was not read, so this run uses "
                  + $"the environment and the built-in defaults.{System.Environment.NewLine}"
                  + $"gg: {refused}");
            }

            _read = parse.Configuration;
            return _read;
        }
    }

    /// <summary>Drops this boot's answer, so the next read opens the file.</summary>
    /// <remarks>
    /// <b>Called where a boot is, not where a write is.</b> Three ports write
    /// this file — the airspace path, the configuration editor, and taking
    /// what the control plane offers — and three call sites to remember is the
    /// shape that goes stale the first time somebody adds a fourth.
    /// <c>LocalFacts</c> runs at boot and on every reload and is the only place
    /// the model learns anything this file says, so telling it there covers
    /// every write by construction.
    /// </remarks>
    internal static void Forget() => _have = false;
}
