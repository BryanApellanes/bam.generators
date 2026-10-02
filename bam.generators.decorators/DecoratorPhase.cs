namespace Bam.Generators.Decorators
{
    /// <summary>
    /// Identifies the point in a decorated method invocation at which a handler runs.
    /// </summary>
    public enum DecoratorPhase
    {
        /// <summary>
        /// Before the decorated method is invoked. A handler that supplies a result here short-circuits the
        /// invocation: the decorated method is never called.
        /// </summary>
        Start,

        /// <summary>
        /// After the decorated method returned (or was short-circuited). A handler that supplies a result here
        /// replaces the value handed back to the caller.
        /// </summary>
        End,

        /// <summary>
        /// After the decorated method threw. A handler that supplies a result here provides a fallback value
        /// and suppresses the exception.
        /// </summary>
        Error
    }
}
