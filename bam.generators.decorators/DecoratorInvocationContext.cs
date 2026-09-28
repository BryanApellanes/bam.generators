using System.Reflection;

namespace Bam.Generators.Decorators
{
    /// <summary>
    /// Describes one decorated method invocation to the handlers subscribed to it. This non-generic form is what
    /// registry-wide handlers receive, since they fire for any decorated service regardless of its type; typed
    /// handlers receive <see cref="DecoratorInvocationContext{T}"/>.
    /// </summary>
    public class DecoratorInvocationContext
    {
        private object? _result;
        private string? _methodName;

        /// <summary>Initializes a context for an invocation on <paramref name="decoratedObject"/>.</summary>
        /// <param name="decoratedObject">The instance the decorator wraps.</param>
        /// <param name="implementationType">The implementation type the decorator was declared for.</param>
        public DecoratorInvocationContext(object decoratedObject, Type implementationType)
        {
            DecoratedObject = decoratedObject;
            ImplementationType = implementationType;
            Args = Array.Empty<object?>();
        }

        /// <summary>Gets the instance the decorator wraps, untyped.</summary>
        public object DecoratedObject { get; }

        /// <summary>Gets the implementation type the decorator was declared for.</summary>
        public Type ImplementationType { get; }

        /// <summary>Gets the phase of the invocation the current handler is running in.</summary>
        public DecoratorPhase Phase { get; internal set; }

        /// <summary>
        /// Gets or sets the reflected method being invoked. Null when the method could not be resolved by
        /// reflection; <see cref="MethodName"/> is still set.
        /// </summary>
        public MethodInfo? Method { get; set; }

        /// <summary>Gets or sets the name of the method being invoked. Falls back to <see cref="Method"/>'s name.</summary>
        public string MethodName
        {
            get => _methodName ?? Method?.Name ?? string.Empty;
            set => _methodName = value;
        }

        /// <summary>
        /// Gets or sets the arguments the method was invoked with, in declaration order. This is a copy made
        /// for the handlers: changing it does not change what the decorated method receives.
        /// </summary>
        public object?[] Args { get; set; }

        /// <summary>
        /// Gets the exception the call was rejected with; null when no handler rejected it.
        /// </summary>
        public Exception? Rejection { get; private set; }

        /// <summary>Gets a value indicating whether a handler rejected the call.</summary>
        public bool Rejected => Rejection != null;

        /// <summary>
        /// Rejects the call: <paramref name="exception"/> is thrown to the caller. At start the decorated
        /// method is never invoked; at end its result is discarded; on error the rejection replaces the
        /// failure. No further handlers run, and no error handler can suppress a rejection. The first
        /// rejection wins.
        /// </summary>
        /// <remarks>
        /// Use this for anything that must stop a call, such as an authorization or validation check. A
        /// handler that throws any other exception is logged and the call goes ahead.
        /// </remarks>
        /// <param name="exception">The exception the caller receives.</param>
        public void Reject(Exception exception)
        {
            ArgumentNullException.ThrowIfNull(exception);

            Rejection ??= exception;
        }

        /// <summary>
        /// Rejects the call with a <see cref="DecoratorRejectionException"/> carrying <paramref name="reason"/>.
        /// See <see cref="Reject(Exception)"/>.
        /// </summary>
        /// <param name="reason">Why the call was rejected.</param>
        public void Reject(string reason)
        {
            Reject(new DecoratorRejectionException(reason));
        }

        /// <summary>
        /// Gets or sets the result of the invocation. During <see cref="DecoratorPhase.End"/> the getter returns
        /// the value the decorated method produced. Setting it — in any phase — overrides the outcome and sets
        /// <see cref="ResultOverridden"/>: at start the decorated method is skipped, at end the returned value
        /// is replaced, on error the exception is suppressed and the value is used as a fallback.
        /// </summary>
        public object? Result
        {
            get => _result;
            set
            {
                _result = value;
                ResultOverridden = true;
            }
        }

        /// <summary>
        /// Gets a value indicating whether a handler supplied a result for the current phase, either by setting
        /// <see cref="Result"/> or by returning a non-null value.
        /// </summary>
        public bool ResultOverridden { get; private set; }

        /// <summary>Gets or sets the exception the decorated method threw; null outside <see cref="DecoratorPhase.Error"/>.</summary>
        public Exception? Exception { get; set; }

        /// <summary>
        /// Moves the context to <paramref name="phase"/> and records the value the invocation has produced so far
        /// without marking it as a handler override.
        /// </summary>
        internal void Enter(DecoratorPhase phase, object? currentResult)
        {
            Phase = phase;
            _result = currentResult;
            ResultOverridden = false;
        }
    }
}
