namespace Bam.Generators.Decorators
{
    /// <summary>
    /// Thrown when a service cannot be decorated at runtime — for example the registered instance is not the
    /// expected implementation type, or an instance that was expected to be decorated is not.
    /// </summary>
    public class DecoratorException : Exception
    {
        /// <summary>Initializes a new instance with the specified message.</summary>
        public DecoratorException(string message) : base(message)
        {
        }

        /// <summary>Initializes a new instance with the specified message and cause.</summary>
        public DecoratorException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
