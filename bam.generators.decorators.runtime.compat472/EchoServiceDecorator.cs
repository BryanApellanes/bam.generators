// The decorator type from bam.generators.decorators.tests/Fixtures/Generated/EchoServiceDecorator.cs, as the
// generator emits it. The generated file also holds an extensions class that targets ServiceRegistry, which a
// consumer without the Bam framework leaves out; that half is bam.generators#15 (a container-agnostic option),
// after which this copy becomes real generator output. Keep this type in step with the fixture.
#nullable enable
namespace Bam.Generators.Decorators.Tests.Fixtures.Decorators
{
    /// <summary>
    /// Generated decorator for EchoService: implements IEchoService by forwarding to the instance it
    /// wraps, running the subscribed start, end and error handlers around every method.
    /// </summary>
    public partial class EchoServiceDecorator : global::Bam.Generators.Decorators.Decorator<global::Bam.Generators.Decorators.Tests.Fixtures.IEchoService, global::Bam.Generators.Decorators.Tests.Fixtures.EchoService>, global::Bam.Generators.Decorators.Tests.Fixtures.IEchoService
    {
        private static readonly global::System.Reflection.MethodInfo? __method0 = global::Bam.Generators.Decorators.DecoratedMethod.Find(typeof(global::Bam.Generators.Decorators.Tests.Fixtures.IEchoService), "Message", 0, "System.String");

        /// <summary>Initializes a decorator around <paramref name="instance"/>.</summary>
        /// <param name="instance">The instance to decorate.</param>
        /// <param name="logger">Receives handler failures. Defaults to <c>DecoratorLogger.Default</c>.</param>
        public EchoServiceDecorator(global::Bam.Generators.Decorators.Tests.Fixtures.EchoService instance, global::Bam.Generators.Decorators.IDecoratorLogger? logger = null) : base(instance, logger)
        {
        }

        /// <inheritdoc />
        public global::System.String Message(global::System.String message)
        {
            return base.Intercept<global::System.String>(__method0, "Message", new object?[] { message }, () => ((global::Bam.Generators.Decorators.Tests.Fixtures.IEchoService)base.Instance).Message(message)).GetValue()!;
        }
    }
}
