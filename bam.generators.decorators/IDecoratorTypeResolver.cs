namespace Bam.Generators.Decorators
{
    /// <summary>
    /// Finds the concrete decorator type for a service at runtime. Register an implementation in a
    /// <c>ServiceRegistry</c> to control how <c>Decorate&lt;I, T&gt;()</c> obtains decorators — for example to
    /// forbid runtime compilation.
    /// </summary>
    public interface IDecoratorTypeResolver
    {
        /// <summary>
        /// Gets the type that decorates <paramref name="implementationType"/> as
        /// <paramref name="interfaceType"/>: a class extending <c>Decorator&lt;I, T&gt;</c> that implements the
        /// interface and has a public <c>(T, ILogger)</c> constructor.
        /// </summary>
        /// <param name="interfaceType">The service interface.</param>
        /// <param name="implementationType">The implementation type being decorated.</param>
        /// <exception cref="DecoratorGenerationException">No decorator type exists and one could not be produced.</exception>
        Type Resolve(Type interfaceType, Type implementationType);
    }
}
