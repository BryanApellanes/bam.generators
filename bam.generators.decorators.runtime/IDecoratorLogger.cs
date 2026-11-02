namespace Bam.Generators.Decorators
{
    /// <summary>
    /// Receives the failures a <see cref="Decorator{T}"/> cannot surface to its caller: a handler or event
    /// subscriber that threw, or a handler that supplied a value of the wrong type. The runtime depends on
    /// nothing but this, so any logging library can stand behind it; <c>bam.generators.decorators</c> adapts
    /// the Bam framework's <c>ILogger</c> to it.
    /// </summary>
    public interface IDecoratorLogger
    {
        /// <summary>Records a failure.</summary>
        /// <param name="message">What failed, and where: the phase, method and decorator type.</param>
        /// <param name="exception">The exception that was caught, or null when the failure threw nothing.</param>
        void Error(string message, Exception? exception);
    }
}
