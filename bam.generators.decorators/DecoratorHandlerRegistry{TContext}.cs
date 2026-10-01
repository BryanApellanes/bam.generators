using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;

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
        /// <exception cref="ArgumentException">
        /// <paramref name="methodName"/> is blank, or <paramref name="handler"/> is an <c>async</c> method or
        /// lambda. Handlers run synchronously: an async one returns at its first <c>await</c> and the call goes
        /// ahead, so a guard written that way could never stop anything.
        /// </exception>
        public void Add(DecoratorPhase phase, string methodName, Func<TContext, object?> handler)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(methodName);
            ArgumentNullException.ThrowIfNull(handler);
            RequireSynchronous(handler);

            _handlers.AddOrUpdate(
                new HandlerKey(phase, methodName),
                _ => ImmutableArray.Create(handler),
                (_, existing) => existing.Contains(handler) ? existing : existing.Add(handler));
        }

        /// <summary>
        /// Subscribes a <paramref name="handler"/> whose return type is the decorated method's result type, as
        /// the generated typed hooks do. Checked and stored like any other handler: the same delegate
        /// subscribed twice is stored once, and a non-null return value overrides the invocation's result.
        /// </summary>
        /// <typeparam name="R">The handler's return type.</typeparam>
        /// <param name="phase">The phase the handler runs in.</param>
        /// <param name="methodName">The method to subscribe to, or <see cref="AnyMethod"/> for all of them.</param>
        /// <param name="handler">The handler.</param>
        /// <exception cref="ArgumentException">
        /// <paramref name="methodName"/> is blank, <paramref name="handler"/> is an <c>async</c> method or
        /// lambda, or <typeparamref name="R"/> is a <c>Task</c>, <c>ValueTask</c> or other awaitable, which a
        /// handler can never hand back as a result.
        /// </exception>
        public void Add<R>(DecoratorPhase phase, string methodName, Func<TContext, R> handler)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(methodName);
            ArgumentNullException.ThrowIfNull(handler);
            // Checked here, on the delegate the subscriber handed over, rather than on the wrapper below,
            // which is always synchronous.
            RequireSynchronous(handler);
            if (Awaitable.Is(typeof(R)))
            {
                throw new ArgumentException(
                    $"A handler returning {Awaitable.Describe(typeof(R))} can never supply a result: handlers run synchronously, and whatever it would decide comes too late. Do the asynchronous work elsewhere and decide from a synchronous handler.",
                    nameof(handler));
            }

            // De-duplicated on the subscriber's delegate by delegate equality, the same rule Add applies to an
            // untyped handler: an instance method group subscribed twice is stored once either way. The
            // wrapper carries the delegate so a later subscription can find it. The check and the add are one
            // compare-and-swap, as in Add, so two threads subscribing the same delegate store it once.
            Func<TContext, object?> wrapper = new TypedHandler<R>(handler).Invoke;
            _handlers.AddOrUpdate(
                new HandlerKey(phase, methodName),
                _ => ImmutableArray.Create(wrapper),
                (_, existing) => existing.Any(stored => stored.Target is ITypedHandler typed && typed.Inner.Equals(handler)) ? existing : existing.Add(wrapper));
        }

        private interface ITypedHandler
        {
            Delegate Inner { get; }
        }

        // Adapts a Func<TContext, R> to the stored shape while keeping the subscriber's delegate reachable.
        private sealed class TypedHandler<R> : ITypedHandler
        {
            private readonly Func<TContext, R> _inner;

            public TypedHandler(Func<TContext, R> inner)
            {
                _inner = inner;
            }

            public Delegate Inner => _inner;

            public object? Invoke(TContext context)
            {
                return _inner(context);
            }
        }

        /// <summary>
        /// Subscribes an observe-only <paramref name="handler"/>: it runs like any other handler but never
        /// overrides the result unless it sets <see cref="DecoratorInvocationContext.Result"/> itself.
        /// </summary>
        /// <param name="phase">The phase the handler runs in.</param>
        /// <param name="methodName">The method to subscribe to, or <see cref="AnyMethod"/> for all of them.</param>
        /// <param name="handler">The handler.</param>
        /// <exception cref="ArgumentException">
        /// <paramref name="methodName"/> is blank, or <paramref name="handler"/> is an <c>async</c> method or
        /// lambda, which would bind here as <c>async void</c> and return at its first <c>await</c>.
        /// </exception>
        public void Add(DecoratorPhase phase, string methodName, Action<TContext> handler)
        {
            ArgumentNullException.ThrowIfNull(handler);
            RequireSynchronous(handler);

            Add(phase, methodName, new Func<TContext, object?>(context =>
            {
                handler(context);
                return null;
            }));
        }

        // The compiler marks every async method and lambda with AsyncStateMachineAttribute. Such a handler
        // returns at its first await, before it can reject anything, and the call goes ahead: a guard that
        // never guards. Refusing it here covers every way to subscribe, since they all end up in Add. Every
        // target of a combined delegate is checked; Delegate.Method alone names only the last one.
        internal static void RequireSynchronous(Delegate handler)
        {
            foreach (Delegate target in handler.GetInvocationList())
            {
                if (target.Method.IsDefined(typeof(AsyncStateMachineAttribute), false))
                {
                    throw new ArgumentException(
                        "Handlers run synchronously and an async handler returns at its first await, before it can stop the call. Do the asynchronous work elsewhere and reject from a synchronous handler.",
                        nameof(handler));
                }
            }
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
