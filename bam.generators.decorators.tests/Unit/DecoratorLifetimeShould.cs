using Bam.DependencyInjection;
using Bam.Generators.Decorators.Tests.Fixtures;
using Bam.Generators.Decorators.Tests.Fixtures.Decorators;
using Bam.Logging;
using Bam.Test;
using NSubstitute;

namespace Bam.Generators.Decorators.Tests.Unit
{
    /// <summary>
    /// Decorating a service must not change how often it is constructed. These tests pin that for a transient
    /// service, for a single instance, and for what happens around decoration: nothing constructed early,
    /// handlers surviving a second resolve, and a service registered again after being decorated.
    /// </summary>
    [UnitTestMenu("Decorator lifetime Should", Selector = "dls")]
    public class DecoratorLifetimeShould : UnitTestMenuContainer
    {
        public DecoratorLifetimeShould(ServiceRegistry serviceRegistry) : base(serviceRegistry)
        {
        }

        private static void Prepare(ServiceRegistry registry)
        {
            registry.For<ILogger>().Use(Substitute.For<ILogger>());
            registry.For<IDecoratorTypeResolver>().Use(DecoratorTypeResolverShould.NewResolver());
        }

        [UnitTest]
        public void KeepATransientServiceTransient()
        {
            After.Setup(reg =>
            {
                Prepare(reg);
                reg.For<ICounterService>().Use<CounterService>();
                reg.For<ServiceRegistry>().Use(reg);
            })
            .When<ServiceRegistry>("decorates a transient service and resolves it twice", registry =>
            {
                List<int> observed = new List<int>();
                registry.OnMethodEnd<ICounterService, CounterService>(nameof(ICounterService.Next), context => { observed.Add(context.Decorated.Id); });

                ICounterService first = registry.Get<ICounterService>();
                ICounterService second = registry.Get<ICounterService>();
                int firstNext = first.Next();
                int secondNext = second.Next();
                return new TransientOutcome(
                    ReferenceEquals(first, second),
                    first.Id == second.Id,
                    firstNext,
                    secondNext,
                    DecoratorExtensions.IsDecorated<CounterService>(first) && DecoratorExtensions.IsDecorated<CounterService>(second),
                    observed.Count == 2 && observed[0] == first.Id && observed[1] == second.Id);
            })
            .TheTest
            .ShouldPass<TransientOutcome>((because, outcome) =>
            {
                because.ItsTrue("each resolve returns its own decorator", !outcome.SameDecorator);
                because.ItsTrue("each decorator wraps its own instance", !outcome.SameInstance);
                because.ItsTrue("the instances do not share state", outcome.FirstNext == 1 && outcome.SecondNext == 1);
                because.ItsTrue("both are decorated", outcome.BothDecorated);
                because.ItsTrue("a handler subscribed once runs for both", outcome.HandlerRanForBoth);
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest]
        public void KeepASingleInstanceSingle()
        {
            CounterService instance = new CounterService();

            After.Setup(reg =>
            {
                Prepare(reg);
                reg.For<ICounterService>().Use(instance);
                reg.For<ServiceRegistry>().Use(reg);
            })
            .When<ServiceRegistry>("decorates a registered instance and resolves it twice", registry =>
            {
                registry.Decorate<ICounterService, CounterService>();
                ICounterService first = registry.Get<ICounterService>();
                ICounterService second = registry.Get<ICounterService>();
                int firstNext = first.Next();
                int secondNext = second.Next();
                return new TransientOutcome(ReferenceEquals(first, second), first.Id == instance.Id && second.Id == instance.Id, firstNext, secondNext, DecoratorExtensions.IsDecorated<CounterService>(first), true);
            })
            .TheTest
            .ShouldPass<TransientOutcome>((because, outcome) =>
            {
                because.ItsTrue("every resolve returns the same decorator", outcome.SameDecorator);
                because.ItsTrue("it wraps the registered instance", outcome.SameInstance);
                because.ItsTrue("state is shared, as it was before decorating", outcome.FirstNext == 1 && outcome.SecondNext == 2);
                because.ItsTrue("it is decorated", outcome.BothDecorated);
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest]
        public void ConstructNothingUntilTheServiceIsResolved()
        {
            After.Setup(reg =>
            {
                Prepare(reg);
                reg.For<ICounterService>().Use<CounterService>();
                reg.For<ServiceRegistry>().Use(reg);
            })
            .When<ServiceRegistry>("decorates and subscribes without resolving", registry =>
            {
                int before = CounterService.Constructed;
                registry.Decorate<ICounterService, CounterService>();
                registry.Decorate<ICounterService, CounterService>();
                registry
                    .OnMethodStart<ICounterService, CounterService>(nameof(ICounterService.Next), context => { })
                    .SubscribeEnd<ICounterService, CounterService>(nameof(ICounterService.Next), context => null);
                int afterDecorating = CounterService.Constructed;
                registry.Get<ICounterService>();
                return new ConstructionOutcome(afterDecorating - before, CounterService.Constructed > afterDecorating);
            })
            .TheTest
            .ShouldPass<ConstructionOutcome>((because, outcome) =>
            {
                because.ItsTrue("decorating and subscribing constructed nothing", outcome.ConstructedWhileDecorating == 0, $"constructed: {outcome.ConstructedWhileDecorating}");
                because.ItsTrue("resolving did", outcome.ConstructedOnResolve);
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest]
        public void ReturnTheSameRegistrationUntilTheServiceIsRegisteredAgain()
        {
            After.Setup(reg =>
            {
                Prepare(reg);
                reg.For<IEchoService>().Use<EchoService>();
                reg.For<ServiceRegistry>().Use(reg);
            })
            .When<ServiceRegistry>("decorates, registers the service again, and decorates again", registry =>
            {
                List<string> observed = new List<string>();
                DecoratorRegistration<IEchoService, EchoService> first = registry.Decorate<IEchoService, EchoService>();
                DecoratorRegistration<IEchoService, EchoService> again = registry.Decorate<IEchoService, EchoService>();
                first.Subscribe(DecoratorPhase.Start, nameof(IEchoService.Message), context => { observed.Add((string)context.Args[0]!); });
                bool registeredBefore = first.IsRegisteredIn(registry);

                registry.For<IEchoService>().Use<EchoService>();
                bool registeredAfter = first.IsRegisteredIn(registry);
                bool decoratedAfterReregistering = DecoratorExtensions.IsDecorated<EchoService>(registry.Get<IEchoService>());

                DecoratorRegistration<IEchoService, EchoService> second = registry.Decorate<IEchoService, EchoService>();
                registry.Get<IEchoService>().Message("after");
                return new ReregistrationOutcome(
                    ReferenceEquals(first, again),
                    registeredBefore,
                    registeredAfter,
                    decoratedAfterReregistering,
                    ReferenceEquals(first, second),
                    second.IsRegisteredIn(registry),
                    string.Join("|", observed),
                    registry.GetDecoratorRegistrations().DecoratedTypes.Contains(typeof(IEchoService)));
            })
            .TheTest
            .ShouldPass<ReregistrationOutcome>((because, outcome) =>
            {
                because.ItsTrue("decorating twice returns the same registration", outcome.SameWhenDecoratedTwice);
                because.ItsTrue("the registry resolves through it", outcome.RegisteredBefore);
                because.ItsTrue("registering the service again takes it out", !outcome.RegisteredAfter && !outcome.DecoratedAfterReregistering);
                because.ItsTrue("decorating again makes a new registration", !outcome.SameAfterReregistering && outcome.NewIsRegistered);
                because.ItsTrue("which carries the handlers subscribed before", outcome.Observed == "after", $"observed: {outcome.Observed}");
                because.ItsTrue("the registry records what it decorated", outcome.Recorded);
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest]
        public void KeepInstanceHandlersToTheirInstance()
        {
            After.Setup(reg =>
            {
                Prepare(reg);
                reg.For<IEchoService>().Use<EchoService>();
                reg.For<ServiceRegistry>().Use(reg);
            })
            .When<ServiceRegistry>("subscribes through the registry and through one resolved instance", registry =>
            {
                List<string> observed = new List<string>();
                registry.OnMessageStart(context => { observed.Add("registry:" + context.Args[0]); });
                IEchoService first = registry.Get<IEchoService>();
                IEchoService second = registry.Get<IEchoService>();
                first.OnMessageStart(context => { observed.Add("instance:" + context.Args[0]); });

                first.Message("one");
                second.Message("two");
                return new OrderOutcome(string.Join("|", observed));
            })
            .TheTest
            .ShouldPass<OrderOutcome>((because, outcome) =>
            {
                because.ItsTrue("the registry's handler ran for both, the instance's for its own instance only",
                    outcome.Order == "registry:one|instance:one|registry:two",
                    $"observed: {outcome.Order}");
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest]
        public void AdoptADecoratorRegisteredByHand()
        {
            EchoServiceDecorator byHand = new EchoServiceDecorator(new EchoService(), Substitute.For<ILogger>());

            After.Setup(reg =>
            {
                Prepare(reg);
                reg.For<IEchoService>().Use(byHand);
                reg.For<ServiceRegistry>().Use(reg);
            })
            .When<ServiceRegistry>("subscribes to a service that was registered as a decorator", registry =>
            {
                List<string> observed = new List<string>();
                byHand.Subscribe(DecoratorPhase.Start, nameof(IEchoService.Message), context => { observed.Add("own"); });
                registry.OnMessageStart(context => { observed.Add("registry"); });
                IEchoService resolved = registry.Get<IEchoService>();
                resolved.Message("hello");
                return new AdoptionOutcome(ReferenceEquals(resolved, byHand), string.Join("|", observed));
            })
            .TheTest
            .ShouldPass<AdoptionOutcome>((because, outcome) =>
            {
                because.ItsTrue("the decorator is not wrapped a second time", outcome.SameDecorator);
                because.ItsTrue("it runs the registry's handlers and keeps its own", outcome.Observed == "registry|own", $"observed: {outcome.Observed}");
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest]
        public void FailOnResolveWhenTheServiceIsNotWhatWasDecorated()
        {
            After.Setup(reg =>
            {
                Prepare(reg);
                reg.For<IEchoService>().Use(Substitute.For<IEchoService>());
                reg.For<ServiceRegistry>().Use(reg);
            })
            .When<ServiceRegistry>("decorates a service as an implementation it is not registered as", registry =>
            {
                registry.Decorate<IEchoService, EchoService>();
                try
                {
                    registry.Get<IEchoService>();
                    return new AdoptionOutcome(false, "resolved");
                }
                catch (DecoratorException ex)
                {
                    return new AdoptionOutcome(false, ex.Message);
                }
            })
            .TheTest
            .ShouldPass<AdoptionOutcome>((because, outcome) =>
            {
                because.ItsTrue("resolving throws DecoratorException naming both types", outcome.Observed.Contains("cannot be decorated as a EchoService"), outcome.Observed);
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        private sealed record TransientOutcome(bool SameDecorator, bool SameInstance, int FirstNext, int SecondNext, bool BothDecorated, bool HandlerRanForBoth);

        private sealed record ConstructionOutcome(int ConstructedWhileDecorating, bool ConstructedOnResolve);

        private sealed record ReregistrationOutcome(bool SameWhenDecoratedTwice, bool RegisteredBefore, bool RegisteredAfter, bool DecoratedAfterReregistering, bool SameAfterReregistering, bool NewIsRegistered, string Observed, bool Recorded);

        private sealed record OrderOutcome(string Order);

        private sealed record AdoptionOutcome(bool SameDecorator, string Observed);
    }
}
