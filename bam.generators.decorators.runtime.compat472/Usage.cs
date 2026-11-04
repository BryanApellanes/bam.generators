using System;
using Bam.Generators.Decorators.Tests.Fixtures;
using Bam.Generators.Decorators.Tests.Fixtures.Decorators;

namespace Bam.Generators.Decorators.Compat
{
    /// <summary>
    /// What a .NET Framework host does with a generated decorator and the runtime alone: wrap the service,
    /// subscribe a handler through the generated extension methods or the shared store, and give the runtime
    /// somewhere to report handler failures.
    /// </summary>
    public static class Usage
    {
        /// <summary>Wraps <paramref name="service"/> and reports calls to <c>Message</c> through the generated hook.</summary>
        public static IEchoService Decorate(EchoService service, Action<string> onMessage, IDecoratorLogger? logger = null)
        {
            EchoServiceDecorator decorator = new EchoServiceDecorator(service, logger);
            return decorator.OnMessageStart(context => onMessage((string)context.Args![0]!));
        }

        /// <summary>
        /// What a container's decoration hook calls: the factory finds the generated decorator and attaches the
        /// handlers subscribed once for the pair through <see cref="DecoratorFactory.Handlers{I, T}"/>.
        /// </summary>
        public static IEchoService DecorateFromContainer(EchoService service, IDecoratorLogger? logger = null)
        {
            return DecoratorFactory.Create<IEchoService, EchoService, EchoServiceDecorator>(service, logger);
        }

        /// <summary>Subscribes <paramref name="onMessage"/> to every decorator the factory creates for the pair.</summary>
        public static void OnEveryMessage(Action<string> onMessage)
        {
            DecoratorFactory.Handlers<IEchoService, EchoService>().Add(DecoratorPhase.Start, nameof(IEchoService.Message), context => onMessage((string)context.Args![0]!));
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
