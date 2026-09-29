using Bam.DependencyInjection;
using Bam.Generators.Decorators.Tests.Fixtures;
using Bam.Generators.Decorators.Tests.Fixtures.Decorators;
using Bam.Logging;
using Bam.Test;
using NSubstitute;

namespace Bam.Generators.Decorators.Tests.Unit
{
    /// <summary>
    /// Exercises the typed per-method extension methods in Fixtures/Generated — <c>OnMessageStart</c> and
    /// friends — on a registry and on a decorated service instance.
    /// </summary>
    [UnitTestMenu("Generated extensions Should", Selector = "ges")]
    public class GeneratedExtensionsShould : UnitTestMenuContainer
    {
        public GeneratedExtensionsShould(ServiceRegistry serviceRegistry) : base(serviceRegistry)
        {
        }

        [UnitTest]
        public void SubscribeTypedHandlersThroughTheRegistry()
        {
            List<string> observed = new List<string>();

            After.Setup(reg =>
            {
                reg.For<ILogger>().Use(Substitute.For<ILogger>());
                reg.For<IEchoService>().Use<EchoService>();
                reg.OnMessageStart(ctx => { observed.Add("start:" + ctx.Args[0]); })
                    .OnMessageEnd(ctx => { observed.Add("end:" + ctx.Result); });
            })
            .When<IEchoService>("has observe-only handlers subscribed by generated extensions", echo =>
            {
                return new HandlerOutcome(echo.Message("hello"), string.Join("|", observed), echo is EchoServiceDecorator);
            })
            .TheTest
            .ShouldPass<HandlerOutcome>((because, outcome) =>
            {
                because.ItsTrue("subscribing decorated the service with its generated decorator", outcome.IsGeneratedDecorator);
                because.ItsTrue("the handlers ran", outcome.Observed == "start:hello|end:hello", $"observed: {outcome.Observed}");
                because.ItsTrue("the result is unchanged", outcome.Message == "hello");
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest]
        public void LetTypedHandlersOverrideTheResult()
        {
            After.Setup(reg =>
            {
                reg.For<ILogger>().Use(Substitute.For<ILogger>());
                reg.For<IEchoService>().Use<EchoService>();
                reg.For<IKitchenSinkService>().Use<KitchenSinkService>();
                reg.OnMessageStart(ctx => (string)ctx.Args[0]! == "cached" ? "from cache" : null)
                    .OnMessageEnd(ctx => ctx.Result is string message && message == "loud" ? "LOUD" : null)
                    .OnFailError(ctx => "recovered from " + ctx.Exception!.Message)
                    .OnAddAsyncEnd(ctx => ctx.Result is int sum && sum > 10 ? 10 : null)
                    .OnFindStart(ctx => ctx.Args[0] == null ? "nothing to find" : null);
                reg.For<ServiceRegistry>().Use(reg);
            })
            .When<ServiceRegistry>("has typed handlers that return values", registry =>
            {
                IEchoService echo = registry.Get<IEchoService>();
                IKitchenSinkService sink = registry.Get<IKitchenSinkService>();
                return new OverrideOutcome(
                    echo.Message("cached"),
                    echo.Message("loud"),
                    echo.Message("plain"),
                    sink.Fail("boom"),
                    sink.AddAsync(20, 30).GetAwaiter().GetResult(),
                    sink.AddAsync(2, 3).GetAwaiter().GetResult(),
                    sink.Find(null),
                    sink.Find("key"));
            })
            .TheTest
            .ShouldPass<OverrideOutcome>((because, outcome) =>
            {
                because.ItsTrue("a start handler short-circuits", outcome.Cached == "from cache");
                because.ItsTrue("an end handler replaces the result", outcome.Loud == "LOUD");
                because.ItsTrue("a handler returning null leaves the call alone", outcome.Plain == "plain");
                because.ItsTrue("an error handler supplies a fallback", outcome.Recovered == "recovered from boom");
                because.ItsTrue("a nullable value-type handler replaces an awaited result", outcome.Capped == 10);
                because.ItsTrue("and leaves it alone when it returns null", outcome.Uncapped == 5);
                because.ItsTrue("a handler on a nullable method short-circuits", outcome.FoundNothing == "nothing to find");
                because.ItsTrue("and lets other calls through", outcome.Found == "found:key");
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest]
        public void SubscribeTypedHandlersOnADecoratedInstance()
        {
            After.Setup(reg =>
            {
                reg.For<IEchoService>().Use(new EchoServiceDecorator(new EchoService(), Substitute.For<ILogger>()));
            })
            .When<IEchoService>("is a decorator and subscribes through the instance", echo =>
            {
                List<string> observed = new List<string>();
                IEchoService chained = echo
                    .OnMessageStart(ctx => { observed.Add("start"); })
                    .OnMessageEnd(ctx => ctx.Result + "!")
                    .OnMessageError(ctx => { observed.Add("error"); });
                return new HandlerOutcome(echo.Message("hello"), string.Join("|", observed), ReferenceEquals(chained, echo));
            })
            .TheTest
            .ShouldPass<HandlerOutcome>((because, outcome) =>
            {
                because.ItsTrue("the extensions return the instance for chaining", outcome.IsGeneratedDecorator);
                because.ItsTrue("the observe-only handler ran and the error handler did not", outcome.Observed == "start", $"observed: {outcome.Observed}");
                because.ItsTrue("the typed handler replaced the result", outcome.Message == "hello!");
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest]
        public void RefuseAnInstanceThatIsNotDecorated()
        {
            After.Setup(reg =>
            {
                reg.For<IEchoService>().Use(new EchoService());
            })
            .When<IEchoService>("is not decorated", echo =>
            {
                try
                {
                    echo.OnMessageStart(ctx => { });
                    return new HandlerOutcome("subscribed", string.Empty, false);
                }
                catch (DecoratorException ex)
                {
                    return new HandlerOutcome("refused", ex.Message, false);
                }
            })
            .TheTest
            .ShouldPass<HandlerOutcome>((because, outcome) =>
            {
                because.ItsTrue("subscribing throws DecoratorException", outcome.Message == "refused");
                because.ItsTrue("the message says the instance is not decorated", outcome.Observed.Contains("is not decorated"), outcome.Observed);
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest]
        public void DecorateThroughTheGeneratedExtension()
        {
            ILogger logger = Substitute.For<ILogger>();

            After.Setup(reg =>
            {
                reg.For<IEchoService>().Use<EchoService>();
                reg.For<ServiceRegistry>().Use(reg);
            })
            .When<ServiceRegistry>("decorates with DecorateEchoService", registry =>
            {
                DecoratorRegistration<IEchoService, EchoService> registration = registry.DecorateEchoService(logger);
                DecoratorRegistration<IEchoService, EchoService> again = registry.DecorateEchoService();
                return new DecorateOutcome(
                    registration.DecoratorType == typeof(EchoServiceDecorator) && ReferenceEquals(registration.Logger, logger),
                    ReferenceEquals(registration, again),
                    registry.Get<IEchoService>() is EchoServiceDecorator);
            })
            .TheTest
            .ShouldPass<DecorateOutcome>((because, outcome) =>
            {
                because.ItsTrue("the generated decorator type and the given logger are used", outcome.IsGeneratedDecorator);
                because.ItsTrue("decorating again returns the same registration", outcome.Idempotent);
                because.ItsTrue("the interface resolves to the generated decorator", outcome.Registered);
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        private sealed record HandlerOutcome(string Message, string Observed, bool IsGeneratedDecorator);

        private sealed record OverrideOutcome(string Cached, string Loud, string Plain, string Recovered, int Capped, int Uncapped, string? FoundNothing, string? Found);

        private sealed record DecorateOutcome(bool IsGeneratedDecorator, bool Idempotent, bool Registered);
    }
}
