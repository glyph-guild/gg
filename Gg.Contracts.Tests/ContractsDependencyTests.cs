using System.Xml.Linq;

namespace Gg.Contracts.Tests;

public class ContractsDependencyTests
{
    [Test]
    public async Task GgContractsHasZeroPackageReferences()
    {
        var csproj = XDocument.Load(FindContractsCsproj());
        var packageReferences = csproj
            .Descendants()
            .Where(e => e.Name.LocalName == "PackageReference")
            .Select(e => e.Attribute("Include")?.Value ?? "(unnamed)")
            .ToList();

        // AMENDED IN SLICE FIFTEEN, from zero to exactly one, and the entry is
        // named rather than counted. This project carried none because it is
        // the public artifact a customer audits and should not inherit anyone
        // else's framework - and that reason has not stopped being true.
        //
        // ADR-0018 § 5 makes the control plane parse a repository's narrowing
        // itself (a runner reporting "no narrowing here" is a silent weakening,
        // and Article IX admits no exception). The control plane references
        // this package and nothing else, so the parser had to be reachable from
        // here. A second published asset and a parser-only third package were
        // both available and both declined.
        //
        // Named rather than counted, because "at most one" would let the next
        // one in by swapping it for something else. Adding a second still
        // fails, and so does changing which one it is.
        await Assert.That(packageReferences).IsEquivalentTo((string[])["YamlDotNet"])
            .Because("this package is what a customer audits. It carries the grammar the "
                   + "parser needs and nothing else, and a second entry is a decision "
                   + "somebody has to make deliberately. Found: "
                   + string.Join(", ", packageReferences));
    }

    private static string FindContractsCsproj()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Gg.sln")))
        {
            dir = dir.Parent;
        }
        return dir is null
            ? throw new InvalidOperationException("Gg.sln not found above " + AppContext.BaseDirectory)
            : Path.Combine(dir.FullName, "Gg.Contracts", "Gg.Contracts.csproj");
    }
}
