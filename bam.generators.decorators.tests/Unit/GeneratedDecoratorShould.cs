using Bam.DependencyInjection;
using Bam.Generators.Decorators.Tests.Fixtures;
using Bam.Generators.Decorators.Tests.Fixtures.Decorators;
using Bam.Logging;
using Bam.Test;
using NSubstitute;
using System.Reflection;

namespace Bam.Generators.Decorators.Tests.Unit
{
    /// <summary>
    /// Exercises the decorators in Fixtures/Generated — real generator output compiled into this assembly — and
    /// guards the round trip: the generator's current output must match what is checked in, and must compile.
    /// </summary>
    [UnitTestMenu("Generated decorator Should", Selector = "gds")]
    public class GeneratedDecoratorShould : UnitTestMenuContainer
    {
        public GeneratedDecoratorShould(ServiceRegistry serviceRegistry) : base(serviceRegistry)
        {
        }

        private static KitchenSinkServiceDecorator NewSink()
        {
            return new KitchenSinkServiceDecorator(new KitchenSinkService(), Substitute.For<ILogger>());
        }

        [UnitTest]
        public void BehaveLikeTheServiceItWraps()
        {
            After.Setup(reg =>
            {
                reg.For<IKitchenSinkService>().Use(NewSink());
            })
            .When<IKitchenSinkService>("is used through its interface", sink =>
            {
                int changes = 0;
                sink.Changed += (sender, args) => changes++;
                sink.Name = "renamed";
                sink[3] = "three";
                int sum = sink.Add(1, 2);
                double doubles = sink.Add(1.5, 2.5);
                int countBeforeReset = sink.Count;
                sink.Reset();
                bool parsed = sink.TryParse("42", out int number);
                return new ForwardingOutcome(sink.Name, sink[3], sum, doubles, countBeforeReset, sink.Count, changes, parsed, number, sink.Find("key"), sink.Find(null), sink.Echo("echoed"), sink.Invoke("fired"));
            })
            .TheTest
            .ShouldPass<ForwardingOutcome>((because, outcome) =>
            {
                because.ItsTrue("an inherited property is forwarded", outcome.Name == "renamed");
                because.ItsTrue("the indexer is forwarded", outcome.Indexed == "three");
                because.ItsTrue("overloads are forwarded to the right method", outcome.Sum == 3 && outcome.Doubles == 4.0);
                because.ItsTrue("a read-only property reflects the wrapped instance", outcome.CountBeforeReset == 2 && outcome.CountAfterReset == 0);
                because.ItsTrue("events raised by the wrapped instance reach subscribers", outcome.Changes == 1);
                because.ItsTrue("out parameters are forwarded", outcome.Parsed && outcome.Number == 42);
                because.ItsTrue("nullable values pass through", outcome.Found == "found:key" && outcome.NotFound == null);
                because.ItsTrue("a generic method is forwarded", outcome.Echoed == "echoed");
                because.ItsTrue("a method named like a decorator member is forwarded", outcome.Invoked == "invoked:fired");
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest]
        public void RunHandlersAroundEveryInterceptedMethod()
        {
            After.Setup(reg =>
            {
                reg.For<KitchenSinkServiceDecorator>().Use(NewSink());
            })
            .When<KitchenSinkServiceDecorator>("has a handler subscribed to every method", decorator =>
            {
                List<string> started = new List<string>();
                decorator.Subscribe(DecoratorPhase.Start, DecoratorSubscriptions.AnyMethod, context => { started.Add(context.MethodName + "/" + context.Args.Length); });
                IKitchenSinkService sink = decorator;
                sink.Add(1, 2);
                sink.Reset();
                sink.AddAsync(1, 2).GetAwaiter().GetResult();
                sink.ResetAsync().GetAwaiter().GetResult();
                sink.DescribeAsync("bam").AsTask().GetAwaiter().GetResult();
                sink.FlushAsync().AsTask().GetAwaiter().GetResult();
                sink.Echo("item");
                sink.Invoke("fired");
                sink.TryParse("1", out int ignored);
                string name = sink.Name;
                return new OrderOutcome(string.Join("|", started));
            })
            .TheTest
            .ShouldPass<OrderOutcome>((because, outcome) =>
            {
                because.ItsTrue("every intercepted method ran the handler; forwarded members did not",
                    outcome.Order == "Add/2|Reset/0|AddAsync/2|ResetAsync/0|DescribeAsync/1|FlushAsync/0|Echo/1|Invoke/1",
                    $"started: {outcome.Order}");
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest]
        public void LetHandlersOverrideSynchronousResults()
        {
            After.Setup(reg =>
            {
                reg.For<KitchenSinkServiceDecorator>().Use(NewSink());
            })
            .When<KitchenSinkServiceDecorator>("has handlers that return values", decorator =>
            {
                IKitchenSinkService sink = decorator;
                decorator.SubscribeStart("Find", context => "cached:" + context.Args[0]);
                decorator.SubscribeEnd("Invoke", context => ((string)context.Result!).ToUpperInvariant());
                decorator.SubscribeError("Fail", context => "recovered from " + context.Exception!.Message);
                return new OverrideOutcome(sink.Find("key"), sink.Invoke("fired"), sink.Fail("boom"));
            })
            .TheTest
            .ShouldPass<OverrideOutcome>((because, outcome) =>
            {
                because.ItsTrue("a start handler short-circuits", outcome.ShortCircuited == "cached:key");
                because.ItsTrue("an end handler replaces the result", outcome.Replaced == "INVOKED:FIRED");
                because.ItsTrue("an error handler supplies a fallback", outcome.Recovered == "recovered from boom");
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest]
        public void LetHandlersOverrideAsynchronousResults()
        {
            After.Setup(reg =>
            {
                reg.For<KitchenSinkServiceDecorator>().Use(NewSink());
            })
            .When<KitchenSinkServiceDecorator>("has handlers on asynchronous methods", decorator =>
            {
                IKitchenSinkService sink = decorator;
                object? seenAtEnd = null;
                decorator.Subscribe(DecoratorPhase.End, "AddAsync", context => { seenAtEnd = context.Result; });
                decorator.SubscribeEnd("AddAsync", context => (int)context.Result! * 10);
                decorator.SubscribeStart("DescribeAsync", context => "short-circuited");
                decorator.SubscribeError("FailAsync", context => "recovered from " + context.Exception!.Message);
                return new AsyncOverrideOutcome(
                    sink.AddAsync(1, 2).GetAwaiter().GetResult(),
                    seenAtEnd,
                    sink.DescribeAsync("bam").AsTask().GetAwaiter().GetResult(),
                    sink.FailAsync("boom").GetAwaiter().GetResult());
            })
            .TheTest
            .ShouldPass<AsyncOverrideOutcome>((because, outcome) =>
            {
                because.ItsTrue("end handlers see the awaited value", Equals(outcome.SeenAtEnd, 3));
                because.ItsTrue("an end handler replaces the awaited value", outcome.Sum == 30);
                because.ItsTrue("a start handler short-circuits a ValueTask<T>", outcome.Described == "short-circuited");
                because.ItsTrue("an error handler recovers a faulted task", outcome.Recovered == "recovered from boom");
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest]
        public void ThrowWhatTheServiceThrows()
        {
            After.Setup(reg =>
            {
                reg.For<IKitchenSinkService>().Use(NewSink());
            })
            .When<IKitchenSinkService>("calls methods that throw", sink =>
            {
                Exception? sync = null;
                Exception? async = null;
                try
                {
                    sink.Fail("sync boom");
                }
                catch (Exception ex)
                {
                    sync = ex;
                }

                try
                {
                    sink.FailAsync("async boom").GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    async = ex;
                }

                return new ThrowOutcome(sync, async);
            })
            .TheTest
            .ShouldPass<ThrowOutcome>((because, outcome) =>
            {
                because.ItsTrue("the synchronous exception is the service's own", outcome.Sync is InvalidOperationException && outcome.Sync.Message == "sync boom");
                because.ItsTrue("its stack trace still points into the service", outcome.Sync?.StackTrace?.Contains(nameof(KitchenSinkService)) == true);
                because.ItsTrue("the asynchronous exception is the service's own", outcome.Async is InvalidOperationException && outcome.Async.Message == "async boom");
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest]
        public void MatchWhatTheGeneratorProducesToday()
        {
            After.Setup(reg =>
            {
                reg.For<DecoratorGenerator>().Use(DecoratorGeneratorShould.NewGenerator());
            })
            .When<DecoratorGenerator>("regenerates the checked-in fixtures", generator =>
            {
                return new DriftOutcome(
                    Normalize(generator.GetSource<IEchoService, EchoService>()) == Normalize(CheckedIn("EchoServiceDecorator.cs")),
                    Normalize(generator.GetSource<IKitchenSinkService, KitchenSinkService>()) == Normalize(CheckedIn("KitchenSinkServiceDecorator.cs")),
                    Normalize(generator.GetSource<IEdgeCaseService, EdgeCaseService>()) == Normalize(CheckedIn("EdgeCaseServiceDecorator.cs")));
            })
            .TheTest
            .ShouldPass<DriftOutcome>((because, outcome) =>
            {
                because.ItsTrue("Fixtures/Generated/EchoServiceDecorator.cs is current", outcome.EchoCurrent, "regenerate the fixtures: the generator's output has changed");
                because.ItsTrue("Fixtures/Generated/KitchenSinkServiceDecorator.cs is current", outcome.SinkCurrent, "regenerate the fixtures: the generator's output has changed");
                because.ItsTrue("Fixtures/Generated/EdgeCaseServiceDecorator.cs is current", outcome.EdgeCurrent, "regenerate the fixtures: the generator's output has changed");
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest]
        public void CompileFromGeneratedSource()
        {
            After.Setup(reg =>
            {
                reg.For<DecoratorGenerator>().Use(DecoratorGeneratorShould.NewGenerator());
            })
            .When<DecoratorGenerator>("compiles the source it generates", generator =>
            {
                DecoratorModel model = new DecoratorModel(typeof(IKitchenSinkService), typeof(KitchenSinkService));
                string source = generator.GetSource(typeof(IKitchenSinkService), typeof(KitchenSinkService));
                try
                {
                    byte[] assembly = new RoslynCompiler().Compile("GeneratedDecorator.Test", source, model.ReferencedTypes);
                    Type? decoratorType = Assembly.Load(assembly).GetType(model.DecoratorFullName);
                    return new CompileOutcome(assembly.Length > 0, decoratorType != null && typeof(IKitchenSinkService).IsAssignableFrom(decoratorType), string.Empty);
                }
                catch (Exception ex)
                {
                    return new CompileOutcome(false, false, ex.Message);
                }
            })
            .TheTest
            .ShouldPass<CompileOutcome>((because, outcome) =>
            {
                because.ItsTrue("the generated source compiles", outcome.Compiled, outcome.Diagnostics);
                because.ItsTrue("the compiled decorator implements the service interface", outcome.ImplementsInterface);
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        private static string CheckedIn(string fileName)
        {
            using Stream? stream = typeof(GeneratedDecoratorShould).Assembly.GetManifestResourceStream("Generated." + fileName);
            if (stream == null)
            {
                return string.Empty;
            }

            using StreamReader reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }

        // Line endings differ by checkout (core.autocrlf) and by the platform that generated the file.
        private static string Normalize(string source)
        {
            return source.Replace("\r\n", "\n").Trim();
        }

        private sealed record ForwardingOutcome(string Name, string Indexed, int Sum, double Doubles, int CountBeforeReset, int CountAfterReset, int Changes, bool Parsed, int Number, string? Found, string? NotFound, string Echoed, string Invoked);

        private sealed record OrderOutcome(string Order);

        private sealed record OverrideOutcome(string? ShortCircuited, string Replaced, string Recovered);

        private sealed record AsyncOverrideOutcome(int Sum, object? SeenAtEnd, string Described, string Recovered);

        private sealed record ThrowOutcome(Exception? Sync, Exception? Async);

        private sealed record DriftOutcome(bool EchoCurrent, bool SinkCurrent, bool EdgeCurrent);

        private sealed record CompileOutcome(bool Compiled, bool ImplementsInterface, string Diagnostics);
    }
}
