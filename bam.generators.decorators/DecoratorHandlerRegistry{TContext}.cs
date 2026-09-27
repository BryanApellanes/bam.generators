using System.Collections.Concurrent;
using System.Collections.Immutable;

namespace Bam.Generators.Decorators
{
    /// <summary>
    /// A thread-safe store of decorator handlers keyed by <see cref="DecoratorPhase"/> and method name.
    /// Subscribing while an invocation is enumerating handlers is safe: each read sees an immutable snapshot.
    /// </summary>
    /// <typeparam name="TContext">The context type handed to the handlers.</typeparam>
    public class DecoratorHandlerRegistry<TContext> where TContext : DecoratorInvocationContext
    {
        /// <summary>
        /// The method name that subscribes a handler to every method, e.g. for timing or logging all calls.
        /// </summary>
        public const string AnyMethod = "*";

        private readonly ConcurrentDictionary<HandlerKey, ImmutableArray<Func<TContext, object?>>> _handlers;

        /// <summary>Initializes an empty registry.</summary>
        public DecoratorHandlerRegistry()
        {
            _handlers = new ConcurrentDictionary<HandlerKey, ImmutableArray<Func<TContext, object?>>>();
        }

        /// <summary>
        /// Subscribes <paramref name="handler"/> to <paramref name="methodName"/> for <paramref name="phase"/>.
        /// Subscribing the same delegate twice has no effect.
        /// </summary>
        /// <param name="phase">The phase the handler runs in.</param>
        /// <param name="methodName">The method to subscribe to, or <see cref="AnyMethod"/> for all of them.</param>
        /// <param name="handler">The handler. A non-null return value overrides the invocation's result.</param>
        public void Add(DecoratorPhase phase, string methodName, Func<TContext, object?> handler)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(methodName);
            ArgumentNullException.ThrowIfNull(handler);

            _handlers.AddOrUpdate(
                new HandlerKey(phase, methodName),
                _ => ImmutableArray.Create(handler),
                (_, existing) => existing.Contains(handler) ? existing : existing.Add(handler));
        }

        /// <summary>
        /// Subscribes an observe-only <paramref name="handler"/>: it runs like any other handler but never
        /// overrides the result unless it sets <see cref="DecoratorInvocationContext.Result"/> itself.
        /// </summary>
        /// <param name="phase">The phase the handler runs in.</param>
        /// <param name="methodName">The method to subscribe to, or <see cref="AnyMethod"/> for all of them.</param>
        /// <param name="handler">The handler.</param>
        public void Add(DecoratorPhase phase, string methodName, Action<TContext> handler)
        {
            ArgumentNullException.ThrowIfNull(handler);

            Add(phase, methodName, new Func<TContext, object?>(context =>
            {
                handler(context);
                return null;
            }));
        }

        /// <summary>
        /// Gets the handlers that apply to <paramref name="methodName"/> in <paramref name="phase"/>, in
        /// subscription order: those subscribed to the method by name, then those subscribed to
        /// <see cref="AnyMethod"/>.
        /// </summary>
        public IReadOnlyList<Func<TContext, object?>> Get(DecoratorPhase phase, string methodName)
        {
            ImmutableArray<Func<TContext, object?>> named = Lookup(phase, methodName);
            if (methodName == AnyMethod)
            {
                return named;
            }

            ImmutableArray<Func<TContext, object?>> any = Lookup(phase, AnyMethod);
            if (any.IsEmpty)
            {
                return named;
            }

            return named.IsEmpty ? any : named.AddRange(any);
        }

        private ImmutableArray<Func<TContext, object?>> Lookup(DecoratorPhase phase, string methodName)
        {
            return _handlers.TryGetValue(new HandlerKey(phase, methodName), out ImmutableArray<Func<TContext, object?>> handlers)
                ? handlers
                : ImmutableArray<Func<TContext, object?>>.Empty;
        }

        /// <summary>Gets the number of handlers that apply to <paramref name="methodName"/> in <paramref name="phase"/>.</summary>
        public int Count(DecoratorPhase phase, string methodName)
        {
            return Get(phase, methodName).Count;
        }

        private readonly record struct HandlerKey(DecoratorPhase Phase, string MethodName);
    }
}
