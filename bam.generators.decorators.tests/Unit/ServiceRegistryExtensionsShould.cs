using Bam.DependencyInjection;
using Bam.Generators.Decorators.Tests.Fixtures;
using Bam.Generators.Decorators.Tests.Fixtures.Decorators;
using Bam.Logging;
using Bam.Test;
using NSubstitute;

namespace Bam.Generators.Decorators.Tests.Unit
{
    [UnitTestMenu("ServiceRegistryExtensions Should", Selector = "sres")]
    public class ServiceRegistryExtensionsShould : UnitTestMenuContainer
    {
        public ServiceRegistryExtensionsShould(ServiceRegistry serviceRegistry) : base(serviceRegistry)
        {
        }

        // Every registry gets a substitute logger and a resolver backed by the embedded templates only, so
        // tests never touch the default logger or a ./Templates directory.
        private static void Prepare(ServiceRegistry registry)
        {
            registry.For<ILogger>().Use(Substitute.For<ILogger>());
            registry.For<IDecoratorTypeResolver>().Use(DecoratorTypeResolverShould.NewResolver());
        }

        [UnitTest]
        public void Decorate()
        {
            string result = "Test Result";

            After.Setup(testCaseRegistry =>
            {
                Prepare(testCaseRegistry);
                testCaseRegistry.OnMethodStart(nameof(IEchoService.Message), decCtx =>
                {
                    // observes every decorated Message call in this registry
                });
                testCaseRegistry.For<IEchoService>().Use<EchoService>();
                testCaseRegistry.SubscribeStart<IEchoService, EchoService>(nameof(IEchoService.Message), decCtx =>
                {
                    return result;
                });
            })
            .When<IEchoService>("is decorated", dec =>
            {
                return new DecorateOutcome(dec.Message("ignored"), dec is EchoServiceDecorator, DecoratorExtensions.IsDecorated<EchoService>(dec));
            })
            .TheTest
            .ShouldPass<DecorateOutcome>((because, outcome) =>
            {
                because.ItsTrue("the start handler's value is returned", outcome.Message == result);
                because.ItsTrue("the service resolves to its generated decorator", outcome.IsGeneratedDecorator);
                because.ItsTrue("the service reports as decorated", outcome.IsDecorated);
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest]
        public void ReplaceTheRegistrationWithADecorator()
        {
            After.Setup(reg =>
            {
                Prepare(reg);
                reg.For<IEchoService>().Use<EchoService>();
                reg.For<ServiceRegistry>().Use(reg);
            })
            .When<ServiceRegistry>("decorates a registered service", registry =>
            {
                DecoratorRegistration<IEchoService, EchoService> registration = registry.Decorate<IEchoService, EchoService>();
                DecoratorRegistration<IEchoService, EchoService> again = registry.Decorate<IEchoService, EchoService>();
                IEchoService resolved = registry.Get<IEchoService>();
                Decorator<IEchoService, EchoService>? decorator = resolved as Decorator<IEchoService, EchoService>;
                return new RegistrationOutcome(
                    decorator is EchoServiceDecorator && registration.DecoratorType == typeof(EchoServiceDecorator),
                    ReferenceEquals(registration, again),
                    resolved.Message("hello"),
                    decorator?.Instance.Calls ?? -1,
                    ReferenceEquals(decorator?.SharedSubscriptions, registry.GetDecoratorSubscriptions()) && decorator?.RegistrationHandlers.Contains(registration.Handlers) == true,
                    registration.InterfaceType == typeof(IEchoService) && registration.ImplementationType == typeof(EchoService) && decorator?.InterfaceType == typeof(IEchoService));
            })
            .TheTest
            .ShouldPass<RegistrationOutcome>((because, outcome) =>
            {
                because.ItsTrue("resolving the interface yields the decorator", outcome.ResolvesToDecorator);
                because.ItsTrue("decorating again returns the same registration", outcome.Idempotent);
                because.ItsTrue("calls reach the wrapped service", outcome.Message == "hello" && outcome.Calls == 1);
                because.ItsTrue("the decorator shares the registry's and the registration's handlers", outcome.SharesSubscriptions);
                because.ItsTrue("the registration and decorator know their interface and implementation", outcome.KnowsItsTypes);
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest]
        public void DecorateAServiceWithNoGeneratedDecorator()
        {
            After.Setup(reg =>
            {
                Prepare(reg);
                reg.For<IGreeterService>().Use(new GreeterService());
                reg.OnMethodEnd<IGreeterService, GreeterService>(nameof(IGreeterService.Greet), context => context.Result + "!");
            })
            .When<IGreeterService>("is decorated by a decorator compiled at runtime", greeter =>
            {
                return new DecorateOutcome(greeter.Greet("bam"), false, DecoratorExtensions.IsDecorated<GreeterService>(greeter));
            })
            .TheTest
            .ShouldPass<DecorateOutcome>((because, outcome) =>
            {
                because.ItsTrue("the service is decorated", outcome.IsDecorated);
                because.ItsTrue("the end handler replaced the result", outcome.Message == "Hello, bam!");
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest]
        public void SubscribeByNameInEveryPhase()
        {
            After.Setup(reg =>
            {
                Prepare(reg);
                reg.For<IKitchenSinkService>().Use(new KitchenSinkService());
                reg.For<ServiceRegistry>().Use(reg);
            })
            .When<ServiceRegistry>("subscribes typed handlers by method name", registry =>
            {
                List<string> observed = new List<string>();
                registry
                    .OnMethodStart<IKitchenSinkService, KitchenSinkService>("Add", context => { observed.Add("start:" + context.Decorated.Name); })
                    .OnMethodEnd<IKitchenSinkService, KitchenSinkService>("Add", context => { observed.Add("end:" + context.Result); })
                    .OnMethodError<IKitchenSinkService, KitchenSinkService>("Fail", context => { observed.Add("error:" + context.Exception?.Message); })
                    .OnMethodStart<IKitchenSinkService, KitchenSinkService>("Find", context => "cached")
                    .OnMethodEnd<IKitchenSinkService, KitchenSinkService>("Invoke", context => "replaced")
                    .OnMethodError<IKitchenSinkService, KitchenSinkService>("Fail", context => "recovered")
                    .SubscribeEnd<IKitchenSinkService, KitchenSinkService>("AddAsync", context => 100)
                    .SubscribeError<IKitchenSinkService, KitchenSinkService>("FailAsync", context => "recovered async");

                IKitchenSinkService sink = registry.Get<IKitchenSinkService>();
                int sum = sink.Add(1, 2);
                return new PhaseOutcome(
                    string.Join("|", observed),
                    sum,
                    sink.Find("key"),
                    sink.Invoke("fired"),
                    sink.Fail("boom"),
                    string.Join("|", observed),
                    sink.AddAsync(1, 2).GetAwaiter().GetResult(),
                    sink.FailAsync("boom").GetAwaiter().GetResult());
            })
            .TheTest
            .ShouldPass<PhaseOutcome>((because, outcome) =>
            {
                because.ItsTrue("observe-only start and end handlers ran", outcome.ObservedAfterAdd == "start:sink|end:3", $"observed: {outcome.ObservedAfterAdd}");
                because.ItsTrue("observe-only handlers did not change the result", outcome.Sum == 3);
                because.ItsTrue("a start handler short-circuited", outcome.Found == "cached");
                because.ItsTrue("an end handler replaced the result", outcome.Invoked == "replaced");
                because.ItsTrue("an error handler recovered", outcome.Failed == "recovered");
                because.ItsTrue("the observe-only error handler ran too", outcome.ObservedAfterFail.EndsWith("|error:boom"), $"observed: {outcome.ObservedAfterFail}");
                because.ItsTrue("SubscribeEnd replaced an awaited result", outcome.AsyncSum == 100);
                because.ItsTrue("SubscribeError recovered a faulted task", outcome.AsyncFailed == "recovered async");
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest]
        public void RunRegistryWideHandlersForEveryDecoratedService()
        {
            After.Setup(reg =>
            {
                Prepare(reg);
                reg.For<ServiceRegistry>().Use(reg);
            })
            .When<ServiceRegistry>("subscribes by name before any service is registered", registry =>
            {
                List<string> observed = new List<string>();
                registry
                    .OnMethodStart(nameof(IEchoService.Message), context => { observed.Add("start:" + context.ImplementationType.Name + ":" + context.Args[0]); })
                    .OnMethodEnd(nameof(IEchoService.Message), context => { observed.Add("end:" + context.ImplementationType.Name + ":" + context.Result); })
                    .OnMethodStart(DecoratorSubscriptions.AnyMethod, context => { observed.Add("any:" + context.MethodName); });

                registry.For<IEchoService>().Use<EchoService>();
                registry.For<IShoutService>().Use<ShoutService>();
                registry.For<IGreeterService>().Use<GreeterService>();
                registry.Decorate<IEchoService, EchoService>();
                registry.Decorate<IShoutService, ShoutService>();

                registry.Get<IEchoService>().Message("one");
                registry.Get<IShoutService>().Message("two");
                registry.Get<IGreeterService>().Greet("three");
                return new RegistryWideOutcome(string.Join("|", observed));
            })
            .TheTest
            .ShouldPass<RegistryWideOutcome>((because, outcome) =>
            {
                because.ItsTrue("handlers fired for both decorated services and not for the undecorated one",
                    outcome.Observed == "start:EchoService:one|any:Message|end:EchoService:one|start:ShoutService:two|any:Message|end:ShoutService:TWO",
                    $"observed: {outcome.Observed}");
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest]
        public void LetRegistryWideHandlersOverrideResults()
        {
            After.Setup(reg =>
            {
                Prepare(reg);
                reg.OnMethodStart(nameof(IEchoService.Message), context => (string)context.Args[0]! == "cached" ? "from cache" : null);
                reg.OnMethodError("Fail", context => "recovered");
                reg.OnMethodEnd("Add", context => context.Result is int sum ? sum * 2 : null);
                reg.For<IEchoService>().Use<EchoService>();
                reg.For<IKitchenSinkService>().Use<KitchenSinkService>();
                reg.Decorate<IEchoService, EchoService>();
                reg.Decorate<IKitchenSinkService, KitchenSinkService>();
                reg.For<ServiceRegistry>().Use(reg);
            })
            .When<ServiceRegistry>("has registry-wide handlers that return values", registry =>
            {
                IEchoService echo = registry.Get<IEchoService>();
                IKitchenSinkService sink = registry.Get<IKitchenSinkService>();
                return new OverrideOutcome(echo.Message("cached"), echo.Message("live"), sink.Fail("boom"), sink.Add(1, 2), sink.Add(1.5, 2.5));
            })
            .TheTest
            .ShouldPass<OverrideOutcome>((because, outcome) =>
            {
                because.ItsTrue("a start handler short-circuits when it returns a value", outcome.Cached == "from cache");
                because.ItsTrue("and lets the call through when it returns null", outcome.Live == "live");
                because.ItsTrue("an error handler recovers", outcome.Recovered == "recovered");
                because.ItsTrue("an end handler replaces the result", outcome.Doubled == 6);
                because.ItsTrue("and leaves the overload it does not apply to alone", outcome.Untouched == 4.0);
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest]
        public void RefuseToDecorateWhatItCannot()
        {
            After.Setup(reg =>
            {
                Prepare(reg);
                reg.For<IEchoService>().Use(Substitute.For<IEchoService>());
                reg.For<ServiceRegistry>().Use(reg);
            })
            .When<ServiceRegistry>("decorates an unregistered service and a mismatched implementation", registry =>
            {
                string? unregistered = Refuses(() => registry.Decorate<IGreeterService, GreeterService>());
                string? subscribeUnregistered = Refuses(() => registry.OnMethodStart<IGreeterService, GreeterService>("Greet", context => { }));
                registry.Decorate<IEchoService, EchoService>();
                return new RefusalOutcome(unregistered, Refuses(() => registry.Get<IEchoService>()), subscribeUnregistered);
            })
            .TheTest
            .ShouldPass<RefusalOutcome>((because, outcome) =>
            {
                because.ItsTrue("an unregistered service is refused", outcome.Unregistered?.Contains("Register it before decorating it") == true, outcome.Unregistered);
                because.ItsTrue("a registration of another implementation is refused when it is resolved", outcome.Mismatched?.Contains("cannot be decorated as a EchoService") == true, outcome.Mismatched);
                because.ItsTrue("subscribing to an unregistered service is refused", outcome.SubscribeUnregistered != null);
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest]
        public void UseTheResolverAndLoggerRegisteredInTheRegistry()
        {
            IDecoratorTypeResolver resolver = Substitute.For<IDecoratorTypeResolver>();
            resolver.Resolve(typeof(IEchoService), typeof(EchoService)).Returns(typeof(EchoServiceDecorator));
            ILogger logger = Substitute.For<ILogger>();

            After.Setup(reg =>
            {
                reg.For<ILogger>().Use(logger);
                reg.For<IDecoratorTypeResolver>().Use(resolver);
                reg.For<IEchoService>().Use<EchoService>();
                reg.For<ServiceRegistry>().Use(reg);
            })
            .When<ServiceRegistry>("has its own resolver and logger", registry =>
            {
                Action<DecoratorInvocationContext<EchoService>> badHandler = context => throw new InvalidOperationException("bad handler");
                registry.OnMethodStart<IEchoService, EchoService>("Message", badHandler);
                registry.Get<IEchoService>().Message("hello");
                return new CollaboratorOutcome(
                    resolver.ReceivedCalls().Count(),
                    logger.ReceivedCalls().Count(call => call.GetMethodInfo().Name == nameof(ILogger.Error)),
                    ReferenceEquals(registry.GetDecoratorTypeResolver(), resolver));
            })
            .TheTest
            .ShouldPass<CollaboratorOutcome>((because, outcome) =>
            {
                because.ItsTrue("the registry's resolver supplied the decorator type, once", outcome.ResolverCalls == 1, $"resolver calls: {outcome.ResolverCalls}");
                because.ItsTrue("the registry's logger received the handler failure", outcome.ErrorsLogged == 1, $"errors logged: {outcome.ErrorsLogged}");
                because.ItsTrue("GetDecoratorTypeResolver returns the registered resolver", outcome.ReturnsRegisteredResolver);
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest]
        public void RefuseADecoratorTypeThatCannotStandIn()
        {
            IDecoratorTypeResolver resolver = Substitute.For<IDecoratorTypeResolver>();
            resolver.Resolve(typeof(IEchoService), typeof(EchoService)).Returns(typeof(Decorator<IEchoService, EchoService>));
            resolver.Resolve(typeof(IShoutService), typeof(ShoutService)).Returns(typeof(string));

            After.Setup(reg =>
            {
                reg.For<ILogger>().Use(Substitute.For<ILogger>());
                reg.For<IDecoratorTypeResolver>().Use(resolver);
                reg.For<IEchoService>().Use<EchoService>();
                reg.For<IShoutService>().Use<ShoutService>();
                reg.For<ServiceRegistry>().Use(reg);
            })
            .When<ServiceRegistry>("is handed decorator types that cannot stand in for the service", registry =>
            {
                return new RefusalOutcome(
                    Refuses(() => registry.Decorate<IEchoService, EchoService>()),
                    Refuses(() => registry.Decorate<IShoutService, ShoutService>()),
                    registry.Get<IEchoService>() is EchoService ? "left alone" : null);
            })
            .TheTest
            .ShouldPass<RefusalOutcome>((because, outcome) =>
            {
                because.ItsTrue("a decorator that does not implement the interface is refused", outcome.Unregistered?.Contains("a decorator must extend") == true, outcome.Unregistered);
                because.ItsTrue("an unrelated type is refused", outcome.Mismatched?.Contains("a decorator must extend") == true, outcome.Mismatched);
                because.ItsTrue("the registration is left as it was", outcome.SubscribeUnregistered == "left alone");
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest]
        public void FallBackToTheDefaultResolver()
        {
            After.Setup(reg =>
            {
                reg.For<ServiceRegistry>().Use(reg);
            })
            .When<ServiceRegistry>("has no resolver registered", registry =>
            {
                return new CollaboratorOutcome(0, 0, ReferenceEquals(registry.GetDecoratorTypeResolver(), DecoratorTypeResolver.Default));
            })
            .TheTest
            .ShouldPass<CollaboratorOutcome>((because, outcome) =>
            {
                because.ItsTrue("the default resolver is used", outcome.ReturnsRegisteredResolver);
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        private static string? Refuses(Action action)
        {
            try
            {
                action();
                return null;
            }
            catch (DecoratorException ex)
            {
                return ex.Message;
            }
        }

        private sealed record DecorateOutcome(string Message, bool IsGeneratedDecorator, bool IsDecorated);

        private sealed record RegistrationOutcome(bool ResolvesToDecorator, bool Idempotent, string Message, int Calls, bool SharesSubscriptions, bool KnowsItsTypes);

        private sealed record PhaseOutcome(string ObservedAfterAdd, int Sum, string? Found, string Invoked, string Failed, string ObservedAfterFail, int AsyncSum, string AsyncFailed);

        private sealed record RegistryWideOutcome(string Observed);

        private sealed record OverrideOutcome(string Cached, string Live, string Recovered, int Doubled, double Untouched);

        private sealed record RefusalOutcome(string? Unregistered, string? Mismatched, string? SubscribeUnregistered);

        private sealed record CollaboratorOutcome(int ResolverCalls, int ErrorsLogged, bool ReturnsRegisteredResolver);
    }
}
