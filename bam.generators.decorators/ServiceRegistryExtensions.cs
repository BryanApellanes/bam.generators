using Bam.DependencyInjection;
using Bam.Logging;
using System.Reflection;

namespace Bam.Generators.Decorators
{
    /// <summary>
    /// Decorates services in a <see cref="ServiceRegistry"/> and subscribes handlers to them. Decorating
    /// re-registers the service interface so that resolving it yields a decorator wrapping the previous
    /// registration.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Decorating resolves the current registration once and registers the decorator as an instance, so a
    /// service registered as transient resolves to the same decorated instance from then on.
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
        /// existing decorator, so its subscriptions are kept.
        /// </summary>
        /// <typeparam name="I">The service interface to decorate.</typeparam>
        /// <typeparam name="T">The implementation type currently registered for <typeparamref name="I"/>.</typeparam>
        /// <param name="registry">The registry holding the registration.</param>
        /// <param name="logger">Receives handler failures. Defaults to the registry's <see cref="ILogger"/>, then <see cref="Log.Default"/>.</param>
        /// <returns>The decorator now registered for <typeparamref name="I"/>.</returns>
        /// <exception cref="DecoratorException"><typeparamref name="I"/> is not registered, or is registered as something other than a <typeparamref name="T"/>.</exception>
        public static Decorator<I, T> Decorate<I, T>(this ServiceRegistry registry, ILogger? logger = null) where I : class where T : class, I
        {
            ArgumentNullException.ThrowIfNull(registry);

            return Decorate<I, T>(registry, logger, () => GetDecoratorTypeResolver(registry).Resolve(typeof(I), typeof(T)));
        }

        /// <summary>
        /// Decorates the <typeparamref name="I"/> registered in <paramref name="registry"/> with a
        /// <typeparamref name="TDecorator"/>. Generated code calls this with its own decorator type. Decorating
        /// an already decorated service returns the existing decorator, whatever its type.
        /// </summary>
        /// <typeparam name="I">The service interface to decorate.</typeparam>
        /// <typeparam name="T">The implementation type currently registered for <typeparamref name="I"/>.</typeparam>
        /// <typeparam name="TDecorator">The decorator type to wrap the registration in.</typeparam>
        /// <param name="registry">The registry holding the registration.</param>
        /// <param name="logger">Receives handler failures. Defaults to the registry's <see cref="ILogger"/>, then <see cref="Log.Default"/>.</param>
        /// <returns>The decorator now registered for <typeparamref name="I"/>.</returns>
        /// <exception cref="DecoratorException"><typeparamref name="I"/> is not registered, or is registered as something other than a <typeparamref name="T"/>.</exception>
        public static Decorator<I, T> Decorate<I, T, TDecorator>(this ServiceRegistry registry, ILogger? logger = null)
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
        /// Gets the <see cref="IDecoratorTypeResolver"/> registered in <paramref name="registry"/>, or
        /// <see cref="DecoratorTypeResolver.Default"/> when none is.
        /// </summary>
        public static IDecoratorTypeResolver GetDecoratorTypeResolver(this ServiceRegistry registry)
        {
            ArgumentNullException.ThrowIfNull(registry);

            if (registry.TryGet(out IDecoratorTypeResolver resolver) && resolver != null)
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

        private static Decorator<I, T> Decorate<I, T>(ServiceRegistry registry, ILogger? logger, Func<Type> decoratorTypeProvider) where I : class where T : class, I
        {
            I current = Resolve<I>(registry);
            if (current is Decorator<I, T> existing)
            {
                return existing;
            }

            if (current is not T instance)
            {
                throw new DecoratorException(
                    $"{typeof(I).FullName} is registered as {current.GetType().FullName}, not as {typeof(T).FullName}; it cannot be decorated as a {typeof(T).Name}.");
            }

            Decorator<I, T> decorator = Construct<I, T>(decoratorTypeProvider(), instance, logger ?? ResolveLogger(registry));
            decorator.SharedSubscriptions = registry.GetDecoratorSubscriptions();
            registry.For<I>().Use(decorator);
            return decorator;
        }

        private static I Resolve<I>(ServiceRegistry registry) where I : class
        {
            I? current;
            try
            {
                current = registry.Get<I>();
            }
            catch (Exception ex)
            {
                throw new DecoratorException(
                    $"{typeof(I).FullName} could not be resolved from the registry. Register it before decorating it.", ex);
            }

            return current ?? throw new DecoratorException(
                $"{typeof(I).FullName} resolved to null. Register it before decorating it.");
        }

        private static Decorator<I, T> Construct<I, T>(Type decoratorType, T instance, ILogger? logger) where I : class where T : class, I
        {
            if (!typeof(Decorator<I, T>).IsAssignableFrom(decoratorType) || !typeof(I).IsAssignableFrom(decoratorType))
            {
                throw new DecoratorException(
                    $"{decoratorType.FullName} cannot decorate {typeof(I).FullName}: a decorator must extend Decorator<{typeof(I).Name}, {typeof(T).Name}> and implement {typeof(I).Name}.");
            }

            ConstructorInfo? constructor = decoratorType.GetConstructor(new Type[] { typeof(T), typeof(ILogger) });
            if (constructor == null)
            {
                throw new DecoratorException(
                    $"{decoratorType.FullName} has no public constructor taking ({typeof(T).Name}, ILogger).");
            }

            return (Decorator<I, T>)constructor.Invoke(new object?[] { instance, logger });
        }

        private static ILogger? ResolveLogger(ServiceRegistry registry)
        {
            return registry.TryGet(out ILogger logger) ? logger : null;
        }
    }
}
