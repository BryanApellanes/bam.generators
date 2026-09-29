namespace Bam.Generators.Decorators
{
    /// <summary>
    /// Wraps an instance of <typeparamref name="T"/> and runs subscribed handlers at the start, end and failure
    /// of every method invoked through it.
    /// </summary>
    /// <typeparam name="T">The implementation type being decorated.</typeparam>
    public interface IDecorator<T> : IDecoratorSubscriber<T> where T : class
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

        /// <summary>Raised when a method threw or a handler rejected the call.</summary>
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
