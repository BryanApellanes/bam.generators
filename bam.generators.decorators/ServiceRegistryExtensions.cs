using Bam.DependencyInjection;
using Bam.Logging;

namespace Bam.Generators.Decorators
{
    /// <summary>
    /// Decorates services in a <see cref="ServiceRegistry"/> and subscribes handlers to them. Decorating
    /// re-registers the service interface so that resolving it yields a decorator wrapping what the previous
    /// registration resolves.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Decorating keeps the service's lifetime. The previous registration is resolved each time the service
    /// is, so a transient service is still constructed per resolve and a single instance is still one
    /// instance. Nothing is constructed when a service is decorated or a handler subscribed.
    /// </para>
    /// <para>
    /// The service has to be registered before it is decorated or a typed handler is subscribed to it; only
    /// the registry-wide forms may come first. Registering the service again afterward replaces the
    /// decorator. Decorate it again, or subscribe another typed handler, and the handlers subscribed before
    /// are applied to the new registration.
    /// </para>
    /// <para>
    /// A handler that throws does not stop the call: the exception is logged and the call goes ahead. To stop
    /// a call — an authorization or validation check — the handler calls
    /// <see cref="DecoratorInvocationContext.Reject(string)"/> or throws a
    /// <see cref="DecoratorRejectionException"/>. An error handler that returns a value suppresses the failure.
    /// </para>
    /// <para>
    /// A registry-wide handler sees the arguments of every decorated call it matches, and one that returns a
    /// value changes the result of every matching method whose return type fits. Subscribe those by method
    /// name; keep <c>*</c> for handlers that only observe.
    /// </para>
    /// </remarks>
    public static class ServiceRegistryExtensions
    {
        /// <summary>
        /// Decorates the <typeparamref name="I"/> registered in <paramref name="registry"/>. Uses a generated
        /// decorator for the pair when one is loaded, and otherwise generates and compiles one on the spot
        /// (see <see cref="IDecoratorTypeResolver"/>). Decorating an already decorated service returns the
        /// existing registration, so its handlers are kept.
        /// </summary>
        /// <typeparam name="I">The service interface to decorate.</typeparam>
        /// <typeparam name="T">The implementation type registered for <typeparamref name="I"/>.</typeparam>
        /// <param name="registry">The registry holding the registration.</param>
        /// <param name="logger">Receives handler failures. Defaults to the registry's <see cref="ILogger"/>, then <see cref="Log.Default"/>.</param>
        /// <returns>The registration now standing in for <typeparamref name="I"/>, to subscribe handlers to.</returns>
        /// <exception cref="DecoratorException"><typeparamref name="I"/> is not registered.</exception>
        /// <exception cref="DecoratorGenerationException">No decorator exists for the pair and one could not be compiled.</exception>
        public static DecoratorRegistration<I, T> Decorate<I, T>(this ServiceRegistry registry, ILogger? logger = null) where I : class where T : class, I
        {
            ArgumentNullException.ThrowIfNull(registry);

            return Decorate<I, T>(registry, logger, () => GetDecoratorTypeResolver(registry).Resolve(typeof(I), typeof(T)));
        }

        /// <summary>
        /// Decorates the <typeparamref name="I"/> registered in <paramref name="registry"/> with
        /// <typeparamref name="TDecorator"/>. Generated code calls this with its own decorator type. Decorating
        /// an already decorated service returns the existing registration, whatever its decorator type.
        /// </summary>
        /// <typeparam name="I">The service interface to decorate.</typeparam>
        /// <typeparam name="T">The implementation type registered for <typeparamref name="I"/>.</typeparam>
        /// <typeparam name="TDecorator">The decorator type to wrap the service in.</typeparam>
        /// <param name="registry">The registry holding the registration.</param>
        /// <param name="logger">Receives handler failures. Defaults to the registry's <see cref="ILogger"/>, then <see cref="Log.Default"/>.</param>
        /// <returns>The registration now standing in for <typeparamref name="I"/>, to subscribe handlers to.</returns>
        /// <exception cref="DecoratorException"><typeparamref name="I"/> is not registered.</exception>
        public static DecoratorRegistration<I, T> Decorate<I, T, TDecorator>(this ServiceRegistry registry, ILogger? logger = null)
            where I : class
            where T : class, I
            where TDecorator : Decorator<I, T>, I
        {
            ArgumentNullException.ThrowIfNull(registry);

            return Decorate<I, T>(registry, logger, () => typeof(TDecorator));
        }

