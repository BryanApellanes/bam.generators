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
                    Normalize(source) == Normalize(CheckedIn("ISecretServiceDecorator.cs")),
                    Normalize(new DecoratorGenerator().WithoutRegistryExtensions().GetSource<ISecretService>()) == Normalize(CheckedIn("NoRegistry.ISecretServiceDecorator.cs")));
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
                because.ItsTrue("Fixtures/Generated/NoRegistry/ISecretServiceDecorator.cs is current", outcome.NoRegistryFixtureCurrent, "regenerate the fixture: the generator's no-registry interface-only output has changed");
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
                ISecretService decorated = DecoratorFactory.Create<ISecretService>(hidden);
                Decorator<ISecretService, ISecretService> decorator = (Decorator<ISecretService, ISecretService>)(object)decorated;
                int shared = 0;
                DecoratorFactory.Handlers<ISecretService>().Add(DecoratorPhase.Start, nameof(ISecretService.Reveal), context => shared++);
                decorated.Reveal("shared");
                int sharedBeforeReset = shared;
                DecoratorFactory.Reset<ISecretService>();
                decorated.Reveal("after reset");
                return new FactoryOutcome(
                    decorator.GetType(),
                    decorated.Reveal("factory"),
                    sharedBeforeReset,
                    shared,
                    ReferenceEquals(decorated, (ISecretService)decorator),
                    ReferenceEquals(decorator.Instance, hidden),
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
                because.ItsTrue("a handler in the interface's shared store runs for it", outcome.SharedBeforeReset == 1, $"runs: {outcome.SharedBeforeReset}");
                because.ItsTrue("Reset<I>() empties that store for a decorator already created", outcome.SharedAfterReset == 1, $"runs: {outcome.SharedAfterReset}");
                because.ItsTrue("the cast to the interface returns the decorator, not the instance", outcome.CastIsDecorator);
                because.ItsTrue("Instance unwraps it", outcome.InstanceUnwraps);
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
                because.ItsTrue("no file is written when a clash is found", outcome.FilesWritten == 0, $"files: {outcome.FilesWritten}");
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest]
        public void RefuseToWriteSameNamedDecoratorsFromDifferentNamespaces()
        {
            When.A<DecoratorGenerator>("queues two same-named interfaces from different namespaces", new DecoratorGenerator(), generator =>
            {
                string directory = Path.Combine(Path.GetTempPath(), "bam.generators.decorators.tests", Guid.NewGuid().ToString("N"));
                try
                {
                    generator.AddInterface<ClashBilling.IClient>().AddInterface<ClashShipping.IClient>();
                    string message = Diagnose(() => generator.WriteSource(directory));
                    string caseMessage = Diagnose(() => new DecoratorGenerator().AddInterface<ClashBilling.IClient>().AddInterface<ClashShipping.Iclient>().WriteSource(directory));
                    int afterRefusals = Directory.Exists(directory) ? Directory.GetFiles(directory).Length : 0;
                    IReadOnlyList<string> alone = new DecoratorGenerator().AddInterface<ClashBilling.IClient>().AddInterface<ISecretService>().WriteSource(directory);
                    return new NamespaceClashOutcome(message, caseMessage, afterRefusals, alone.Count, Directory.GetFiles(directory).Length);
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
            .ShouldPass<NamespaceClashOutcome>((because, outcome) =>
            {
                because.ItsTrue("the clash is refused and names the file", outcome.Message.Contains("IClientDecorator.cs") && outcome.Message.Contains("overwrite"), outcome.Message);
                because.ItsTrue("it names both decorators", outcome.Message.Contains(new DecoratorModel(typeof(ClashBilling.IClient), null).DecoratorFullName) && outcome.Message.Contains(new DecoratorModel(typeof(ClashShipping.IClient), null).DecoratorFullName), outcome.Message);
                because.ItsTrue("names differing only by case are refused too", outcome.CaseMessage.Contains("overwrite"), outcome.CaseMessage);
                because.ItsTrue("no file is written when a clash is found", outcome.FilesAfterRefusals == 0, $"files: {outcome.FilesAfterRefusals}");
                because.ItsTrue("distinct names are still written, one file each", outcome.PathsReturned == 2 && outcome.FilesWritten == 2, $"paths: {outcome.PathsReturned}, files: {outcome.FilesWritten}");
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest]
        public void ReportTheMethodOfWhateverInstanceTheRegistrationResolves()
        {
            When.A<DecoratorGenerator>("decorates an interface whose registration resolves the service or a wrapper around it", new DecoratorGenerator(), generator =>
            {
                return new WrapperOutcome(RevealThroughRegistry(false), RevealThroughRegistry(true));
            })
            .TheTest
            .ShouldPass<WrapperOutcome>((because, outcome) =>
            {
                because.ItsTrue("the service itself: ctx.Method is the service's method", outcome.Direct.DeclaringType == typeof(GuardedSecret), outcome.Direct.DeclaringType?.FullName);
                because.ItsTrue("and its attribute is there to read", outcome.Direct.SeesGuarded);
                because.ItsTrue("the call reaches the service", outcome.Direct.Result == "guarded:k", outcome.Direct.Result);
                because.ItsTrue("a wrapper: ctx.Method is the wrapper's method", outcome.Wrapped.DeclaringType == typeof(SecretWrapper), outcome.Wrapped.DeclaringType?.FullName);
                because.ItsTrue("so the service's attribute is not on it (a guard keyed on it would pass)", !outcome.Wrapped.SeesGuarded);
                because.ItsTrue("the method name is the same either way", outcome.Direct.MethodName == nameof(ISecretService.Reveal) && outcome.Wrapped.MethodName == nameof(ISecretService.Reveal), outcome.Direct.MethodName + "," + outcome.Wrapped.MethodName);
                because.ItsTrue("the wrapped call reaches the service through the wrapper", outcome.Wrapped.Result == "wrapped:guarded:k", outcome.Wrapped.Result);
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest]
        public void ReportTheInterfaceMethodWhenTheInstanceHasNoInterfaceMap()
        {
            When.A<Decorator<ICollection<int>, ICollection<int>>>("wraps an array through a generic interface", new Decorator<ICollection<int>, ICollection<int>>(new int[] { 1, 2, 3 }), decorator =>
            {
                MethodInfo getCount = typeof(ICollection<int>).GetProperty(nameof(ICollection<int>.Count))!.GetGetMethod()!;
                MethodInfo? seen = null;
                decorator.Subscribe(DecoratorPhase.Start, getCount.Name, context =>
                {
                    seen = context.Method;
                });
                int count = decorator.Intercept<int>(getCount, getCount.Name, Array.Empty<object?>(), () => decorator.Instance.Count).Value;
                int again = decorator.Intercept<int>(getCount, getCount.Name, Array.Empty<object?>(), () => decorator.Instance.Count).Value;
                return new UnmappedOutcome(count, again, seen, getCount);
            })
            .TheTest
            .ShouldPass<UnmappedOutcome>((because, outcome) =>
            {
                because.ItsTrue("the call goes through", outcome.Count == 3 && outcome.Again == 3, $"{outcome.Count}, {outcome.Again}");
                because.ItsTrue("handlers are told the interface method", outcome.Seen == outcome.InterfaceMethod, outcome.Seen?.ToString());
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        private static WrapperCall RevealThroughRegistry(bool wrap)
        {
            ServiceRegistry registry = new ServiceRegistry();
            GuardedSecret service = new GuardedSecret();
            registry.For<ISecretService>().Use(wrap ? new SecretWrapper(service) : service);
            MethodInfo? seen = null;
            registry.Decorate<ISecretService>();
            registry.OnMethodStart<ISecretService, ISecretService>(nameof(ISecretService.Reveal), context =>
            {
                seen = context.Method;
            });
            string result = registry.Get<ISecretService>().Reveal("k");
            return new WrapperCall(result, seen?.DeclaringType, seen?.Name ?? string.Empty, seen?.GetCustomAttribute<GuardedAttribute>() != null);
        }

        [UnitTest]
        public void CompileAnInterfaceOnlyDecoratorOnTheSpot()
        {
            After.Setup(reg =>
            {
                reg.For<IWhisper>().Use(new Whisperer());
                reg.For<IEchoService>().Use(new EchoService());
                reg.For<ServiceRegistry>().Use(reg);
            })
            .When<ServiceRegistry>("decorates an interface for which no generated decorator is loaded", registry =>
            {
                // No decorator for IWhisper is checked in or compiled by another test, so Decorate<I>() has to
                // reach DecoratorTypeResolver.Resolve(I, I) and compile one. The echo pair pins the other
                // direction: a service decorated as a pair cannot also be decorated as the interface alone.
                Type? before = GeneratedDecorators.Find(typeof(IWhisper), typeof(IWhisper));
                MethodInfo? seen = null;
                registry.Decorate<IWhisper>();
                registry.OnMethodStart<IWhisper, IWhisper>(nameof(IWhisper.Whisper), context => seen = context.Method);
                IWhisper whisper = registry.Get<IWhisper>();
                string result = whisper.Whisper("psst");
                registry.Decorate<IEchoService, EchoService>();
                string mixed = DiagnoseAny(() => registry.Decorate<IEchoService>());
                return new CompileOutcome(before, whisper.GetType(), result, seen?.DeclaringType, mixed);
            })
            .TheTest
            .ShouldPass<CompileOutcome>((because, outcome) =>
            {
                because.ItsTrue("no generated decorator for the interface was loaded beforehand", outcome.Before == null, outcome.Before?.FullName);
                because.ItsTrue("the compiled decorator extends Decorator<I, I>", outcome.DecoratorType.BaseType == typeof(Decorator<IWhisper, IWhisper>), outcome.DecoratorType.BaseType?.FullName);
                because.ItsTrue("and is named after the interface", outcome.DecoratorType.Name == "IWhisperDecorator", outcome.DecoratorType.Name);
                because.ItsTrue("and was compiled in memory rather than found in a loaded file", string.IsNullOrEmpty(outcome.DecoratorType.Assembly.Location) && outcome.DecoratorType.Assembly != typeof(InterfaceOnlyDecoratorsShould).Assembly, outcome.DecoratorType.Assembly.FullName);
                because.ItsTrue("the call is forwarded", outcome.Result == "whisper:psst", outcome.Result);
                because.ItsTrue("the handler sees the method on the instance's runtime type", outcome.SeenDeclaringType == typeof(Whisperer), outcome.SeenDeclaringType?.FullName);
                because.ItsTrue("decorating as a pair and then as the interface alone is refused", outcome.Mixed.Contains("already decorated as") && outcome.Mixed.Contains("EchoService"), outcome.Mixed);
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        private static string DiagnoseAny(Action action)
        {
            try
            {
                action();
                return string.Empty;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
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

        /// <summary>A service no checked-in decorator covers, so Decorate&lt;I&gt;() has to compile one.</summary>
        public interface IWhisper
        {
            string Whisper(string words);
        }

        public class Whisperer : IWhisper
        {
            public string Whisper(string words) => "whisper:" + words;
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

        [AttributeUsage(AttributeTargets.Method)]
        private sealed class GuardedAttribute : Attribute
        {
        }

        /// <summary>The real service, whose method carries the attribute a guard might look for.</summary>
        private sealed class GuardedSecret : ISecretService
        {
            [Guarded]
            public string Reveal(string key)
            {
                return "guarded:" + key;
            }
        }

        /// <summary>What a factory registration might resolve in its place: a wrapper without the attribute.</summary>
        private sealed class SecretWrapper : ISecretService
        {
            private readonly ISecretService _inner;

            public SecretWrapper(ISecretService inner)
            {
                _inner = inner;
            }

            public string Reveal(string key)
            {
                return "wrapped:" + _inner.Reveal(key);
            }
        }

        private sealed record NamespaceClashOutcome(string Message, string CaseMessage, int FilesAfterRefusals, int PathsReturned, int FilesWritten);

        private sealed record WrapperCall(string Result, Type? DeclaringType, string MethodName, bool SeesGuarded);

        private sealed record WrapperOutcome(WrapperCall Direct, WrapperCall Wrapped);

        private sealed record UnmappedOutcome(int Count, int Again, MethodInfo? Seen, MethodInfo InterfaceMethod);

        private sealed record SourceOutcome(bool InterfaceOnly, bool PairInterfaceOnly, bool InterfaceStandsIn, string TypeName, string DecorateMethodName, bool ExtendsPair, bool CtorTakesInterface, bool DocumentsAny, bool NullAgrees, bool FixtureCurrent, bool NoRegistryFixtureCurrent);

        private sealed record WrapOutcome(string Result, int Calls, Type? SeenDeclaringType, bool SeesInstance, bool Hidden, Type ImplementationType);

        private sealed record RegistryOutcome(string Result, Type ResolvedType, Type ImplementationType, string Seen, bool SameRegistration);

        private sealed record FactoryOutcome(Type DecoratorType, string Result, int SharedBeforeReset, int SharedAfterReset, bool CastIsDecorator, bool InstanceUnwraps, bool SameStore, bool DistinctFromPair, Type? FoundForInterface, Type? FoundForPair);

        private sealed record CompileOutcome(Type? Before, Type DecoratorType, string Result, Type? SeenDeclaringType, string Mixed);

        private sealed record ValidateOutcome(string Class, string OpenGeneric, string Hidden, string HiddenImplementation, string OtherInterface);

        private sealed record ClashOutcome(string Message, int FilesWritten);
    }
}

namespace Bam.Generators.Decorators.Tests.Unit.ClashBilling
{
    /// <summary>Shares its simple name with <see cref="ClashShipping.IClient"/>: both decorators would be written to IClientDecorator.cs.</summary>
    public interface IClient
    {
        int Bill();
    }
}

namespace Bam.Generators.Decorators.Tests.Unit.ClashShipping
{
    /// <summary>Shares its simple name with <see cref="ClashBilling.IClient"/>.</summary>
    public interface IClient
    {
        int Ship();
    }

    /// <summary>Differs from <see cref="ClashBilling.IClient"/> by case alone, which a case-insensitive file system does not tell apart.</summary>
    public interface Iclient
    {
        int Ship();
    }
}
