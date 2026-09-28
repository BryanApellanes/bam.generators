using Bam.DependencyInjection;
using Bam.Generators.Decorators.Tests.Fixtures;
using Bam.Generators.Decorators.Tests.Fixtures.Decorators;
using Bam.Logging;
using Bam.Test;
using NSubstitute;

namespace Bam.Generators.Decorators.Tests.Unit
{
    /// <summary>
    /// A handler that throws is logged and the call goes ahead, so a guard written that way fails open. These
    /// tests pin the supported way to stop a call: rejecting it.
    /// </summary>
    [UnitTestMenu("Decorator rejection Should", Selector = "drs")]
    public class DecoratorRejectionShould : UnitTestMenuContainer
    {
        public DecoratorRejectionShould(ServiceRegistry serviceRegistry) : base(serviceRegistry)
        {
        }

        [UnitTest]
        public void StopTheCallWhenAStartHandlerRejectsIt()
        {
            After.Setup(reg =>
            {
                reg.For<Decorator<EchoService>>().Use(new Decorator<EchoService>(new EchoService(), Substitute.For<ILogger>()));
            })
            .When<Decorator<EchoService>>("has a start handler that rejects the call", decorator =>
            {
                bool laterHandlerRan = false;
                bool endHandlerRan = false;
                string? errorRaised = null;
                decorator.Subscribe(DecoratorPhase.Start, "Message", context => { context.Reject("not allowed"); });
                decorator.Subscribe(DecoratorPhase.Start, "Message", context => { laterHandlerRan = true; });
                decorator.Subscribe(DecoratorPhase.End, "Message", context => { endHandlerRan = true; });
                decorator.MethodError += (sender, args) => errorRaised = args.Exception?.Message;

                DecoratorInvocationResult<EchoService, string> result = decorator.Invoke<string>("Message", "hello");
                return new RejectionOutcome(result.Success, result.Rejected, result.Value, result.Exception, Thrown(() => result.GetValue()), decorator.Instance.Calls, laterHandlerRan || endHandlerRan, errorRaised);
            })
            .TheTest
            .ShouldPass<RejectionOutcome>((because, outcome) =>
            {
                because.ItsTrue("the decorated method never ran", outcome.Calls == 0);
                because.ItsTrue("the invocation did not succeed", !outcome.Success && outcome.Rejected);
                because.ItsTrue("the result carries a DecoratorRejectionException with the reason", outcome.Exception is DecoratorRejectionException && outcome.Exception.Message == "not allowed");
                because.ItsTrue("GetValue throws the rejection", ReferenceEquals(outcome.Rethrown, outcome.Exception));
                because.ItsTrue("no value is produced", outcome.Value == null);
                because.ItsTrue("no further handlers ran", !outcome.OtherHandlersRan);
                because.ItsTrue("MethodError was raised for observers", outcome.ErrorRaised == "not allowed");
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest]
        public void TreatAThrownRejectionAsARejection()
        {
            ILogger logger = Substitute.For<ILogger>();

            After.Setup(reg =>
            {
                reg.For<Decorator<EchoService>>().Use(new Decorator<EchoService>(new EchoService(), logger));
            })
            .When<Decorator<EchoService>>("has a start handler that throws DecoratorRejectionException", decorator =>
            {
                Action<DecoratorInvocationContext<EchoService>> guard = context => throw new DecoratorRejectionException("thrown to reject");
                decorator.Subscribe(DecoratorPhase.Start, "Message", guard);

                DecoratorInvocationResult<EchoService, string> result = decorator.Invoke<string>("Message", "hello");
                int errorsLogged = logger.ReceivedCalls().Count(call => call.GetMethodInfo().Name == nameof(ILogger.Error));
                return new RejectionOutcome(result.Success, result.Rejected, result.Value, result.Exception, Thrown(() => result.GetValue()), decorator.Instance.Calls, errorsLogged > 0, null);
            })
            .TheTest
            .ShouldPass<RejectionOutcome>((because, outcome) =>
            {
                because.ItsTrue("the decorated method never ran", outcome.Calls == 0);
                because.ItsTrue("the call is rejected with the thrown exception", outcome.Rejected && outcome.Exception?.Message == "thrown to reject");
                because.ItsTrue("the caller receives it", ReferenceEquals(outcome.Rethrown, outcome.Exception));
                because.ItsTrue("a rejection is not logged as a handler failure", !outcome.OtherHandlersRan);
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest]
        public void LetTheCallThroughWhenAGuardThrowsAnythingElse()
        {
            // Documented behavior, pinned so it cannot change unnoticed: only a rejection stops a call.
            After.Setup(reg =>
            {
                reg.For<Decorator<EchoService>>().Use(new Decorator<EchoService>(new EchoService(), Substitute.For<ILogger>()));
            })
            .When<Decorator<EchoService>>("has a start handler that throws an ordinary exception", decorator =>
            {
                Action<DecoratorInvocationContext<EchoService>> guard = context => throw new UnauthorizedAccessException("ignored");
                decorator.Subscribe(DecoratorPhase.Start, "Message", guard);

                DecoratorInvocationResult<EchoService, string> result = decorator.Invoke<string>("Message", "hello");
                return new RejectionOutcome(result.Success, result.Rejected, result.Value, result.Exception, null, decorator.Instance.Calls, false, null);
            })
            .TheTest
            .ShouldPass<RejectionOutcome>((because, outcome) =>
            {
                because.ItsTrue("the call went ahead", outcome.Calls == 1 && outcome.Success && outcome.Value == "hello");
                because.ItsTrue("it is not reported as rejected", !outcome.Rejected);
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest]
        public void NotLetAnErrorHandlerSuppressARejection()
        {
            After.Setup(reg =>
            {
                reg.For<Decorator<KitchenSinkService>>().Use(new Decorator<KitchenSinkService>(new KitchenSinkService(), Substitute.For<ILogger>()));
            })
            .When<Decorator<KitchenSinkService>>("has error handlers that supply a fallback", decorator =>
            {
                decorator.SubscribeError(DecoratorSubscriptions.AnyMethod, context => "fallback");
                decorator.Subscribe(DecoratorPhase.Start, "Find", context => { context.Reject(new UnauthorizedAccessException("denied")); });
                decorator.Subscribe(DecoratorPhase.Error, "Fail", context => { context.Reject("failure is final"); });

                DecoratorInvocationResult<KitchenSinkService, string> rejectedAtStart = decorator.Invoke<string>("Find", "key");
                DecoratorInvocationResult<KitchenSinkService, string> rejectedOnError = decorator.Invoke<string>("Fail", "boom");
                return new SuppressionOutcome(rejectedAtStart.Success, rejectedAtStart.Handled, rejectedAtStart.Exception, rejectedOnError.Success, rejectedOnError.Handled, rejectedOnError.Exception, rejectedOnError.Value);
            })
            .TheTest
            .ShouldPass<SuppressionOutcome>((because, outcome) =>
            {
                because.ItsTrue("a start rejection is not turned into a success", !outcome.StartSuccess && !outcome.StartHandled);
                because.ItsTrue("the caller's exception is the one the handler chose", outcome.StartException is UnauthorizedAccessException);
                because.ItsTrue("a rejection during error handling replaces the failure", outcome.ErrorException is DecoratorRejectionException && outcome.ErrorException.Message == "failure is final");
                because.ItsTrue("and the fallback subscribed after it is not used", !outcome.ErrorSuccess && !outcome.ErrorHandled && outcome.ErrorValue == null);
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest]
        public void DiscardTheResultWhenAnEndHandlerRejectsIt()
        {
            After.Setup(reg =>
            {
                reg.For<Decorator<EchoService>>().Use(new Decorator<EchoService>(new EchoService(), Substitute.For<ILogger>()));
            })
            .When<Decorator<EchoService>>("has an end handler that rejects what the method returned", decorator =>
            {
                bool endRaised = false;
                decorator.Subscribe(DecoratorPhase.End, "Message", context =>
                {
                    if ((string?)context.Result == "secret")
                    {
                        context.Reject("refusing to return that");
                    }
                });
                decorator.MethodEnd += (sender, args) => endRaised = true;

                DecoratorInvocationResult<EchoService, string> rejected = decorator.Invoke<string>("Message", "secret");
                bool endRaisedForRejected = endRaised;
                DecoratorInvocationResult<EchoService, string> allowed = decorator.Invoke<string>("Message", "public");
                return new EndOutcome(rejected.Rejected, rejected.Value, decorator.Instance.Calls, endRaisedForRejected, allowed.Success, allowed.Value);
            })
            .TheTest
            .ShouldPass<EndOutcome>((because, outcome) =>
            {
                because.ItsTrue("the method ran both times", outcome.Calls == 2);
                because.ItsTrue("the rejected result never reaches the caller", outcome.Rejected && outcome.RejectedValue == null);
                because.ItsTrue("MethodEnd is not raised for a rejected call", !outcome.EndRaisedForRejected);
                because.ItsTrue("other calls are unaffected", outcome.AllowedSuccess && outcome.AllowedValue == "public");
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest]
        public void ReachTheCallerOfAGeneratedDecorator()
        {
            After.Setup(reg =>
            {
                reg.For<ILogger>().Use(Substitute.For<ILogger>());
                reg.For<IDecoratorTypeResolver>().Use(DecoratorTypeResolverShould.NewResolver());
                reg.For<IKitchenSinkService>().Use<KitchenSinkService>();
                reg.OnMethodStart("Add", context => { context.Reject(new UnauthorizedAccessException("registry-wide guard")); });
                reg.OnAddAsyncStart(context => { context.Reject("typed guard"); });
                Action<DecoratorInvocationContext<KitchenSinkService>> thrownGuard = context => throw new DecoratorRejectionException("thrown guard");
                reg.OnFindStart(thrownGuard);
            })
            .When<IKitchenSinkService>("is guarded by handlers that reject", sink =>
            {
                return new CallerOutcome(
                    Thrown(() => sink.Add(1, 2)),
                    Thrown(() => sink.AddAsync(1, 2).GetAwaiter().GetResult()),
                    Thrown(() => sink.Find("key")),
                    sink.Count,
                    sink.Invoke("unguarded"));
            })
            .TheTest
            .ShouldPass<CallerOutcome>((because, outcome) =>
            {
                because.ItsTrue("a registry-wide guard throws its exception to the caller", outcome.Sync is UnauthorizedAccessException && outcome.Sync.Message == "registry-wide guard");
                because.ItsTrue("a typed guard on an asynchronous method throws to the awaiting caller", outcome.Async is DecoratorRejectionException && outcome.Async.Message == "typed guard");
                because.ItsTrue("a guard that throws a rejection throws to the caller", outcome.Thrown is DecoratorRejectionException && outcome.Thrown.Message == "thrown guard");
                because.ItsTrue("none of the guarded methods ran", outcome.Count == 0);
                because.ItsTrue("unguarded methods still work", outcome.Unguarded == "invoked:unguarded");
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest]
        public void KeepTheFirstRejection()
        {
            When.A<DecoratorInvocationContext>("is rejected twice", new DecoratorInvocationContext(new object(), typeof(object)), context =>
            {
                bool rejectedInitially = context.Rejected;
                context.Reject("first");
                context.Reject(new InvalidOperationException("second"));
                Exception? nullRejection = Thrown(() => context.Reject((Exception)null!));
                return new FirstOutcome(rejectedInitially, context.Rejected, context.Rejection, nullRejection);
            })
            .TheTest
            .ShouldPass<FirstOutcome>((because, outcome) =>
            {
                because.ItsTrue("a new context is not rejected", !outcome.RejectedInitially);
                because.ItsTrue("the first rejection is kept", outcome.Rejected && outcome.Rejection?.Message == "first");
                because.ItsTrue("a null rejection is refused", outcome.NullRejection is ArgumentNullException);
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest]
        public void NotLetHandlersChangeTheArguments()
        {
            After.Setup(reg =>
            {
                reg.For<Decorator<EchoService>>().Use(new Decorator<EchoService>(new EchoService(), Substitute.For<ILogger>()));
            })
            .When<Decorator<EchoService>>("has a start handler that overwrites an argument", decorator =>
            {
                object?[] arguments = new object?[] { "original" };
                decorator.Subscribe(DecoratorPhase.Start, "Message", context => { context.Args[0] = "tampered"; });
                DecoratorInvocationResult<EchoService, string> byName = decorator.Invoke<string>("Message", arguments);

                IEchoService generated = new EchoServiceDecorator(new EchoService(), Substitute.For<ILogger>())
                    .OnMessageStart(context => { context.Args[0] = "tampered"; });
                return new ArgumentOutcome(byName.Value, (string?)arguments[0], generated.Message("original"));
            })
            .TheTest
            .ShouldPass<ArgumentOutcome>((because, outcome) =>
            {
                because.ItsTrue("the method invoked by name received the original argument", outcome.ByName == "original");
                because.ItsTrue("the caller's array is untouched", outcome.CallersArgument == "original");
                because.ItsTrue("a generated decorator behaves the same way", outcome.Generated == "original");
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        private static Exception? Thrown(Action action)
        {
            try
            {
                action();
                return null;
            }
            catch (Exception ex)
            {
                return ex;
            }
        }

        private sealed record RejectionOutcome(bool Success, bool Rejected, string? Value, Exception? Exception, Exception? Rethrown, int Calls, bool OtherHandlersRan, string? ErrorRaised);

        private sealed record SuppressionOutcome(bool StartSuccess, bool StartHandled, Exception? StartException, bool ErrorSuccess, bool ErrorHandled, Exception? ErrorException, string? ErrorValue);

        private sealed record EndOutcome(bool Rejected, string? RejectedValue, int Calls, bool EndRaisedForRejected, bool AllowedSuccess, string? AllowedValue);

        private sealed record CallerOutcome(Exception? Sync, Exception? Async, Exception? Thrown, int Count, string Unguarded);

        private sealed record FirstOutcome(bool RejectedInitially, bool Rejected, Exception? Rejection, Exception? NullRejection);

        private sealed record ArgumentOutcome(string? ByName, string? CallersArgument, string Generated);
    }
}
