namespace Bam.Generators.Decorators
{
    /// <summary>
    /// Subscribes handlers to a decorator by method name — either on the decorator itself or on a service
    /// instance that is one. These work without generated code; a generated decorator adds typed per-method
    /// equivalents (<c>OnMessageStart</c>, ...) on top of them.
    /// </summary>
    public static class DecoratorExtensions
    {
        /// <summary>
        /// Gets the decorator behind <paramref name="service"/>.
        /// </summary>
        /// <typeparam name="T">The implementation type the decorator wraps.</typeparam>
        /// <param name="service">A service instance expected to be a decorator.</param>
        /// <exception cref="DecoratorException"><paramref name="service"/> is not a decorator of <typeparamref name="T"/>.</exception>
        public static IDecorator<T> GetDecorator<T>(object service) where T : class
        {
            ArgumentNullException.ThrowIfNull(service);

            if (service is IDecorator<T> decorator)
            {
                return decorator;
            }

            throw new DecoratorException(
                $"The {service.GetType().FullName} instance is not decorated: it is not an IDecorator<{typeof(T).Name}>. Decorate the service in its ServiceRegistry before subscribing to it.");
        }

        /// <summary>Gets a value indicating whether <paramref name="service"/> is a decorator of <typeparamref name="T"/>.</summary>
        /// <typeparam name="T">The implementation type the decorator wraps.</typeparam>
        /// <param name="service">The service instance to test.</param>
        public static bool IsDecorated<T>(object? service) where T : class
        {
            return service is IDecorator<T>;
        }

        /// <summary>Subscribes an observe-only <paramref name="handler"/> to run before <paramref name="methodName"/> is invoked.</summary>
        /// <returns><paramref name="decorator"/>, for chaining.</returns>
        public static IDecorator<T> OnMethodStart<T>(this IDecorator<T> decorator, string methodName, Action<DecoratorInvocationContext<T>> handler) where T : class
        {
            return Subscribe(decorator, DecoratorPhase.Start, methodName, handler);
        }

        /// <summary>
        /// Subscribes <paramref name="handler"/> to run before <paramref name="methodName"/> is invoked. A
        /// non-null return value short-circuits the invocation and is handed to the caller instead.
        /// </summary>
        /// <returns><paramref name="decorator"/>, for chaining.</returns>
        public static IDecorator<T> OnMethodStart<T>(this IDecorator<T> decorator, string methodName, Func<DecoratorInvocationContext<T>, object?> handler) where T : class
        {
            return Subscribe(decorator, DecoratorPhase.Start, methodName, handler);
        }

        /// <summary>Subscribes an observe-only <paramref name="handler"/> to run after <paramref name="methodName"/> returns.</summary>
        /// <returns><paramref name="decorator"/>, for chaining.</returns>
        public static IDecorator<T> OnMethodEnd<T>(this IDecorator<T> decorator, string methodName, Action<DecoratorInvocationContext<T>> handler) where T : class
        {
            return Subscribe(decorator, DecoratorPhase.End, methodName, handler);
        }

        /// <summary>
        /// Subscribes <paramref name="handler"/> to run after <paramref name="methodName"/> returns. A non-null
        /// return value replaces the value handed to the caller.
        /// </summary>
        /// <returns><paramref name="decorator"/>, for chaining.</returns>
        public static IDecorator<T> OnMethodEnd<T>(this IDecorator<T> decorator, string methodName, Func<DecoratorInvocationContext<T>, object?> handler) where T : class
        {
            return Subscribe(decorator, DecoratorPhase.End, methodName, handler);
        }

        /// <summary>Subscribes an observe-only <paramref name="handler"/> to run when <paramref name="methodName"/> throws.</summary>
        /// <returns><paramref name="decorator"/>, for chaining.</returns>
        public static IDecorator<T> OnMethodError<T>(this IDecorator<T> decorator, string methodName, Action<DecoratorInvocationContext<T>> handler) where T : class
        {
            return Subscribe(decorator, DecoratorPhase.Error, methodName, handler);
        }

        /// <summary>
        /// Subscribes <paramref name="handler"/> to run when <paramref name="methodName"/> throws. A non-null
        /// return value is handed to the caller as a fallback and the exception is suppressed.
        /// </summary>
        /// <returns><paramref name="decorator"/>, for chaining.</returns>
        public static IDecorator<T> OnMethodError<T>(this IDecorator<T> decorator, string methodName, Func<DecoratorInvocationContext<T>, object?> handler) where T : class
        {
            return Subscribe(decorator, DecoratorPhase.Error, methodName, handler);
        }

