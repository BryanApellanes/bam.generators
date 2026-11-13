using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;

namespace Bam.Generators.Decorators
{
    /// <summary>
    /// A thread-safe store of decorator handlers keyed by <see cref="DecoratorPhase"/> and method name.
    /// Subscribing while an invocation is enumerating handlers is safe: each read sees an immutable snapshot.
    /// </summary>
    /// <remarks>
    /// Subscriptions count the way <c>event +=</c> and <c>-=</c> do, for every <c>Add</c> overload: each call
    /// stores one subscription, the same delegate subscribed twice runs twice, and <see cref="Remove"/> takes
    /// one subscription back per call. Two callers that subscribe the same delegate (a method group on one
    /// shared guard, say) each own a subscription, so one of them removing its own never takes the other's
    /// away.
    /// </remarks>
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
        /// Each call is one subscription: the same delegate subscribed twice runs twice, and
        /// <see cref="Remove"/> takes one back per call.
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
            Guard.NotNullOrWhiteSpace(methodName, nameof(methodName));
            Guard.NotNull(handler, nameof(handler));
            RequireSynchronous(handler);

            _handlers.AddOrUpdate(
                new HandlerKey(phase, methodName),
                _ => ImmutableArray.Create(handler),
                (_, existing) => existing.Add(handler));
        }

        /// <summary>
        /// Subscribes a <paramref name="handler"/> whose return type is the decorated method's result type, as
        /// the generated typed hooks do. Checked and stored like any other handler: each call is one
        /// subscription, and a non-null return value overrides the invocation's result.
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
            Guard.NotNullOrWhiteSpace(methodName, nameof(methodName));
            Guard.NotNull(handler, nameof(handler));
            // Checked here, on the delegate the subscriber handed over, rather than on the wrapper below,
            // which is always synchronous.
            RequireSynchronous(handler);
            if (Awaitable.Is(typeof(R)))
            {
                throw new ArgumentException(
                    $"A handler returning {Awaitable.Describe(typeof(R))} can never supply a result: handlers run synchronously, and whatever it would decide comes too late. Do the asynchronous work elsewhere and decide from a synchronous handler.",
                    nameof(handler));
            }

            // Wrapped to the stored shape; the wrapper carries the subscriber's delegate so Remove can find the
            // subscription by the delegate the subscriber holds.
            TypedHandler<R> typedHandler = new TypedHandler<R>(handler);
            _handlers.AddOrUpdate(
                new HandlerKey(phase, methodName),
                _ => ImmutableArray.Create(typedHandler.Stored),
                (_, existing) => existing.Add(typedHandler.Stored));
        }

        private interface ITypedHandler
        {
            Delegate Inner { get; }

            Func<TContext, object?> Stored { get; }
        }

        // Adapts a Func<TContext, R> to the stored shape while keeping the subscriber's delegate reachable.
        private sealed class TypedHandler<R> : ITypedHandler
        {
            private readonly Func<TContext, R> _inner;

            public TypedHandler(Func<TContext, R> inner)
            {
                _inner = inner;
                Stored = Invoke;
            }

            public Delegate Inner => _inner;

            public Func<TContext, object?> Stored { get; }

            public object? Invoke(TContext context)
            {
                return _inner(context);
            }
        }

        /// <summary>
        /// Subscribes an observe-only <paramref name="handler"/>: it runs like any other handler but never
        /// overrides the result unless it sets <see cref="DecoratorInvocationContext.Result"/> itself. Each call
        /// is one subscription, as for the <c>Func</c> overloads: the same delegate subscribed twice runs
        /// twice, and <see cref="Remove"/> takes one back per call.
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
            Guard.NotNullOrWhiteSpace(methodName, nameof(methodName));
            Guard.NotNull(handler, nameof(handler));
            RequireSynchronous(handler);

            // Wrapped so Remove can find the subscription by the delegate the subscriber holds.
            ActionHandler actionHandler = new ActionHandler(handler);
            _handlers.AddOrUpdate(
                new HandlerKey(phase, methodName),
                _ => ImmutableArray.Create(actionHandler.Stored),
                (_, existing) => existing.Add(actionHandler.Stored));
        }

        // Adapts an Action<TContext> to the stored shape while keeping the subscriber's delegate reachable.
        private sealed class ActionHandler : ITypedHandler
        {
            private readonly Action<TContext> _inner;

            public ActionHandler(Action<TContext> inner)
            {
                _inner = inner;
                Stored = Invoke;
            }

            public Delegate Inner => _inner;

            public Func<TContext, object?> Stored { get; }

            public object? Invoke(TContext context)
            {
                _inner(context);
                return null;
            }
        }

        /// <summary>
        /// Unsubscribes <paramref name="handler"/> from <paramref name="methodName"/> in <paramref name="phase"/>:
        /// the delegate handed to any <c>Add</c> overload, found by delegate equality. Removes one subscription
        /// per call, the most recent one, as <c>event -=</c> does: a handler subscribed twice needs two calls
        /// (<see cref="Clear()"/> removes all). Only a subscription of that delegate counts: another method
        /// bound to the same object does not remove it. Decorators already holding this registry stop running
        /// the handler at their next call.
        /// </summary>
        /// <param name="phase">The phase the handler was subscribed to.</param>
        /// <param name="methodName">The method it was subscribed to, or <see cref="AnyMethod"/>.</param>
        /// <param name="handler">The delegate that was subscribed.</param>
        /// <returns>True when a handler was removed; false when none matched.</returns>
        public bool Remove(DecoratorPhase phase, string methodName, Delegate handler)
        {
            Guard.NotNullOrWhiteSpace(methodName, nameof(methodName));
            Guard.NotNull(handler, nameof(handler));

            HandlerKey key = new HandlerKey(phase, methodName);
            while (_handlers.TryGetValue(key, out ImmutableArray<Func<TContext, object?>> existing))
            {
                int index = LastIndexOf(existing, handler);
                if (index < 0)
                {
                    return false;
                }

                ImmutableArray<Func<TContext, object?>> updated = existing.RemoveAt(index);
                bool replaced = updated.IsEmpty
                    ? ((ICollection<KeyValuePair<HandlerKey, ImmutableArray<Func<TContext, object?>>>>)_handlers).Remove(new KeyValuePair<HandlerKey, ImmutableArray<Func<TContext, object?>>>(key, existing))
                    : _handlers.TryUpdate(key, updated, existing);
                if (replaced)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Unsubscribes every handler from <paramref name="methodName"/> in <paramref name="phase"/>. Handlers
        /// subscribed to <see cref="AnyMethod"/> are a separate entry: clear them by that name.
        /// </summary>
        /// <returns>True when any handler was removed.</returns>
        public bool Clear(DecoratorPhase phase, string methodName)
        {
            Guard.NotNullOrWhiteSpace(methodName, nameof(methodName));

            return _handlers.TryRemove(new HandlerKey(phase, methodName), out _);
        }

        /// <summary>
        /// Unsubscribes every handler. Decorators already holding this registry run none of them at their
        /// next call; the registry stays usable for new subscriptions.
        /// </summary>
        public void Clear()
        {
            _handlers.Clear();
        }

        private static int LastIndexOf(ImmutableArray<Func<TContext, object?>> stored, Delegate handler)
        {
            for (int i = stored.Length - 1; i >= 0; i--)
            {
                if (Matches(stored[i], handler))
                {
                    return i;
                }
            }

            return -1;
        }

        // A stored delegate is the subscriber's own, or a wrapper's own Invoke carrying it. Something else bound
        // to a wrapper's object (an extension method, CreateDelegate) shares the Target but is not the
        // subscription, so it must not pass for one and get a guard removed.
        private static bool Matches(Func<TContext, object?> stored, Delegate handler)
        {
            return stored.Equals(handler)
                || (stored.Target is ITypedHandler typed && typed.Stored.Equals(stored) && typed.Inner.Equals(handler));
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
