using Bam.DependencyInjection;
using Bam.Generators.Decorators.Tests.Fixtures;
using Bam.Test;
using System.Reflection;

namespace Bam.Generators.Decorators.Tests.Unit
{
    [UnitTestMenu("CSharpTypeName Should", Selector = "cstns")]
    public class CSharpTypeNameShould : UnitTestMenuContainer
    {
        public CSharpTypeNameShould(ServiceRegistry serviceRegistry) : base(serviceRegistry)
        {
        }

        /// <summary>Members with the annotations the renderer has to read back.</summary>
        public interface IAnnotated
        {
            string? MaybeText { get; }

            string Text { get; }

            event EventHandler<EventArgs>? Raised;

            Dictionary<string, List<int?>?> Shape(string?[] names, ref int counter, Dictionary<string, string?>? map);

            Task<string?> FindAsync(string key);

            TItem Echo<TItem>(TItem item, List<TItem> items);

            void Keywords(string @class, int @event, bool ordinary);
        }

        public class Outer
        {
            public class Inner
            {
            }
        }

        [UnitTest]
        public void QualifyTypeNames()
        {
            When.A<CSharpTypeNameShould>("names types", this, test =>
            {
                return new NameOutcome(
                    CSharpTypeName.Of(typeof(void)),
                    CSharpTypeName.Of(typeof(string)),
                    CSharpTypeName.Of(typeof(int[])),
                    CSharpTypeName.Of(typeof(int[,])),
                    CSharpTypeName.Of(typeof(Dictionary<string, List<int>>)),
                    CSharpTypeName.Of(typeof(int?)),
                    CSharpTypeName.Of(typeof(Outer.Inner)),
                    CSharpTypeName.Of(typeof(IBoxService<EchoService>)),
                    CSharpTypeName.Of(typeof(int).MakeByRefType()));
            })
            .TheTest
            .ShouldPass<NameOutcome>((because, outcome) =>
            {
                because.ItsTrue("void is the keyword", outcome.Void == "void");
                because.ItsTrue("a simple type is fully qualified", outcome.Simple == "global::System.String");
                because.ItsTrue("an array keeps its brackets", outcome.Array == "global::System.Int32[]");
                because.ItsTrue("a multi-dimensional array keeps its rank", outcome.Matrix == "global::System.Int32[,]");
                because.ItsTrue("generic arguments are qualified recursively", outcome.Generic == "global::System.Collections.Generic.Dictionary<global::System.String, global::System.Collections.Generic.List<global::System.Int32>>");
                because.ItsTrue("a nullable value type is a Nullable<T>", outcome.NullableValue == "global::System.Nullable<global::System.Int32>");
                because.ItsTrue("a nested type uses dots", outcome.Nested == "global::Bam.Generators.Decorators.Tests.Unit.CSharpTypeNameShould.Outer.Inner");
                because.ItsTrue("a closed generic interface is qualified", outcome.ClosedGeneric == "global::Bam.Generators.Decorators.Tests.Fixtures.IBoxService<global::Bam.Generators.Decorators.Tests.Fixtures.EchoService>");
                because.ItsTrue("a by-ref type names its element", outcome.ByRef == "global::System.Int32");
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest]
        public void NameTypesNestedInGenericTypes()
        {
            When.A<CSharpTypeNameShould>("names nested types", this, test =>
            {
                return new NestedOutcome(
                    CSharpTypeName.Of(typeof(Dictionary<string, int>.KeyCollection)),
                    CSharpTypeName.Of(typeof(Wrapper<int>.Item)),
                    CSharpTypeName.Of(typeof(Wrapper<string>.Pair<bool>)),
                    CSharpTypeName.Of(typeof(List<Wrapper<string>.Pair<int[]>>)),
                    CSharpTypeName.Of(typeof(Dictionary<string, int>.Enumerator)));
            })
            .TheTest
            .ShouldPass<NestedOutcome>((because, outcome) =>
            {
                because.ItsTrue("a type nested in a generic BCL type keeps its own name", outcome.KeyCollection == "global::System.Collections.Generic.Dictionary<global::System.String, global::System.Int32>.KeyCollection", outcome.KeyCollection);
                because.ItsTrue("a type nested in a generic user type keeps its own name", outcome.Item == "global::Bam.Generators.Decorators.Tests.Fixtures.Wrapper<global::System.Int32>.Item", outcome.Item);
                because.ItsTrue("each level gets the arguments it declares", outcome.Pair == "global::Bam.Generators.Decorators.Tests.Fixtures.Wrapper<global::System.String>.Pair<global::System.Boolean>", outcome.Pair);
                because.ItsTrue("nested types are named inside other types' arguments", outcome.ListOfPair == "global::System.Collections.Generic.List<global::Bam.Generators.Decorators.Tests.Fixtures.Wrapper<global::System.String>.Pair<global::System.Int32[]>>", outcome.ListOfPair);
                because.ItsTrue("a nested struct is named the same way", outcome.Enumerator == "global::System.Collections.Generic.Dictionary<global::System.String, global::System.Int32>.Enumerator", outcome.Enumerator);
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest]
        public void RestateGenericConstraints()
        {
            When.A<CSharpTypeNameShould>("names the constraints of generic methods", this, test =>
            {
                MethodInfo Method(string name)
                {
                    return typeof(IEdgeCaseService).GetMethod(name)!;
                }

                return new ConstraintOutcome(
                    CSharpTypeName.GenericConstraintsOf(Method(nameof(IEdgeCaseService.Locate))),
                    CSharpTypeName.GenericConstraintsOf(Method(nameof(IEdgeCaseService.Maybe))),
                    CSharpTypeName.GenericConstraintsOf(Method(nameof(IEdgeCaseService.Twice))),
                    CSharpTypeName.GenericConstraintsOf(Method(nameof(IEdgeCaseService.Same))),
                    CSharpTypeName.GenericConstraintsOf(Method(nameof(IEdgeCaseService.Keys))),
                    CSharpTypeName.OfReturn(Method(nameof(IEdgeCaseService.Locate))),
                    CSharpTypeName.Of(Method(nameof(IEdgeCaseService.Maybe)).GetParameters()[0]),
                    CSharpTypeName.OfReturn(Method(nameof(IEdgeCaseService.Twice))),
                    CSharpTypeName.Literal("a \"quoted\" \\ name"));
            })
            .TheTest
            .ShouldPass<ConstraintOutcome>((because, outcome) =>
            {
                because.ItsTrue("a class constraint is restated", outcome.Class == " where TItem : class", outcome.Class);
                because.ItsTrue("no constraint becomes default", outcome.Unconstrained == " where TValue : default", outcome.Unconstrained);
                because.ItsTrue("a struct constraint is restated", outcome.Struct == " where TNumber : struct", outcome.Struct);
                because.ItsTrue("a base-class constraint becomes class", outcome.BaseClass == " where TBase : class", outcome.BaseClass);
                because.ItsTrue("a method that is not generic has none", outcome.NotGeneric == string.Empty);
                because.ItsTrue("an annotated generic return is annotated", outcome.NullableReturn == "TItem?");
                because.ItsTrue("an annotated generic parameter is annotated", outcome.NullableParameter == "TValue?");
                because.ItsTrue("an unannotated one is not", outcome.PlainReturn == "TNumber");
                because.ItsTrue("text is escaped into a literal", outcome.Literal == "\"a \\\"quoted\\\" \\\\ name\"", outcome.Literal);
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest]
        public void KeepNullableAnnotations()
        {
            When.A<CSharpTypeNameShould>("names annotated members", this, test =>
            {
                MethodInfo shape = typeof(IAnnotated).GetMethod(nameof(IAnnotated.Shape))!;
                MethodInfo findAsync = typeof(IAnnotated).GetMethod(nameof(IAnnotated.FindAsync))!;
                ParameterInfo[] parameters = shape.GetParameters();
                return new AnnotationOutcome(
                    CSharpTypeName.Of(typeof(IAnnotated).GetProperty(nameof(IAnnotated.MaybeText))!),
                    CSharpTypeName.Of(typeof(IAnnotated).GetProperty(nameof(IAnnotated.Text))!),
                    CSharpTypeName.Of(typeof(IAnnotated).GetEvent(nameof(IAnnotated.Raised))!),
                    CSharpTypeName.OfReturn(shape),
                    CSharpTypeName.Of(parameters[0]),
                    CSharpTypeName.Of(parameters[1]),
                    CSharpTypeName.Of(parameters[2]),
                    CSharpTypeName.OfReturn(findAsync),
                    CSharpTypeName.OfResult(findAsync, typeof(string)),
                    CSharpTypeName.OfResult(shape, shape.ReturnType));
            })
            .TheTest
            .ShouldPass<AnnotationOutcome>((because, outcome) =>
            {
                because.ItsTrue("a nullable property is annotated", outcome.NullableProperty == "global::System.String?");
                because.ItsTrue("a non-nullable property is not", outcome.Property == "global::System.String");
                because.ItsTrue("a nullable event is annotated", outcome.Event == "global::System.EventHandler<global::System.EventArgs>?");
                because.ItsTrue("annotations inside a return type are kept", outcome.Return == "global::System.Collections.Generic.Dictionary<global::System.String, global::System.Collections.Generic.List<global::System.Nullable<global::System.Int32>>?>", outcome.Return);
                because.ItsTrue("a nullable array element is annotated", outcome.ArrayParameter == "global::System.String?[]", outcome.ArrayParameter);
                because.ItsTrue("a ref parameter names its element type", outcome.RefParameter == "global::System.Int32");
                because.ItsTrue("a nullable parameter and its arguments are annotated", outcome.NullableParameter == "global::System.Collections.Generic.Dictionary<global::System.String, global::System.String?>?", outcome.NullableParameter);
                because.ItsTrue("a task's argument is annotated", outcome.TaskReturn == "global::System.Threading.Tasks.Task<global::System.String?>");
                because.ItsTrue("the result of a task is the annotated argument", outcome.TaskResult == "global::System.String?");
                because.ItsTrue("the result of a synchronous method is its return type", outcome.SyncResult == outcome.Return);
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest]
        public void NameGenericParametersAndEscapeKeywords()
        {
            When.A<CSharpTypeNameShould>("names generic parameters and keyword identifiers", this, test =>
            {
                MethodInfo echo = typeof(IAnnotated).GetMethod(nameof(IAnnotated.Echo))!;
                ParameterInfo[] keywords = typeof(IAnnotated).GetMethod(nameof(IAnnotated.Keywords))!.GetParameters();
                return new IdentifierOutcome(
                    CSharpTypeName.OfReturn(echo),
                    CSharpTypeName.Of(echo.GetParameters()[1]),
                    CSharpTypeName.Identifier(keywords[0].Name!),
                    CSharpTypeName.Identifier(keywords[1].Name!),
                    CSharpTypeName.Identifier(keywords[2].Name!));
            })
            .TheTest
            .ShouldPass<IdentifierOutcome>((because, outcome) =>
            {
                because.ItsTrue("a generic parameter is named bare", outcome.GenericReturn == "TItem");
                because.ItsTrue("a generic parameter inside a type is named bare", outcome.GenericList == "global::System.Collections.Generic.List<TItem>");
                because.ItsTrue("keywords are escaped", outcome.Class == "@class" && outcome.Event == "@event");
                because.ItsTrue("ordinary names are left alone", outcome.Ordinary == "ordinary");
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        private sealed record NestedOutcome(string KeyCollection, string Item, string Pair, string ListOfPair, string Enumerator);

        private sealed record ConstraintOutcome(string Class, string Unconstrained, string Struct, string BaseClass, string NotGeneric, string NullableReturn, string NullableParameter, string PlainReturn, string Literal);

        private sealed record NameOutcome(string Void, string Simple, string Array, string Matrix, string Generic, string NullableValue, string Nested, string ClosedGeneric, string ByRef);

        private sealed record AnnotationOutcome(string NullableProperty, string Property, string Event, string Return, string ArrayParameter, string RefParameter, string NullableParameter, string TaskReturn, string TaskResult, string SyncResult);

        private sealed record IdentifierOutcome(string GenericReturn, string GenericList, string Class, string Event, string Ordinary);
    }
}
