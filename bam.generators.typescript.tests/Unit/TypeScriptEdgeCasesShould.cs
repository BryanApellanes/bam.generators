using System.Reflection;
using System.Reflection.Emit;
using Bam.Generators.TypeScript.Tests.Fixtures;
using Bam.Test;
using Systems.Contracts;

namespace Bam.Generators.TypeScript.Tests.Unit;

/// <summary>Where the serializer and a naive reflection walk disagree (bam.generators#41 review, round 1).</summary>
[UnitTestMenu("TypeScript edge cases should", Selector = "tsec")]
public class TypeScriptEdgeCasesShould
{
    [UnitTest]
    public void WriteWhatTheSerializerWritesForHiddenAndCollidingProperties()
    {
        When.A<TypeScriptContractGenerator>("generates a hidden property and two kinds of name collision",
            () => new TypeScriptContractGenerator(),
            generator => new CollisionOutcome(
                Lines(generator.GetSource(new[] { typeof(HidingDerived) })),
                Refusal(() => generator.GetSource(new[] { typeof(CaseCollision) })),
                Refusal(() => generator.GetSource(new[] { typeof(RenameCollision) }))))
            .TheTest
            .ShouldPass<CollisionOutcome>((because, outcome) =>
            {
                because.ItsTrue("the most derived declaration wins", outcome.Hidden.Contains("  readonly payload: string;"));
                because.ItsTrue("the hidden base type is not written", !outcome.Hidden.Contains("  readonly payload: number;"));
                because.ItsTrue("the hidden property keeps the base position", outcome.Hidden.IndexOf("  readonly payload: string;") < outcome.Hidden.IndexOf("  readonly sequence: number;"));
                because.ItsTrue($"Id and ID are refused ({outcome.CaseClash})", outcome.CaseClash.Contains("collides with another property", StringComparison.Ordinal));
                because.ItsTrue($"two renames to one name are refused ({outcome.RenameClash})", outcome.RenameClash.Contains("collides with another property", StringComparison.Ordinal));
            })
            .SoBeHappy()
            .UnlessItFailed();
    }

    [UnitTest]
    public void RefuseCustomCollectionsFlagsEnumsAndShadowingNames()
    {
        When.A<TypeScriptContractGenerator>("generates types it has to refuse",
            () => new TypeScriptContractGenerator(),
            generator => new EdgeRefusalOutcome(
                Refusal(() => generator.GetSource(new[] { typeof(HasTagList) })),
                Refusal(() => generator.GetSource(new[] { typeof(HasCountMap) })),
                Refusal(() => generator.GetSource(new[] { typeof(HasPerms) })),
                Refusal(() => generator.GetSource(new[] { typeof(HasRecord) })),
                Refusal(() => generator.GetSource(new[] { typeof(HasGrid) })),
                Refusal(() => generator.GetSource(new[] { typeof(HasInterface) })),
                Refusal(() => generator.GetSource(new[] { typeof(HasAbstract) })),
                Refusal(() => generator.GetSource(new[] { typeof(HasHashSet) }))))
            .TheTest
            .ShouldPass<EdgeRefusalOutcome>((because, outcome) =>
            {
                because.ItsTrue($"a List<string> subclass is refused ({outcome.CustomList})", outcome.CustomList.Contains("is the collection type", StringComparison.Ordinal) && outcome.CustomList.Contains("HasTagList.Tags", StringComparison.Ordinal));
                because.ItsTrue($"a Dictionary subclass is refused the same way ({outcome.CustomDictionary})", outcome.CustomDictionary.Contains("is the collection type", StringComparison.Ordinal));
                because.ItsTrue($"a [Flags] enum is refused ({outcome.Flags})", outcome.Flags.Contains("[Flags]", StringComparison.Ordinal));
                because.ItsTrue($"a type named Record is refused ({outcome.Shadowing})", outcome.Shadowing.Contains("shadows the TypeScript type", StringComparison.Ordinal));
                because.ItsTrue($"a multi-dimensional array is refused ({outcome.Grid})", outcome.Grid.Contains("multi-dimensional", StringComparison.Ordinal));
                because.ItsTrue($"an interface is refused ({outcome.Interface})", outcome.Interface.Contains("has no TypeScript mapping", StringComparison.Ordinal));
                because.ItsTrue($"an abstract type is refused ({outcome.Abstract})", outcome.Abstract.Contains("has no TypeScript mapping", StringComparison.Ordinal));
                because.ItsTrue($"HashSet<string> is refused as a collection, not as generic ({outcome.HashSet})", outcome.HashSet.Contains("is the collection type HashSet<System.String>", StringComparison.Ordinal));
            })
            .SoBeHappy()
            .UnlessItFailed();
    }

