using System.Text;
using Bam.Generators.TypeScript;
using Bam.Generators.TypeScript.Tests.Fixtures;
using Bam.Test;

namespace Bam.Generators.TypeScript.Tests.Unit;

/// <summary>Unit coverage for the TypeScript target (bam.generators#37).</summary>
[UnitTestMenu("TypeScript contract generator should", Selector = "tscg")]
public class TypeScriptContractGeneratorShould
{
    [UnitTest]
    public void MapEveryShapeOfAContractRecord()
    {
        When.A<TypeScriptContractGenerator>("generates NodeSnapshot",
            () => new TypeScriptContractGenerator(),
            generator => Lines(generator.GetSource(new[] { typeof(NodeSnapshot) })))
            .TheTest
            .ShouldPass<List<string>>((because, lines) =>
            {
                string[] expected =
                {
                    "export interface NodeSnapshot {",
                    "  readonly id: NodeId;",
                    "  readonly name: string;",
                    "  readonly description: string | null;",
                    "  readonly port: number;",
                    "  readonly count: number;",
                    "  readonly ratio: number;",
                    "  readonly price: number;",
                    "  readonly active: boolean;",
                    "  readonly retries: number | null;",
                    "  readonly observedAt: string;",
                    "  readonly updatedAt: string;",
                    "  readonly instance: string;",
                    "  readonly health: NodeHealth;",
                    "  readonly previousHealth: NodeHealth | null;",
                    "  readonly tags: readonly string[];",
                    "  readonly edges: readonly NodeEdge[];",
                    "  readonly aliases: readonly string[];",
                    "  readonly notes: readonly (string | null)[];",
                    "  readonly metrics: Readonly<Record<string, number>>;",
                    "  readonly peers: Readonly<Record<string, NodeEdge | null>>;",
                    "  readonly parent: NodeEdge | null;",
                    "  readonly thumbprint: string;",
                    "  readonly \"x-trace\": string;",
                    "}",
                };
                int start = lines.IndexOf(expected[0]);
                because.ItsTrue("the interface is generated", start >= 0);
                List<string> body = start < 0 ? new List<string>() : lines.Skip(start).Take(expected.Length).ToList();
                for (int index = 0; index < expected.Length; index++)
                {
                    string actual = index < body.Count ? body[index] : "(missing)";
                    because.ItsTrue($"line {index} is {expected[index]} (was {actual})", actual == expected[index]);
                }
                because.ItsTrue("a [JsonIgnore] property is left out", !lines.Any(line => line.Contains("ignored", StringComparison.OrdinalIgnoreCase)));
                because.ItsTrue("a type reached through a property is generated", lines.Contains("export interface NodeEdge {"));
                because.ItsTrue("with its own members", lines.Contains("  readonly from: NodeId;") && lines.Contains("  readonly to: NodeId;"));
            })
            .SoBeHappy()
            .UnlessItFailed();
    }

    [UnitTest]
    public void WriteEnumsAsUnionsAndTypedIdsAsBrands()
    {
        When.A<TypeScriptContractGenerator>("generates NodeHealth and NodeId",
            () => new TypeScriptContractGenerator(),
            generator => new NamingOutcome(
                Lines(generator.GetSource(new[] { typeof(NodeHealth), typeof(NodeId) })),
                Lines(new TypeScriptContractGenerator(new TypeScriptGenerationOptions { PropertyNaming = TypeScriptNaming.AsDeclared, EnumMemberNaming = TypeScriptNaming.CamelCase }, new HandlebarsTypeScriptCodeWriter())
                    .GetSource(new[] { typeof(NodeEdge), typeof(NodeHealth) }))))
            .TheTest
            .ShouldPass<NamingOutcome>((because, outcome) =>
            {
                because.ItsTrue("an enum is a string union, renamed members included", outcome.Defaults.Contains("export type NodeHealth = \"Healthy\" | \"Degraded\" | \"not-reporting\";"));
                because.ItsTrue("a typed id is a branded string", outcome.Defaults.Contains("export type NodeId = string & { readonly __brand: \"NodeId\" };"));
                because.ItsTrue("property naming can be as declared", outcome.Options.Contains("  readonly From: NodeId;"));
                because.ItsTrue("enum naming can be camelCase, and a rename still wins", outcome.Options.Contains("export type NodeHealth = \"healthy\" | \"degraded\" | \"not-reporting\";"));
            })
            .SoBeHappy()
            .UnlessItFailed();
    }

