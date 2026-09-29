using Bam.DependencyInjection;
using Bam.Logging;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Bam.Generators.Decorators
{
    /// <summary>
    /// The registration of a decorated service in a <see cref="ServiceRegistry"/>. It stands between the
    /// registry and the registration it replaced: each time the service is resolved it resolves the previous
    /// registration and wraps what comes back. That keeps the service's lifetime — a transient service is still
    /// constructed per resolve, each instance wrapped in its own decorator, and a single instance gets the same
    /// decorator every time.
    /// </summary>
    /// <remarks>
    /// Handlers subscribed here are shared by every decorator the registration creates, so they keep running
    /// however many times the service is resolved. Nothing is resolved or constructed until the service is.
    /// </remarks>
    /// <typeparam name="I">The service interface the decorator is resolved as.</typeparam>
    /// <typeparam name="T">The implementation type being decorated.</typeparam>
    public class DecoratorRegistration<I, T> : IDecoratorRegistration, IDecoratorSubscriber<T> where I : class where T : class, I
    {
        // Set while asking a registry whether this registration is still the one it resolves I with.
        [ThreadStatic]
        private static DecoratorRegistration<I, T>? _probe;

        private readonly Func<I?> _previous;
        private readonly ConstructorInfo _constructor;
        private readonly ConditionalWeakTable<T, Decorator<I, T>> _decorators;

        /// <summary>Initializes a registration that decorates what <paramref name="previous"/> resolves.</summary>
        /// <param name="previous">Resolves the registration being decorated.</param>
        /// <param name="decoratorType">
        /// The decorator type: a class extending <see cref="Decorator{I, T}"/> that implements
        /// <typeparamref name="I"/> and has a public <c>(T, ILogger)</c> constructor.
        /// </param>
        /// <param name="logger">Handed to every decorator created, to receive handler failures.</param>
        /// <param name="sharedSubscriptions">The registry-wide handlers every decorator created also runs.</param>
        /// <param name="handlers">The handlers to start with; null starts with none.</param>
        /// <exception cref="DecoratorException"><paramref name="decoratorType"/> cannot stand in for the service.</exception>
        public DecoratorRegistration(
            Func<I?> previous,
            Type decoratorType,
            ILogger? logger = null,
            DecoratorSubscriptions? sharedSubscriptions = null,
            DecoratorHandlerRegistry<DecoratorInvocationContext<T>>? handlers = null)
        {
            ArgumentNullException.ThrowIfNull(previous);
            ArgumentNullException.ThrowIfNull(decoratorType);

            if (!typeof(Decorator<I, T>).IsAssignableFrom(decoratorType) || !typeof(I).IsAssignableFrom(decoratorType))
            {
                throw new DecoratorException(
                    $"{decoratorType.FullName} cannot decorate {typeof(I).FullName}: a decorator must extend Decorator<{typeof(I).Name}, {typeof(T).Name}> and implement {typeof(I).Name}.");
            }

            _constructor = decoratorType.GetConstructor(new Type[] { typeof(T), typeof(ILogger) })
                ?? throw new DecoratorException($"{decoratorType.FullName} has no public constructor taking ({typeof(T).Name}, ILogger).");
            _previous = previous;
            _decorators = new ConditionalWeakTable<T, Decorator<I, T>>();

            DecoratorType = decoratorType;
            Logger = logger;
            SharedSubscriptions = sharedSubscriptions;
            Handlers = handlers ?? new DecoratorHandlerRegistry<DecoratorInvocationContext<T>>();
        }

        /// <summary>Gets the service interface being decorated.</summary>
        public Type InterfaceType => typeof(I);

        /// <summary>Gets the implementation type being decorated.</summary>
        public Type ImplementationType => typeof(T);

        /// <summary>Gets the type of the decorators this registration creates.</summary>
        public Type DecoratorType { get; }

        /// <summary>Gets the logger handed to every decorator created.</summary>
        public ILogger? Logger { get; }

        /// <summary>Gets the registry-wide handlers every decorator created also runs.</summary>
        public DecoratorSubscriptions? SharedSubscriptions { get; }

        /// <summary>Gets the handlers shared by every decorator this registration creates.</summary>
        public DecoratorHandlerRegistry<DecoratorInvocationContext<T>> Handlers { get; }

        /// <inheritdoc />
        public void Subscribe(DecoratorPhase phase, string methodName, Func<DecoratorInvocationContext<T>, object?> handler)
        {
            Handlers.Add(phase, methodName, handler);
        }

        /// <inheritdoc />
        public void Subscribe(DecoratorPhase phase, string methodName, Action<DecoratorInvocationContext<T>> handler)
        {
            Handlers.Add(phase, methodName, handler);
        }

        /// <summary>
        /// Resolves the registration being decorated and wraps what comes back. The same instance always gets
        /// the same decorator; a new instance gets a new one.
        /// </summary>
        /// <exception cref="DecoratorException">
        /// The previous registration resolved to null, or to something that is neither a
        /// <typeparamref name="T"/> nor already a decorator of one.
        /// </exception>
        public Decorator<I, T> CreateDecorator()
        {
            I resolved = _previous() ?? throw new DecoratorException(
                $"{typeof(I).FullName} resolved to null; there is nothing to decorate.");

            if (resolved is Decorator<I, T> alreadyDecorated)
            {
                // A decorator was registered by hand. Adopt it rather than wrap it twice.
                return Attach(alreadyDecorated);
            }

            if (resolved is not T instance)
            {
                throw new DecoratorException(
                    $"{typeof(I).FullName} is registered as {resolved.GetType().FullName}, not as {typeof(T).FullName}; it cannot be decorated as a {typeof(T).Name}.");
            }

            return _decorators.GetValue(instance, Wrap);
        }

        /// <summary>
        /// Gets a value indicating whether <paramref name="registry"/> still resolves
        /// <typeparamref name="I"/> through this registration. It stops doing so when
        /// <typeparamref name="I"/> is registered again. Nothing is constructed to find out.
        /// </summary>
        /// <param name="registry">The registry to ask.</param>
        public bool IsRegisteredIn(ServiceRegistry registry)
        {
            ArgumentNullException.ThrowIfNull(registry);

            DecoratorRegistration<I, T>? outer = _probe;
            _probe = this;
            try
            {
                return ReferenceEquals(registry[typeof(I)], this);
            }
            catch (Exception)
            {
                // Some other registration answered, and failed doing it. Either way it is not this one.
                return false;
            }
            finally
            {
                _probe = outer;
            }
        }

        /// <summary>
        /// What the registry invokes to resolve <typeparamref name="I"/>. Registered as a factory, so the
        /// registry asks again on every resolve.
        /// </summary>
        internal object Resolve()
        {
            if (ReferenceEquals(_probe, this))
            {
                return this;
            }

            return CreateDecorator();
        }

        private Decorator<I, T> Wrap(T instance)
        {
            return Attach((Decorator<I, T>)_constructor.Invoke(new object?[] { instance, Logger }));
        }

        private Decorator<I, T> Attach(Decorator<I, T> decorator)
        {
            decorator.RegistrationHandlers = Handlers;
            decorator.SharedSubscriptions ??= SharedSubscriptions;
            return decorator;
        }
    }
}
