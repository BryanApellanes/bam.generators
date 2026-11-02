namespace Bam.Generators.Decorators
{
    /// <summary>
    /// Describes one decorated method invocation on a <typeparamref name="T"/> to the typed handlers subscribed
    /// to it, exposing the wrapped instance as <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The implementation type the decorator wraps.</typeparam>
    public class DecoratorInvocationContext<T> : DecoratorInvocationContext where T : class
    {
        /// <summary>Initializes a context for an invocation made through <paramref name="decorator"/>.</summary>
        /// <param name="decorator">The decorator performing the invocation.</param>
        public DecoratorInvocationContext(Decorator<T> decorator) : base(decorator.Instance, decorator.ImplementationType)
        {
            this.Decorator = decorator;
            this.Decorated = decorator.Instance;
        }

        /// <summary>Gets or sets the decorator performing the invocation.</summary>
        protected Decorator<T> Decorator { get; set; }

        /// <summary>Gets the instance the decorator wraps.</summary>
        public T Decorated { get; }
    }
}
