namespace Gg.Cli;

public abstract record CliAction
{
    public sealed record LaunchConsole : CliAction;

    public sealed record PrintVersion : CliAction;

    public sealed record Login : CliAction;

    public sealed record Logout : CliAction;

    public sealed record WhoAmI : CliAction;

    public sealed record RunnerUp : CliAction;

    public sealed record RunnerServe : CliAction;

    public sealed record Unknown(string Message) : CliAction;
}

public static class CliArgs
{
    public static CliAction Parse(string[] args) => args switch
    {
        [] => new CliAction.LaunchConsole(),
        ["--version"] or ["-v"] or ["version"] => new CliAction.PrintVersion(),
        ["login"] => new CliAction.Login(),
        ["logout"] => new CliAction.Logout(),
        ["whoami"] => new CliAction.WhoAmI(),
        ["runner", "up"] => new CliAction.RunnerUp(),
        ["runner", "serve"] => new CliAction.RunnerServe(),
        _ => new CliAction.Unknown("usage: gg | gg version | gg login | gg logout | gg whoami | gg runner up"),
    };
}