    [UnitTest]
    public void MarkMembersTheSerializerCanLeaveOutAsOptional()
    {
        When.A<TypeScriptContractGenerator>("generates conditionally ignored members",
            () => new TypeScriptContractGenerator(),
            generator => Lines(generator.GetSource(new[] { typeof(Sparse) })))
            .TheTest
            .ShouldPass<List<string>>((because, lines) =>
            {
                because.ItsTrue("WhenWritingNull is optional", lines.Contains("  readonly description?: string | null;"));
                because.ItsTrue("WhenWritingDefault is optional", lines.Contains("  readonly count?: number;"));
                because.ItsTrue("Never is required", lines.Contains("  readonly name: string;"));
            })
            .SoBeHappy()
            .UnlessItFailed();
    }

    [UnitTest]
    public void MapTheShapesBeyondTheIssuesList()
    {
        When.A<TypeScriptContractGenerator>("generates MoreShapes, a Systems.* contract and a typed id with an ignored helper",
            () => new TypeScriptContractGenerator(),
            generator => Lines(generator.GetSource(new[] { typeof(MoreShapes), typeof(Reading), typeof(TaggedId) })))
            .TheTest
            .ShouldPass<List<string>>((because, lines) =>
            {
                string[] expected =
                {
                    "export interface MoreShapes {",
                    "  readonly day: string;",
                    "  readonly at: string;",
                    "  readonly took: string;",
                    "  readonly link: string;",
                    "  readonly initial: string;",
                    "  readonly a: readonly string[];",
                    "  readonly b: readonly string[];",
                    "  readonly c: readonly string[];",
                    "  readonly d: readonly string[];",
                    "  readonly e: Readonly<Record<string, number>>;",
                    "  readonly f: Readonly<Record<string, number>>;",
                    "  readonly grid: readonly (readonly number[])[];",
                    "  readonly nested: Outer_Inner;",
                    "  readonly empty: Nothing;",
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
                because.ItsTrue("a nested type is named Outer_Inner", lines.Contains("export interface Outer_Inner {"));
                because.ItsTrue("an enum with no members is never", lines.Contains("export type Nothing = never;"));
                because.ItsTrue("a Systems.* namespace is not mistaken for System", lines.Contains("export interface Reading {"));
                because.ItsTrue("an ignored helper doesn't stop a typed id being a brand", lines.Contains("export type TaggedId = string & { readonly __brand: \"TaggedId\" };"));
            })
            .SoBeHappy()
            .UnlessItFailed();
    }

    [UnitTest]
    public void RefuseATypeNameThatIsNotAnIdentifierAndANullRoot()
    {
        When.A<TypeScriptContractGenerator>("generates an emitted type whose name holds a newline, and a null root",
            () => new TypeScriptContractGenerator(),
            generator =>
            {
                AssemblyBuilder assembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("bam-ts-hostile"), AssemblyBuilderAccess.Run);
                ModuleBuilder module = assembly.DefineDynamicModule("bam-ts-hostile");
                TypeBuilder builder = module.DefineType("Hostile.Bad\nexport const injected = 1;\n//", TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.Class);
                Type hostile = builder.CreateType();
                string nullRoot;
                try
                {
                    generator.GetSource(new Type[] { null! });
                    nullRoot = string.Empty;
                }
                catch (ArgumentException exception)
                {
                    nullRoot = exception.Message;
                }
                return new HostileOutcome(Refusal(() => generator.GetSource(new[] { hostile })), nullRoot);
            })
            .TheTest
            .ShouldPass<HostileOutcome>((because, outcome) =>
            {
                because.ItsTrue($"the name is refused ({outcome.Hostile})", outcome.Hostile.Contains("isn't an ASCII TypeScript identifier", StringComparison.Ordinal));
                because.ItsTrue($"a null root is an argument error ({outcome.NullRoot})", outcome.NullRoot.Contains("root type is null", StringComparison.Ordinal));
            })
            .SoBeHappy()
            .UnlessItFailed();
    }

