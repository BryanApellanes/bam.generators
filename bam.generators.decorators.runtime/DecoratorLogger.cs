namespace Bam.Generators.Decorators
{
    /// <summary>
    /// The <see cref="IDecoratorLogger"/> a decorator falls back to when it is constructed without one.
    /// Starts as a <see cref="TraceDecoratorLogger"/>; a host that has a logging library sets it once at
    /// startup, the way <c>bam.generators.decorators</c> does for the Bam framework's default logger.
    /// </summary>
    public static class DecoratorLogger
    {
        private static IDecoratorLogger _default = new TraceDecoratorLogger();

        /// <summary>
        /// Gets or sets the logger used by decorators constructed without one. Setting it to null restores
        /// the <see cref="TraceDecoratorLogger"/>.
        /// </summary>
        public static IDecoratorLogger Default
        {
            get => _default;
            set => _default = value ?? new TraceDecoratorLogger();
        }
    }
}
