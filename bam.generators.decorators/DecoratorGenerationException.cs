namespace Bam.Generators.Decorators
{
    /// <summary>
    /// Thrown when a decorator cannot be generated for a service — for example the service type is not an
    /// interface, the implementation does not implement it, or a member cannot be expressed in generated code.
    /// </summary>
    public class DecoratorGenerationException : Exception
    {
        /// <summary>Initializes a new instance for the specified service interface and reason.</summary>
        /// <param name="interfaceType">The service interface a decorator was requested for.</param>
        /// <param name="message">Why it could not be generated.</param>
        public DecoratorGenerationException(Type interfaceType, string message)
            : base($"[{interfaceType.FullName}] {message}")
        {
            InterfaceType = interfaceType;
        }

        /// <summary>Initializes a new instance for the specified service interface, reason and cause.</summary>
        /// <param name="interfaceType">The service interface a decorator was requested for.</param>
        /// <param name="message">Why it could not be generated.</param>
        /// <param name="innerException">The underlying failure.</param>
        public DecoratorGenerationException(Type interfaceType, string message, Exception innerException)
            : base($"[{interfaceType.FullName}] {message}", innerException)
        {
            InterfaceType = interfaceType;
        }

        /// <summary>Gets the service interface a decorator could not be generated for.</summary>
        public Type InterfaceType { get; }
    }
}
