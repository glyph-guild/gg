namespace Gg.Cli.Tests;

/// <summary>
/// Messages rewritten under the voice guide's rule 4, "Say it directly": each
/// states the consequence or the next step rather than a riddle, an instruction
/// told in the third person, or a chain of clauses.
/// </summary>
/// <remarks>
/// Read from source, because many of these are thrown or printed where no verb
/// result reaches them. Each new fragment sits inside one string literal, so
/// line wrapping in the source cannot move it. The old wording is held absent so
/// it cannot come back beside the new.
/// </remarks>
public class MessagesSayItDirectlyTests
{
    public static IEnumerable<Func<(string File, string Says, string NoLongerSays)>> Rewrites()
    {
        // The first-run path.
        yield return () => ("deploy/install.sh",
            "A runner is always installed as gg, because its service runs $service_binary.",
            "and a runner's service runs $service_binary. Nothing was installed. Remove");
        yield return () => ("Gg.Cli/ServiceInstaller.cs",
            "--agent-binary needs one, because the ",
            "whoever typed");
        yield return () => ("Gg.Cli/ServiceInstaller.cs",
            "There is no file at '",
            "would make this machine hold");
        yield return () => ("Gg.Cli/ServiceInstaller.cs",
            "install gg itself.",
            "writes a byte of gg: install.sh");
        yield return () => ("Gg.Cli/Program.cs",
            "did not return the plan that says what this flight needs.",
            "sent back no plan");
        yield return () => ("Gg.Cli/Program.cs",
            "cannot be renewed. Run `gg login` ",
            "a person signs in once");
        yield return () => ("Gg.Client/VerbResult.cs",
            "Anyone who opens it can join this organisation. Send it as privately as a password.",
            "joins this organisation, so send it");
        yield return () => ("Gg.Client/VerbResult.cs",
            "Never pass it as an argument, because a process list would show it.",
            "Shown once - the control plane keeps only its hash.");
        yield return () => ("Gg.Client/Doctor.cs",
            "ended without the agent's closing account",
            "takeover seeds are measurements only");

        // Common verbs.
        yield return () => ("Gg.Client/FlightCommands.cs",
            "A rejection needs a reason. The flight runs again with that reason. Say what should change.",
            "Rejecting needs a reason. The loop runs again");
        yield return () => ("Gg.Client/FlightCommands.cs",
            "Nothing was recorded. Run the command again with one of those.",
            "Rejecting a gate is not built yet");
        yield return () => ("Gg.Client/FlightCommands.cs",
            "The other endings are not decisions.",
            "belong to the board, the world, the clock and the rules");
        yield return () => ("Gg.Client/FlightCommands.cs",
            " with one of those, or with no obligation to see every one.",
            "is a different thing from one");
        yield return () => ("Gg.Client/FlightCommands.cs",
            "To apply one, run gg envelope apply <file>.",
            "is where the rules come from");
        yield return () => ("Gg.Client/FlightCommands.cs",
            "not a work kind. The rules in force are composed ",
            "that is what a flight has");
        yield return () => ("Gg.Client/FlightCommands.cs",
            "declared under root, or under the work kind its file extends.",
            "declare them yourself");
        yield return () => ("Gg.Client/VerbResult.cs",
            "Trusting a machine adds a holder and removes none.",
            "The person who ran this is still one of them");
        yield return () => ("Gg.Client/VerbResult.cs",
            " now waits on ",
            "so it rides flight ");
        yield return () => ("Gg.Client/VerbResult.cs",
            "It cannot add any more machines.",
            "it enrolls nothing more");
        yield return () => ("Gg.Client/VerbResult.cs",
            "To bring that machine back, run gg runner up on it.",
            "Bringing that machine back is gg runner up.");
        yield return () => ("Gg.Client/ControlPlaneClient.cs",
            "decides who may reach it, because reaching a ",
            "Reaching a machine means reading what it is doing");
        yield return () => ("Gg.Client/ConsoleChannel.cs",
            "Ask the administrator of this network",
            "whoever runs this network");

        // The console.
        yield return () => ("Gg.Console/ConsoleGround.cs",
            "The reason is the only record of why it was not done.",
            "the only thing that survives");
        yield return () => ("Gg.Console/ConsoleBringUp.cs",
            "The console cannot do this.",
            "the machine's to have");
        yield return () => ("Gg.Console/ConsoleBringUp.cs",
            "The console cannot open it.",
            "nothing a console does opens one");
        yield return () => ("Gg.Console/ConsoleRetire.cs",
            "Until that gate opens, the name still governs every flight that starts.",
            "Each of these rides a gate");

        // The long tail.
        yield return () => ("Gg.Client/VerbResult.cs",
            "To chart one, run gg environment chart <name>.",
            "the chart is what it is refused against");
        yield return () => ("Gg.Client/VerbResult.cs",
            "no action is taken toward this pool",
            "nothing outward is decided");
    }

    [Test]
    [MethodDataSource(nameof(Rewrites))]
    public async Task The_message_says_it_directly(string file, string says, string noLongerSays)
    {
        var source = Read(file);

        await Assert.That(source).Contains(says)
            .Because($"{file} states the consequence or the next step plainly.");
        await Assert.That(source).DoesNotContain(noLongerSays)
            .Because($"{file} no longer carries the wording it replaced.");
    }

    private static string Read(string relative)
    {
        var here = new DirectoryInfo(AppContext.BaseDirectory);
        while (here is not null && !File.Exists(Path.Combine(here.FullName, "Gg.sln")))
        {
            here = here.Parent;
        }

        return File.ReadAllText(Path.Combine(
            here?.FullName ?? throw new InvalidOperationException("Gg.sln not found"),
            relative));
    }
}