    [UnitTest]
    public void GenerateTheKeysTheSerializerWrites()
    {
        When.A<TypeScriptContractGenerator>("generates each hiding, overriding and sparse fixture and serializes an instance of it",
            () => new TypeScriptContractGenerator(),
            generator =>
            {
                object[] instances =
                {
                    new HidingDerived(), new HideIgnored(), new HideRenamed(), new HideSetterOnly(), new VirtualDerived(),
                    new Sparse { Description = "d", Count = 1, Name = "n" },
                    new WithFields { MaybeShown = "m" },
                };
                List<KeyComparison> comparisons = new List<KeyComparison>();
                foreach (object instance in instances)
                {
                    Type type = instance.GetType();
                    comparisons.Add(new KeyComparison(type.Name, GeneratedKeys(generator.GetSource(new[] { type }), type.Name), WrittenKeys(instance)));
                }
                return comparisons;
            })
            .TheTest
            .ShouldPass<List<KeyComparison>>((because, comparisons) =>
            {
                foreach (KeyComparison comparison in comparisons)
                {
                    because.ItsTrue($"{comparison.Type}: generated [{string.Join(", ", comparison.Generated)}], written [{string.Join(", ", comparison.Written)}]", comparison.Generated.SequenceEqual(comparison.Written));
                }
            })
            .SoBeHappy()
            .UnlessItFailed();
    }

    [UnitTest]
    public void RefuseACollectionClassPassedAsARoot()
    {
        When.A<TypeScriptContractGenerator>("generates collection classes as roots, directly and through the assembly form",
            () => new TypeScriptContractGenerator(),
            generator => new RootCollectionOutcome(
                Refusal(() => generator.GetSource(new[] { typeof(TagList) })),
                Refusal(() => generator.GetSource(new[] { typeof(CountMap) })),
                Refusal(() => generator.GetSource(typeof(TagList).Assembly, type => type == typeof(TagList) || type == typeof(NodeEdge)))))
            .TheTest
            .ShouldPass<RootCollectionOutcome>((because, outcome) =>
            {
                because.ItsTrue($"a List<string> subclass root is refused as an array ({outcome.List})", outcome.List.Contains("written by the serializer as an array", StringComparison.Ordinal));
                because.ItsTrue($"a Dictionary subclass root is refused as a map ({outcome.Map})", outcome.Map.Contains("written by the serializer as a map", StringComparison.Ordinal));
                because.ItsTrue($"the assembly form refuses it too ({outcome.Assembly})", outcome.Assembly.Contains("TagList", StringComparison.Ordinal) && outcome.Assembly.Contains("as an array", StringComparison.Ordinal));
            })
            .SoBeHappy()
            .UnlessItFailed();
    }

