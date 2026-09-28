namespace Bam.Generators.Decorators
{
    /// <summary>
    /// Thrown to the caller of a decorated method when a handler rejected the call. A handler rejects a call
    /// by throwing this exception or by calling <see cref="DecoratorInvocationContext.Reject(string)"/>.
    /// </summary>
    /// <remarks>
    /// This is the one exception a handler can throw that reaches the caller. Any other exception thrown by a
    /// handler is logged and the call goes ahead, so a handler that guards a call — an authorization or
    /// validation check — must reject it this way.
    /// </remarks>
    public class DecoratorRejectionException : Exception
    {
        /// <summary>Initializes a new instance with the reason the call was rejected.</summary>
        public DecoratorRejectionException(string message) : base(message)
        {
        }

        /// <summary>Initializes a new instance with the reason the call was rejected and its cause.</summary>
        public DecoratorRejectionException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
