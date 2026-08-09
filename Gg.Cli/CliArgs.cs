namespace Gg.Cli;

public abstract record CliAction
{
    public sealed record LaunchConsole : CliAction;

    public sealed record PrintVersion : CliAction;

    public sealed record RunnerUp : CliAction;

    public sealed record RunnerServe : CliAction;

    public sealed record Unknown(string Message) : CliAction;
}

public static class CliArgs
{
    public static CliAction Parse(string[] args) => args switch
    {
        [] => new CliAction.LaunchConsole(),
        ["--version"] or ["-v"] => new CliAction.PrintVersion(),
        ["runner", "up"] => new CliAction.RunnerUp(),
        ["runner", "serve"] => new CliAction.RunnerServe(),
        _ => new CliAction.Unknown("usage: gg | gg --version | gg runner up"),
    };
}
