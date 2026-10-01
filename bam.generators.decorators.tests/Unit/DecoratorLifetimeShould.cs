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
        public void ServeBothRegistrationsWhenOneRegistryResolvesThroughAnother()
        {
            // Registry 2 delegates ICounterService to registry 1, and both decorate it. The decorator registry 1
            // hands out serves registry 2's registration too; neither registration's handlers displace the other's.
            CounterService instance = new CounterService();
            ServiceRegistry first = new ServiceRegistry();
            first.For<ILogger>().Use(Substitute.For<ILogger>());
            first.For<IDecoratorTypeResolver>().Use(DecoratorTypeResolverShould.NewResolver());
            first.For<ICounterService>().Use(instance);

            After.Setup(reg =>
            {
                Prepare(reg);
                reg.For<ICounterService>().Use<ICounterService>(() => first.Get<ICounterService>());
                reg.For<ServiceRegistry>().Use(reg);
            })
            .When<ServiceRegistry>("subscribes in both registries", second =>
            {
                // Registry-wide handlers in both registries too, by name and by `*`: the decorator runs every
                // store of every registry it serves, registry-wide stores first, in the order they attached.
                List<string> observed = new List<string>();
                first.OnMethodStart(nameof(ICounterService.Next), context => { observed.Add("first-wide"); });
                second.OnMethodStart(nameof(ICounterService.Next), context => { observed.Add("second-wide"); });
                second.OnMethodStart("*", context => { observed.Add("second-star"); });
                first.OnMethodStart<ICounterService, CounterService>(nameof(ICounterService.Next), context => { observed.Add("first"); });
                second.OnMethodStart<ICounterService, CounterService>(nameof(ICounterService.Next), context => { observed.Add("second"); });

                ICounterService fromFirst = first.Get<ICounterService>();
                ICounterService fromSecond = second.Get<ICounterService>();
                fromFirst.Next();
                string afterFirst = string.Join("|", observed);
                observed.Clear();
                fromSecond.Next();
                Decorator<ICounterService, CounterService>? decorator = fromSecond as Decorator<ICounterService, CounterService>;
                return new CompositionOutcome(ReferenceEquals(fromFirst, fromSecond), afterFirst, string.Join("|", observed), decorator?.RegistrationHandlers.Count ?? 0, decorator?.SharedSubscriptions.Count ?? 0);
            })
            .TheTest
            .ShouldPass<CompositionOutcome>((because, outcome) =>
            {
                because.ItsTrue("both registries hand out the one decorator", outcome.SameDecorator);
                because.ItsTrue("a call through the first registry runs both registries' registry-wide handlers, then both registrations' handlers", outcome.ThroughFirst == "first-wide|second-wide|second-star|first|second", $"observed: {outcome.ThroughFirst}");
                because.ItsTrue("so does a call through the second", outcome.ThroughSecond == "first-wide|second-wide|second-star|first|second", $"observed: {outcome.ThroughSecond}");
                because.ItsTrue("the decorator serves two registrations", outcome.Registrations == 2);
                because.ItsTrue("and two registries' registry-wide stores", outcome.SharedStores == 2);
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest]
        public void RunEveryHandlerSubscribedFromParallelFirstDecorations()
        {
            // Nothing has been decorated in the registry yet, so the stores don't exist either. Sixteen
            // threads decorating and subscribing at once must end up with one registration and one
            // registry-wide store, and every handler they subscribed must run on one call. One round lets a
            // broken lock through three times in four, so it runs 25 rounds, each on a fresh registry, and
            // reports the first bad one.
            When.A<DecoratorLifetimeShould>("decorates fresh registries from sixteen threads at once, 25 times", this, test =>
            {
                for (int round = 1; round <= 25; round++)
                {
                    ServiceRegistry registry = new ServiceRegistry();
                    Prepare(registry);
                    registry.For<ICounterService>().Use(new CounterService());
                    ParallelOutcome outcome = DecorateInParallel(registry, round);
                    if (outcome.Failures != 0 || outcome.Registrations != 1 || outcome.Stores != 1 || outcome.Ran != 16 || outcome.Decorated != 1)
                    {
                        return outcome;
                    }
                }

                return new ParallelOutcome(0, 1, 1, 16, 1, 25);
            })
            .TheTest
            .ShouldPass<ParallelOutcome>((because, outcome) =>
            {
                because.ItsTrue("no thread failed", outcome.Failures == 0, $"round {outcome.Round}: failures: {outcome.Failures}");
                because.ItsTrue("every decorating thread got the one registration", outcome.Registrations == 1, $"round {outcome.Round}: registrations: {outcome.Registrations}");
                because.ItsTrue("every subscribing thread got the one registry-wide store", outcome.Stores == 1, $"round {outcome.Round}: stores: {outcome.Stores}");
                because.ItsTrue("one call ran all sixteen handlers", outcome.Ran == 16, $"round {outcome.Round}: ran: {outcome.Ran}");
                because.ItsTrue("one service is recorded as decorated", outcome.Decorated == 1, $"round {outcome.Round}");
                because.ItsTrue("all 25 rounds were clean", outcome.Round == 25, $"stopped at round {outcome.Round}");
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        private static ParallelOutcome DecorateInParallel(ServiceRegistry registry, int round)
        {
            int ran = 0;
            List<Exception> failures = new List<Exception>();
            HashSet<DecoratorRegistration<ICounterService, CounterService>> registrations = new HashSet<DecoratorRegistration<ICounterService, CounterService>>();
            HashSet<DecoratorSubscriptions> stores = new HashSet<DecoratorSubscriptions>();
            using Barrier starting = new Barrier(16);
            Thread[] threads = Enumerable.Range(0, 16).Select(index => new Thread(() =>
            {
                try
                {
                    starting.SignalAndWait();
                    if (index % 2 == 0)
                    {
                        DecoratorRegistration<ICounterService, CounterService> registration = registry.Decorate<ICounterService, CounterService>();
                        registration.Subscribe(DecoratorPhase.Start, nameof(ICounterService.Next), context => { Interlocked.Increment(ref ran); });
                        lock (registrations)
                        {
                            registrations.Add(registration);
                        }
                    }
                    else
                    {
                        registry.OnMethodStart(nameof(ICounterService.Next), context => { Interlocked.Increment(ref ran); });
                        lock (stores)
                        {
                            stores.Add(registry.GetDecoratorSubscriptions());
                        }
                    }
                }
                catch (Exception ex)
                {
                    lock (failures)
                    {
                        failures.Add(ex);
                    }
                }
            })).ToArray();
            foreach (Thread thread in threads)
            {
                thread.Start();
            }

            foreach (Thread thread in threads)
            {
                thread.Join();
            }

            registry.Get<ICounterService>().Next();
            return new ParallelOutcome(failures.Count, registrations.Count, stores.Count, ran, registry.GetDecoratorRegistrations().DecoratedTypes.Count(), round);
        }

        [UnitTest]
        public void KeepItsHandlersWhenItIncludesAnotherRegistry()
        {
            // The stores belong to the registry by identity, not as entries: Include copies services, never
            // handlers or the record of what was decorated. A guard subscribed before the Include keeps guarding
            // what this registry decorates afterward, including the service it took from the other registry,
            // and never reaches the other registry's own consumers.
            ServiceRegistry library = new ServiceRegistry();
            Prepare(library);
            library.For<IEchoService>().Use<EchoService>();
            library.Decorate<IEchoService, EchoService>();

            After.Setup(reg =>
            {
                Prepare(reg);
                reg.For<ICounterService>().Use<CounterService>();
                reg.For<ServiceRegistry>().Use(reg);
            })
            .When<ServiceRegistry>("subscribes a guard, includes a registry that decorated, then decorates", app =>
            {
                int guarded = 0;
                app.OnMethodStart("*", context => { guarded++; context.Reject("guarded by app"); });
                DecoratorSubscriptions before = app.GetDecoratorSubscriptions();
                app.Include(library);
                bool storeKept = ReferenceEquals(before, app.GetDecoratorSubscriptions());
                bool echoRecorded = app.GetDecoratorRegistrations().DecoratedTypes.Contains(typeof(IEchoService));

                app.Decorate<ICounterService, CounterService>();
                string? counter = Rejection(() => app.Get<ICounterService>().Next());
                string? echoBefore = Rejection(() => app.Get<IEchoService>().Message("hi"));
                app.Decorate<IEchoService, EchoService>();
                string? echoAfter = Rejection(() => app.Get<IEchoService>().Message("hi"));
                string? libraryOwn = Rejection(() => library.Get<IEchoService>().Message("hi"));
                return new IncludeOutcome(storeKept, echoRecorded, counter, echoBefore, echoAfter, libraryOwn, guarded);
            })
            .TheTest
            .ShouldPass<IncludeOutcome>((because, outcome) =>
            {
                because.ItsTrue("Include left the registry-wide store in place", outcome.StoreKept);
                because.ItsTrue("the included registry's decoration is not recorded here until this registry decorates it", !outcome.EchoRecorded);
                because.ItsTrue("a service decorated after the Include is guarded", outcome.Counter == "guarded by app", outcome.Counter);
                because.ItsTrue("the included service is not guarded until this registry decorates it", outcome.EchoBefore == null, outcome.EchoBefore);
                because.ItsTrue("once decorated here, the included service is guarded", outcome.EchoAfter == "guarded by app", outcome.EchoAfter);
                because.ItsTrue("the other registry's own consumers are not guarded by this registry", outcome.LibraryOwn == null, outcome.LibraryOwn);
                because.ItsTrue("the guard ran for the two guarded calls", outcome.Guarded == 2, $"guarded: {outcome.Guarded}");
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest]
        public void GuardWhatItDecoratedBeforeIncludingAnotherRegistry()
        {
            ServiceRegistry library = new ServiceRegistry();
            Prepare(library);
            library.For<IEchoService>().Use<EchoService>();
            library.Decorate<IEchoService, EchoService>();

            After.Setup(reg =>
            {
                Prepare(reg);
                reg.For<ICounterService>().Use<CounterService>();
                reg.For<ServiceRegistry>().Use(reg);
            })
            .When<ServiceRegistry>("decorates, includes a registry that decorated, then subscribes a guard", app =>
            {
                app.Decorate<ICounterService, CounterService>();
                app.Include(library);
                app.OnMethodStart("*", context => context.Reject("guarded by app"));
                string? counter = Rejection(() => app.Get<ICounterService>().Next());
                string? libraryOwn = Rejection(() => library.Get<IEchoService>().Message("hi"));
                return new IncludeOutcome(true, false, counter, null, null, libraryOwn, 0);
            })
            .TheTest
            .ShouldPass<IncludeOutcome>((because, outcome) =>
            {
                because.ItsTrue("the service decorated before the Include is guarded by the handler subscribed after it", outcome.Counter == "guarded by app", outcome.Counter);
                because.ItsTrue("the other registry's own consumers are not", outcome.LibraryOwn == null, outcome.LibraryOwn);
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        private static string? Rejection(Action call)
        {
            try
            {
                call();
                return null;
            }
            catch (DecoratorRejectionException ex)
            {
                return ex.Message;
            }
        }

        [UnitTest]
        public void RefuseToDecorateAsASecondImplementationType()
        {
            After.Setup(reg =>
            {
                Prepare(reg);
                reg.For<IEchoService>().Use<EchoService>();
                reg.For<ServiceRegistry>().Use(reg);
            })
            .When<ServiceRegistry>("decorates the same interface as two implementation types", registry =>
            {
                registry.Decorate<IEchoService, EchoService>();
                string? refused = null;
                try
                {
                    registry.Decorate<IEchoService, LoudEchoService>();
                }
                catch (DecoratorException ex)
                {
                    refused = ex.Message;
                }

                string stillWorks = registry.Get<IEchoService>().Message("hello");

                // Once the service is registered again as the other type, decorating as that type is fine.
                registry.For<IEchoService>().Use<LoudEchoService>();
                registry.Decorate<IEchoService, LoudEchoService>();
                return new SecondTypeOutcome(refused, stillWorks, registry.Get<IEchoService>().Message("hello"));
            })
            .TheTest
            .ShouldPass<SecondTypeOutcome>((because, outcome) =>
            {
                because.ItsTrue("the second decoration is refused, naming both types", outcome.Refused?.Contains("EchoService") == true && outcome.Refused.Contains("LoudEchoService"), outcome.Refused);
                because.ItsTrue("the first decoration still resolves", outcome.StillWorks == "hello");
                because.ItsTrue("after re-registering as the other type, decorating as it works", outcome.AfterReregistering == "HELLO!");
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

        private sealed record CompositionOutcome(bool SameDecorator, string ThroughFirst, string ThroughSecond, int Registrations, int SharedStores);

        private sealed record ParallelOutcome(int Failures, int Registrations, int Stores, int Ran, int Decorated, int Round);

        private sealed record IncludeOutcome(bool StoreKept, bool EchoRecorded, string? Counter, string? EchoBefore, string? EchoAfter, string? LibraryOwn, int Guarded);

        private sealed record SecondTypeOutcome(string? Refused, string StillWorks, string AfterReregistering);

        private sealed record AdoptionOutcome(bool SameDecorator, string Observed);
    }
}
