using Bam.DependencyInjection;
using System.Runtime.CompilerServices;

namespace Bam.Generators.Decorators
{
    /// <summary>
    /// What a <see cref="ServiceRegistry"/> holds for decoration: its registry-wide handlers, the record of
    /// what it decorated, and the lock decoration takes. Kept beside the registry by identity rather than
    /// registered in it, so composing registries (<c>Include</c>, <c>CombineWith</c>, <c>CopyFrom</c>)
    /// copies services and never a registry's handlers or its record.
    /// </summary>
    internal sealed class DecoratorState
    {
        private static readonly ConditionalWeakTable<ServiceRegistry, DecoratorState> _states = new ConditionalWeakTable<ServiceRegistry, DecoratorState>();

        private DecoratorState()
        {
            Subscriptions = new DecoratorSubscriptions();
            Registrations = new DecoratorRegistrations();
            Lock = new object();
        }

        /// <summary>Gets the registry-wide handlers.</summary>
        public DecoratorSubscriptions Subscriptions { get; }

        /// <summary>Gets the record of the services decorated in the registry.</summary>
        public DecoratorRegistrations Registrations { get; }

        /// <summary>Gets the object decoration in the registry is serialized on. Held only by <c>Decorate</c>.</summary>
        internal object Lock { get; }

        /// <summary>
        /// Gets the state of <paramref name="registry"/>, creating it on first use. The same registry always
        /// gets the same state, for as long as the registry lives; a copy of a registry starts with none.
        /// </summary>
        /// <param name="registry">The registry.</param>
        public static DecoratorState Of(ServiceRegistry registry)
        {
            ArgumentNullException.ThrowIfNull(registry);

            return _states.GetValue(registry, _ => new DecoratorState());
        }
    }
}
