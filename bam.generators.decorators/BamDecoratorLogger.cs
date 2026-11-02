using Bam.Logging;

namespace Bam.Generators.Decorators
{
    /// <summary>
    /// Adapts a Bam <see cref="ILogger"/> to the runtime's <see cref="IDecoratorLogger"/>. Wrap a specific
    /// logger, or construct it without one to follow <see cref="Log.Default"/> as it is at the time of each
    /// failure, which is what <c>ServiceRegistryExtensions</c> installs as <see cref="DecoratorLogger.Default"/>.
    /// </summary>
    public sealed class BamDecoratorLogger : IDecoratorLogger
    {
        private readonly ILogger? _logger;

        /// <summary>Initializes an adapter that reads <see cref="Log.Default"/> on every call.</summary>
        public BamDecoratorLogger()
        {
        }

        /// <summary>Initializes an adapter over <paramref name="logger"/>.</summary>
        /// <param name="logger">The logger to forward to.</param>
        /// <exception cref="ArgumentNullException"><paramref name="logger"/> is null.</exception>
        public BamDecoratorLogger(ILogger logger)
        {
            ArgumentNullException.ThrowIfNull(logger);

            _logger = logger;
        }

        /// <summary>Gets the logger failures go to: the one given, or <see cref="Log.Default"/>.</summary>
        public ILogger? Logger => _logger ?? Log.Default;

        /// <inheritdoc />
        public void Error(string message, Exception? exception)
        {
            ILogger? logger = Logger;
            if (logger == null)
            {
                return;
            }

            // The message is already formatted; it goes through as a single argument so a brace in an
            // exception message cannot be taken for a format item.
            if (exception == null)
            {
                logger.Error("{0}", message);
            }
            else
            {
                logger.Error("{0}", exception, message);
            }
        }

        /// <summary>
        /// Installs a <see cref="BamDecoratorLogger"/> following <see cref="Log.Default"/> as the runtime's
        /// <see cref="DecoratorLogger.Default"/>, unless something other than the runtime's own
        /// <see cref="TraceDecoratorLogger"/> is already there. Called once by <c>ServiceRegistryExtensions</c>;
        /// a host that builds decorators by hand and never touches a registry calls it at startup.
        /// </summary>
        public static void InstallAsDefault()
        {
            if (DecoratorLogger.Default is TraceDecoratorLogger)
            {
                DecoratorLogger.Default = new BamDecoratorLogger();
            }
        }
    }

    /// <summary>Conversions between Bam loggers and decorator loggers.</summary>
    public static class BamDecoratorLoggerExtensions
    {
        /// <summary>
        /// Wraps <paramref name="logger"/> for the decorator runtime, or returns null when it is null so the
        /// decorator falls back to <see cref="DecoratorLogger.Default"/>.
        /// </summary>
        public static IDecoratorLogger? ToDecoratorLogger(this ILogger? logger)
        {
            return logger == null ? null : new BamDecoratorLogger(logger);
        }
    }
}
