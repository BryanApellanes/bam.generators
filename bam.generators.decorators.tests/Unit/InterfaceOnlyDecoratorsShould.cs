using Bam.DependencyInjection;
using Bam.Generators.Decorators.Tests.Fixtures;
using Bam.Generators.Decorators.Tests.Fixtures.Decorators;
using Bam.Test;
using System.Reflection;

namespace Bam.Generators.Decorators.Tests.Unit
{
    /// <summary>
    /// Pins bam.generators#16: a decorator generated from an interface alone (<c>Decorator&lt;I, I&gt;</c>)
    /// wraps any instance of the interface, including an internal implementation generated code cannot name,
    /// through the generator, a registry and the factory. The implementation-typed mode is unchanged.
    /// </summary>
    [UnitTestMenu("Interface-only decorators Should", Selector = "iods")]
    public class InterfaceOnlyDecoratorsShould : UnitTestMenuContainer
    {
        public InterfaceOnlyDecoratorsShould(ServiceRegistry serviceRegistry) : base(serviceRegistry)
        {
        }

        [UnitTest]
        public void GenerateFromAnInterfaceAlone()
        {
            When.A<DecoratorGenerator>("generates from the interface", new DecoratorGenerator(), generator =>
            {
                DecoratorModel model = new DecoratorModel(typeof(ISecretService), null);
                string source = generator.GetSource<ISecretService>();
                string viaNull = generator.GetSource(typeof(ISecretService), null);
                return new SourceOutcome(
                    model.InterfaceOnly,
                    new DecoratorModel(typeof(IEchoService), typeof(EchoService)).InterfaceOnly,
                    model.ImplementationType == typeof(ISecretService),
                    model.DecoratorTypeName,
                    model.DecorateMethodName,
                    source.Contains("public partial class ISecretServiceDecorator : global::Bam.Generators.Decorators.Decorator<global::Bam.Generators.Decorators.Tests.Fixtures.ISecretService, global::Bam.Generators.Decorators.Tests.Fixtures.ISecretService>, global::Bam.Generators.Decorators.Tests.Fixtures.ISecretService"),
                    source.Contains("public ISecretServiceDecorator(global::Bam.Generators.Decorators.Tests.Fixtures.ISecretService instance, global::Bam.Generators.Decorators.IDecoratorLogger? logger = null)"),
                    source.Contains("Generated decorator for any ISecretService"),
                    source == viaNull,
                    Normalize(source) == Normalize(CheckedIn("ISecretServiceDecorator.cs")));
            })
            .TheTest
            .ShouldPass<SourceOutcome>((because, outcome) =>
            {
                because.ItsTrue("the model is interface-only", outcome.InterfaceOnly);
                because.ItsTrue("a model with an implementation is not", !outcome.PairInterfaceOnly);
                because.ItsTrue("the interface stands in for the implementation type", outcome.InterfaceStandsIn);
                because.ItsTrue("the decorator is named after the interface", outcome.TypeName == "ISecretServiceDecorator", outcome.TypeName);
                because.ItsTrue("and so is the decorate method", outcome.DecorateMethodName == "DecorateISecretService", outcome.DecorateMethodName);
                because.ItsTrue("the decorator extends Decorator<I, I> and implements I", outcome.ExtendsPair);
                because.ItsTrue("the constructor takes the interface", outcome.CtorTakesInterface);
                because.ItsTrue("the documentation says any implementation is wrapped", outcome.DocumentsAny);
                because.ItsTrue("GetSource<I>() and GetSource(I, null) agree", outcome.NullAgrees);
                because.ItsTrue("Fixtures/Generated/ISecretServiceDecorator.cs is current", outcome.FixtureCurrent, "regenerate the fixture: the generator's interface-only output has changed");
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest]
        public void WrapAnyImplementationAtRunTime()
        {
            When.A<SecretService>("is internal and decorated through the interface", new SecretService(), hidden =>
            {
                ISecretServiceDecorator decorator = new ISecretServiceDecorator(hidden);
                MethodInfo? seen = null;
                object? decorated = null;
                decorator.Subscribe(DecoratorPhase.Start, nameof(ISecretService.Reveal), context =>
                {
                    seen = context.Method;
                    decorated = context.Decorated;
                });
                ISecretService secret = decorator;
                string result = secret.Reveal("hi");
                return new WrapOutcome(
                    result,
                    hidden.Reveals,
                    seen?.DeclaringType,
                    ReferenceEquals(decorated, hidden),
                    typeof(ISecretService).IsVisible && !typeof(SecretService).IsVisible,
                    decorator.ImplementationType);
            })
            .TheTest
            .ShouldPass<WrapOutcome>((because, outcome) =>
            {
                because.ItsTrue("the call is forwarded to the hidden implementation", outcome.Result == "secret:hi" && outcome.Calls == 1, outcome.Result);
                because.ItsTrue("the handler sees the method on the instance's runtime type", outcome.SeenDeclaringType == typeof(SecretService), outcome.SeenDeclaringType?.FullName);
                because.ItsTrue("the handler sees the decorated instance", outcome.SeesInstance);
                because.ItsTrue("the fixture really is an internal implementation of a public interface", outcome.Hidden);
                because.ItsTrue("the decorator reports the interface as its implementation type", outcome.ImplementationType == typeof(ISecretService), outcome.ImplementationType.FullName);
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest]
        public void DecorateAnInterfaceInARegistry()
        {
            After.Setup(reg =>
            {
                reg.For<ISecretService>().Use(new SecretService());
                reg.For<ServiceRegistry>().Use(reg);
            })
            .When<ServiceRegistry>("decorates the interface alone", registry =>
            {
                List<string> seen = new List<string>();
                DecoratorRegistration<ISecretService, ISecretService> registration = registry.Decorate<ISecretService>();
                registry.OnMethodEnd<ISecretService, ISecretService>(nameof(ISecretService.Reveal), context => seen.Add("end:" + context.Result));
                registry.OnRevealStart(context => seen.Add("start:" + context.Args[0]));
                ISecretService secret = registry.Get<ISecretService>();
                string result = secret.Reveal("reg");
                return new RegistryOutcome(result, secret.GetType(), registration.ImplementationType, string.Join(",", seen), ReferenceEquals(registration, registry.Decorate<ISecretService, ISecretService>()));
            })
            .TheTest
            .ShouldPass<RegistryOutcome>((because, outcome) =>
            {
                because.ItsTrue("the hidden implementation is reached", outcome.Result == "secret:reg", outcome.Result);
                because.ItsTrue("the generated interface-only decorator is used", outcome.ResolvedType == typeof(ISecretServiceDecorator), outcome.ResolvedType.FullName);
                because.ItsTrue("the registration is typed on the interface", outcome.ImplementationType == typeof(ISecretService));
                because.ItsTrue("typed and generated hooks both fire", outcome.Seen == "start:reg,end:secret:reg", outcome.Seen);
                because.ItsTrue("Decorate<I>() is Decorate<I, I>()", outcome.SameRegistration);
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest]
        public void CreateAnInterfaceOnlyDecoratorThroughTheFactory()
        {
            When.A<SecretService>("is created through the factory", new SecretService(), hidden =>
            {
                Decorator<ISecretService, ISecretService> decorator = DecoratorFactory.Create<ISecretService>(hidden);
                return new FactoryOutcome(
                    decorator.GetType(),
                    ((ISecretService)decorator).Reveal("factory"),
                    ReferenceEquals(DecoratorFactory.Handlers<ISecretService>(), DecoratorFactory.Handlers<ISecretService, ISecretService>()),
                    !ReferenceEquals(DecoratorFactory.Handlers<ISecretService>(), DecoratorFactory.Handlers<IEchoService, EchoService>()),
                    GeneratedDecorators.Find(typeof(ISecretService), typeof(ISecretService)),
                    GeneratedDecorators.Find(typeof(IEchoService), typeof(EchoService)));
            })
            .TheTest
            .ShouldPass<FactoryOutcome>((because, outcome) =>
            {
                because.ItsTrue("the interface-only decorator is created", outcome.DecoratorType == typeof(ISecretServiceDecorator), outcome.DecoratorType.FullName);
                because.ItsTrue("and forwards", outcome.Result == "secret:factory", outcome.Result);
                because.ItsTrue("Handlers<I>() is Handlers<I, I>()", outcome.SameStore);
                because.ItsTrue("and not the store of an implementation-typed pair", outcome.DistinctFromPair);
                because.ItsTrue("the lookup tells interface-only and implementation-typed decorators apart", outcome.FoundForInterface == typeof(ISecretServiceDecorator) && outcome.FoundForPair == typeof(EchoServiceDecorator), $"{outcome.FoundForInterface?.Name} / {outcome.FoundForPair?.Name}");
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest]
        public void StillValidateTheInterface()
        {
            When.A<DecoratorGenerator>("is given things that are not decoratable interfaces", new DecoratorGenerator(), generator =>
            {
                return new ValidateOutcome(
                    Diagnose(() => generator.GetSource(typeof(EchoService), null)),
                    Diagnose(() => generator.GetSource(typeof(IList<>), null)),
                    Diagnose(() => generator.GetSource(typeof(IHiddenService), null)),
                    Diagnose(() => generator.GetSource(typeof(ISecretService), typeof(SecretService))),
                    Diagnose(() => generator.GetSource(typeof(IEchoService), typeof(IShoutService))));
            })
            .TheTest
            .ShouldPass<ValidateOutcome>((because, outcome) =>
            {
                because.ItsTrue("a class is refused", outcome.Class.Contains("not an interface"), outcome.Class);
                because.ItsTrue("an open generic interface is refused", outcome.OpenGeneric.Contains("open generic"), outcome.OpenGeneric);
                because.ItsTrue("a non-public interface is refused", outcome.Hidden.Contains("must both be public"), outcome.Hidden);
                because.ItsTrue("an internal implementation named explicitly is still refused", outcome.HiddenImplementation.Contains("must both be public"), outcome.HiddenImplementation);
                because.ItsTrue("another interface in place of the implementation is still refused", outcome.OtherInterface.Contains("not a closed class"), outcome.OtherInterface);
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest]
        public void RefuseToWriteTwoDecoratorsOfOneName()
        {
            When.A<DecoratorGenerator>("queues two services whose decorators would share a name", new DecoratorGenerator(), generator =>
            {
                string directory = Path.Combine(Path.GetTempPath(), "bam.generators.decorators.tests", Guid.NewGuid().ToString("N"));
                try
                {
                    generator.AddServiceType<First.ISame, First.Same>().AddServiceType<Second.ISame, Second.Same>();
                    string message = Diagnose(() => generator.WriteSource(directory));
                    return new ClashOutcome(message, Directory.Exists(directory) ? Directory.GetFiles(directory).Length : 0);
                }
                finally
                {
                    if (Directory.Exists(directory))
                    {
                        Directory.Delete(directory, true);
                    }
                }
            })
            .TheTest
            .ShouldPass<ClashOutcome>((because, outcome) =>
            {
                because.ItsTrue("the clash is refused and names the decorator", outcome.Message.Contains("SameDecorator") && outcome.Message.Contains("overwrite"), outcome.Message);
                because.ItsTrue("the first file was written before the clash was found", outcome.FilesWritten == 1, $"files: {outcome.FilesWritten}");
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        private static string Diagnose(Action action)
        {
            try
            {
                action();
                return string.Empty;
            }
            catch (DecoratorGenerationException ex)
            {
                return ex.Message;
            }
        }

        private static string CheckedIn(string fileName)
        {
            using Stream? stream = typeof(InterfaceOnlyDecoratorsShould).Assembly.GetManifestResourceStream("Generated." + fileName);
            if (stream == null)
            {
                return string.Empty;
            }

            using StreamReader reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }

        private static string Normalize(string source)
        {
            return source.Replace("\r\n", "\n").Trim();
        }

        /// <summary>Two services whose decorators would be named alike in one namespace.</summary>
        public static class First
        {
            public interface ISame
            {
                int Value();
            }

            public class Same : ISame
            {
                public int Value() => 1;
            }
        }

        public static class Second
        {
            public interface ISame
            {
                int Value();
            }

            public class Same : ISame
            {
                public int Value() => 2;
            }
        }

        private sealed record SourceOutcome(bool InterfaceOnly, bool PairInterfaceOnly, bool InterfaceStandsIn, string TypeName, string DecorateMethodName, bool ExtendsPair, bool CtorTakesInterface, bool DocumentsAny, bool NullAgrees, bool FixtureCurrent);

        private sealed record WrapOutcome(string Result, int Calls, Type? SeenDeclaringType, bool SeesInstance, bool Hidden, Type ImplementationType);

        private sealed record RegistryOutcome(string Result, Type ResolvedType, Type ImplementationType, string Seen, bool SameRegistration);

        private sealed record FactoryOutcome(Type DecoratorType, string Result, bool SameStore, bool DistinctFromPair, Type? FoundForInterface, Type? FoundForPair);

        private sealed record ValidateOutcome(string Class, string OpenGeneric, string Hidden, string HiddenImplementation, string OtherInterface);

        private sealed record ClashOutcome(string Message, int FilesWritten);
    }
}
