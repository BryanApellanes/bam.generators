using System.Reflection;
using System.Runtime.ExceptionServices;

namespace Bam.Generators.Decorators
{
    /// <summary>
    /// The outcome of one decorated method invocation: the value produced, or the exception thrown, along with
    /// whether handlers short-circuited the call or handled its failure.
    /// </summary>
    /// <typeparam name="T">The implementation type the decorator wraps.</typeparam>
    /// <typeparam name="R">The type of value the invocation produces.</typeparam>
    public class DecoratorInvocationResult<T, R> where T : class
    {
        /// <summary>Converts a result to its <see cref="Value"/>. Does not throw when the invocation failed.</summary>
        public static implicit operator R?(DecoratorInvocationResult<T, R> result)
        {
            if (result == null) throw new ArgumentNullException("result");
            return result.Value ?? default;
        }

        /// <summary>Initializes a result for an invocation made through <paramref name="decorator"/>.</summary>
        /// <param name="decorator">The decorator that performed the invocation.</param>
        /// <param name="value">The value the invocation produced.</param>
        public DecoratorInvocationResult(Decorator<T> decorator, R? value = default)
        {
            this.Decorator = decorator;
            this.Value = value;
        }

        /// <summary>Gets or sets the decorator that performed the invocation.</summary>
        public Decorator<T> Decorator { get; set; }

        /// <summary>Gets the instance the decorator wraps.</summary>
        public T Decorated
        {
            get => Decorator.Instance;
        }

        /// <summary>Gets the reflected method that was invoked; null when it could not be resolved.</summary>
        public MethodInfo? Method { get; init; }

        string? _methodName;

        /// <summary>Gets or sets the name of the method that was invoked. Falls back to <see cref="Method"/>'s name.</summary>
        public string? MethodName
        {
            get => _methodName ?? this.Method?.Name ?? string.Empty;
            set => _methodName = value;
        }

        /// <summary>
        /// Gets the value the invocation produced — the decorated method's return value, or the value a handler
        /// supplied in its place. Default when the invocation failed.
        /// </summary>
        public R? Value { get; init; }

        /// <summary>
        /// Gets the exception the invocation threw, if any. Still set when an error handler supplied a fallback,
        /// in which case <see cref="Handled"/> is true.
        /// </summary>
        public Exception? Exception { get; init; }

        /// <summary>Gets a value indicating whether an error handler supplied a fallback for <see cref="Exception"/>.</summary>
        public bool Handled { get; init; }

        /// <summary>
        /// Gets a value indicating whether a start handler supplied the result, so the decorated method was
        /// never called.
        /// </summary>
        public bool ShortCircuited { get; init; }

        /// <summary>
        /// Gets a value indicating whether the invocation produced a usable value: it did not throw, or its
        /// failure was handled.
        /// </summary>
        public bool Success
        {
            get
            {
                return Exception == null || Handled;
            }
        }

        /// <summary>
        /// Gets or sets a message describing the outcome. When the invocation failed the getter returns the
        /// exception's message.
        /// </summary>
        public string Message
        {
            get
            {
                if (!Success)
                {
                    return Exception?.Message ?? string.Empty;
                }

                return field;
            }
            set;
        } = String.Empty;

        /// <summary>
        /// Rethrows <see cref="Exception"/> with its original stack trace when the invocation failed and no error
        /// handler supplied a fallback. Does nothing otherwise.
        /// </summary>
        /// <returns>This result, for chaining.</returns>
        public DecoratorInvocationResult<T, R> ThrowIfFailed()
        {
            if (!Success && Exception != null)
            {
                ExceptionDispatchInfo.Capture(Exception).Throw();
            }

            return this;
        }

        /// <summary>
        /// Gets <see cref="Value"/>, rethrowing <see cref="Exception"/> first when the invocation failed. This is
        /// what makes a generated decorator behave like the service it wraps: callers see the original exception.
        /// </summary>
        public R? GetValue()
        {
            ThrowIfFailed();
            return Value;
        }
    }
}
