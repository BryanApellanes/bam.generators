namespace Bam.Generators.Decorators
{
    /// <summary>
    /// Event data for the <see cref="Decorator{T}.MethodStart"/>, <see cref="Decorator{T}.MethodEnd"/>,
    /// <see cref="Decorator{T}.MethodError"/> and <see cref="Decorator{T}.MethodNotFound"/> events. Events
    /// observe an invocation; to change its outcome subscribe a handler instead.
    /// </summary>
    /// <typeparam name="T">The implementation type the decorator wraps.</typeparam>
    public class DecoratorEventArgs<T> : EventArgs where T : class
    {
        /// <summary>Initializes event data for an invocation made through <paramref name="decorator"/>.</summary>
        /// <param name="decorator">The decorator raising the event.</param>
        public DecoratorEventArgs(Decorator<T> decorator)
        {
            this.Decorator = decorator;
            this.MethodName = string.Empty;
        }

        /// <summary>Gets the decorator that raised the event.</summary>
        public Decorator<T> Decorator { get; init; }

        /// <summary>Gets or sets the name of the method being invoked.</summary>
        public string MethodName { get; set; }

        /// <summary>Gets or sets the arguments the method was invoked with.</summary>
        public object?[]? Args { get; set; }

        /// <summary>Gets or sets the value handed back to the caller; set for <see cref="Decorator{T}.MethodEnd"/>.</summary>
        public object? Result { get; set; }

        /// <summary>Gets or sets the exception the method threw; set for <see cref="Decorator{T}.MethodError"/>.</summary>
        public Exception? Exception { get; set; }
    }
}
