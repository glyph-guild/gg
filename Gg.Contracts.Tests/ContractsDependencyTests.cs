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

        await Assert.That(packageReferences).IsEmpty()
            .Because("Gg.Contracts is the public artifact a customer audits; it must not inherit anyone else's framework");
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
