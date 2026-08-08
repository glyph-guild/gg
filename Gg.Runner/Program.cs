using System.Reflection;

var version = typeof(Gg.Contracts.ProtocolHello).Assembly
    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0";

if (args is ["--version"] or ["-v"])
{
    Console.WriteLine($"gg-runner {version}");
    return 0;
}

Console.WriteLine("gg-runner: the work loop is not built yet. Try --version.");
return 0;