        /// <summary>
        /// Gets the registry-wide handler store of <paramref name="registry"/>, creating it on first use.
        /// </summary>
        public static DecoratorSubscriptions GetDecoratorSubscriptions(this ServiceRegistry registry)
        {
            ArgumentNullException.ThrowIfNull(registry);

            return registry.Get<DecoratorSubscriptions>();
        }

        /// <summary>
        /// Gets the record of the services decorated in <paramref name="registry"/>, creating it on first use.
        /// </summary>
        public static DecoratorRegistrations GetDecoratorRegistrations(this ServiceRegistry registry)
        {
            ArgumentNullException.ThrowIfNull(registry);

            return registry.Get<DecoratorRegistrations>();
        }

        /// <summary>
        /// Gets the <see cref="IDecoratorTypeResolver"/> registered in <paramref name="registry"/>, or
        /// <see cref="DecoratorTypeResolver.Default"/> when none is.
        /// </summary>
        public static IDecoratorTypeResolver GetDecoratorTypeResolver(this ServiceRegistry registry)
        {
            ArgumentNullException.ThrowIfNull(registry);

            if (registry.MappedTypes.Contains(typeof(IDecoratorTypeResolver)) && registry.TryGet(out IDecoratorTypeResolver resolver) && resolver != null)
            {
                return resolver;
            }

            return DecoratorTypeResolver.Default;
        }

        /// <summary>Decorates <typeparamref name="I"/> if needed and subscribes <paramref name="func"/> to run before <paramref name="methodName"/>; a non-null return value short-circuits the invocation.</summary>
        /// <returns><paramref name="registry"/>, for chaining.</returns>
        public static ServiceRegistry SubscribeStart<I, T>(this ServiceRegistry registry, string methodName, Func<DecoratorInvocationContext<T>, object?> func, ILogger? logger = null) where I : class where T : class, I
        {
            registry.Decorate<I, T>(logger).Subscribe(DecoratorPhase.Start, methodName, func);
            return registry;
        }

        /// <summary>Decorates <typeparamref name="I"/> if needed and subscribes <paramref name="func"/> to run after <paramref name="methodName"/> returns; a non-null return value replaces the result.</summary>
        /// <returns><paramref name="registry"/>, for chaining.</returns>
        public static ServiceRegistry SubscribeEnd<I, T>(this ServiceRegistry registry, string methodName, Func<DecoratorInvocationContext<T>, object?> func, ILogger? logger = null) where I : class where T : class, I
        {
            registry.Decorate<I, T>(logger).Subscribe(DecoratorPhase.End, methodName, func);
            return registry;
        }

        /// <summary>Decorates <typeparamref name="I"/> if needed and subscribes <paramref name="func"/> to run when <paramref name="methodName"/> throws; a non-null return value is used as a fallback and suppresses the exception.</summary>
        /// <returns><paramref name="registry"/>, for chaining.</returns>
        public static ServiceRegistry SubscribeError<I, T>(this ServiceRegistry registry, string methodName, Func<DecoratorInvocationContext<T>, object?> func, ILogger? logger = null) where I : class where T : class, I
        {
            registry.Decorate<I, T>(logger).Subscribe(DecoratorPhase.Error, methodName, func);
            return registry;
        }

        /// <summary>Decorates <typeparamref name="I"/> if needed and subscribes an observe-only <paramref name="handler"/> to run before <paramref name="methodName"/> is invoked.</summary>
        /// <returns><paramref name="registry"/>, for chaining.</returns>
        public static ServiceRegistry OnMethodStart<I, T>(this ServiceRegistry registry, string methodName, Action<DecoratorInvocationContext<T>> handler) where I : class where T : class, I
        {
            registry.Decorate<I, T>().Subscribe(DecoratorPhase.Start, methodName, handler);
            return registry;
        }

        /// <summary>Decorates <typeparamref name="I"/> if needed and subscribes <paramref name="handler"/> to run before <paramref name="methodName"/> is invoked; a non-null return value short-circuits the invocation.</summary>
        /// <returns><paramref name="registry"/>, for chaining.</returns>
        public static ServiceRegistry OnMethodStart<I, T>(this ServiceRegistry registry, string methodName, Func<DecoratorInvocationContext<T>, object?> handler) where I : class where T : class, I
        {
            registry.Decorate<I, T>().Subscribe(DecoratorPhase.Start, methodName, handler);
            return registry;
        }

