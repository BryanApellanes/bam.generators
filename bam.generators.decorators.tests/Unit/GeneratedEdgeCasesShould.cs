using Bam.DependencyInjection;
using Bam.Generators.Decorators.Tests.Fixtures;
using Bam.Generators.Decorators.Tests.Fixtures.Decorators;
using Bam.Logging;
using Bam.Test;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NSubstitute;
using System.Reflection;

namespace Bam.Generators.Decorators.Tests.Unit
{
    /// <summary>
    /// The shapes that are easy to get wrong in generated source, exercised through
    /// Fixtures/Generated/EdgeCaseServiceDecorator.cs and through fresh compiles.
    /// </summary>
    [UnitTestMenu("Generated edge cases Should", Selector = "gecs")]
    public class GeneratedEdgeCasesShould : UnitTestMenuContainer
    {
        public GeneratedEdgeCasesShould(ServiceRegistry serviceRegistry) : base(serviceRegistry)
        {
        }

        private static EdgeCaseServiceDecorator NewDecorator()
        {
            return new EdgeCaseServiceDecorator(new EdgeCaseService(), Substitute.For<ILogger>());
        }

        [UnitTest]
        public void TellHandlersWhichOverloadRan()
        {
            After.Setup(reg =>
            {
                reg.For<EdgeCaseServiceDecorator>().Use(NewDecorator());
            })
            .When<EdgeCaseServiceDecorator>("calls overloads that differ by object and string", decorator =>
            {
                List<string> seen = new List<string>();
                decorator.Subscribe(DecoratorPhase.Start, "Log", context =>
                {
                    seen.Add(context.Method?.GetParameters()[0].ParameterType.Name + "/" + context.Method?.DeclaringType?.Name);
                });

                IEdgeCaseService service = decorator;
                string asObject = service.Log((object)"text");
                string nullString = service.Log((string?)null);
                string asString = service.Log("text");
                return new OverloadOutcome(asObject, nullString, asString, string.Join("|", seen));
            })
            .TheTest
            .ShouldPass<OverloadOutcome>((because, outcome) =>
            {
                because.ItsTrue("each call reached the overload the compiler bound", outcome.AsObject == "object:text" && outcome.NullString == "string:null" && outcome.AsString == "string:text");
                because.ItsTrue("handlers were told that overload, on the implementation, whatever the argument's runtime type",
                    outcome.Seen == "Object/EdgeCaseService|String/EdgeCaseService|String/EdgeCaseService",
                    $"seen: {outcome.Seen}");
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest]
        public void ForwardKeywordNamedAndGenericMembers()
        {
            After.Setup(reg =>
            {
                reg.For<IEdgeCaseService>().Use(NewDecorator());
            })
            .When<IEdgeCaseService>("is used through its interface", service =>
            {
                service.@event = "raised";
                EdgeCaseBase item = new EdgeCaseBase();
                return new ForwardingOutcome(
                    service.@event,
                    service.@default("kind"),
                    service.Locate<string>("found"),
                    service.Locate<EdgeCaseBase>("not a base"),
                    service.Maybe<string>(null),
                    service.Maybe(5),
                    service.Twice(21),
                    ReferenceEquals(service.Same(item), item),
                    string.Join(",", service.Keys()),
                    service.Wrap(new Wrapper<string>.Pair<bool> { Second = true }).Value);
            })
            .TheTest
            .ShouldPass<ForwardingOutcome>((because, outcome) =>
            {
                because.ItsTrue("a keyword-named property is forwarded", outcome.Event == "raised");
                because.ItsTrue("a keyword-named method with a keyword-named parameter is forwarded", outcome.Default == "default:kind");
                because.ItsTrue("a nullable class-constrained generic passes values and nulls", outcome.Located == "found" && outcome.NotLocated == null);
                because.ItsTrue("a nullable unconstrained generic passes a null reference and a value", outcome.MaybeNull == null && outcome.MaybeValue == 5);
                because.ItsTrue("a struct-constrained generic is forwarded", outcome.Twice == 21);
                because.ItsTrue("a base-class-constrained generic is forwarded", outcome.Same);
                because.ItsTrue("a type nested in a generic BCL type is returned", outcome.Keys == "one");
                because.ItsTrue("types nested in a generic user type are passed and returned", outcome.Wrapped == 1);
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest]
        public void CompileWithoutWarnings()
        {
            After.Setup(reg =>
            {
                reg.For<DecoratorGenerator>().Use(DecoratorGeneratorShould.NewGenerator());
            })
            .When<DecoratorGenerator>("compiles what it generates for every fixture", generator =>
            {
                return new DiagnosticOutcome(string.Join("\n", new string[]
                {
                    Diagnose(generator, typeof(IEchoService), typeof(EchoService)),
                    Diagnose(generator, typeof(IKitchenSinkService), typeof(KitchenSinkService)),
                    Diagnose(generator, typeof(IEdgeCaseService), typeof(EdgeCaseService)),
                    Diagnose(generator, typeof(IClashService), typeof(ClashService)),
                    Diagnose(generator, typeof(IElsewhereService), typeof(ElsewhereService)),
                    Diagnose(generator, typeof(IObservedService), typeof(ObservedService)),
                    Diagnose(generator, typeof(IBoxService<string>), typeof(BoxService<string>))
                }.Where(problems => problems.Length > 0)));
            })
            .TheTest
            .ShouldPass<DiagnosticOutcome>((because, outcome) =>
            {
                because.ItsTrue("no generated source raises a warning or an error", outcome.Problems.Length == 0, outcome.Problems);
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest]
        public void ReferenceTheAssembliesOfBaseTypes()
        {
            When.A<GeneratedEdgeCasesShould>("lists what generated source depends on", this, test =>
            {
                Assembly[] elsewhere = new DecoratorModel(typeof(IElsewhereService), typeof(ElsewhereService)).ReferencedTypes.Select(type => type.Assembly).ToArray();
                Assembly[] observed = new DecoratorModel(typeof(IObservedService), typeof(ObservedService)).ReferencedTypes.Select(type => type.Assembly).ToArray();
                return new ReferenceOutcome(
                    elsewhere.Contains(typeof(System.Collections.Specialized.NameValueCollection).Assembly),
                    observed.Contains(typeof(System.ComponentModel.INotifyPropertyChanging).Assembly),
                    elsewhere.Distinct().Count() == elsewhere.Length);
            })
            .TheTest
            .ShouldPass<ReferenceOutcome>((because, outcome) =>
            {
                because.ItsTrue("the assembly of the implementation's base class is referenced", outcome.BaseClassAssembly);
                because.ItsTrue("the assembly of an inherited interface is referenced", outcome.InheritedInterfaceAssembly);
                because.ItsTrue("each assembly still appears once", outcome.OnePerAssembly);
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest]
        public void FindTheExactMethod()
        {
            When.A<GeneratedEdgeCasesShould>("looks methods up the way generated fields do", this, test =>
            {
                MethodInfo? logObject = DecoratedMethod.Find(typeof(IEdgeCaseService), "Log", 0, "System.Object");
                MethodInfo? logString = DecoratedMethod.Find(typeof(IEdgeCaseService), "Log", 0, "System.String");
                MethodInfo? generic = DecoratedMethod.Find(typeof(IEdgeCaseService), "Maybe", 1, "TValue");
                MethodInfo? parameterless = DecoratedMethod.Find(typeof(IEdgeCaseService), "Keys", 0);
                return new FindOutcome(
                    logObject?.GetParameters()[0].ParameterType,
                    logString?.GetParameters()[0].ParameterType,
                    generic?.IsGenericMethodDefinition == true,
                    parameterless?.Name,
                    DecoratedMethod.Find(typeof(IEdgeCaseService), "Log", 0, "System.Int32") == null
                        && DecoratedMethod.Find(typeof(IEdgeCaseService), "Log", 1, "System.String") == null
                        && DecoratedMethod.Find(typeof(IEdgeCaseService), "Missing", 0) == null,
                    typeof(IEdgeCaseService).GetMethods().All(method =>
                        DecoratedMethod.Find(typeof(IEdgeCaseService), method.Name, method.GetGenericArguments().Length, method.GetParameters().Select(parameter => DecoratedMethod.SignatureOf(parameter.ParameterType)).ToArray()) == method));
            })
            .TheTest
            .ShouldPass<FindOutcome>((because, outcome) =>
            {
                because.ItsTrue("overloads are told apart by parameter type", outcome.LogObject == typeof(object) && outcome.LogString == typeof(string));
                because.ItsTrue("a generic method is found by its arity and parameter names", outcome.GenericFound);
                because.ItsTrue("a parameterless method is found", outcome.Parameterless == "Keys");
                because.ItsTrue("a signature the interface does not declare finds nothing", outcome.MismatchesFindNothing);
                because.ItsTrue("every method of the interface finds itself", outcome.EveryMethodFindsItself);
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest]
        public void FallBackToTheNameWhenTheMethodIsUnknown()
        {
            After.Setup(reg =>
            {
                reg.For<Decorator<EchoService>>().Use(new Decorator<EchoService>(new EchoService(), Substitute.For<ILogger>()));
            })
            .When<Decorator<EchoService>>("intercepts with no method, as a stale generated decorator would", decorator =>
            {
                string? seen = null;
                decorator.Subscribe(DecoratorPhase.Start, "Message", context => { seen = context.Method?.Name; });
                DecoratorInvocationResult<EchoService, string> result = decorator.Intercept<string>(null, "Message", new object?[] { "hello" }, () => decorator.Instance.Message("hello"));
                return new FallbackOutcome(result.Value, seen);
            })
            .TheTest
            .ShouldPass<FallbackOutcome>((because, outcome) =>
            {
                because.ItsTrue("the call still goes through", outcome.Value == "hello");
                because.ItsTrue("the method is found by name instead", outcome.MethodSeen == "Message");
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        // Compiles the source generated for the pair and returns its warnings and errors, one per line.
        // CS0436 is left out: it only says the test assembly already holds a decorator of the same name.
        private static string Diagnose(DecoratorGenerator generator, Type interfaceType, Type implementationType)
        {
            DecoratorModel model = new DecoratorModel(interfaceType, implementationType);
            IEnumerable<MetadataReference> references = new RoslynCompiler().MetadataReferenceResolver.GetMetaDataReferences()
                .Concat(model.ReferencedTypes.Select(type => (MetadataReference)MetadataReference.CreateFromFile(type.Assembly.Location)));
            CSharpCompilation compilation = CSharpCompilation.Create("Diagnose." + model.DecoratorTypeName)
                .WithOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
                .AddReferences(references)
                .AddSyntaxTrees(CSharpSyntaxTree.ParseText(generator.GetSource(interfaceType, implementationType)));

            return string.Join("\n", compilation.GetDiagnostics()
                .Where(diagnostic => diagnostic.Severity >= DiagnosticSeverity.Warning && diagnostic.Id != "CS0436")
                .Select(diagnostic => model.DecoratorTypeName + ": " + diagnostic));
        }

        private sealed record OverloadOutcome(string AsObject, string NullString, string AsString, string Seen);

        private sealed record ForwardingOutcome(string Event, string Default, string? Located, EdgeCaseBase? NotLocated, string? MaybeNull, int MaybeValue, int Twice, bool Same, string Keys, int Wrapped);

        private sealed record DiagnosticOutcome(string Problems);

        private sealed record ReferenceOutcome(bool BaseClassAssembly, bool InheritedInterfaceAssembly, bool OnePerAssembly);

        private sealed record FindOutcome(Type? LogObject, Type? LogString, bool GenericFound, string? Parameterless, bool MismatchesFindNothing, bool EveryMethodFindsItself);

        private sealed record FallbackOutcome(string? Value, string? MethodSeen);
    }
}
