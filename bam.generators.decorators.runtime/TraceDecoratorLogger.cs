using System.Diagnostics;

namespace Bam.Generators.Decorators
{
    /// <summary>
    /// Writes decorator failures to <see cref="Trace"/>, which is available on every target the runtime
    /// supports and goes nowhere unless the host attached a listener. The default when no logger is given.
    /// </summary>
    public sealed class TraceDecoratorLogger : IDecoratorLogger
    {
        /// <inheritdoc />
        public void Error(string message, Exception? exception)
        {
            Trace.TraceError(exception == null ? message : $"{message}{Environment.NewLine}{exception}");
        }
    }
}