        /// <summary>Decorates <typeparamref name="I"/> if needed and subscribes an observe-only <paramref name="handler"/> to run after <paramref name="methodName"/> returns.</summary>
        /// <returns><paramref name="registry"/>, for chaining.</returns>
        public static ServiceRegistry OnMethodEnd<I, T>(this ServiceRegistry registry, string methodName, Action<DecoratorInvocationContext<T>> handler) where I : class where T : class, I
        {
            registry.Decorate<I, T>().Subscribe(DecoratorPhase.End, methodName, handler);
            return registry;
        }

        /// <summary>Decorates <typeparamref name="I"/> if needed and subscribes <paramref name="handler"/> to run after <paramref name="methodName"/> returns; a non-null return value replaces the result.</summary>
        /// <returns><paramref name="registry"/>, for chaining.</returns>
        public static ServiceRegistry OnMethodEnd<I, T>(this ServiceRegistry registry, string methodName, Func<DecoratorInvocationContext<T>, object?> handler) where I : class where T : class, I
        {
            registry.Decorate<I, T>().Subscribe(DecoratorPhase.End, methodName, handler);
            return registry;
        }

        /// <summary>Decorates <typeparamref name="I"/> if needed and subscribes an observe-only <paramref name="handler"/> to run when <paramref name="methodName"/> throws.</summary>
        /// <returns><paramref name="registry"/>, for chaining.</returns>
        public static ServiceRegistry OnMethodError<I, T>(this ServiceRegistry registry, string methodName, Action<DecoratorInvocationContext<T>> handler) where I : class where T : class, I
        {
            registry.Decorate<I, T>().Subscribe(DecoratorPhase.Error, methodName, handler);
            return registry;
        }

        /// <summary>Decorates <typeparamref name="I"/> if needed and subscribes <paramref name="handler"/> to run when <paramref name="methodName"/> throws; a non-null return value is used as a fallback and suppresses the exception.</summary>
        /// <returns><paramref name="registry"/>, for chaining.</returns>
        public static ServiceRegistry OnMethodError<I, T>(this ServiceRegistry registry, string methodName, Func<DecoratorInvocationContext<T>, object?> handler) where I : class where T : class, I
        {
            registry.Decorate<I, T>().Subscribe(DecoratorPhase.Error, methodName, handler);
            return registry;
        }

        /// <summary>
        /// Subscribes an observe-only <paramref name="handler"/> to run before <paramref name="methodName"/> is
        /// invoked on any decorated service in <paramref name="registry"/>. Decorates nothing by itself, so it
        /// may be called before the services it applies to are registered.
        /// </summary>
        /// <param name="registry">The registry whose decorated services the handler applies to.</param>
        /// <param name="methodName">The method name to match, or <c>*</c> for every method.</param>
        /// <param name="handler">The handler.</param>
        /// <returns><paramref name="registry"/>, for chaining.</returns>
        public static ServiceRegistry OnMethodStart(this ServiceRegistry registry, string methodName, Action<DecoratorInvocationContext> handler)
        {
            registry.GetDecoratorSubscriptions().Add(DecoratorPhase.Start, methodName, handler);
            return registry;
        }

        /// <summary>
        /// Subscribes <paramref name="handler"/> to run before <paramref name="methodName"/> is invoked on any
        /// decorated service in <paramref name="registry"/>. A non-null return value short-circuits the invocation.
        /// </summary>
        /// <param name="registry">The registry whose decorated services the handler applies to.</param>
        /// <param name="methodName">The method name to match, or <c>*</c> for every method.</param>
        /// <param name="handler">The handler.</param>
        /// <returns><paramref name="registry"/>, for chaining.</returns>
        public static ServiceRegistry OnMethodStart(this ServiceRegistry registry, string methodName, Func<DecoratorInvocationContext, object?> handler)
        {
            registry.GetDecoratorSubscriptions().Add(DecoratorPhase.Start, methodName, handler);
            return registry;
        }

