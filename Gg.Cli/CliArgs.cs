namespace Gg.Cli;

public abstract record CliAction
{
    /// <summary>
    /// A verb that produces a structured result, and can print it either way.
    /// </summary>
    /// <remarks>
    /// An interface rather than a field on the base record, so a verb that
    /// produces no result - <c>login</c>, <c>runner up</c> - cannot be handed a
    /// <c>--json</c> that would do nothing.
    /// </remarks>
    public interface IEmitsResult
    {
        /// <summary>Print the result as JSON rather than rendering it.</summary>
        bool Json { get; }
    }

    public sealed record LaunchConsole : CliAction;

    public sealed record PrintVersion : CliAction;

    public sealed record Login : CliAction;

    public sealed record Logout : CliAction;

    public sealed record WhoAmI : CliAction;

    public sealed record RunnerUp : CliAction;

    public sealed record RunnerServe : CliAction;

    /// <summary>Opens a flight. Exactly one of <see cref="Text"/> and <see cref="Uri"/>.</summary>
    public sealed record Fly(string? Text, string? Uri, bool Json) : CliAction, IEmitsResult;

    public sealed record Flights(bool Json) : CliAction, IEmitsResult;

    public sealed record Show(string Reference, bool Json) : CliAction, IEmitsResult;

    public sealed record Log(string Reference, bool Json) : CliAction, IEmitsResult;

    public sealed record Runners(bool Json) : CliAction, IEmitsResult;

    public sealed record Doctor(bool Json) : CliAction, IEmitsResult;

    public sealed record Unknown(string Message) : CliAction;
}

public static class CliArgs
{
    /// <summary>
    /// What gg actually does today.
    /// </summary>
    /// <remarks>
    /// A usage string is a promise. <c>credential add</c> and <c>bundle</c> are
    /// deliberately absent: their features do not exist, and both listing them
    /// and stubbing them tell a person something gg cannot deliver. An
    /// unimplemented verb that reports success is Article XI's failure mode
    /// wearing a CLI - the flight fails much later, for a reason nobody can
    /// trace back to here.
    /// </remarks>
    private static readonly string[] Verbs =
    [
        "gg                          the console",
        "gg fly <text>|--uri <uri>   open a flight",
        "gg flights                  list flights",
        "gg show <flight>            one flight, by GG-42 or by id",
        "gg log <flight>             a flight's log",
        "gg runners                  the runners this tenant has",
        "gg doctor                   check what gg needs to work",
        "gg login | logout | whoami  identity",
        "gg runner up                take work on this machine",
        "gg version                  binary, protocol and fact vocabulary",
    ];

    public static CliAction Parse(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        // --json is position-independent, because a person will type it in
        // both places and being told off for one of them is not helpful.
        var json = args.Contains("--json", StringComparer.Ordinal);
        var rest = args.Where(a => a != "--json").ToArray();

        return rest switch
        {
            [] => new CliAction.LaunchConsole(),
            ["--version"] or ["-v"] or ["version"] => new CliAction.PrintVersion(),
            ["login"] => new CliAction.Login(),
            ["logout"] => new CliAction.Logout(),
            ["whoami"] => new CliAction.WhoAmI(),
            ["runner", "up"] => new CliAction.RunnerUp(),
            ["runner", "serve"] => new CliAction.RunnerServe(),

            ["flights"] => new CliAction.Flights(json),
            ["runners"] => new CliAction.Runners(json),
            ["doctor"] => new CliAction.Doctor(json),

            ["show", var reference] => new CliAction.Show(reference, json),
            ["log", var reference] => new CliAction.Log(reference, json),

            // One payload, never both. Which one wins would otherwise be
            // decided by whoever wrote this method.
            ["fly", "--uri", var uri] => new CliAction.Fly(null, uri, json),
            ["fly", var text] => new CliAction.Fly(text, null, json),
            ["fly"] => Unknown("gg fly needs something to act on: some text, or --uri <uri>."),
            ["fly", ..] => Unknown(
                "gg fly takes either some text or --uri, and this has both. "
              + "An intent that says two things says nothing."),

            ["show"] => Unknown("gg show needs a flight: gg show GG-42, or the id."),
            ["log"] => Unknown("gg log needs a flight: gg log GG-42, or the id."),

            [var verb, ..] => Unknown($"'{verb}' is not a gg command."),
            _ => Unknown("unrecognised arguments."),
        };
    }

    /// <summary>A refusal that says what was wrong AND what is available.</summary>
    /// <remarks>
    /// Naming what was typed matters: "unknown command" alone makes a typo in a
    /// script something you find by bisecting.
    /// </remarks>
    private static CliAction.Unknown Unknown(string problem) =>
        new(problem + Environment.NewLine + Environment.NewLine
          + "usage:" + Environment.NewLine
          + string.Join(Environment.NewLine, Verbs.Select(v => "  " + v)));
}
