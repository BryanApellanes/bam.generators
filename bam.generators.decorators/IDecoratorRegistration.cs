namespace Bam.Generators.Decorators
{
    /// <summary>
    /// What every <see cref="DecoratorRegistration{I, T}"/> has in common, whatever it decorates. It lets
    /// registrations of different services be kept together.
    /// </summary>
    public interface IDecoratorRegistration
    {
        /// <summary>Gets the service interface being decorated.</summary>
        Type InterfaceType { get; }

        /// <summary>Gets the implementation type being decorated.</summary>
        Type ImplementationType { get; }

        /// <summary>Gets the type of the decorators the registration creates.</summary>
        Type DecoratorType { get; }
    }
}