    [UnitTest]
    public void ReadNullabilityThroughAClosedGenericBaseAndRefuseWhatAConverterChanges()
    {
        When.A<TypeScriptContractGenerator>("generates contracts on a generic base, included fields, and properties with converters",
            () => new TypeScriptContractGenerator(),
            generator => new Round3Outcome(
                Lines(generator.GetSource(new[] { typeof(StringsOnGeneric), typeof(ListsOnGeneric), typeof(WithFields), typeof(WithEnumConverter) })),
                Refusal(() => generator.GetSource(new[] { typeof(WithConverter) })),
                Refusal(() => generator.GetSource(new[] { typeof(WithNumbersAsStrings) }))))
            .TheTest
            .ShouldPass<Round3Outcome>((because, outcome) =>
            {
                int strings = outcome.Lines.IndexOf("export interface StringsOnGeneric {");
                int lists = outcome.Lines.IndexOf("export interface ListsOnGeneric {");
                because.ItsTrue("GenericBase<string> gives a non-null value", strings >= 0 && outcome.Lines[strings + 1] == "  readonly value: string;");
                because.ItsTrue("and non-null items", strings >= 0 && outcome.Lines[strings + 2] == "  readonly items: readonly string[];");
                because.ItsTrue($"GenericBase<List<string?>> keeps the argument's nulls ({(lists >= 0 ? outcome.Lines[lists + 1] : "missing")})", lists >= 0 && outcome.Lines[lists + 1] == "  readonly value: readonly (string | null)[];");
                because.ItsTrue("an included nullable field is nullable", outcome.Lines.Contains("  readonly maybeShown: string | null;"));
                because.ItsTrue("an included renamed field keeps its name", outcome.Lines.Contains("  readonly \"renamed-field\": string;"));
                because.ItsTrue("fields come after the class's properties", outcome.Lines.IndexOf("  readonly z: string;") < outcome.Lines.IndexOf("  readonly maybeShown: string | null;"));
                because.ItsTrue("a string-enum converter on an enum property is still the union", outcome.Lines.Contains("  readonly health: NodeHealth;"));
                because.ItsTrue($"a property converter is refused ({outcome.Converter})", outcome.Converter.Contains("has a [JsonConverter] (EpochConverter)", StringComparison.Ordinal));
                because.ItsTrue($"WriteAsString is refused ({outcome.Numbers})", outcome.Numbers.Contains("WriteAsString", StringComparison.Ordinal));
            })
            .SoBeHappy()
            .UnlessItFailed();
    }

    private static List<string> GeneratedKeys(string source, string name)
    {
        List<string> lines = Lines(source);
        int start = lines.IndexOf($"export interface {name} {{");
        List<string> keys = new List<string>();
        for (int index = start + 1; start >= 0 && index < lines.Count && lines[index] != "}"; index++)
        {
            string member = lines[index].Trim().Substring("readonly ".Length);
            string key = member.Substring(0, member.IndexOf(':')).TrimEnd('?').Trim('"');
            keys.Add(key);
        }
        return keys.OrderBy(key => key, StringComparer.Ordinal).ToList();
    }

    private static List<string> WrittenKeys(object instance)
    {
        string json = System.Text.Json.JsonSerializer.Serialize(instance, instance.GetType(), new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
        using System.Text.Json.JsonDocument document = System.Text.Json.JsonDocument.Parse(json);
        return document.RootElement.EnumerateObject().Select(property => property.Name).OrderBy(key => key, StringComparer.Ordinal).ToList();
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

    private sealed record CollisionOutcome(List<string> Hidden, string CaseClash, string RenameClash);

    private sealed record EdgeRefusalOutcome(string CustomList, string CustomDictionary, string Flags, string Shadowing, string Grid, string Interface, string Abstract, string HashSet);

    private sealed record HostileOutcome(string Hostile, string NullRoot);

    private sealed record KeyComparison(string Type, List<string> Generated, List<string> Written);

    private sealed record RootCollectionOutcome(string List, string Map, string Assembly);

    private sealed record Round3Outcome(List<string> Lines, string Converter, string Numbers);
}
