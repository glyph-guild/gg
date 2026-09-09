using Gg.Local;

namespace Gg.Cli;

/// <summary>
/// The configuration on this machine, read once.
/// </summary>
/// <remarks>
/// <para>
/// <b>Once, because the root touches it from a dozen places</b> — the control
/// plane address, the doctor's picture of the machine, the labels, the hold, the
/// sessions the console is handed. Reading the file at each would be a dozen
/// opens and, worse, a dozen chances to be handed a different answer if
/// somebody edited it mid-run.
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
    private static bool _tried;

    /// <summary>What the file says, or null when there is none or it is broken.</summary>
    internal static Configuration? Configuration
    {
        get
        {
            if (_tried)
            {
                return _read;
            }

            _tried = true;
            var parse = ConfigurationFile.Read();

            if (parse.Diagnosis is { } refused)
            {
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
}
