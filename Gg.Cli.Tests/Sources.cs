using System.Text.RegularExpressions;

namespace Gg.Cli.Tests;

/// <summary>
/// Production source, for the walks that ask what is wired rather than what exists.
/// </summary>
/// <remarks>
/// <b>Source and not reflection</b>, for the reason <c>ConsoleSource</c> gives one
/// project over: what reflection answers well is <i>what exists</i>; what the
/// source answers is <i>what is read</i>. A variable is read by a string in a
/// call, and no type carries it.
/// </remarks>
internal static partial class Sources
{
    /// <summary>Variables that are read in production and belong on no page.</summary>
    /// <remarks>
    /// <b>Each carries its reason, so a future addition is argued rather than
    /// quietly appended</b> — <c>EveryVerbIsDiscoverableTests</c>' rule for the
    /// same shape. Every one of these is gg writing into a child's environment,
    /// never a person configuring a machine: putting them on a page of things
    /// somebody may set would be inviting them to set one.
    /// </remarks>
    internal static IReadOnlyDictionary<string, string> NotOnThePage { get; } =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["GG_INTENT_PATH"] =
                "the console writes it into the tool server's own environment, so the "
              + "server has somewhere to record a composed intent. A person setting it "
              + "would be choosing a path gg is about to overwrite.",

            ["GG_MEMBER_NONCE"] =
                "a pool maintainer writes it into a member container, single use. A "
              + "person cannot mint one and a stale one is refused.",

            ["GG_MEMBER_CONTROL_PLANE"] =
                "a pool maintainer writes it into a member container when the address "
              + "the member must use differs from the host's own.",

            ["GG_IMAGE_DIGEST"] =
                "a pool maintainer writes it into a member container so the member can "
              + "report which image it is. It is measured, not chosen.",
        };

    /// <summary>Every `GG_*` name that appears anywhere in production source.</summary>
    /// <remarks>
    /// <b>A superset, deliberately.</b> It matches a doc comment as readily as a
    /// call, so a variable named only in prose is reported too — and
    /// over-reporting costs an exemption with a reason, while under-reporting
    /// costs a variable nobody can find. That is the trade
    /// <c>AbsentCollectionsSurviveTheWireTests</c> makes for the same kind of
    /// sweep.
    /// </remarks>
    internal static IReadOnlyList<string> EveryGgVariable()
    {
        var found = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var project in (string[])
                 ["Gg.Cli", "Gg.Client", "Gg.Console", "Gg.Contracts", "Gg.Local", "Gg.Runner"])
        {
            var at = Path.Combine(Root(), project);

            if (!Directory.Exists(at))
            {
                throw new InvalidOperationException(
                    $"'{at}' is not there, so this walk would scan nothing and pass. That "
                  + "is a broken scan, never a clean tree.");
            }

            foreach (var file in Directory.EnumerateFiles(at, "*.cs", SearchOption.AllDirectories))
            {
                if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
                        StringComparison.Ordinal)
                 || file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}",
                        StringComparison.Ordinal))
                {
                    continue;
                }

                foreach (Match match in Variable().Matches(File.ReadAllText(file)))
                {
                    found.Add(match.Value);
                }
            }
        }

        return [.. found];
    }

    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Gg.sln")))
        {
            directory = directory.Parent;
        }

        return (directory ?? throw new InvalidOperationException(
            "Gg.sln not found above " + AppContext.BaseDirectory)).FullName;
    }

    [GeneratedRegex(@"\bGG_[A-Z0-9_]+\b")]
    private static partial Regex Variable();
}
