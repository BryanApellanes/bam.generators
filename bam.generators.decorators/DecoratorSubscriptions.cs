namespace Bam.Generators.Decorators
{
    /// <summary>
    /// The registry-wide handler store: handlers subscribed here fire for every decorated service that shares
    /// it, matched by method name alone. One instance lives in each <c>ServiceRegistry</c> and is attached to
    /// every decorator that registry creates, which is why a handler can be subscribed before the service it
    /// applies to has been registered.
    /// </summary>
    public class DecoratorSubscriptions : DecoratorHandlerRegistry<DecoratorInvocationContext>
    {
        /// <summary>Initializes an empty registry-wide handler store.</summary>
        public DecoratorSubscriptions()
        {
        }
    }
}
