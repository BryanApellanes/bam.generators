using Bam.DependencyInjection;
using Bam.Generators.Decorators.Tests.Fixtures;
using Bam.Generators.Decorators.Tests.Fixtures.Decorators;
using Bam.Test;
using System.Reflection;

namespace Bam.Generators.Decorators.Tests.Unit
{
    /// <summary>
    /// Pins bam.generators#15: a decorator generated without the <c>ServiceRegistry</c> extension methods
    /// compiles against the runtime alone, and <see cref="DecoratorFactory"/> creates generated decorators for
    /// hosts on another container, sharing handlers per service pair.
    /// </summary>
    [UnitTestMenu("Container-agnostic decorators Should", Selector = "cads")]
    public class ContainerAgnosticDecoratorsShould : UnitTestMenuContainer
    {
        public ContainerAgnosticDecoratorsShould(ServiceRegistry serviceRegistry) : base(serviceRegistry)
        {
        }

        [UnitTest]
        public void OmitTheRegistryExtensionsWhenAsked()
        {
            When.A<DecoratorGenerator>("generates without registry extensions", new DecoratorGenerator().WithoutRegistryExtensions(), generator =>
            {
                string source = generator.GetSource<IEchoService, EchoService>();
                return new SourceOutcome(
                    generator.RegistryExtensions,
                    source.Contains("ServiceRegistry"),
                    source.Contains("Bam.Logging"),
                    source.Contains("DecoratorRegistration"),
                    source.Contains("DecorateEchoService"),
                    source.Contains("public partial class EchoServiceDecorator"),
                    source.Contains("public static partial class EchoServiceDecoratorExtensions"),
                    source.Contains("OnMessageStart(this global::Bam.Generators.Decorators.Tests.Fixtures.IEchoService service, global::System.Action<"),
                    source.Contains("OnMessageError(this global::Bam.Generators.Decorators.Tests.Fixtures.IEchoService service, global::System.Func<"),
                    Normalize(source) == Normalize(CheckedIn("NoRegistry.EchoServiceDecorator.cs")));
            })
            .TheTest
            .ShouldPass<SourceOutcome>((because, outcome) =>
            {
                because.ItsTrue("the generator reports the option off", !outcome.RegistryExtensions);
                because.ItsTrue("the source names no ServiceRegistry", !outcome.MentionsRegistry);
                because.ItsTrue("the source names no Bam.Logging type", !outcome.MentionsLogging);
                because.ItsTrue("the source names no DecoratorRegistration", !outcome.MentionsRegistration);
                because.ItsTrue("the Decorate method is not generated", !outcome.HasDecorateMethod);
                because.ItsTrue("the decorator type is generated", outcome.HasDecorator);
                because.ItsTrue("the extensions class is still generated", outcome.HasExtensionsClass);
                because.ItsTrue("the observe-only hook on the service instance is generated", outcome.HasInstanceActionHook);
                because.ItsTrue("the typed hook on the service instance is generated", outcome.HasInstanceFuncHook);
                because.ItsTrue("Fixtures/Generated/NoRegistry/EchoServiceDecorator.cs is current", outcome.FixtureCurrent, "regenerate the fixture: the generator's no-registry output has changed");
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest]
        public void KeepTheRegistryExtensionsByDefault()
        {
            When.A<DecoratorGenerator>("generates with the defaults", new DecoratorGenerator(), generator =>
            {
                string source = generator.GetSource<IEchoService, EchoService>();
                bool restored = generator.WithoutRegistryExtensions().WithRegistryExtensions().RegistryExtensions;
                return new DefaultOutcome(
                    generator.RegistryExtensions,
                    source.Contains("DecorateEchoService(this global::Bam.DependencyInjection.ServiceRegistry registry, global::Bam.Logging.ILogger? logger = null)"),
                    source.Contains("OnMessageStart(this global::Bam.DependencyInjection.ServiceRegistry registry"),
                    source.Contains("OnMessageStart(this global::Bam.Generators.Decorators.Tests.Fixtures.IEchoService service"),
                    restored);
            })
            .TheTest
            .ShouldPass<DefaultOutcome>((because, outcome) =>
            {
                because.ItsTrue("the option defaults to on", outcome.RegistryExtensions);
                because.ItsTrue("the Decorate method is generated", outcome.HasDecorateMethod);
                because.ItsTrue("the registry hook overloads are generated", outcome.HasRegistryHook);
                because.ItsTrue("the instance hook overloads are generated", outcome.HasInstanceHook);
                because.ItsTrue("WithRegistryExtensions turns the option back on", outcome.Restored);
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest]
        public void DropTheRegistryTypesFromTheReferencesWhenOff()
        {
            When.A<DecoratorModel>("is built without registry extensions", new DecoratorModel(typeof(IEchoService), typeof(EchoService), false), model =>
            {
                string[] withoutRegistry = AssemblyNames(model.ReferencedTypes);
                string[] withRegistry = AssemblyNames(new DecoratorModel(typeof(IEchoService), typeof(EchoService)).ReferencedTypes);
                return new ReferencesOutcome(
                    model.RegistryExtensions,
                    model.Hooks.All(hook => !hook.RegistryExtensions),
                    withoutRegistry,
                    withRegistry.Contains(typeof(ServiceRegistry).Assembly.GetName().Name ?? string.Empty),
                    withoutRegistry.Contains(typeof(Decorator<>).Assembly.GetName().Name ?? string.Empty));
            })
            .TheTest
            .ShouldPass<ReferencesOutcome>((because, outcome) =>
            {
                because.ItsTrue("the model reports the option off", !outcome.RegistryExtensions);
                because.ItsTrue("every hook mirrors the option", outcome.HooksMirror);
                because.ItsTrue("no referenced type comes from a Bam framework assembly", outcome.WithoutRegistry.All(name => !name.StartsWith("bam.", StringComparison.OrdinalIgnoreCase) || name == "bam.generators.decorators.runtime" || name == "bam.generators.decorators.tests"), string.Join(", ", outcome.WithoutRegistry));
                because.ItsTrue("the default model still references bam.base", outcome.DefaultReferencesBamBase);
                because.ItsTrue("the runtime is still referenced", outcome.ReferencesRuntime);
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest]
        public void CompileWithoutRegistryExtensionsAgainstTheRuntimeAlone()
        {
            When.A<DecoratorGenerator>("generates without registry extensions", new DecoratorGenerator().WithoutRegistryExtensions(), generator =>
            {
                DecoratorModel model = new DecoratorModel(typeof(IEchoService), typeof(EchoService), false);
                string source = generator.GetSource<IEchoService, EchoService>();
                try
                {
                    byte[] assembly = new RoslynCompiler().Compile("NoRegistryDecorator.Test", source, model.ReferencedTypes);
                    Assembly loaded = Assembly.Load(assembly);
                    Type? decoratorType = loaded.GetType(model.DecoratorFullName);
                    Type? extensionsType = loaded.GetType(model.Namespace + "." + model.ExtensionsTypeName);
                    MethodInfo[] hooks = extensionsType?.GetMethods(BindingFlags.Public | BindingFlags.Static) ?? Array.Empty<MethodInfo>();
                    return new CompileOutcome(
                        true,
                        decoratorType != null && typeof(IEchoService).IsAssignableFrom(decoratorType),
                        hooks.Length,
                        hooks.All(hook => hook.GetParameters().FirstOrDefault()?.ParameterType == typeof(IEchoService)),
                        loaded.GetReferencedAssemblies().Select(name => name.Name ?? string.Empty).ToArray(),
                        string.Empty);
                }
                catch (Exception ex)
                {
                    return new CompileOutcome(false, false, 0, false, Array.Empty<string>(), ex.Message);
                }
            })
            .TheTest
            .ShouldPass<CompileOutcome>((because, outcome) =>
            {
                because.ItsTrue("the source compiles", outcome.Compiled, outcome.Diagnostics);
                because.ItsTrue("the decorator implements the service interface", outcome.ImplementsInterface);
                because.ItsTrue("six hooks are generated for the one method", outcome.HookCount == 6, $"hooks: {outcome.HookCount}");
                because.ItsTrue("every hook extends the service instance", outcome.HooksExtendTheService);
                because.ItsTrue("the compiled assembly references no Bam framework assembly", outcome.Referenced.All(name => !name.StartsWith("bam.", StringComparison.OrdinalIgnoreCase) || name == "bam.generators.decorators.runtime" || name == "bam.generators.decorators.tests"), string.Join(", ", outcome.Referenced));
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest]
        public void NameDecoratorsTheWayTheGeneratorDoes()
        {
            When.A<DecoratorModel>("names a decorator", new DecoratorModel(typeof(IEchoService), typeof(EchoService)), model =>
            {
                return new NamingOutcome(
                    GeneratedDecorators.SimpleName(typeof(Dictionary<string, int>)),
                    GeneratedDecorators.TypeNameOf(typeof(EchoService)) == model.DecoratorTypeName,
                    GeneratedDecorators.FullNameOf(typeof(EchoService)) == model.DecoratorFullName,
                    GeneratedDecorators.NamespaceOf(typeof(EchoService)) == model.Namespace,
                    typeof(EchoServiceDecorator).FullName == GeneratedDecorators.FullNameOf(typeof(EchoService)));
            })
            .TheTest
            .ShouldPass<NamingOutcome>((because, outcome) =>
            {
                because.ItsTrue("generic arguments are spelled out", outcome.GenericName == "DictionaryOfStringInt32", outcome.GenericName);
                because.ItsTrue("the type name matches the model", outcome.TypeNameMatches);
                because.ItsTrue("the full name matches the model", outcome.FullNameMatches);
                because.ItsTrue("the namespace matches the model", outcome.NamespaceMatches);
                because.ItsTrue("the checked-in fixture sits at the conventional name", outcome.FixtureAtConventionalName);
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest]
        public void FindTheLoadedDecoratorForAPair()
        {
            When.A<ContainerAgnosticDecoratorsShould>("looks for loaded decorators", this, test =>
            {
                return new FindOutcome(
                    GeneratedDecorators.Find(typeof(IEchoService), typeof(EchoService)),
                    GeneratedDecorators.Find(typeof(IOrphan), typeof(Orphan)),
                    GeneratedDecorators.Find(typeof(INobody), typeof(Nobody)),
                    GeneratedDecorators.FindAll(typeof(INobody), typeof(Nobody)).Count);
            })
            .TheTest
            .ShouldPass<FindOutcome>((because, outcome) =>
            {
                because.ItsTrue("the generated fixture is found", outcome.Echo == typeof(EchoServiceDecorator), outcome.Echo?.FullName);
                because.ItsTrue("a hand-written decorator at another name is found", outcome.Orphan == typeof(OrphanDecorator), outcome.Orphan?.FullName);
                because.ItsTrue("a pair with no decorator yields null", outcome.Nobody == null, outcome.Nobody?.FullName);
                because.ItsTrue("and no candidates", outcome.NobodyCandidates == 0);
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest]
        public void CreateTheGeneratedDecoratorThroughTheFactory()
        {
            When.A<EchoService>("is decorated by the factory", new EchoService(), service =>
            {
                RecordingLogger logger = new RecordingLogger();
                Decorator<IEchoService, EchoService> decorator = DecoratorFactory.Create<IEchoService, EchoService>(service, logger);
                IEchoService echo = (IEchoService)decorator;
                string forwarded = echo.Message("hello");
                Action<DecoratorInvocationContext<EchoService>> badHandler = context => throw new InvalidOperationException("bad handler");
                decorator.Subscribe(DecoratorPhase.Start, nameof(IEchoService.Message), badHandler);
                string afterFailure = echo.Message("again");
                EchoServiceDecorator typed = DecoratorFactory.Create<IEchoService, EchoService, EchoServiceDecorator>(service);
                return new CreateOutcome(
                    decorator.GetType(),
                    forwarded,
                    afterFailure,
                    service.Calls,
                    logger.Messages.Count,
                    ReferenceEquals(typed.Instance, service),
                    decorator.RegistrationHandlers.Count == 1 && ReferenceEquals(decorator.RegistrationHandlers[0], DecoratorFactory.Handlers<IEchoService, EchoService>()));
            })
            .TheTest
            .ShouldPass<CreateOutcome>((because, outcome) =>
            {
                because.ItsTrue("the generated decorator type is created", outcome.DecoratorType == typeof(EchoServiceDecorator), outcome.DecoratorType.FullName);
                because.ItsTrue("calls are forwarded", outcome.Forwarded == "hello" && outcome.AfterFailure == "again" && outcome.Calls == 2);
                because.ItsTrue("the given logger receives handler failures", outcome.Errors == 1, $"errors logged: {outcome.Errors}");
                because.ItsTrue("the typed overload wraps the same instance", outcome.TypedWrapsInstance);
                because.ItsTrue("the pair's shared handler store is attached", outcome.SharedStoreAttached);
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest]
        public void ShareHandlersAcrossDecoratorsOfOnePairOnly()
        {
            When.A<ContainerAgnosticDecoratorsShould>("creates two decorators of one pair", this, test =>
            {
                List<string> order = new List<string>();
                DecoratorFactory.Handlers<IOrphan, Orphan>().Add(DecoratorPhase.Start, nameof(IOrphan.Ping), context => order.Add("shared:" + context.Args[0]));
                OrphanDecorator first = DecoratorFactory.Create<IOrphan, Orphan, OrphanDecorator>(new Orphan());
                OrphanDecorator second = DecoratorFactory.Create<IOrphan, Orphan, OrphanDecorator>(new Orphan());
                first.Subscribe(DecoratorPhase.Start, nameof(IOrphan.Ping), context => order.Add("own:" + context.Args[0]));
                string one = first.Ping("1");
                string two = second.Ping("2");
                return new ShareOutcome(
                    one + two,
                    order.ToArray(),
                    ReferenceEquals(DecoratorFactory.Handlers<IOrphan, Orphan>(), DecoratorFactory.Handlers<IOrphan, Orphan>()),
                    ReferenceEquals(DecoratorFactory.Handlers<IOrphan, Orphan>(), DecoratorFactory.Handlers<IEchoService, EchoService>()));
            })
            .TheTest
            .ShouldPass<ShareOutcome>((because, outcome) =>
            {
                because.ItsTrue("both decorators forward", outcome.Pinged == "pong 1pong 2", outcome.Pinged);
                because.ItsTrue("the shared handler ran for both, before the first decorator's own handler", string.Join(",", outcome.Order) == "shared:1,own:1,shared:2", string.Join(",", outcome.Order));
                because.ItsTrue("the store is one per pair", outcome.SameStoreForPair);
                because.ItsTrue("and not shared across pairs", !outcome.SharedAcrossPairs);
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest]
        public void RefuseToCreateWhenNoDecoratorIsLoaded()
        {
            When.A<Nobody>("has no generated decorator", new Nobody(), nobody =>
            {
                string missing = string.Empty;
                try
                {
                    DecoratorFactory.Create<INobody, Nobody>(nobody);
                }
                catch (DecoratorException ex)
                {
                    missing = ex.Message;
                }

                bool nullRejected = false;
                try
                {
                    DecoratorFactory.Create<IEchoService, EchoService>(null!);
                }
                catch (ArgumentNullException)
                {
                    nullRejected = true;
                }

                return new RefuseOutcome(missing, nullRejected);
            })
            .TheTest
            .ShouldPass<RefuseOutcome>((because, outcome) =>
            {
                because.ItsTrue("a DecoratorException names the pair", outcome.Missing.Contains(typeof(INobody).FullName!) && outcome.Missing.Contains(typeof(Nobody).FullName!), outcome.Missing);
                because.ItsTrue("a null instance is rejected", outcome.NullRejected);
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        private static string[] AssemblyNames(IEnumerable<Type> types)
        {
            return types.Select(type => type.Assembly.GetName().Name ?? string.Empty).Distinct().OrderBy(name => name).ToArray();
        }

        private static string CheckedIn(string fileName)
        {
            using Stream? stream = typeof(ContainerAgnosticDecoratorsShould).Assembly.GetManifestResourceStream("Generated." + fileName);
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

        /// <summary>A pair with a hand-written decorator at a name the generator would not use.</summary>
        public interface IOrphan
        {
            string Ping(string value);
        }

        public class Orphan : IOrphan
        {
            public string Ping(string value)
            {
                return "pong " + value;
            }
        }

        public class OrphanDecorator : Decorator<IOrphan, Orphan>, IOrphan
        {
            public OrphanDecorator(Orphan instance, IDecoratorLogger? logger = null) : base(instance, logger)
            {
            }

            public string Ping(string value)
            {
                return Intercept<string>(nameof(Ping), new object?[] { value }, () => Instance.Ping(value)).Value!;
            }
        }

        /// <summary>A pair nothing decorates.</summary>
        public interface INobody
        {
            void Nothing();
        }

        public class Nobody : INobody
        {
            public void Nothing()
            {
            }
        }

        private sealed class RecordingLogger : IDecoratorLogger
        {
            public List<string> Messages { get; } = new List<string>();

            public void Error(string message, Exception? exception)
            {
                Messages.Add(message);
            }
        }

        private sealed record SourceOutcome(bool RegistryExtensions, bool MentionsRegistry, bool MentionsLogging, bool MentionsRegistration, bool HasDecorateMethod, bool HasDecorator, bool HasExtensionsClass, bool HasInstanceActionHook, bool HasInstanceFuncHook, bool FixtureCurrent);

        private sealed record DefaultOutcome(bool RegistryExtensions, bool HasDecorateMethod, bool HasRegistryHook, bool HasInstanceHook, bool Restored);

        private sealed record ReferencesOutcome(bool RegistryExtensions, bool HooksMirror, string[] WithoutRegistry, bool DefaultReferencesBamBase, bool ReferencesRuntime);

        private sealed record CompileOutcome(bool Compiled, bool ImplementsInterface, int HookCount, bool HooksExtendTheService, string[] Referenced, string Diagnostics);

        private sealed record NamingOutcome(string GenericName, bool TypeNameMatches, bool FullNameMatches, bool NamespaceMatches, bool FixtureAtConventionalName);

        private sealed record FindOutcome(Type? Echo, Type? Orphan, Type? Nobody, int NobodyCandidates);

        private sealed record CreateOutcome(Type DecoratorType, string Forwarded, string AfterFailure, int Calls, int Errors, bool TypedWrapsInstance, bool SharedStoreAttached);

        private sealed record ShareOutcome(string Pinged, string[] Order, bool SameStoreForPair, bool SharedAcrossPairs);

        private sealed record RefuseOutcome(string Missing, bool NullRejected);
    }
}
