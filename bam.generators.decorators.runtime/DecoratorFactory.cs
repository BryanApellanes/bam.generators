using System.Collections.Concurrent;
using System.Reflection;

namespace Bam.Generators.Decorators
{
    /// <summary>
    /// Creates generated decorators without a <c>ServiceRegistry</c>, for hosts on another container. The
    /// container calls <see cref="Create{I, T}(T, IDecoratorLogger?)"/> from its own decoration hook
    /// (SimpleInjector's <c>ExpressionBuilt</c> event, <c>Decorate</c> in Microsoft.Extensions.DependencyInjection
    /// with Scrutor, or a factory lambda), and subscribes handlers once per service type through
    /// <see cref="Handlers{I, T}"/>: every decorator the factory creates for that pair runs them, before the
    /// handlers subscribed to the decorator itself. The store is the process's, not any container's: a host
    /// that is torn down and rebuilt in one process (integration tests) calls <see cref="Reset{I, T}"/>, or
    /// the store's own <c>Remove</c>, so handlers do not accumulate across hosts.
    /// </summary>
    public static class DecoratorFactory
    {
        private static readonly ConcurrentDictionary<PairKey, Type> _types = new ConcurrentDictionary<PairKey, Type>();

        /// <summary>
        /// Gets the handler store shared by every decorator created for <typeparamref name="I"/> as
        /// <typeparamref name="T"/>. One per pair, for the life of the process; see <see cref="Reset{I, T}"/>.
        /// </summary>
        public static DecoratorHandlerRegistry<DecoratorInvocationContext<T>> Handlers<I, T>() where I : class where T : class, I
        {
            return Store<I, T>.Handlers;
        }

        /// <summary>
        /// Empties the handler store shared by the pair. Decorators the factory already created for the pair
        /// stop running the shared handlers at their next call, and keep their own. Call it when a host is
        /// torn down, so a host built afterwards in the same process starts from an empty store.
        /// </summary>
        public static void Reset<I, T>() where I : class where T : class, I
        {
            Store<I, T>.Handlers.Clear();
        }

        /// <summary>
        /// Wraps <paramref name="instance"/> in the decorator generated for the pair, attaching the shared
        /// store, and returns it as the service interface, the type a container's decoration hook hands back.
        /// The decorator is found among the loaded assemblies (<see cref="GeneratedDecorators.Find"/>) and
        /// remembered; nothing is compiled. The generated hooks are extension methods on <typeparamref name="I"/>,
        /// so they apply to the result as returned; the decorator itself is one cast away
        /// (<c>(Decorator&lt;I, T&gt;)(object)service</c>, or <see cref="DecoratorExtensions.GetDecorator{T}"/>).
        /// </summary>
        /// <param name="instance">The instance to decorate.</param>
        /// <param name="logger">Receives handler failures. Defaults to <see cref="DecoratorLogger.Default"/>.</param>
        /// <returns>The decorator, as <typeparamref name="I"/>.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="instance"/> is null.</exception>
        /// <exception cref="DecoratorException">No generated decorator for the pair is loaded.</exception>
        public static I Create<I, T>(T instance, IDecoratorLogger? logger = null) where I : class where T : class, I
        {
            Guard.NotNull(instance, nameof(instance));

            Type decoratorType = _types.GetOrAdd(new PairKey(typeof(I), typeof(T)), key =>
                GeneratedDecorators.Find(key.InterfaceType, key.ImplementationType)
                ?? throw new DecoratorException(
                    $"No generated decorator for {typeof(I).FullName} as {typeof(T).FullName} is loaded. Generate one with DecoratorGenerator, or name the decorator type with Create<I, T, TDecorator>."));

            return (I)(object)Attach<I, T>((Decorator<I, T>)Construct(decoratorType, typeof(T), instance, logger));
        }

        /// <summary>
        /// Wraps <paramref name="instance"/> in <typeparamref name="TDecorator"/>, attaching the shared store
        /// for the pair. Use this when the decorator type is known at compile time.
        /// </summary>
        /// <param name="instance">The instance to decorate.</param>
        /// <param name="logger">Receives handler failures. Defaults to <see cref="DecoratorLogger.Default"/>.</param>
        /// <exception cref="ArgumentNullException"><paramref name="instance"/> is null.</exception>
        /// <exception cref="DecoratorException"><typeparamref name="TDecorator"/> has no <c>(T, IDecoratorLogger)</c> constructor.</exception>
        public static TDecorator Create<I, T, TDecorator>(T instance, IDecoratorLogger? logger = null)
            where I : class
            where T : class, I
            where TDecorator : Decorator<I, T>, I
        {
            Guard.NotNull(instance, nameof(instance));

            return (TDecorator)Attach<I, T>((TDecorator)Construct(typeof(TDecorator), typeof(T), instance, logger));
        }

        private static object Construct(Type decoratorType, Type implementationType, object instance, IDecoratorLogger? logger)
        {
            ConstructorInfo constructor = decoratorType.GetConstructor(new Type[] { implementationType, typeof(IDecoratorLogger) })
                ?? throw new DecoratorException($"{decoratorType.FullName} has no public constructor taking ({implementationType.Name}, IDecoratorLogger).");

            try
            {
                return constructor.Invoke(new object?[] { instance, logger });
            }
            catch (TargetInvocationException ex) when (ex.InnerException != null)
            {
                throw new DecoratorException($"{decoratorType.FullName} could not be constructed: {ex.InnerException.Message}", ex.InnerException);
            }
        }

        private static Decorator<I, T> Attach<I, T>(Decorator<I, T> decorator) where I : class where T : class, I
        {
            decorator.AttachRegistrationHandlers(Store<I, T>.Handlers);
            return decorator;
        }

        // A static generic class is one instance per closed (I, T): the per-pair store for free.
        private static class Store<I, T> where I : class where T : class, I
        {
            public static readonly DecoratorHandlerRegistry<DecoratorInvocationContext<T>> Handlers = new DecoratorHandlerRegistry<DecoratorInvocationContext<T>>();
        }

        private readonly record struct PairKey(Type InterfaceType, Type ImplementationType);
    }
}
