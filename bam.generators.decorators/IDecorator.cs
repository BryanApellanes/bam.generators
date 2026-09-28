namespace Bam.Generators.Decorators
{
    /// <summary>
    /// Wraps an instance of <typeparamref name="T"/> and runs subscribed handlers at the start, end and failure
    /// of every method invoked through it.
    /// </summary>
    /// <typeparam name="T">The implementation type being decorated.</typeparam>
    public interface IDecorator<T> where T : class
    {
        /// <summary>Gets the implementation type being decorated.</summary>
        Type ImplementationType { get; }

        /// <summary>Gets or sets the wrapped instance that invocations are forwarded to.</summary>
        T Instance { get; set; }

        /// <summary>
        /// Gets or sets the registry-wide handlers this decorator also runs, ahead of its own. Null when the
        /// decorator was not created through a <c>ServiceRegistry</c>.
        /// </summary>
        DecoratorSubscriptions? SharedSubscriptions { get; set; }

        /// <summary>Raised after a method returned, or was short-circuited by a start handler.</summary>
        event EventHandler<DecoratorEventArgs<T>>? MethodEnd;

        /// <summary>Raised when a method threw, before the error handlers run.</summary>
        event EventHandler<DecoratorEventArgs<T>>? MethodError;

        /// <summary>Raised when <see cref="Invoke{R}(string, object?[])"/> cannot find the named method.</summary>
        event EventHandler<DecoratorEventArgs<T>>? MethodNotFound;

        /// <summary>Raised before a method is invoked, ahead of the start handlers.</summary>
        event EventHandler<DecoratorEventArgs<T>>? MethodStart;

        /// <summary>
        /// Invokes the named method on <see cref="Instance"/> by reflection, running the subscribed handlers
        /// around it. Never throws for a failing method: the exception is captured on the result.
        /// </summary>
        /// <typeparam name="R">The type of value the method returns.</typeparam>
        /// <param name="methodName">The name of the method to invoke.</param>
        /// <param name="args">The arguments to pass, in declaration order.</param>
        DecoratorInvocationResult<T, R> Invoke<R>(string methodName, params object?[] args);

        /// <summary>
        /// Invokes the named method on <see cref="Instance"/> by reflection and awaits what it returns, running
        /// the end and error handlers once the returned task completes.
        /// </summary>
        /// <typeparam name="R">The type of value the method's task produces.</typeparam>
        /// <param name="methodName">The name of the method to invoke.</param>
        /// <param name="args">The arguments to pass, in declaration order.</param>
        Task<DecoratorInvocationResult<T, R>> InvokeAsync<R>(string methodName, params object?[] args);

        /// <summary>Subscribes <paramref name="handler"/> to <paramref name="methodName"/> for <paramref name="phase"/>.</summary>
        /// <remarks>
        /// A handler that throws does not stop the call: the exception is logged and the call goes ahead. To
        /// stop a call, call <see cref="DecoratorInvocationContext.Reject(string)"/> on the context or throw a
        /// <see cref="DecoratorRejectionException"/>. A handler subscribed to
        /// <see cref="DecoratorPhase.Error"/> that returns a value suppresses the failure.
        /// </remarks>
        /// <param name="phase">The phase the handler runs in.</param>
        /// <param name="methodName">The method to subscribe to, or <c>*</c> for every method.</param>
        /// <param name="handler">The handler. A non-null return value overrides the invocation's result.</param>
        void Subscribe(DecoratorPhase phase, string methodName, Func<DecoratorInvocationContext<T>, object?> handler);

        /// <summary>Subscribes an observe-only <paramref name="handler"/> to <paramref name="methodName"/> for <paramref name="phase"/>.</summary>
        /// <remarks>
        /// A handler that throws does not stop the call: the exception is logged and the call goes ahead. To
        /// stop a call, call <see cref="DecoratorInvocationContext.Reject(string)"/> on the context or throw a
        /// <see cref="DecoratorRejectionException"/>.
        /// </remarks>
        /// <param name="phase">The phase the handler runs in.</param>
        /// <param name="methodName">The method to subscribe to, or <c>*</c> for every method.</param>
        /// <param name="handler">The handler.</param>
        void Subscribe(DecoratorPhase phase, string methodName, Action<DecoratorInvocationContext<T>> handler);

        /// <summary>Subscribes <paramref name="func"/> to run after <paramref name="methodName"/> returns; a non-null return value replaces the result.</summary>
        void SubscribeEnd(string methodName, Func<DecoratorInvocationContext<T>, object?> func);

        /// <summary>Subscribes <paramref name="func"/> to run when <paramref name="methodName"/> throws; a non-null return value is used as a fallback and suppresses the exception.</summary>
        void SubscribeError(string methodName, Func<DecoratorInvocationContext<T>, object?> func);

        /// <summary>Subscribes <paramref name="func"/> to run before <paramref name="methodName"/> is invoked; a non-null return value short-circuits the invocation.</summary>
        /// <remarks>
        /// Throwing from <paramref name="func"/> does not stop the call. To stop it, call
        /// <see cref="DecoratorInvocationContext.Reject(string)"/> or throw a <see cref="DecoratorRejectionException"/>.
        /// </remarks>
        void SubscribeStart(string methodName, Func<DecoratorInvocationContext<T>, object?> func);
    }
}
