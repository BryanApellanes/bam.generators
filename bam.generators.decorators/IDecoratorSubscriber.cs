namespace Bam.Generators.Decorators
{
    /// <summary>
    /// Something handlers can be subscribed to: a decorator, or the registration of a decorated service in a
    /// <c>ServiceRegistry</c>.
    /// </summary>
    /// <typeparam name="T">The implementation type being decorated.</typeparam>
    public interface IDecoratorSubscriber<T> where T : class
    {
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
    }
}