        /// <summary>
        /// Subscribes an observe-only <paramref name="handler"/> to run after <paramref name="methodName"/>
        /// returns on any decorated service in <paramref name="registry"/>.
        /// </summary>
        /// <param name="registry">The registry whose decorated services the handler applies to.</param>
        /// <param name="methodName">The method name to match, or <c>*</c> for every method.</param>
        /// <param name="handler">The handler.</param>
        /// <returns><paramref name="registry"/>, for chaining.</returns>
        public static ServiceRegistry OnMethodEnd(this ServiceRegistry registry, string methodName, Action<DecoratorInvocationContext> handler)
        {
            registry.GetDecoratorSubscriptions().Add(DecoratorPhase.End, methodName, handler);
            return registry;
        }

        /// <summary>
        /// Subscribes <paramref name="handler"/> to run after <paramref name="methodName"/> returns on any
        /// decorated service in <paramref name="registry"/>. A non-null return value replaces the result.
        /// </summary>
        /// <param name="registry">The registry whose decorated services the handler applies to.</param>
        /// <param name="methodName">The method name to match, or <c>*</c> for every method.</param>
        /// <param name="handler">The handler.</param>
        /// <returns><paramref name="registry"/>, for chaining.</returns>
        public static ServiceRegistry OnMethodEnd(this ServiceRegistry registry, string methodName, Func<DecoratorInvocationContext, object?> handler)
        {
            registry.GetDecoratorSubscriptions().Add(DecoratorPhase.End, methodName, handler);
            return registry;
        }

        /// <summary>
        /// Subscribes an observe-only <paramref name="handler"/> to run when <paramref name="methodName"/>
        /// throws on any decorated service in <paramref name="registry"/>.
        /// </summary>
        /// <param name="registry">The registry whose decorated services the handler applies to.</param>
        /// <param name="methodName">The method name to match, or <c>*</c> for every method.</param>
        /// <param name="handler">The handler.</param>
        /// <returns><paramref name="registry"/>, for chaining.</returns>
        public static ServiceRegistry OnMethodError(this ServiceRegistry registry, string methodName, Action<DecoratorInvocationContext> handler)
        {
            registry.GetDecoratorSubscriptions().Add(DecoratorPhase.Error, methodName, handler);
            return registry;
        }

        /// <summary>
        /// Subscribes <paramref name="handler"/> to run when <paramref name="methodName"/> throws on any
        /// decorated service in <paramref name="registry"/>. A non-null return value is used as a fallback and
        /// suppresses the exception.
        /// </summary>
        /// <param name="registry">The registry whose decorated services the handler applies to.</param>
        /// <param name="methodName">The method name to match, or <c>*</c> for every method.</param>
        /// <param name="handler">The handler.</param>
        /// <returns><paramref name="registry"/>, for chaining.</returns>
        public static ServiceRegistry OnMethodError(this ServiceRegistry registry, string methodName, Func<DecoratorInvocationContext, object?> handler)
        {
            registry.GetDecoratorSubscriptions().Add(DecoratorPhase.Error, methodName, handler);
            return registry;
        }

        private static DecoratorRegistration<I, T> Decorate<I, T>(ServiceRegistry registry, ILogger? logger, Func<Type> decoratorTypeProvider) where I : class where T : class, I
        {
            DecoratorRegistrations registrations = registry.GetDecoratorRegistrations();
            bool decoratedBefore = registrations.TryGet(out DecoratorRegistration<I, T>? existing);
            if (decoratedBefore && existing!.IsRegisteredIn(registry))
            {
                return existing;
            }

            if (!registry.MappedTypes.Contains(typeof(I)))
            {
                throw new DecoratorException(
                    $"{typeof(I).FullName} is not registered. Register it before decorating it.");
            }

            // Keep hold of the registration as it stands, without resolving it, so it can be resolved each time
            // the service is. Decorating must not construct the service or change how often it is constructed.
            DependencyProvider previous = new DependencyProvider();
            previous.CopyTypeFrom(typeof(I), registry);

            DecoratorRegistration<I, T> registration = new DecoratorRegistration<I, T>(
                () => previous[typeof(I)] as I,
                decoratorTypeProvider(),
                logger ?? ResolveLogger(registry),
                registry.GetDecoratorSubscriptions(),
                decoratedBefore ? existing!.Handlers : null);

            registry.Set(typeof(I), new Func<object>(registration.Resolve));
            registrations.Set(registration);
            return registration;
        }

        private static ILogger? ResolveLogger(ServiceRegistry registry)
        {
            if (!registry.MappedTypes.Contains(typeof(ILogger)))
            {
                return null;
            }

            return registry.TryGet(out ILogger logger) ? logger : null;
        }
    }
}
