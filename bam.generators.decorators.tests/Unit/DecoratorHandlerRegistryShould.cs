using Bam.DependencyInjection;
using Bam.Test;

namespace Bam.Generators.Decorators.Tests.Unit
{
    [UnitTestMenu("DecoratorHandlerRegistry Should", Selector = "dhrs")]
    public class DecoratorHandlerRegistryShould : UnitTestMenuContainer
    {
        public DecoratorHandlerRegistryShould(ServiceRegistry serviceRegistry) : base(serviceRegistry)
        {
        }

        private static DecoratorInvocationContext NewContext()
        {
            return new DecoratorInvocationContext(new object(), typeof(object));
        }

        [UnitTest]
        public void ReturnHandlersInSubscriptionOrder()
        {
            After.Setup(reg =>
            {
                reg.For<DecoratorSubscriptions>().Use(new DecoratorSubscriptions());
            })
            .When<DecoratorSubscriptions>("has handlers for a method and for any method", subscriptions =>
            {
                List<string> ran = new List<string>();
                subscriptions.Add(DecoratorPhase.Start, DecoratorSubscriptions.AnyMethod, context => { ran.Add("any"); });
                subscriptions.Add(DecoratorPhase.Start, "Message", context => { ran.Add("first"); });
                subscriptions.Add(DecoratorPhase.Start, "Message", context => { ran.Add("second"); });
                subscriptions.Add(DecoratorPhase.End, "Message", context => { ran.Add("end"); });
                subscriptions.Add(DecoratorPhase.Start, "Other", context => { ran.Add("other"); });

                foreach (Func<DecoratorInvocationContext, object?> handler in subscriptions.Get(DecoratorPhase.Start, "Message"))
                {
                    handler(NewContext());
                }

                return new RegistryOutcome(string.Join("|", ran), subscriptions.Count(DecoratorPhase.Start, "Message"), subscriptions.Count(DecoratorPhase.Error, "Message"));
            })
            .TheTest
            .ShouldPass<RegistryOutcome>((because, outcome) =>
            {
                because.ItsTrue("named handlers run in order, then any-method handlers", outcome.Ran == "first|second|any", $"ran: {outcome.Ran}");
                because.ItsTrue("the count covers named and any-method handlers", outcome.StartCount == 3);
                because.ItsTrue("a phase with no handlers is empty", outcome.ErrorCount == 0);
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest]
        public void NotSubscribeTheSameHandlerTwice()
        {
            After.Setup(reg =>
            {
                reg.For<DecoratorSubscriptions>().Use(new DecoratorSubscriptions());
            })
            .When<DecoratorSubscriptions>("is given the same delegate twice", subscriptions =>
            {
                Func<DecoratorInvocationContext, object?> handler = context => "value";
                subscriptions.Add(DecoratorPhase.Start, "Message", handler);
                subscriptions.Add(DecoratorPhase.Start, "Message", handler);
                return new RegistryOutcome(string.Empty, subscriptions.Count(DecoratorPhase.Start, "Message"), 0);
            })
            .TheTest
            .ShouldPass<RegistryOutcome>((because, outcome) =>
            {
                because.ItsTrue("the handler is subscribed once", outcome.StartCount == 1);
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest]
        public void NotRunAnyMethodHandlersTwiceWhenAskedForThemDirectly()
        {
            After.Setup(reg =>
            {
                reg.For<DecoratorSubscriptions>().Use(new DecoratorSubscriptions());
            })
            .When<DecoratorSubscriptions>("is asked for the any-method handlers by their own name", subscriptions =>
            {
                subscriptions.Add(DecoratorPhase.Start, DecoratorSubscriptions.AnyMethod, context => { });
                return new RegistryOutcome(string.Empty, subscriptions.Count(DecoratorPhase.Start, DecoratorSubscriptions.AnyMethod), 0);
            })
            .TheTest
            .ShouldPass<RegistryOutcome>((because, outcome) =>
            {
                because.ItsTrue("each handler is returned once", outcome.StartCount == 1);
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest]
        public void RejectABlankMethodNameOrNullHandler()
        {
            After.Setup(reg =>
            {
                reg.For<DecoratorSubscriptions>().Use(new DecoratorSubscriptions());
            })
            .When<DecoratorSubscriptions>("is given invalid subscriptions", subscriptions =>
            {
                Func<DecoratorInvocationContext, object?> handler = context => null;
                Func<DecoratorInvocationContext, object?>? noFunc = null;
                Action<DecoratorInvocationContext>? noAction = null;
                return new RejectionOutcome(
                    Throws<ArgumentException>(() => subscriptions.Add(DecoratorPhase.Start, " ", handler)),
                    Throws<ArgumentNullException>(() => subscriptions.Add(DecoratorPhase.Start, "Message", noFunc!)),
                    Throws<ArgumentNullException>(() => subscriptions.Add(DecoratorPhase.Start, "Message", noAction!)));
            })
            .TheTest
            .ShouldPass<RejectionOutcome>((because, outcome) =>
            {
                because.ItsTrue("a blank method name is rejected", outcome.BlankNameRejected);
                because.ItsTrue("a null Func handler is rejected", outcome.NullFuncRejected);
                because.ItsTrue("a null Action handler is rejected", outcome.NullActionRejected);
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest]
        public void CheckATypedHandlerBeforeWrappingIt()
        {
            // The generated hooks subscribe a Func typed to the method's result. The async check has to see
            // that delegate, not the synchronous wrapper the store puts around it.
            After.Setup(reg =>
            {
                reg.For<DecoratorSubscriptions>().Use(new DecoratorSubscriptions());
            })
            .When<DecoratorSubscriptions>("is given typed handlers", subscriptions =>
            {
                Func<DecoratorInvocationContext, object?> asyncByCovariance = AsyncByCovariance;
                Func<DecoratorInvocationContext, int> typed = context => 42;
                Func<DecoratorInvocationContext, Task<int>> taskTyped = context => Task.FromResult(42);
                subscriptions.Add(DecoratorPhase.Start, "Add", typed);
                subscriptions.Add(DecoratorPhase.Start, "Add", typed);
                object? value = subscriptions.Get(DecoratorPhase.Start, "Add").Single()(NewContext());
                // Two delegate instances over the same instance method are equal delegates, stored once, the
                // same rule the untyped overload applies.
                Func<DecoratorInvocationContext, int> firstGroup = Seven;
                Func<DecoratorInvocationContext, int> secondGroup = Seven;
                subscriptions.Add(DecoratorPhase.Start, "Reset", firstGroup);
                subscriptions.Add(DecoratorPhase.Start, "Reset", secondGroup);
                return new TypedOutcome(
                    Throws<ArgumentException>(() => subscriptions.Add<object?>(DecoratorPhase.Start, "Add", asyncByCovariance)),
                    value is int boxed && boxed == 42,
                    subscriptions.Count(DecoratorPhase.Start, "Add") == 1 && subscriptions.Count(DecoratorPhase.Start, "Reset") == 1,
                    Throws<ArgumentException>(() => subscriptions.Add(DecoratorPhase.Start, "Add", taskTyped)));
            })
            .TheTest
            .ShouldPass<TypedOutcome>((because, outcome) =>
            {
                because.ItsTrue("an async method group bound through return-type covariance is refused", outcome.AsyncRefused);
                because.ItsTrue("a typed handler's value comes through the wrapper boxed", outcome.ValueBoxed);
                because.ItsTrue("the same typed delegate subscribed twice is stored once", outcome.Deduplicated);
                because.ItsTrue("a typed handler declared to return a task is refused when subscribed", outcome.TaskTypedRefused);
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        private int Seven(DecoratorInvocationContext context)
        {
            return 7;
        }

        private static async Task<object?> AsyncByCovariance(DecoratorInvocationContext context)
        {
            await Task.Yield();
            return null;
        }

        [UnitTest]
        public void TrackWhetherAHandlerOverrodeTheResult()
        {
            When.A<DecoratorInvocationContext>("has its result set by a handler", NewContext(), context =>
            {
                bool overriddenInitially = context.ResultOverridden;
                object? initial = context.Result;
                context.Result = null;
                return new ContextOutcome(overriddenInitially, initial, context.ResultOverridden, context.Result, context.MethodName);
            })
            .TheTest
            .ShouldPass<ContextOutcome>((because, outcome) =>
            {
                because.ItsTrue("a new context has no override", !outcome.OverriddenInitially && outcome.InitialResult == null);
                because.ItsTrue("setting the result is an override, even to null", outcome.OverriddenAfterSet && outcome.ResultAfterSet == null);
                because.ItsTrue("the method name defaults to empty", outcome.MethodName == string.Empty);
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        private static bool Throws<TException>(Action action) where TException : Exception
        {
            try
            {
                action();
                return false;
            }
            catch (TException)
            {
                return true;
            }
        }

        private sealed record RegistryOutcome(string Ran, int StartCount, int ErrorCount);

        private sealed record RejectionOutcome(bool BlankNameRejected, bool NullFuncRejected, bool NullActionRejected);

        private sealed record TypedOutcome(bool AsyncRefused, bool ValueBoxed, bool Deduplicated, bool TaskTypedRefused);

        private sealed record ContextOutcome(bool OverriddenInitially, object? InitialResult, bool OverriddenAfterSet, object? ResultAfterSet, string MethodName);
    }
}
