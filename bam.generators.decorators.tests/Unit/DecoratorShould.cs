using Bam.DependencyInjection;
using Bam.Generators.Decorators.Tests.Fixtures;
using Bam.Logging;
using Bam.Test;
using NSubstitute;

namespace Bam.Generators.Decorators.Tests.Unit
{
    [UnitTestMenu("Decorator Should", Selector = "ds")]
    public class DecoratorShould : UnitTestMenuContainer
    {
        public DecoratorShould(ServiceRegistry serviceRegistry) : base(serviceRegistry)
        {
        }

        private static int ErrorsLoggedTo(ILogger logger)
        {
            return logger.ReceivedCalls().Count(call => call.GetMethodInfo().Name == nameof(ILogger.Error));
        }

        [UnitTest]
        public void InvokeTheDecoratedMethodByName()
        {
            After.Setup(reg =>
            {
                reg.For<Decorator<EchoService>>().Use(new Decorator<EchoService>(new EchoService(), Substitute.For<ILogger>()));
            })
            .When<Decorator<EchoService>>("invokes a method by name", decorator =>
            {
                DecoratorInvocationResult<EchoService, string> result = decorator.Invoke<string>("Message", "hello");
                return new InvocationOutcome(result.Value, result.Success, result.ShortCircuited, result.Method?.Name, decorator.Instance.Calls);
            })
            .TheTest
            .ShouldPass<InvocationOutcome>((because, outcome) =>
            {
                because.ItsTrue("the decorated method's value is returned", outcome.Value == "hello");
                because.ItsTrue("the invocation succeeded", outcome.Success);
                because.ItsTrue("the invocation was not short-circuited", !outcome.ShortCircuited);
                because.ItsTrue("the reflected method is reported", outcome.MethodName == "Message");
                because.ItsTrue("the decorated method ran once", outcome.Calls == 1);
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest]
        public void HandTheInvocationToStartHandlers()
        {
            EchoService service = new EchoService();

            After.Setup(reg =>
            {
                reg.For<Decorator<EchoService>>().Use(new Decorator<EchoService>(service, Substitute.For<ILogger>()));
            })
            .When<Decorator<EchoService>>("runs a start handler", decorator =>
            {
                ContextOutcome? seen = null;
                decorator.Subscribe(DecoratorPhase.Start, "Message", context =>
                {
                    seen = new ContextOutcome(context.Phase, context.MethodName, context.Args.Length == 1 ? context.Args[0] as string : null, ReferenceEquals(context.Decorated, service), context.Method?.Name);
                });
                decorator.Invoke<string>("Message", "hello");
                return seen ?? new ContextOutcome(DecoratorPhase.Error, string.Empty, null, false, null);
            })
            .TheTest
            .ShouldPass<ContextOutcome>((because, outcome) =>
            {
                because.ItsTrue("the handler ran in the start phase", outcome.Phase == DecoratorPhase.Start);
                because.ItsTrue("the handler saw the method name", outcome.MethodName == "Message");
                because.ItsTrue("the handler saw the argument", outcome.FirstArgument == "hello");
                because.ItsTrue("the handler saw the decorated instance", outcome.SawDecoratedInstance);
                because.ItsTrue("the handler saw the reflected method", outcome.ReflectedMethodName == "Message");
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest]
        public void ShortCircuitWhenAStartHandlerReturnsAValue()
        {
            After.Setup(reg =>
            {
                reg.For<Decorator<EchoService>>().Use(new Decorator<EchoService>(new EchoService(), Substitute.For<ILogger>()));
            })
            .When<Decorator<EchoService>>("has a start handler that returns a value", decorator =>
            {
                decorator.SubscribeStart("Message", context => "cached");
                DecoratorInvocationResult<EchoService, string> result = decorator.Invoke<string>("Message", "hello");
                return new InvocationOutcome(result.Value, result.Success, result.ShortCircuited, result.Method?.Name, decorator.Instance.Calls);
            })
            .TheTest
            .ShouldPass<InvocationOutcome>((because, outcome) =>
            {
                because.ItsTrue("the handler's value is returned", outcome.Value == "cached");
                because.ItsTrue("the invocation is reported as short-circuited", outcome.ShortCircuited);
                because.ItsTrue("the decorated method never ran", outcome.Calls == 0);
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest]
        public void ShortCircuitWithoutArgumentsTheMethodRequires()
        {
            // The shape of the original sketch: the call supplies no arguments, which would fail if the
            // decorated method ran — but the start handler answers first.
            After.Setup(reg =>
            {
                reg.For<Decorator<EchoService>>().Use(new Decorator<EchoService>(new EchoService(), Substitute.For<ILogger>()));
            })
            .When<Decorator<EchoService>>("is invoked without arguments but a start handler answers", decorator =>
            {
                decorator.SubscribeStart("Message", context => "Test Result");
                DecoratorInvocationResult<EchoService, string> result = decorator.Invoke<string>("Message");
                return new InvocationOutcome(result.Value, result.Success, result.ShortCircuited, result.Method?.Name, decorator.Instance.Calls);
            })
            .TheTest
            .ShouldPass<InvocationOutcome>((because, outcome) =>
            {
                because.ItsTrue("the handler's value is returned", outcome.Value == "Test Result");
                because.ItsTrue("the invocation succeeded", outcome.Success);
                because.ItsTrue("the decorated method never ran", outcome.Calls == 0);
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest]
        public void ReplaceTheResultWhenAnEndHandlerReturnsAValue()
        {
            After.Setup(reg =>
            {
                reg.For<Decorator<EchoService>>().Use(new Decorator<EchoService>(new EchoService(), Substitute.For<ILogger>()));
            })
            .When<Decorator<EchoService>>("has an end handler that returns a value", decorator =>
            {
                string? seen = null;
                decorator.SubscribeEnd("Message", context =>
                {
                    seen = context.Result as string;
                    return seen?.ToUpperInvariant();
                });
                DecoratorInvocationResult<EchoService, string> result = decorator.Invoke<string>("Message", "hello");
                return new ReplacementOutcome(seen, result.Value, decorator.Instance.Calls);
            })
            .TheTest
            .ShouldPass<ReplacementOutcome>((because, outcome) =>
            {
                because.ItsTrue("the handler saw the decorated method's value", outcome.SeenByHandler == "hello");
                because.ItsTrue("the caller received the handler's value", outcome.Value == "HELLO");
                because.ItsTrue("the decorated method still ran", outcome.Calls == 1);
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest]
        public void CaptureTheExceptionWhenTheMethodThrows()
        {
            After.Setup(reg =>
            {
                reg.For<Decorator<KitchenSinkService>>().Use(new Decorator<KitchenSinkService>(new KitchenSinkService(), Substitute.For<ILogger>()));
            })
            .When<Decorator<KitchenSinkService>>("invokes a method that throws", decorator =>
            {
                DecoratorInvocationResult<KitchenSinkService, string> result = decorator.Invoke<string>("Fail", "boom");
                Exception? rethrown = null;
                try
                {
                    result.GetValue();
                }
                catch (Exception ex)
                {
                    rethrown = ex;
                }

                return new FailureOutcome(result.Success, result.Handled, result.Value, result.Exception, rethrown, result.Message);
            })
            .TheTest
            .ShouldPass<FailureOutcome>((because, outcome) =>
            {
                because.ItsTrue("the invocation did not succeed", !outcome.Success);
                because.ItsTrue("the original exception is captured, not reflection's wrapper", outcome.Exception is InvalidOperationException);
                because.ItsTrue("the message is the exception's", outcome.Message == "boom");
                because.ItsTrue("GetValue rethrows the same exception", ReferenceEquals(outcome.Rethrown, outcome.Exception));
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest]
        public void UseAnErrorHandlersValueAsAFallback()
        {
            After.Setup(reg =>
            {
                reg.For<Decorator<KitchenSinkService>>().Use(new Decorator<KitchenSinkService>(new KitchenSinkService(), Substitute.For<ILogger>()));
            })
            .When<Decorator<KitchenSinkService>>("has an error handler that returns a value", decorator =>
            {
                decorator.SubscribeError("Fail", context => "fallback for " + context.Exception?.Message);
                DecoratorInvocationResult<KitchenSinkService, string> result = decorator.Invoke<string>("Fail", "boom");
                Exception? rethrown = null;
                try
                {
                    result.GetValue();
                }
                catch (Exception ex)
                {
                    rethrown = ex;
                }

                return new FailureOutcome(result.Success, result.Handled, result.Value, result.Exception, rethrown, result.Message);
            })
            .TheTest
            .ShouldPass<FailureOutcome>((because, outcome) =>
            {
                because.ItsTrue("the invocation is reported as successful", outcome.Success);
                because.ItsTrue("the failure is reported as handled", outcome.Handled);
                because.ItsTrue("the handler's value is returned", outcome.Value == "fallback for boom");
                because.ItsTrue("the exception is still available", outcome.Exception is InvalidOperationException);
                because.ItsTrue("GetValue does not throw", outcome.Rethrown == null);
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest]
        public void SuppressAFailureWhenAHandlerSetsANullResult()
        {
            After.Setup(reg =>
            {
                reg.For<Decorator<KitchenSinkService>>().Use(new Decorator<KitchenSinkService>(new KitchenSinkService(), Substitute.For<ILogger>()));
            })
            .When<Decorator<KitchenSinkService>>("has an observe-only error handler that sets the result to null", decorator =>
            {
                decorator.Subscribe(DecoratorPhase.Error, "Fail", context => { context.Result = null; });
                DecoratorInvocationResult<KitchenSinkService, string> result = decorator.Invoke<string>("Fail", "boom");
                return new FailureOutcome(result.Success, result.Handled, result.Value, result.Exception, null, result.Message);
            })
            .TheTest
            .ShouldPass<FailureOutcome>((because, outcome) =>
            {
                because.ItsTrue("the failure is handled", outcome.Success && outcome.Handled);
                because.ItsTrue("the value is null", outcome.Value == null);
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest]
        public void ReportAMethodThatDoesNotExist()
        {
            After.Setup(reg =>
            {
                reg.For<Decorator<EchoService>>().Use(new Decorator<EchoService>(new EchoService(), Substitute.For<ILogger>()));
            })
            .When<Decorator<EchoService>>("invokes a method that does not exist", decorator =>
            {
                string? notFound = null;
                bool started = false;
                decorator.MethodNotFound += (sender, args) => notFound = args.MethodName;
                decorator.MethodStart += (sender, args) => started = true;
                DecoratorInvocationResult<EchoService, string> result = decorator.Invoke<string>("Whisper", "hello");
                return new NotFoundOutcome(notFound, started, result.Success, result.Exception);
            })
            .TheTest
            .ShouldPass<NotFoundOutcome>((because, outcome) =>
            {
                because.ItsTrue("MethodNotFound was raised with the name", outcome.NotFoundName == "Whisper");
                because.ItsTrue("MethodStart was not raised", !outcome.Started);
                because.ItsTrue("the invocation did not succeed", !outcome.Success);
                because.ItsTrue("the result carries a MissingMethodException", outcome.Exception is MissingMethodException);
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest]
        public void ReportWhatItCannotInvokeByName()
        {
            After.Setup(reg =>
            {
                reg.For<Decorator<KitchenSinkService>>().Use(new Decorator<KitchenSinkService>(new KitchenSinkService(), Substitute.For<ILogger>()));
            })
            .When<Decorator<KitchenSinkService>>("invokes a missing method asynchronously and a generic method by name", decorator =>
            {
                DecoratorInvocationResult<KitchenSinkService, string> missing = decorator.InvokeAsync<string>("Whisper").GetAwaiter().GetResult();
                DecoratorInvocationResult<KitchenSinkService, string> generic = decorator.Invoke<string>("Echo", "item");
                DecoratorInvocationResult<KitchenSinkService, int> wrongType = decorator.Invoke<int>("Find", "key");
                return new UninvokableOutcome(missing.Exception, generic.Exception, wrongType.Exception);
            })
            .TheTest
            .ShouldPass<UninvokableOutcome>((because, outcome) =>
            {
                because.ItsTrue("a missing method is reported on the awaited result", outcome.Missing is MissingMethodException);
                because.ItsTrue("a generic method is reported as unsupported by name", outcome.Generic is NotSupportedException);
                because.ItsTrue("asking for the wrong result type is reported, not hidden", outcome.WrongType is InvalidCastException);
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest]
        public void RaiseEventsAroundTheInvocation()
        {
            After.Setup(reg =>
            {
                reg.For<Decorator<KitchenSinkService>>().Use(new Decorator<KitchenSinkService>(new KitchenSinkService(), Substitute.For<ILogger>()));
            })
            .When<Decorator<KitchenSinkService>>("has event subscribers", decorator =>
            {
                List<string> raised = new List<string>();
                decorator.MethodStart += (sender, args) => raised.Add($"start:{args.MethodName}:{args.Args?.Length}");
                decorator.MethodEnd += (sender, args) => raised.Add($"end:{args.MethodName}:{args.Result}");
                decorator.MethodError += (sender, args) => raised.Add($"error:{args.MethodName}:{args.Exception?.Message}");
                decorator.Invoke<int>("Add", 1, 2);
                decorator.Invoke<string>("Fail", "boom");
                return new EventOutcome(string.Join("|", raised));
            })
            .TheTest
            .ShouldPass<EventOutcome>((because, outcome) =>
            {
                because.ItsTrue("start, end and error were raised in order with their data",
                    outcome.Raised == "start:Add:2|end:Add:3|start:Fail:1|error:Fail:boom",
                    $"raised: {outcome.Raised}");
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest]
        public void KeepGoingWhenAHandlerThrows()
        {
            ILogger logger = Substitute.For<ILogger>();

            After.Setup(reg =>
            {
                reg.For<Decorator<EchoService>>().Use(new Decorator<EchoService>(new EchoService(), logger));
            })
            .When<Decorator<EchoService>>("has a handler and an event subscriber that throw", decorator =>
            {
                bool secondHandlerRan = false;
                Action<DecoratorInvocationContext<EchoService>> badHandler = context => throw new InvalidOperationException("bad handler");
                decorator.Subscribe(DecoratorPhase.Start, "Message", badHandler);
                decorator.Subscribe(DecoratorPhase.Start, "Message", context => { secondHandlerRan = true; });
                decorator.MethodEnd += (sender, args) => throw new InvalidOperationException("bad subscriber");
                DecoratorInvocationResult<EchoService, string> result = decorator.Invoke<string>("Message", "hello");
                return new ResilienceOutcome(result.Success, result.Value, secondHandlerRan, ErrorsLoggedTo(logger));
            })
            .TheTest
            .ShouldPass<ResilienceOutcome>((because, outcome) =>
            {
                because.ItsTrue("the invocation still succeeded", outcome.Success);
                because.ItsTrue("the decorated method's value is returned", outcome.Value == "hello");
                because.ItsTrue("later handlers still ran", outcome.LaterHandlerRan);
                because.ItsTrue("both failures were logged", outcome.ErrorsLogged == 2, $"errors logged: {outcome.ErrorsLogged}");
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest]
        public void IgnoreAnOverrideOfTheWrongType()
        {
            ILogger logger = Substitute.For<ILogger>();

            After.Setup(reg =>
            {
                reg.For<Decorator<EchoService>>().Use(new Decorator<EchoService>(new EchoService(), logger));
            })
            .When<Decorator<EchoService>>("has a start handler that returns the wrong type", decorator =>
            {
                decorator.SubscribeStart("Message", context => 42);
                DecoratorInvocationResult<EchoService, string> result = decorator.Invoke<string>("Message", "hello");
                return new ResilienceOutcome(result.Success, result.Value, decorator.Instance.Calls == 1, ErrorsLoggedTo(logger));
            })
            .TheTest
            .ShouldPass<ResilienceOutcome>((because, outcome) =>
            {
                because.ItsTrue("the decorated method ran instead", outcome.LaterHandlerRan);
                because.ItsTrue("its value is returned", outcome.Value == "hello");
                because.ItsTrue("the unusable override was logged", outcome.ErrorsLogged == 1, $"errors logged: {outcome.ErrorsLogged}");
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest]
        public void TellOverloadsApartByArgumentType()
        {
            After.Setup(reg =>
            {
                reg.For<Decorator<KitchenSinkService>>().Use(new Decorator<KitchenSinkService>(new KitchenSinkService(), Substitute.For<ILogger>()));
            })
            .When<Decorator<KitchenSinkService>>("invokes overloads by name", decorator =>
            {
                DecoratorInvocationResult<KitchenSinkService, int> integers = decorator.Invoke<int>("Add", 1, 2);
                DecoratorInvocationResult<KitchenSinkService, double> doubles = decorator.Invoke<double>("Add", 1.5, 2.5);
                return new OverloadOutcome(integers.Value, integers.Method?.ReturnType, doubles.Value, doubles.Method?.ReturnType);
            })
            .TheTest
            .ShouldPass<OverloadOutcome>((because, outcome) =>
            {
                because.ItsTrue("the int overload was invoked", outcome.IntValue == 3 && outcome.IntMethodReturnType == typeof(int));
                because.ItsTrue("the double overload was invoked", outcome.DoubleValue == 4.0 && outcome.DoubleMethodReturnType == typeof(double));
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest]
        public void AwaitAsynchronousMethods()
        {
            After.Setup(reg =>
            {
                reg.For<Decorator<KitchenSinkService>>().Use(new Decorator<KitchenSinkService>(new KitchenSinkService(), Substitute.For<ILogger>()));
            })
            .When<Decorator<KitchenSinkService>>("invokes asynchronous methods by name", decorator =>
            {
                object? seenAtEnd = null;
                decorator.Subscribe(DecoratorPhase.End, "AddAsync", context => { seenAtEnd = context.Result; });
                DecoratorInvocationResult<KitchenSinkService, int> sum = decorator.InvokeAsync<int>("AddAsync", 1, 2).GetAwaiter().GetResult();
                DecoratorInvocationResult<KitchenSinkService, string> described = decorator.InvokeAsync<string>("DescribeAsync", "bam").GetAwaiter().GetResult();
                DecoratorInvocationResult<KitchenSinkService, object> flushed = decorator.InvokeAsync<object>("FlushAsync").GetAwaiter().GetResult();
                DecoratorInvocationResult<KitchenSinkService, string> failed = decorator.InvokeAsync<string>("FailAsync", "boom").GetAwaiter().GetResult();
                return new AsyncOutcome(sum.Value, seenAtEnd, described.Value, flushed.Success, decorator.Instance.Flushes, failed.Success, failed.Exception);
            })
            .TheTest
            .ShouldPass<AsyncOutcome>((because, outcome) =>
            {
                because.ItsTrue("a Task<T> is awaited for its value", outcome.Sum == 3);
                because.ItsTrue("the end handler ran after the task completed", Equals(outcome.SeenAtEnd, 3));
                because.ItsTrue("a ValueTask<T> is awaited for its value", outcome.Described == "about bam");
                because.ItsTrue("a ValueTask is awaited", outcome.FlushSucceeded && outcome.Flushes == 1);
                because.ItsTrue("a faulted task is captured", !outcome.FailSucceeded && outcome.FailException is InvalidOperationException);
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest]
        public void RunSharedSubscriptionsBeforeItsOwn()
        {
            After.Setup(reg =>
            {
                reg.For<Decorator<EchoService>>().Use(new Decorator<EchoService>(new EchoService(), Substitute.For<ILogger>()));
            })
            .When<Decorator<EchoService>>("has shared, named and any-method handlers", decorator =>
            {
                List<string> order = new List<string>();
                DecoratorSubscriptions shared = new DecoratorSubscriptions();
                shared.Add(DecoratorPhase.Start, "Message", context => { order.Add("shared:" + context.ImplementationType.Name); });
                // Attached twice: the second attach is a no-op, so the shared handler runs once.
                decorator.AttachSharedSubscriptions(shared);
                decorator.AttachSharedSubscriptions(shared);
                decorator.Subscribe(DecoratorPhase.Start, DecoratorSubscriptions.AnyMethod, context => { order.Add("any"); });
                decorator.Subscribe(DecoratorPhase.Start, "Message", context => { order.Add("own"); });
                decorator.Invoke<string>("Message", "hello");
                return new EventOutcome(string.Join("|", order) + $" stores:{decorator.SharedSubscriptions.Count}");
            })
            .TheTest
            .ShouldPass<EventOutcome>((because, outcome) =>
            {
                because.ItsTrue("shared handlers ran first and once, then named, then any-method",
                    outcome.Raised == "shared:EchoService|own|any stores:1",
                    $"order: {outcome.Raised}");
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest]
        public void InterceptASuppliedInvocation()
        {
            After.Setup(reg =>
            {
                reg.For<Decorator<EchoService>>().Use(new Decorator<EchoService>(new EchoService(), Substitute.For<ILogger>()));
            })
            .When<Decorator<EchoService>>("intercepts a delegate, as generated code does", decorator =>
            {
                string? seen = null;
                decorator.Subscribe(DecoratorPhase.End, "Message", context => { seen = context.Result as string; });
                DecoratorInvocationResult<EchoService, string> result = decorator.Intercept<string>("Message", new object?[] { "hello" }, () => decorator.Instance.Message("hello"));
                return new ReplacementOutcome(seen, result.Value, decorator.Instance.Calls);
            })
            .TheTest
            .ShouldPass<ReplacementOutcome>((because, outcome) =>
            {
                because.ItsTrue("the delegate's value is returned", outcome.Value == "hello");
                because.ItsTrue("handlers ran around it", outcome.SeenByHandler == "hello");
                because.ItsTrue("the delegate ran once", outcome.Calls == 1);
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest]
        public void RejectANullInstance()
        {
            When.A<DecoratorShould>("constructs a decorator around null", this, test =>
            {
                try
                {
                    Decorator<EchoService> decorator = new Decorator<EchoService>(null!, Substitute.For<ILogger>());
                    return new EventOutcome("constructed");
                }
                catch (ArgumentNullException)
                {
                    return new EventOutcome("rejected");
                }
            })
            .TheTest
            .ShouldPass<EventOutcome>((because, outcome) =>
            {
                because.ItsTrue("an ArgumentNullException is thrown", outcome.Raised == "rejected");
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        private sealed record InvocationOutcome(string? Value, bool Success, bool ShortCircuited, string? MethodName, int Calls);

        private sealed record ContextOutcome(DecoratorPhase Phase, string MethodName, string? FirstArgument, bool SawDecoratedInstance, string? ReflectedMethodName);

        private sealed record ReplacementOutcome(string? SeenByHandler, string? Value, int Calls);

        private sealed record FailureOutcome(bool Success, bool Handled, string? Value, Exception? Exception, Exception? Rethrown, string Message);

        private sealed record NotFoundOutcome(string? NotFoundName, bool Started, bool Success, Exception? Exception);

        private sealed record EventOutcome(string Raised);

        private sealed record UninvokableOutcome(Exception? Missing, Exception? Generic, Exception? WrongType);

        private sealed record ResilienceOutcome(bool Success, string? Value, bool LaterHandlerRan, int ErrorsLogged);

        private sealed record OverloadOutcome(int IntValue, Type? IntMethodReturnType, double DoubleValue, Type? DoubleMethodReturnType);

        private sealed record AsyncOutcome(int Sum, object? SeenAtEnd, string? Described, bool FlushSucceeded, int Flushes, bool FailSucceeded, Exception? FailException);
    }
}