    [UnitTest]
    public void GenerateTheSameBytesInAStableOrder()
    {
        When.A<TypeScriptContractGenerator>("generates the fixtures twice, with the roots in different orders",
            () => new TypeScriptContractGenerator(),
            generator =>
            {
                string first = generator.GetSource(new[] { typeof(NodeSnapshot), typeof(DerivedMessage), typeof(TreeNode) });
                string second = new TypeScriptContractGenerator().GetSource(new[] { typeof(TreeNode), typeof(DerivedMessage), typeof(NodeSnapshot) });
                List<string> names = Lines(first).Where(line => line.StartsWith("export ", StringComparison.Ordinal)).Select(line => line.Split(' ')[2]).ToList();
                return new OrderOutcome(first, second, names, Lines(first));
            })
            .TheTest
            .ShouldPass<OrderOutcome>((because, outcome) =>
            {
                because.ItsTrue("generating twice gives byte-identical output", outcome.First == outcome.Second);
                because.ItsTrue($"declarations are sorted by name ({string.Join(", ", outcome.Names)})", outcome.Names.SequenceEqual(outcome.Names.OrderBy(name => name, StringComparer.Ordinal)));
                because.ItsTrue("the first line is the generated header", outcome.Lines[0] == "// <auto-generated/>");
                because.ItsTrue("lines end in \\n only", !outcome.First.Contains('\r'));
                because.ItsTrue("base-class members come first", outcome.Lines.IndexOf("  readonly sequence: number;") < outcome.Lines.IndexOf("  readonly payload: string;") && outcome.Lines.Contains("  readonly payload: string;"));
                because.ItsTrue("a self-referencing type terminates", outcome.Lines.Contains("  readonly children: readonly TreeNode[];"));
            })
            .SoBeHappy()
            .UnlessItFailed();
    }

    [UnitTest]
    public void WriteTheFileAConsoleToolAsksFor()
    {
        When.A<string>("writes the module to a file twice",
            () => Path.Combine(Path.GetTempPath(), "bam-ts-" + Guid.NewGuid().ToString("N"), "contracts", "scope.ts"),
            path =>
            {
                try
                {
                    TypeScriptContractGenerator generator = new TypeScriptContractGenerator();
                    generator.WriteFile(path, new[] { typeof(NodeSnapshot) });
                    byte[] first = File.ReadAllBytes(path);
                    generator.WriteFile(path, new[] { typeof(NodeSnapshot) });
                    byte[] second = File.ReadAllBytes(path);
                    string assemblySource = generator.GetSource(typeof(NodeSnapshot).Assembly, type => type == typeof(NodeSnapshot));
                    return new FileOutcome(first, second, Encoding.UTF8.GetString(first) == assemblySource);
                }
                finally
                {
                    string root = Path.GetDirectoryName(Path.GetDirectoryName(path)!)!;
                    if (Directory.Exists(root))
                    {
                        Directory.Delete(root, true);
                    }
                }
            })
            .TheTest
            .ShouldPass<FileOutcome>((because, outcome) =>
            {
                because.ItsTrue("the directory is created and the file written", outcome.First.Length > 0);
                because.ItsTrue("writing twice gives the same bytes", outcome.First.AsSpan().SequenceEqual(outcome.Second));
                because.ItsTrue("there is no byte-order mark", outcome.First[0] == (byte)'/');
                because.ItsTrue("selecting from the assembly gives the same module", outcome.SameAsAssembly);
            })
            .SoBeHappy()
            .UnlessItFailed();
    }

    [UnitTest]
    public void RefuseWhatTypeScriptCannotExpress()
    {
        When.A<TypeScriptContractGenerator>("generates contracts the target can't express",
            () => new TypeScriptContractGenerator(),
            generator => new RefusalOutcome(
                Refusal(() => generator.GetSource(new[] { typeof(Untyped) })),
                Refusal(() => generator.GetSource(new[] { typeof(IntKeyed) })),
                Refusal(() => generator.GetSource(new[] { typeof(EdgePage) })),
                Refusal(() => generator.GetSource(new[] { typeof(NodeEdge), typeof(Clash) }))))
            .TheTest
            .ShouldPass<RefusalOutcome>((because, outcome) =>
            {
                because.ItsTrue($"object is refused, naming the property ({outcome.Untyped})", outcome.Untyped.Contains("reached from Bam.Generators.TypeScript.Tests.Fixtures.Untyped.Anything", StringComparison.Ordinal) && outcome.Untyped.Contains("no TypeScript mapping", StringComparison.Ordinal));
                because.ItsTrue($"a non-string key is refused ({outcome.IntKeyed})", outcome.IntKeyed.Contains("keyed by Int32", StringComparison.Ordinal));
                because.ItsTrue($"a generic contract is refused ({outcome.Generic})", outcome.Generic.Contains("is generic", StringComparison.Ordinal));
                because.ItsTrue($"two types with one name are refused ({outcome.Clash})", outcome.Clash.Contains("would both be generated as NodeEdge", StringComparison.Ordinal));
            })
            .SoBeHappy()
            .UnlessItFailed();
    }

    private static List<string> Lines(string source) => source.Split('\n').ToList();

    private static string Refusal(Func<string> generate)
    {
        try
        {
            generate();
            return string.Empty;
        }
        catch (TypeScriptGenerationException exception)
        {
            return exception.Message;
        }
    }

    private sealed record NamingOutcome(List<string> Defaults, List<string> Options);

    private sealed record OrderOutcome(string First, string Second, List<string> Names, List<string> Lines);

    private sealed record FileOutcome(byte[] First, byte[] Second, bool SameAsAssembly);

    private sealed record RefusalOutcome(string Untyped, string IntKeyed, string Generic, string Clash);
}
