using Bam.DependencyInjection;
using Bam.Generators.Decorators.Tests.Fixtures;
using Bam.Logging;
using Bam.Test;
using NSubstitute;

namespace Bam.Generators.Decorators.Tests.Unit
{
    [UnitTestMenu("DecoratorInvocationResult Should", Selector = "dirs")]
    public class DecoratorInvocationResultShould : UnitTestMenuContainer
    {
        public DecoratorInvocationResultShould(ServiceRegistry serviceRegistry) : base(serviceRegistry)
        {
        }

        private static Decorator<EchoService> NewDecorator()
        {
            return new Decorator<EchoService>(new EchoService(), Substitute.For<ILogger>());
        }

        [UnitTest]
        public void SucceedWhenThereIsNoException()
        {
            When.A<Decorator<EchoService>>("produces a result without an exception", NewDecorator(), decorator =>
            {
                DecoratorInvocationResult<EchoService, string> result = new DecoratorInvocationResult<EchoService, string>(decorator, "value")
                {
                    MethodName = "Message",
                    Message = "all good"
                };
                string? converted = result;
                return new ResultOutcome(result.Success, result.GetValue(), converted, result.Message, result.MethodName, ReferenceEquals(result.Decorated, decorator.Instance));
            })
            .TheTest
            .ShouldPass<ResultOutcome>((because, outcome) =>
            {
                because.ItsTrue("it reports success", outcome.Success);
                because.ItsTrue("GetValue returns the value", outcome.Value == "value");
                because.ItsTrue("it converts implicitly to the value", outcome.Converted == "value");
                because.ItsTrue("it keeps the message it was given", outcome.Message == "all good");
                because.ItsTrue("it reports the method name", outcome.MethodName == "Message");
                because.ItsTrue("it exposes the decorated instance", outcome.ExposesDecorated);
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest]
        public void FailWhenThereIsAnUnhandledException()
        {
            When.A<Decorator<EchoService>>("produces a result with an exception", NewDecorator(), decorator =>
            {
                DecoratorInvocationResult<EchoService, string> result = new DecoratorInvocationResult<EchoService, string>(decorator)
                {
                    Exception = new InvalidOperationException("boom"),
                    Message = "ignored while failed"
                };
                string? converted = result;
                Exception? thrown = null;
                try
                {
                    result.ThrowIfFailed();
                }
                catch (Exception ex)
                {
                    thrown = ex;
                }

                return new FailedOutcome(result.Success, converted, result.Message, thrown);
            })
            .TheTest
            .ShouldPass<FailedOutcome>((because, outcome) =>
            {
                because.ItsTrue("it reports failure", !outcome.Success);
                because.ItsTrue("implicit conversion yields the default without throwing", outcome.Converted == null);
                because.ItsTrue("the message is the exception's", outcome.Message == "boom");
                because.ItsTrue("ThrowIfFailed rethrows the exception", outcome.Thrown is InvalidOperationException);
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest]
        public void SucceedWhenTheExceptionWasHandled()
        {
            When.A<Decorator<EchoService>>("produces a result whose exception was handled", NewDecorator(), decorator =>
            {
                DecoratorInvocationResult<EchoService, string> result = new DecoratorInvocationResult<EchoService, string>(decorator, "fallback")
                {
                    Exception = new InvalidOperationException("boom"),
                    Handled = true
                };
                Exception? thrown = null;
                try
                {
                    result.ThrowIfFailed();
                }
                catch (Exception ex)
                {
                    thrown = ex;
                }

                return new FailedOutcome(result.Success, result.GetValue(), result.Message, thrown);
            })
            .TheTest
            .ShouldPass<FailedOutcome>((because, outcome) =>
            {
                because.ItsTrue("it reports success", outcome.Success);
                because.ItsTrue("the fallback is the value", outcome.Converted == "fallback");
                because.ItsTrue("nothing is rethrown", outcome.Thrown == null);
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        private sealed record ResultOutcome(bool Success, string? Value, string? Converted, string Message, string? MethodName, bool ExposesDecorated);

        private sealed record FailedOutcome(bool Success, string? Converted, string Message, Exception? Thrown);
    }
}
