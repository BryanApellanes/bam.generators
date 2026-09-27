using Bam.Logging;

namespace Bam.Generators.Decorators
{
    /// <summary>
    /// The base every generated decorator extends: a <see cref="Decorator{T}"/> that knows the service
    /// interface it stands in for. A generated decorator derives from this and implements
    /// <typeparamref name="I"/>, which is what lets it replace the registration for <typeparamref name="I"/>
    /// in a <c>ServiceRegistry</c>.
    /// </summary>
    /// <typeparam name="I">The service interface the decorator is resolved as.</typeparam>
    /// <typeparam name="T">The implementation type being decorated.</typeparam>
    public class Decorator<I, T> : Decorator<T> where I : class where T : class, I
    {
        /// <summary>Unwraps a decorator to the instance it decorates.</summary>
        public static explicit operator T(Decorator<I, T> decorator)
        {
            return decorator.Instance;
        }

        /// <summary>Initializes a decorator around <paramref name="value"/>.</summary>
        /// <param name="value">The instance to decorate.</param>
        /// <param name="logger">Receives handler failures. Defaults to <see cref="Log.Default"/>.</param>
        /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
        public Decorator(T value, ILogger? logger = null) : base(value, logger)
        {
        }

        /// <summary>Gets the service interface the decorator is resolved as.</summary>
        public Type InterfaceType
        {
            get => typeof(I);
        }
    }
}
