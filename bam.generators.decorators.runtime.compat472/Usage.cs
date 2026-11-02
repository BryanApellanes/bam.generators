using System;
using Bam.Generators.Decorators.Tests.Fixtures;
using Bam.Generators.Decorators.Tests.Fixtures.Decorators;

namespace Bam.Generators.Decorators.Compat
{
    /// <summary>
    /// What a .NET Framework host does with a generated decorator and the runtime alone: wrap the service,
    /// subscribe a handler, and give the runtime somewhere to report handler failures.
    /// </summary>
    public static class Usage
    {
        /// <summary>Wraps <paramref name="service"/> and counts calls to <c>Message</c>.</summary>
        public static IEchoService Decorate(EchoService service, Action<string> onMessage, IDecoratorLogger? logger = null)
        {
            EchoServiceDecorator decorator = new EchoServiceDecorator(service, logger);
            decorator.Subscribe(DecoratorPhase.Start, nameof(IEchoService.Message), context => onMessage((string)context.Args![0]!));
            return decorator;
        }

        /// <summary>Routes decorator failures to the host's own logging.</summary>
        public sealed class DelegateLogger : IDecoratorLogger
        {
            private readonly Action<string, Exception?> _sink;

            public DelegateLogger(Action<string, Exception?> sink)
            {
                _sink = sink ?? throw new ArgumentNullException(nameof(sink));
            }

            public void Error(string message, Exception? exception)
            {
                _sink(message, exception);
            }
        }
    }
}
