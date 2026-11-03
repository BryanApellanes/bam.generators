using Bam.DependencyInjection;
using Bam.Generators.Decorators.Tests.Fixtures;
using Bam.Generators.Decorators.Tests.Fixtures.Decorators;
using Bam.Test;
using Microsoft.CodeAnalysis;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Loader;

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
                IEchoService echo = DecoratorFactory.Create<IEchoService, EchoService>(service, logger);
                Decorator<IEchoService, EchoService> decorator = (Decorator<IEchoService, EchoService>)(object)echo;
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
        public void CompileTheDocumentedContainerHook()
        {
            // The README's hook, verbatim in shape: a method returning the service interface from Create<I, T>.
            const string source =
                "using Bam.Generators.Decorators;\n" +
                "using Bam.Generators.Decorators.Tests.Fixtures;\n" +
                "public static class Host\n" +
                "{\n" +
                "    public static IEchoService Decorate(EchoService inner) => DecoratorFactory.Create<IEchoService, EchoService>(inner);\n" +
                "}\n";

            When.A<string>("is the README's container hook", source, hookSource =>
            {
                try
                {
                    Assembly loaded = Assembly.Load(new RoslynCompiler().Compile("DocumentedHook.Test", hookSource, typeof(DecoratorFactory), typeof(IEchoService)));
                    MethodInfo decorate = loaded.GetType("Host")!.GetMethod("Decorate")!;
                    EchoService inner = new EchoService();
                    object? decorated = decorate.Invoke(null, new object[] { inner });
                    string answer = decorated is IEchoService echo ? echo.Message("hi") : string.Empty;
                    return new HookOutcome(true, decorated?.GetType(), answer, inner.Calls, string.Empty);
                }
                catch (Exception ex)
                {
                    return new HookOutcome(false, null, string.Empty, 0, ex.Message);
                }
            })
            .TheTest
            .ShouldPass<HookOutcome>((because, outcome) =>
            {
                because.ItsTrue("the documented hook compiles", outcome.Compiled, outcome.Diagnostics);
                because.ItsTrue("it returns the generated decorator as the service interface", outcome.DecoratedType == typeof(EchoServiceDecorator), outcome.DecoratedType?.FullName);
                because.ItsTrue("the call goes through the decorator to the instance", outcome.Answer == "hi" && outcome.Calls == 1);
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest]
        public void EmptyTheSharedStore()
        {
            When.A<ContainerAgnosticDecoratorsShould>("resets a pair's shared store", this, test =>
            {
                List<string> seen = new List<string>();
                Action<DecoratorInvocationContext<Fuse>> shared = context => seen.Add("shared:" + context.Args[0]);
                DecoratorFactory.Handlers<IFuse, Fuse>().Add(DecoratorPhase.Start, nameof(IFuse.Light), shared);
                FuseDecorator decorator = DecoratorFactory.Create<IFuse, Fuse, FuseDecorator>(new Fuse());
                decorator.Subscribe(DecoratorPhase.Start, nameof(IFuse.Light), context => seen.Add("own:" + context.Args[0]));
                decorator.Light("1");
                DecoratorFactory.Reset<IFuse, Fuse>();
                decorator.Light("2");
                DecoratorFactory.Handlers<IFuse, Fuse>().Add(DecoratorPhase.Start, nameof(IFuse.Light), shared);
                decorator.Light("3");
                DecoratorFactory.Reset<IFuse, Fuse>();

                // Remove and Clear on a registry of their own, so no other test's subscriptions take part.
                DecoratorHandlerRegistry<DecoratorInvocationContext<Fuse>> registry = new DecoratorHandlerRegistry<DecoratorInvocationContext<Fuse>>();
                Func<DecoratorInvocationContext<Fuse>, object?> untyped = context => null;
                Func<DecoratorInvocationContext<Fuse>, string?> typed = context => null;
                Action<DecoratorInvocationContext<Fuse>> action = context => { };
                registry.Add(DecoratorPhase.End, nameof(IFuse.Light), untyped);
                registry.Add(DecoratorPhase.End, nameof(IFuse.Light), typed);
                registry.Add(DecoratorPhase.End, nameof(IFuse.Light), action);
                registry.Add(DecoratorPhase.End, nameof(IFuse.Light), action);
                int afterAdds = registry.Count(DecoratorPhase.End, nameof(IFuse.Light));
                bool removedTyped = registry.Remove(DecoratorPhase.End, nameof(IFuse.Light), typed);
                bool removedAction = registry.Remove(DecoratorPhase.End, nameof(IFuse.Light), action) && registry.Remove(DecoratorPhase.End, nameof(IFuse.Light), action);
                bool removedAgain = registry.Remove(DecoratorPhase.End, nameof(IFuse.Light), action);
                int afterRemoves = registry.Count(DecoratorPhase.End, nameof(IFuse.Light));
                registry.Add(DecoratorPhase.Error, DecoratorHandlerRegistry<DecoratorInvocationContext<Fuse>>.AnyMethod, untyped);
                bool cleared = registry.Clear(DecoratorPhase.End, nameof(IFuse.Light));
                int afterClear = registry.Count(DecoratorPhase.End, nameof(IFuse.Light));
                int errorsLeft = registry.Count(DecoratorPhase.Error, nameof(IFuse.Light));
                registry.Clear();
                int afterClearAll = registry.Count(DecoratorPhase.Error, nameof(IFuse.Light));

                return new ResetOutcome(string.Join(",", seen), afterAdds, removedTyped && removedAction, removedAgain, afterRemoves, cleared, afterClear, errorsLeft, afterClearAll);
            })
            .TheTest
            .ShouldPass<ResetOutcome>((because, outcome) =>
            {
                because.ItsTrue("a decorator created before Reset stops running the shared handler and keeps its own, and a later subscription runs again", outcome.Seen == "shared:1,own:1,own:2,shared:3,own:3", outcome.Seen);
                because.ItsTrue("an action subscribed twice is stored twice, as before", outcome.AfterAdds == 4, $"after adds: {outcome.AfterAdds}");
                because.ItsTrue("typed and action handlers are removed by the delegate the subscriber holds, one subscription per call", outcome.Removed);
                because.ItsTrue("removing a handler that is gone reports false", !outcome.RemovedAgain);
                because.ItsTrue("the untyped handler remains", outcome.AfterRemoves == 1);
                because.ItsTrue("Clear(phase, method) empties that entry only", outcome.Cleared && outcome.AfterClear == 0 && outcome.ErrorsLeft == 1);
                because.ItsTrue("Clear() empties everything", outcome.AfterClearAll == 0);
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest]
        public void WrapAConstructorFailure()
        {
            When.A<Fuse>("has a decorator whose constructor throws", new Fuse(), fuse =>
            {
                try
                {
                    DecoratorFactory.Create<IFuse, Fuse, ExplodingFuseDecorator>(fuse);
                    return new WrapOutcome(null);
                }
                catch (Exception ex)
                {
                    return new WrapOutcome(ex);
                }
            })
            .TheTest
            .ShouldPass<WrapOutcome>((because, outcome) =>
            {
                because.ItsTrue("a DecoratorException is thrown, not a TargetInvocationException", outcome.Thrown is DecoratorException, outcome.Thrown?.GetType().Name);
                because.ItsTrue("its message carries the constructor's", outcome.Thrown?.Message.Contains("lit too early") == true, outcome.Thrown?.Message);
                because.ItsTrue("the constructor's exception is the inner exception", outcome.Thrown?.InnerException is InvalidOperationException, outcome.Thrown?.InnerException?.GetType().Name);
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest]
        public void PreferCandidatesByRule()
        {
            SweepTwinDirectories();
            string twinDirectory = Path.Combine(Path.GetTempPath(), TwinDirectoryPrefix + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(twinDirectory);

            When.A<DecoratorGenerator>("has twins of the fixture loaded from bytes and from disk", new DecoratorGenerator().WithoutRegistryExtensions(), generator =>
            {
                // The decorator the fixtures assembly holds, compiled again: twice from bytes (another assembly,
                // no Location), once written to disk and loaded from there (another assembly, a Location), and
                // once as an empty type in a dynamic assembly (Location throws). All four sit at the conventional
                // name. OddEchoDecorator is hand-written in this assembly at another name. Prefer is pure, so each
                // rule is tested with the loser first in the list and with the other rules tied.
                DecoratorModel model = new DecoratorModel(typeof(IEchoService), typeof(EchoService), false);
                string source = generator.GetSource<IEchoService, EchoService>();
                RoslynCompiler compiler = new RoslynCompiler();
                Type bytesTwin = Assembly.Load(compiler.Compile("EchoTwin.Bytes.A", source, model.ReferencedTypes)).GetType(model.DecoratorFullName)!;
                Type bytesTwinB = Assembly.Load(compiler.Compile("EchoTwin.Bytes.B", source, model.ReferencedTypes)).GetType(model.DecoratorFullName)!;
                string diskPath = Path.Combine(twinDirectory, "EchoTwin.Disk.dll");
                File.WriteAllBytes(diskPath, compiler.Compile("EchoTwin.Disk", source, model.ReferencedTypes));
                Type diskTwin = Assembly.LoadFrom(diskPath).GetType(model.DecoratorFullName)!;
                AssemblyBuilder dynamicAssembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("EchoTwin.Dynamic"), AssemblyBuilderAccess.Run);
                Type dynamicTwin = dynamicAssembly.DefineDynamicModule("EchoTwin.Dynamic").DefineType(model.DecoratorFullName, TypeAttributes.Public | TypeAttributes.Class).CreateType()!;
                Type fixture = typeof(EchoServiceDecorator);
                Type odd = typeof(OddEchoDecorator);

                // A pair whose implementation is in an assembly loaded from bytes, with its decorator in that same
                // assembly, against a decorator for the pair written to disk from another assembly: the name rule
                // ties, so only the assembly rule standing ahead of the Location rule picks the bytes one.
                PlugPair plug = CompilePlugPair(generator, compiler, twinDirectory);

                return new PreferenceOutcome(
                    GeneratedDecorators.Prefer(new Type[] { odd, fixture }, typeof(EchoService)) == fixture && GeneratedDecorators.Prefer(new Type[] { odd }, typeof(EchoService)) == odd,
                    GeneratedDecorators.Prefer(new Type[] { bytesTwin, fixture }, typeof(EchoService)) == fixture,
                    GeneratedDecorators.Prefer(new Type[] { diskTwin, fixture }, typeof(EchoService)) == fixture && !string.IsNullOrEmpty(diskTwin.Assembly.Location),
                    GeneratedDecorators.Prefer(new Type[] { plug.DiskDecorator, plug.BytesDecorator }, plug.Implementation) == plug.BytesDecorator && string.IsNullOrEmpty(plug.Implementation.Assembly.Location) && !string.IsNullOrEmpty(plug.DiskDecorator.Assembly.Location),
                    GeneratedDecorators.Prefer(new Type[] { bytesTwin, diskTwin }, typeof(EchoService)) == diskTwin && string.IsNullOrEmpty(bytesTwin.Assembly.Location),
                    GeneratedDecorators.Prefer(new Type[] { dynamicTwin, diskTwin }, typeof(EchoService)) == diskTwin && GeneratedDecorators.Prefer(new Type[] { dynamicTwin, bytesTwin }, typeof(EchoService)) == dynamicTwin && dynamicTwin.Assembly.IsDynamic,
                    GeneratedDecorators.Prefer(new Type[] { bytesTwinB, bytesTwin }, typeof(EchoService)) == bytesTwinB && GeneratedDecorators.Prefer(new Type[] { bytesTwin, bytesTwinB }, typeof(EchoService)) == bytesTwin,
                    GeneratedDecorators.Prefer(Array.Empty<Type>(), typeof(EchoService)) == null,
                    GeneratedDecorators.FindAll(typeof(IEchoService), typeof(EchoService)).Count(candidate => candidate == bytesTwin || candidate == bytesTwinB || candidate == diskTwin || candidate == odd),
                    GeneratedDecorators.Find(typeof(IEchoService), typeof(EchoService)));
            })
            .TheTest
            .ShouldPass<PreferenceOutcome>((because, outcome) =>
            {
                because.ItsTrue("the conventional name beats another name", outcome.ConventionalName);
                because.ItsTrue("the implementation's assembly beats another assembly loaded from bytes", outcome.OwnAssemblyOverBytes);
                because.ItsTrue("the implementation's assembly beats another assembly loaded from disk", outcome.OwnAssemblyOverDisk);
                because.ItsTrue("the implementation's assembly without a Location beats another assembly with one", outcome.OwnAssemblyOverLocation);
                because.ItsTrue("an assembly loaded from disk beats one loaded from bytes", outcome.FromDisk);
                because.ItsTrue("a dynamic assembly counts as loaded from bytes instead of throwing", outcome.DynamicTolerated);
                because.ItsTrue("when every rule ties, the first in the list wins", outcome.FirstWhenEqual);
                because.ItsTrue("no candidates gives null", outcome.EmptyIsNull);
                because.ItsTrue("all four extra candidates are loaded and found", outcome.ExtraCandidates == 4, $"extra candidates found: {outcome.ExtraCandidates}");
                because.ItsTrue("Find still returns the fixture with every twin loaded", outcome.Found == typeof(EchoServiceDecorator), outcome.Found?.Assembly.FullName);
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        private const string TwinDirectoryPrefix = "bam-decorator-twins-";

        /// <summary>
        /// Removes the twin directories earlier runs left behind. A loaded twin is locked for the life of its
        /// process, so a run cannot delete its own; a directory still locked by another run is skipped.
        /// </summary>
        private static void SweepTwinDirectories()
        {
            IEnumerable<string> stale = Directory.EnumerateDirectories(Path.GetTempPath(), TwinDirectoryPrefix + "*")
                .Append(Path.Combine(Path.GetTempPath(), "bam-decorator-tests"));
            foreach (string directory in stale)
            {
                try
                {
                    if (Directory.Exists(directory))
                    {
                        Directory.Delete(directory, true);
                    }
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
        }

        /// <summary>
        /// Compiles a service pair and its decorator into one assembly loaded from bytes, and the same decorator
        /// again into a file loaded from disk. The bytes assembly is not resolvable by name, so the disk decorator's
        /// base type is served through the default load context's Resolving event for the life of the process.
        /// </summary>
        private static PlugPair CompilePlugPair(DecoratorGenerator generator, RoslynCompiler compiler, string twinDirectory)
        {
            const string plugNamespace = "Bam.Generators.Decorators.Tests.Plugs";
            const string plugSource = "namespace " + plugNamespace + " { public interface IPlug { string Name(); } public class Plug : IPlug { public string Name() { return \"plug\"; } } }";
            Assembly stageOne = Assembly.Load(compiler.Compile("Plug.StageOne", plugSource, typeof(object)));
            Type stageOneInterface = stageOne.GetType(plugNamespace + ".IPlug")!;
            Type stageOneImplementation = stageOne.GetType(plugNamespace + ".Plug")!;
            DecoratorModel model = new DecoratorModel(stageOneInterface, stageOneImplementation, false);
            string decoratorSource = generator.GetSource(stageOneInterface, stageOneImplementation);
            MetadataReference[] located = model.ReferencedTypes
                .Select(type => type.Assembly)
                .Where(assembly => !string.IsNullOrEmpty(assembly.Location))
                .Distinct()
                .Select(assembly => (MetadataReference)MetadataReference.CreateFromFile(assembly.Location))
                .ToArray();

            byte[] bytesImage = compiler.Compile("Plug.Bytes", plugSource + Environment.NewLine + decoratorSource, () => compiler.MetadataReferenceResolver.GetMetaDataReferences().Concat(located).ToArray());
            Assembly bytesAssembly = Assembly.Load(bytesImage);
            AssemblyLoadContext.Default.Resolving += (context, name) => name.Name == "Plug.Bytes" ? bytesAssembly : null;
            Type implementation = bytesAssembly.GetType(plugNamespace + ".Plug")!;
            Type bytesDecorator = bytesAssembly.GetType(model.DecoratorFullName)!;

            string diskPath = Path.Combine(twinDirectory, "Plug.Disk.dll");
            File.WriteAllBytes(diskPath, compiler.Compile("Plug.Disk", decoratorSource, () => compiler.MetadataReferenceResolver.GetMetaDataReferences().Concat(located).Append(MetadataReference.CreateFromImage(bytesImage)).ToArray()));
            Type diskDecorator = Assembly.LoadFrom(diskPath).GetType(model.DecoratorFullName)!;
            return new PlugPair(implementation, bytesDecorator, diskDecorator);
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

        /// <summary>A pair for the shared-store and constructor-failure tests, so no other test's subscriptions take part.</summary>
        public interface IFuse
        {
            string Light(string value);
        }

        public class Fuse : IFuse
        {
            public string Light(string value)
            {
                return "lit " + value;
            }
        }

        public class FuseDecorator : Decorator<IFuse, Fuse>, IFuse
        {
            public FuseDecorator(Fuse instance, IDecoratorLogger? logger = null) : base(instance, logger)
            {
            }

            public string Light(string value)
            {
                return Intercept<string>(nameof(Light), new object?[] { value }, () => Instance.Light(value)).Value!;
            }
        }

        public class ExplodingFuseDecorator : Decorator<IFuse, Fuse>, IFuse
        {
            public ExplodingFuseDecorator(Fuse instance, IDecoratorLogger? logger = null) : base(instance, logger)
            {
                throw new InvalidOperationException("lit too early");
            }

            public string Light(string value)
            {
                return Instance.Light(value);
            }
        }

        /// <summary>A decorator of the echo pair at a name the generator would not use; the conventional one must beat it.</summary>
        public class OddEchoDecorator : Decorator<IEchoService, EchoService>, IEchoService
        {
            public OddEchoDecorator(EchoService instance, IDecoratorLogger? logger = null) : base(instance, logger)
            {
            }

            public string Message(string message)
            {
                return Intercept<string>(nameof(Message), new object?[] { message }, () => Instance.Message(message)).Value!;
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

        private sealed record HookOutcome(bool Compiled, Type? DecoratedType, string Answer, int Calls, string Diagnostics);

        private sealed record ResetOutcome(string Seen, int AfterAdds, bool Removed, bool RemovedAgain, int AfterRemoves, bool Cleared, int AfterClear, int ErrorsLeft, int AfterClearAll);

        private sealed record WrapOutcome(Exception? Thrown);

        private sealed record PreferenceOutcome(bool ConventionalName, bool OwnAssemblyOverBytes, bool OwnAssemblyOverDisk, bool OwnAssemblyOverLocation, bool FromDisk, bool DynamicTolerated, bool FirstWhenEqual, bool EmptyIsNull, int ExtraCandidates, Type? Found);

        private sealed record PlugPair(Type Implementation, Type BytesDecorator, Type DiskDecorator);
    }
}
