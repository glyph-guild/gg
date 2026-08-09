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
    public static CliAction Parse(string[] args)
    {
        throw new NotImplementedException();
    }
}
