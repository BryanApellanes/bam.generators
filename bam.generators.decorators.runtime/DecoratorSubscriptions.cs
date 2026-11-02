namespace Bam.Generators.Decorators
{
    /// <summary>
    /// The registry-wide handler store: handlers subscribed here fire for every decorated service that shares
    /// it, matched by method name alone. Each <c>ServiceRegistry</c> has one, reached through
    /// <c>GetDecoratorSubscriptions()</c> (never <c>Get&lt;DecoratorSubscriptions&gt;()</c>, which would make a
    /// store nothing runs); it is attached to every decorator that registry creates, which is why a handler
    /// can be subscribed before the service it applies to has been registered.
    /// </summary>
    public class DecoratorSubscriptions : DecoratorHandlerRegistry<DecoratorInvocationContext>
    {
        /// <summary>Initializes an empty registry-wide handler store.</summary>
        public DecoratorSubscriptions()
        {
        }
    }
}
