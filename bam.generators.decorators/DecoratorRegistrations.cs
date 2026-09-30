using System.Collections.Concurrent;

namespace Bam.Generators.Decorators
{
    /// <summary>
    /// The decorated services of one <c>ServiceRegistry</c>, by service interface. One instance lives in each
    /// registry. It is how decorating a service twice finds the first decoration, and how a service that was
    /// registered again after being decorated gets its handlers back when it is decorated again.
    /// </summary>
    public class DecoratorRegistrations
    {
        private readonly ConcurrentDictionary<Type, IDecoratorRegistration> _registrations;

        /// <summary>Initializes an empty set of registrations.</summary>
        public DecoratorRegistrations()
        {
            _registrations = new ConcurrentDictionary<Type, IDecoratorRegistration>();
        }

        /// <summary>Gets the service interfaces that have been decorated.</summary>
        public IReadOnlyCollection<Type> DecoratedTypes => _registrations.Keys.ToArray();

        /// <summary>Gets the registration of <typeparamref name="I"/> decorated as a <typeparamref name="T"/>.</summary>
        /// <typeparam name="I">The service interface.</typeparam>
        /// <typeparam name="T">The implementation type it was decorated as.</typeparam>
        /// <param name="registration">The registration, or null when the service was not decorated as a <typeparamref name="T"/>.</param>
        /// <returns>True when there is one.</returns>
        public bool TryGet<I, T>(out DecoratorRegistration<I, T>? registration) where I : class where T : class, I
        {
            registration = _registrations.TryGetValue(typeof(I), out IDecoratorRegistration? found) ? found as DecoratorRegistration<I, T> : null;
            return registration != null;
        }

        /// <summary>Gets the registration of <paramref name="interfaceType"/>, whatever it was decorated as.</summary>
        /// <param name="interfaceType">The service interface.</param>
        /// <param name="registration">The registration, or null when the service was not decorated.</param>
        /// <returns>True when there is one.</returns>
        public bool TryGet(Type interfaceType, out IDecoratorRegistration? registration)
        {
            ArgumentNullException.ThrowIfNull(interfaceType);

            return _registrations.TryGetValue(interfaceType, out registration);
        }

        /// <summary>Records <paramref name="registration"/> as the decoration of <typeparamref name="I"/>, replacing any before it.</summary>
        /// <typeparam name="I">The service interface.</typeparam>
        /// <typeparam name="T">The implementation type it is decorated as.</typeparam>
        /// <param name="registration">The registration to record.</param>
        public void Set<I, T>(DecoratorRegistration<I, T> registration) where I : class where T : class, I
        {
            ArgumentNullException.ThrowIfNull(registration);

            _registrations[typeof(I)] = registration;
        }
    }
}