        /// <summary>Subscribes an observe-only <paramref name="handler"/> to run before <paramref name="methodName"/> is invoked on the decorated <paramref name="service"/>.</summary>
        /// <returns><paramref name="service"/>, for chaining.</returns>
        /// <exception cref="DecoratorException"><paramref name="service"/> is not decorated.</exception>
        public static I OnMethodStart<I, T>(this I service, string methodName, Action<DecoratorInvocationContext<T>> handler) where I : class where T : class, I
        {
            Subscribe(GetDecorator<T>(service), DecoratorPhase.Start, methodName, handler);
            return service;
        }

        /// <summary>
        /// Subscribes <paramref name="handler"/> to run before <paramref name="methodName"/> is invoked on the
        /// decorated <paramref name="service"/>. A non-null return value short-circuits the invocation.
        /// </summary>
        /// <returns><paramref name="service"/>, for chaining.</returns>
        /// <exception cref="DecoratorException"><paramref name="service"/> is not decorated.</exception>
        public static I OnMethodStart<I, T>(this I service, string methodName, Func<DecoratorInvocationContext<T>, object?> handler) where I : class where T : class, I
        {
            Subscribe(GetDecorator<T>(service), DecoratorPhase.Start, methodName, handler);
            return service;
        }

        /// <summary>Subscribes an observe-only <paramref name="handler"/> to run after <paramref name="methodName"/> returns on the decorated <paramref name="service"/>.</summary>
        /// <returns><paramref name="service"/>, for chaining.</returns>
        /// <exception cref="DecoratorException"><paramref name="service"/> is not decorated.</exception>
        public static I OnMethodEnd<I, T>(this I service, string methodName, Action<DecoratorInvocationContext<T>> handler) where I : class where T : class, I
        {
            Subscribe(GetDecorator<T>(service), DecoratorPhase.End, methodName, handler);
            return service;
        }

        /// <summary>
        /// Subscribes <paramref name="handler"/> to run after <paramref name="methodName"/> returns on the
        /// decorated <paramref name="service"/>. A non-null return value replaces the value handed to the caller.
        /// </summary>
        /// <returns><paramref name="service"/>, for chaining.</returns>
        /// <exception cref="DecoratorException"><paramref name="service"/> is not decorated.</exception>
        public static I OnMethodEnd<I, T>(this I service, string methodName, Func<DecoratorInvocationContext<T>, object?> handler) where I : class where T : class, I
        {
            Subscribe(GetDecorator<T>(service), DecoratorPhase.End, methodName, handler);
            return service;
        }

        /// <summary>Subscribes an observe-only <paramref name="handler"/> to run when <paramref name="methodName"/> throws on the decorated <paramref name="service"/>.</summary>
        /// <returns><paramref name="service"/>, for chaining.</returns>
        /// <exception cref="DecoratorException"><paramref name="service"/> is not decorated.</exception>
        public static I OnMethodError<I, T>(this I service, string methodName, Action<DecoratorInvocationContext<T>> handler) where I : class where T : class, I
        {
            Subscribe(GetDecorator<T>(service), DecoratorPhase.Error, methodName, handler);
            return service;
        }

        /// <summary>
        /// Subscribes <paramref name="handler"/> to run when <paramref name="methodName"/> throws on the decorated
        /// <paramref name="service"/>. A non-null return value is used as a fallback and the exception is suppressed.
        /// </summary>
        /// <returns><paramref name="service"/>, for chaining.</returns>
        /// <exception cref="DecoratorException"><paramref name="service"/> is not decorated.</exception>
        public static I OnMethodError<I, T>(this I service, string methodName, Func<DecoratorInvocationContext<T>, object?> handler) where I : class where T : class, I
        {
            Subscribe(GetDecorator<T>(service), DecoratorPhase.Error, methodName, handler);
            return service;
        }

        private static IDecorator<T> Subscribe<T>(IDecorator<T> decorator, DecoratorPhase phase, string methodName, Action<DecoratorInvocationContext<T>> handler) where T : class
        {
            ArgumentNullException.ThrowIfNull(decorator);

            decorator.Subscribe(phase, methodName, handler);
            return decorator;
        }

        private static IDecorator<T> Subscribe<T>(IDecorator<T> decorator, DecoratorPhase phase, string methodName, Func<DecoratorInvocationContext<T>, object?> handler) where T : class
        {
            ArgumentNullException.ThrowIfNull(decorator);

            decorator.Subscribe(phase, methodName, handler);
            return decorator;
        }
    }
}
