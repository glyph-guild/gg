// Emits console/src/generated (TypeScript types + Zod schemas) from the public
// surface of Gg.Contracts. Deterministic: stable ordering, LF line endings, no
// timestamps — CI diffs the output to prove the committed files are in sync.
using System.Reflection;
using System.Text;
using System.Text.Json;

var outDir = args.Length > 0
    ? args[0]
    : Path.Combine(FindRepoRoot(), "console", "src", "generated");

var contractTypes = typeof(Gg.Contracts.ProtocolHello).Assembly
    .GetExportedTypes()
    .Where(t => !t.IsAbstract && !t.IsInterface && !t.IsEnum)
    .OrderBy(t => t.Name, StringComparer.Ordinal)
    .ToList();

var sb = new StringBuilder();
sb.Append("// GENERATED from Gg.Contracts — do not edit.\n");
sb.Append("// Regenerate: dotnet run --project tools/Gg.ContractsGen\n");
sb.Append("import { z } from 'zod';\n");

foreach (var type in contractTypes)
{
    var schemaName = Camel(type.Name) + "Schema";
    sb.Append('\n');
    sb.Append($"export const {schemaName} = z.object({{\n");
    foreach (var property in type
        .GetProperties(BindingFlags.Public | BindingFlags.Instance)
        .OrderBy(p => p.MetadataToken))
    {
        sb.Append($"  {Camel(property.Name)}: {ZodFor(property.PropertyType, type, property)},\n");
    }
    sb.Append("});\n");
    sb.Append($"export type {type.Name} = z.infer<typeof {schemaName}>;\n");
}

Directory.CreateDirectory(outDir);
File.WriteAllText(Path.Combine(outDir, "contracts.ts"), sb.ToString());
File.WriteAllText(Path.Combine(outDir, "index.ts"),
    "// GENERATED from Gg.Contracts — do not edit.\nexport * from './contracts.js';\n");
Console.WriteLine($"Wrote {contractTypes.Count} contract type(s) to {outDir}");
return 0;

static string Camel(string name) => JsonNamingPolicy.CamelCase.ConvertName(name);

static string ZodFor(Type type, Type owner, PropertyInfo property) => type switch
{
    _ when type == typeof(string) => "z.string()",
    _ when type == typeof(int) || type == typeof(long) => "z.number().int()",
    _ when type == typeof(double) || type == typeof(decimal) => "z.number()",
    _ when type == typeof(bool) => "z.boolean()",
    _ when type == typeof(Guid) => "z.string().uuid()",
    _ when type == typeof(DateTimeOffset) => "z.string().datetime({ offset: true })",
    _ => throw new NotSupportedException(
        $"{owner.Name}.{property.Name}: no TypeScript mapping for {type}. Teach Gg.ContractsGen about it."),
};

static string FindRepoRoot()
{
    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Gg.sln")))
    {
        dir = dir.Parent;
    }
    return dir?.FullName ?? throw new InvalidOperationException("Gg.sln not found above " + AppContext.BaseDirectory);
}
