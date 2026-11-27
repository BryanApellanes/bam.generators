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
                because.ItsTrue($"Id and ID are refused ({outcome.CaseClash})", outcome.CaseClash.Contains("would both be written as \"id\"", StringComparison.Ordinal));
                because.ItsTrue($"two renames to one name are refused ({outcome.RenameClash})", outcome.RenameClash.Contains("would both be written as \"value\"", StringComparison.Ordinal));
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
}
