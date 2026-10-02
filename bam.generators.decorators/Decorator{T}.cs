using Bam.Logging;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Reflection;

namespace Bam.Generators.Decorators
{
    /// <summary>
    /// Wraps an instance of <typeparamref name="T"/> and runs subscribed handlers at the start, end and failure
    /// of every method invoked through it. Generated decorators extend this (through
    /// <see cref="Decorator{I, T}"/>) and route each interface member through <c>Intercept</c>; the
    /// reflection-based <see cref="Invoke{R}(string, object?[])"/> serves callers that only know a method by name.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Handlers run in subscription order: registry-wide handlers (<see cref="SharedSubscriptions"/>) first,
    /// then those subscribed to the registrations (<see cref="RegistrationHandlers"/>), then this instance's
    /// own. A decorator that serves several registries runs every registry's stores, in the order they attached.
    /// A handler overrides the outcome by returning a non-null value or by setting
    /// <see cref="DecoratorInvocationContext.Result"/>: at start the decorated method is skipped, at end the
    /// returned value is replaced, on error the exception is suppressed. When several handlers override, the
    /// last one wins. An override of the wrong type for the method is logged and ignored.
    /// </para>
    /// <para>
    /// A handler or event subscriber that throws never breaks the decorated call: the exception is logged and
    /// the invocation continues. That makes a handler that throws to stop a call fail open. To stop a call,
    /// a handler calls <see cref="DecoratorInvocationContext.Reject(string)"/> or throws a
    /// <see cref="DecoratorRejectionException"/>; the rejection reaches the caller, no further handlers run,
    /// and no error handler can suppress it.
    /// </para>
    /// <para>
    /// An error handler that supplies a result turns a failure into a success. Don't subscribe one to a
    /// service whose callers rely on the exception, such as an authorization check that denies by throwing.
    /// </para>
    /// </remarks>
    /// <typeparam name="T">The implementation type being decorated.</typeparam>
    public class Decorator<T> : IDecorator<T> where T : class
    {
        /// <summary>Unwraps a decorator to the instance it decorates.</summary>
        public static explicit operator T(Decorator<T> decorator)
        {
            return decorator.Instance;
        }

        private readonly DecoratorHandlerRegistry<DecoratorInvocationContext<T>> _handlers;
        // Keyed by the decorated instance's runtime type, so one decorator's lookups serve every decorator
        // of a transient service rather than starting cold per instance.
        private static readonly ConcurrentDictionary<MethodCacheKey, MethodInfo[]> _methods = new ConcurrentDictionary<MethodCacheKey, MethodInfo[]>();
        private static readonly ConcurrentDictionary<ImplementationKey, MethodInfo> _implementations = new ConcurrentDictionary<ImplementationKey, MethodInfo>();

        private readonly object _attachLock = new object();
        private ImmutableList<DecoratorHandlerRegistry<DecoratorInvocationContext<T>>> _registrationHandlers = ImmutableList<DecoratorHandlerRegistry<DecoratorInvocationContext<T>>>.Empty;
        private ImmutableList<DecoratorSubscriptions> _sharedSubscriptions = ImmutableList<DecoratorSubscriptions>.Empty;

        ILogger? _logger;

        /// <summary>Initializes a decorator around <paramref name="value"/>.</summary>
        /// <param name="value">The instance to decorate.</param>
        /// <param name="logger">Receives handler failures. Defaults to <see cref="Log.Default"/>.</param>
        /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
        public Decorator(T value, ILogger? logger = null)
        {
            ArgumentNullException.ThrowIfNull(value);

            this.Instance = value;
            this._handlers = new DecoratorHandlerRegistry<DecoratorInvocationContext<T>>();
            this._logger = logger ?? Log.Default;
        }

        /// <inheritdoc />
        public T Instance { get; set; }

        /// <summary>
        /// Gets the store holding the handlers subscribed to this decorator instance. They run for calls made
        /// through this instance only.
        /// </summary>
        public DecoratorHandlerRegistry<DecoratorInvocationContext<T>> Handlers => _handlers;

        /// <summary>
        /// Gets the handler stores of the registrations this decorator serves, in the order they attached.
        /// Every decorator a <c>ServiceRegistry</c> creates for the same service shares that registration's
        /// store, which is what keeps a handler subscribed through the registry running when the service is
        /// resolved again. A decorator that a second registry resolves through the first serves both
        /// registrations, and runs both stores. Empty when the decorator was not created through a registry.
        /// </summary>
        public IReadOnlyList<DecoratorHandlerRegistry<DecoratorInvocationContext<T>>> RegistrationHandlers => _registrationHandlers;

        /// <summary>
        /// Adds a registration's handler store to those this decorator runs. Attaching the same store twice
        /// has no effect, and attaching a second store never displaces the first.
        /// </summary>
        /// <param name="handlers">The registration's store.</param>
        public void AttachRegistrationHandlers(DecoratorHandlerRegistry<DecoratorInvocationContext<T>> handlers)
        {
            ArgumentNullException.ThrowIfNull(handlers);

            lock (_attachLock)
            {
                if (!_registrationHandlers.Contains(handlers))
                {
                    _registrationHandlers = _registrationHandlers.Add(handlers);
                }
            }
        }

        /// <inheritdoc />
        public IReadOnlyList<DecoratorSubscriptions> SharedSubscriptions => _sharedSubscriptions;

        /// <inheritdoc />
        public void AttachSharedSubscriptions(DecoratorSubscriptions subscriptions)
        {
            ArgumentNullException.ThrowIfNull(subscriptions);

            lock (_attachLock)
            {
                if (!_sharedSubscriptions.Contains(subscriptions))
                {
                    _sharedSubscriptions = _sharedSubscriptions.Add(subscriptions);
                }
            }
        }

        /// <inheritdoc />
        public event EventHandler<DecoratorEventArgs<T>>? MethodStart;

        /// <inheritdoc />
        public event EventHandler<DecoratorEventArgs<T>>? MethodEnd;

        /// <inheritdoc />
        public event EventHandler<DecoratorEventArgs<T>>? MethodError;

        /// <inheritdoc />
        public event EventHandler<DecoratorEventArgs<T>>? MethodNotFound;

        /// <inheritdoc />
        public Type ImplementationType
        {
            get => typeof(T);
        }

        /// <inheritdoc />
        public void Subscribe(DecoratorPhase phase, string methodName, Func<DecoratorInvocationContext<T>, object?> handler)
        {
            _handlers.Add(phase, methodName, handler);
        }

        /// <inheritdoc />
        public void Subscribe<R>(DecoratorPhase phase, string methodName, Func<DecoratorInvocationContext<T>, R> handler)
        {
            _handlers.Add(phase, methodName, handler);
        }

        /// <inheritdoc />
        public void Subscribe(DecoratorPhase phase, string methodName, Action<DecoratorInvocationContext<T>> handler)
        {
            _handlers.Add(phase, methodName, handler);
        }

        /// <inheritdoc />
        public void SubscribeStart(string methodName, Func<DecoratorInvocationContext<T>, object?> func)
        {
            Subscribe(DecoratorPhase.Start, methodName, func);
        }

        /// <inheritdoc />
        public void SubscribeEnd(string methodName, Func<DecoratorInvocationContext<T>, object?> func)
        {
            Subscribe(DecoratorPhase.End, methodName, func);
        }

        /// <inheritdoc />
        public void SubscribeError(string methodName, Func<DecoratorInvocationContext<T>, object?> func)
        {
            Subscribe(DecoratorPhase.Error, methodName, func);
        }

        /// <inheritdoc />
        public DecoratorInvocationResult<T, R> Invoke<R>(string methodName, params object?[] args)
        {
            object?[] arguments = args ?? Array.Empty<object?>();
            MethodInfo? method = ResolveMethod(methodName, arguments);
            if (method == null)
            {
                return NotFound<R>(methodName, arguments);
            }

            return Run<R>(methodName, method, arguments, () => Cast<R>(InvokeReflected(method, arguments)));
        }

        /// <inheritdoc />
        public Task<DecoratorInvocationResult<T, R>> InvokeAsync<R>(string methodName, params object?[] args)
        {
            object?[] arguments = args ?? Array.Empty<object?>();
            MethodInfo? method = ResolveMethod(methodName, arguments);
            if (method == null)
            {
                return Task.FromResult(NotFound<R>(methodName, arguments));
            }

            return RunAsync<R>(methodName, method, arguments, () => AwaitReturned<R>(InvokeReflected(method, arguments)));
        }

        /// <summary>
        /// Runs <paramref name="invocation"/> as the named method, with the subscribed handlers around it.
        /// Generated decorators call this with a delegate that forwards to <see cref="Instance"/>, which avoids
        /// reflection on the call itself. Never throws for a failing invocation: the exception is captured on
        /// the result, and <see cref="DecoratorInvocationResult{T, R}.GetValue"/> rethrows it.
        /// </summary>
        /// <typeparam name="R">The type of value the method returns.</typeparam>
        /// <param name="methodName">The name of the method being invoked, used to select handlers.</param>
        /// <param name="args">The arguments the method was called with, made available to handlers.</param>
        /// <param name="invocation">The call to the decorated instance.</param>
        public DecoratorInvocationResult<T, R> Intercept<R>(string methodName, object?[] args, Func<R> invocation)
        {
            ArgumentNullException.ThrowIfNull(invocation);

            object?[] arguments = args ?? Array.Empty<object?>();
            return Run<R>(methodName, ResolveMethod(methodName, arguments), arguments, invocation);
        }

        /// <summary>
        /// Runs <paramref name="invocation"/> as the named method that returns no value, with the subscribed
        /// handlers around it.
        /// </summary>
        /// <param name="methodName">The name of the method being invoked, used to select handlers.</param>
        /// <param name="args">The arguments the method was called with, made available to handlers.</param>
        /// <param name="invocation">The call to the decorated instance.</param>
        public DecoratorInvocationResult<T, object> Intercept(string methodName, object?[] args, Action invocation)
        {
            ArgumentNullException.ThrowIfNull(invocation);

            return Intercept<object>(methodName, args, () =>
            {
                invocation();
                return null!;
            });
        }

        /// <summary>
        /// Runs <paramref name="invocation"/> as the named asynchronous method. End and error handlers run once
        /// the returned task completes, so they see the value or exception the task produced.
        /// </summary>
        /// <typeparam name="R">The type of value the method's task produces.</typeparam>
        /// <param name="methodName">The name of the method being invoked, used to select handlers.</param>
        /// <param name="args">The arguments the method was called with, made available to handlers.</param>
        /// <param name="invocation">The call to the decorated instance.</param>
        public Task<DecoratorInvocationResult<T, R>> InterceptAsync<R>(string methodName, object?[] args, Func<Task<R>> invocation)
        {
            ArgumentNullException.ThrowIfNull(invocation);

            object?[] arguments = args ?? Array.Empty<object?>();
            return RunAsync<R>(methodName, ResolveMethod(methodName, arguments), arguments, invocation);
        }

        /// <summary>
        /// Runs <paramref name="invocation"/> as the named asynchronous method whose task produces no value.
        /// </summary>
        /// <param name="methodName">The name of the method being invoked, used to select handlers.</param>
        /// <param name="args">The arguments the method was called with, made available to handlers.</param>
        /// <param name="invocation">The call to the decorated instance.</param>
        public Task<DecoratorInvocationResult<T, object>> InterceptAsync(string methodName, object?[] args, Func<Task> invocation)
        {
            ArgumentNullException.ThrowIfNull(invocation);

            return InterceptAsync<object>(methodName, args, async () =>
            {
                await invocation().ConfigureAwait(false);
                return null!;
            });
        }

        /// <summary>
        /// Runs <paramref name="invocation"/> as <paramref name="method"/>, with the subscribed handlers around
        /// it. This is what generated decorators call: they know exactly which method each member implements,
        /// so handlers are told the overload that ran rather than one guessed from the arguments.
        /// </summary>
        /// <typeparam name="R">The type of value the method returns.</typeparam>
        /// <param name="method">The interface method being invoked; null falls back to finding it by name.</param>
        /// <param name="methodName">The name of the method being invoked, used to select handlers.</param>
        /// <param name="args">The arguments the method was called with, made available to handlers.</param>
        /// <param name="invocation">The call to the decorated instance.</param>
        public DecoratorInvocationResult<T, R> Intercept<R>(MethodInfo? method, string methodName, object?[] args, Func<R> invocation)
        {
            ArgumentNullException.ThrowIfNull(invocation);

            object?[] arguments = args ?? Array.Empty<object?>();
            return Run<R>(methodName, ImplementationOf(method) ?? ResolveMethod(methodName, arguments), arguments, invocation);
        }

        /// <summary>
        /// Runs <paramref name="invocation"/> as <paramref name="method"/>, which returns no value, with the
        /// subscribed handlers around it.
        /// </summary>
        /// <param name="method">The interface method being invoked; null falls back to finding it by name.</param>
        /// <param name="methodName">The name of the method being invoked, used to select handlers.</param>
        /// <param name="args">The arguments the method was called with, made available to handlers.</param>
        /// <param name="invocation">The call to the decorated instance.</param>
        public DecoratorInvocationResult<T, object> Intercept(MethodInfo? method, string methodName, object?[] args, Action invocation)
        {
            ArgumentNullException.ThrowIfNull(invocation);

            return Intercept<object>(method, methodName, args, () =>
            {
                invocation();
                return null!;
            });
        }

        /// <summary>
        /// Runs <paramref name="invocation"/> as the asynchronous <paramref name="method"/>. End and error
        /// handlers run once the returned task completes.
        /// </summary>
        /// <typeparam name="R">The type of value the method's task produces.</typeparam>
        /// <param name="method">The interface method being invoked; null falls back to finding it by name.</param>
        /// <param name="methodName">The name of the method being invoked, used to select handlers.</param>
        /// <param name="args">The arguments the method was called with, made available to handlers.</param>
        /// <param name="invocation">The call to the decorated instance.</param>
        public Task<DecoratorInvocationResult<T, R>> InterceptAsync<R>(MethodInfo? method, string methodName, object?[] args, Func<Task<R>> invocation)
        {
            ArgumentNullException.ThrowIfNull(invocation);

            object?[] arguments = args ?? Array.Empty<object?>();
            return RunAsync<R>(methodName, ImplementationOf(method) ?? ResolveMethod(methodName, arguments), arguments, invocation);
        }

        /// <summary>
        /// Runs <paramref name="invocation"/> as the asynchronous <paramref name="method"/> whose task produces
        /// no value.
        /// </summary>
        /// <param name="method">The interface method being invoked; null falls back to finding it by name.</param>
        /// <param name="methodName">The name of the method being invoked, used to select handlers.</param>
        /// <param name="args">The arguments the method was called with, made available to handlers.</param>
        /// <param name="invocation">The call to the decorated instance.</param>
        public Task<DecoratorInvocationResult<T, object>> InterceptAsync(MethodInfo? method, string methodName, object?[] args, Func<Task> invocation)
        {
            ArgumentNullException.ThrowIfNull(invocation);

            return InterceptAsync<object>(method, methodName, args, async () =>
            {
                await invocation().ConfigureAwait(false);
                return null!;
            });
        }

        // Handlers are told about the method on the decorated instance, where its attributes are, so an
        // interface method is mapped to the method that implements it.
        private MethodInfo? ImplementationOf(MethodInfo? method)
        {
            if (method == null)
            {
                return null;
            }

            return _implementations.GetOrAdd(new ImplementationKey(Instance.GetType(), method), key =>
            {
                Type? declaringType = key.Method.DeclaringType;
                if (declaringType == null || !declaringType.IsInterface || !declaringType.IsAssignableFrom(key.Type))
                {
                    return key.Method;
                }

                InterfaceMapping mapping = key.Type.GetInterfaceMap(declaringType);
                int index = Array.IndexOf(mapping.InterfaceMethods, key.Method);
                return index >= 0 ? mapping.TargetMethods[index] : key.Method;
            });
        }

        /// <summary>
        /// Finds the method on <see cref="Instance"/> that <paramref name="methodName"/> and
        /// <paramref name="args"/> refer to. Overloads are told apart by argument count and runtime type; when
        /// no overload accepts the arguments but the name is unambiguous, that method is returned so the
        /// invocation fails with the runtime's own argument error.
        /// </summary>
        /// <param name="methodName">The name of the method.</param>
        /// <param name="args">The arguments it is being called with.</param>
        /// <returns>The method, or null when there is none by that name or the name is ambiguous.</returns>
        protected virtual MethodInfo? ResolveMethod(string methodName, object?[] args)
        {
            if (string.IsNullOrEmpty(methodName))
            {
                return null;
            }

            MethodInfo[] candidates = _methods.GetOrAdd(new MethodCacheKey(Instance.GetType(), methodName), FindMethods);
            MethodInfo? best = null;
            int bestScore = -1;
            foreach (MethodInfo candidate in candidates)
            {
                int score = Score(candidate, args);
                if (score > bestScore)
                {
                    best = candidate;
                    bestScore = score;
                }
            }

            if (best != null)
            {
                return best;
            }

            return candidates.Length == 1 ? candidates[0] : null;
        }

        private static MethodInfo[] FindMethods(MethodCacheKey key)
        {
            MethodInfo[] methods = key.Type
                .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(method => method.Name == key.MethodName)
                .ToArray();
            if (methods.Length > 0)
            {
                return methods;
            }

            // Explicit interface implementations are not public members of the type; find them on its interfaces.
            return key.Type
                .GetInterfaces()
                .SelectMany(interfaceType => interfaceType.GetMethods())
                .Where(method => method.Name == key.MethodName)
                .ToArray();
        }

        // -1 when the method cannot take the arguments; otherwise the number of exact type matches, so the
        // most specific overload wins.
        private static int Score(MethodInfo method, object?[] args)
        {
            ParameterInfo[] parameters = method.GetParameters();
            if (parameters.Length != args.Length)
            {
                return -1;
            }

            int score = 0;
            for (int i = 0; i < parameters.Length; i++)
            {
                Type parameterType = parameters[i].ParameterType;
                if (parameterType.IsByRef)
                {
                    parameterType = parameterType.GetElementType()!;
                }

                if (parameterType.IsGenericParameter)
                {
                    continue;
                }

                object? arg = args[i];
                if (arg == null)
                {
                    if (parameterType.IsValueType && Nullable.GetUnderlyingType(parameterType) == null)
                    {
                        return -1;
                    }

                    continue;
                }

                if (!parameterType.IsInstanceOfType(arg))
                {
                    return -1;
                }

                if (parameterType == arg.GetType())
                {
                    score++;
                }
            }

            return score;
        }

        // Returns object because that is all reflection knows about the value; callers cast it to their R.
        private object? InvokeReflected(MethodInfo method, object?[] args)
        {
            if (method.IsGenericMethodDefinition)
            {
                throw new NotSupportedException(
                    $"{typeof(T).Name}.{method.Name} is generic and cannot be invoked by name. Call it through a generated decorator instead.");
            }

            return method.Invoke(Instance, args);
        }

        private static async Task<R> AwaitReturned<R>(object? returned)
        {
            if (returned == null)
            {
                return default!;
            }

            if (returned is Task<R> typed)
            {
                return await typed.ConfigureAwait(false);
            }

            if (returned is Task task)
            {
                await task.ConfigureAwait(false);
                return ResultOf<R>(task);
            }

            if (returned is ValueTask valueTask)
            {
                await valueTask.ConfigureAwait(false);
                return default!;
            }

            Type returnedType = returned.GetType();
            if (returnedType.IsGenericType && returnedType.GetGenericTypeDefinition() == typeof(ValueTask<>))
            {
                Task asTask = (Task)returnedType.GetMethod(nameof(ValueTask<R>.AsTask))!.Invoke(returned, null)!;
                await asTask.ConfigureAwait(false);
                return ResultOf<R>(asTask);
            }

            return Cast<R>(returned);
        }

        // Null (a void method, or a null return) becomes default; anything else must actually be an R.
        private static R Cast<R>(object? returned)
        {
            return returned == null ? default! : (R)returned;
        }

        // Reads the result of a completed Task<X> whose X is not statically R (e.g. R is object).
        private static R ResultOf<R>(Task completed)
        {
            Type taskType = completed.GetType();
            if (!taskType.IsGenericType)
            {
                return default!;
            }

            object? value = taskType.GetProperty(nameof(Task<R>.Result))?.GetValue(completed);
            return value is R result ? result : default!;
        }

        private DecoratorInvocationResult<T, R> Run<R>(string methodName, MethodInfo? method, object?[] args, Func<R> invocation)
        {
            DecoratorInvocationContext<T> context = CreateContext(methodName, method, args);
            if (TryStart(context, out R? value))
            {
                return Complete(context, value, true);
            }

            if (context.Rejected)
            {
                return Refuse<R>(context);
            }

            try
            {
                value = invocation();
            }
            catch (Exception ex)
            {
                return Fail<R>(context, ex);
            }

            return Complete(context, value, false);
        }

        private async Task<DecoratorInvocationResult<T, R>> RunAsync<R>(string methodName, MethodInfo? method, object?[] args, Func<Task<R>> invocation)
        {
            DecoratorInvocationContext<T> context = CreateContext(methodName, method, args);
            if (TryStart(context, out R? value))
            {
                return Complete(context, value, true);
            }

            if (context.Rejected)
            {
                return Refuse<R>(context);
            }

            try
            {
                value = await invocation().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                return Fail<R>(context, ex);
            }

            return Complete(context, value, false);
        }

        // Handlers get their own copy of the arguments, so what they do to it never reaches the decorated method.
        private DecoratorInvocationContext<T> CreateContext(string methodName, MethodInfo? method, object?[] args)
        {
            return new DecoratorInvocationContext<T>(this)
            {
                Method = method,
                MethodName = methodName,
                Args = (object?[])args.Clone()
            };
        }

        // True when a start handler supplied the result, in which case the decorated method must not be called.
        // False with context.Rejected set when a start handler rejected the call.
        private bool TryStart<R>(DecoratorInvocationContext<T> context, out R? value)
        {
            value = default;
            context.Enter(DecoratorPhase.Start, null);
            Raise(MethodStart, context, null);
            RunHandlers(context);
            return !context.Rejected && context.ResultOverridden && TryConvert(context, out value);
        }

        private DecoratorInvocationResult<T, R> Complete<R>(DecoratorInvocationContext<T> context, R? value, bool shortCircuited)
        {
            context.Enter(DecoratorPhase.End, value);
            RunHandlers(context);
            if (context.Rejected)
            {
                return Refuse<R>(context);
            }

            if (context.ResultOverridden && TryConvert(context, out R? replaced))
            {
                value = replaced;
            }

            Raise(MethodEnd, context, value);
            return new DecoratorInvocationResult<T, R>(this, value)
            {
                Method = context.Method,
                MethodName = context.MethodName,
                ShortCircuited = shortCircuited
            };
        }

        private DecoratorInvocationResult<T, R> Fail<R>(DecoratorInvocationContext<T> context, Exception thrown)
        {
            // Reflection wraps what the method threw; handlers and callers should see the original.
            Exception exception = thrown is TargetInvocationException && thrown.InnerException != null
                ? thrown.InnerException
                : thrown;

            context.Enter(DecoratorPhase.Error, null);
            context.Exception = exception;
            Raise(MethodError, context, null);
            RunHandlers(context);
            if (context.Rejected)
            {
                return Refuse<R>(context);
            }

            R? fallback = default;
            bool handled = context.ResultOverridden && TryConvert(context, out fallback);
            return new DecoratorInvocationResult<T, R>(this, handled ? fallback : default)
            {
                Method = context.Method,
                MethodName = context.MethodName,
                Exception = exception,
                Handled = handled
            };
        }

        // A rejection is final: it becomes the invocation's failure, and no handler gets to suppress it.
        private DecoratorInvocationResult<T, R> Refuse<R>(DecoratorInvocationContext<T> context)
        {
            bool alreadyFailing = context.Phase == DecoratorPhase.Error;
            context.Exception = context.Rejection;
            if (!alreadyFailing)
            {
                context.Phase = DecoratorPhase.Error;
                Raise(MethodError, context, null);
            }

            return new DecoratorInvocationResult<T, R>(this)
            {
                Method = context.Method,
                MethodName = context.MethodName,
                Exception = context.Rejection,
                Rejected = true
            };
        }

        private DecoratorInvocationResult<T, R> NotFound<R>(string methodName, object?[] args)
        {
            DecoratorInvocationContext<T> context = CreateContext(methodName, null, args);
            Raise(MethodNotFound, context, null);
            return new DecoratorInvocationResult<T, R>(this)
            {
                MethodName = methodName,
                Exception = new MissingMethodException(Instance.GetType().FullName, methodName)
            };
        }

        private void RunHandlers(DecoratorInvocationContext<T> context)
        {
            foreach (DecoratorSubscriptions shared in _sharedSubscriptions)
            {
                foreach (Func<DecoratorInvocationContext, object?> handler in shared.Get(context.Phase, context.MethodName))
                {
                    if (context.Rejected)
                    {
                        return;
                    }

                    RunHandler(context, () => handler(context));
                }
            }

            foreach (DecoratorHandlerRegistry<DecoratorInvocationContext<T>> registration in _registrationHandlers)
            {
                foreach (Func<DecoratorInvocationContext<T>, object?> handler in registration.Get(context.Phase, context.MethodName))
                {
                    if (context.Rejected)
                    {
                        return;
                    }

                    RunHandler(context, () => handler(context));
                }
            }

            foreach (Func<DecoratorInvocationContext<T>, object?> handler in _handlers.Get(context.Phase, context.MethodName))
            {
                if (context.Rejected)
                {
                    return;
                }

                RunHandler(context, () => handler(context));
            }
        }

        private void RunHandler(DecoratorInvocationContext<T> context, Func<object?> handler)
        {
            try
            {
                object? returned = handler();
                if (returned != null && Awaitable.Is(returned.GetType()))
                {
                    // A handler that hands back something awaitable does its work asynchronously, after this
                    // point. It is not a result, and treating it as one would skip a void method's call without
                    // a word; letting the call go ahead would let a guard written that way fail open. So the
                    // call fails closed, and the author finds out on the first call rather than in a log.
                    RejectAwaitable(context, "returned", returned);
                    return;
                }

                if (returned != null && !context.Rejected)
                {
                    context.Result = returned;
                }
            }
            catch (DecoratorRejectionException rejection)
            {
                // The one exception a handler throws on purpose to stop the call; it goes to the caller.
                context.Reject(rejection);
            }
            catch (Exception ex)
            {
                this._logger?.Error("Exception invoking {0} handler for method {1} on type of Decorator<{2}>: {3}", ex, context.Phase.ToString().ToUpperInvariant(), context.MethodName, typeof(T).Name, ex.Message);
            }

            // The same thing put on the context by hand, checked after the catch so a handler that set it
            // and then threw is still caught. Read once: a handler that kept the context could change it
            // from another thread between reads.
            object? byHand = context.Result;
            if (!context.Rejected && context.ResultOverridden && byHand != null && Awaitable.Is(byHand.GetType()))
            {
                RejectAwaitable(context, "set Result to", byHand);
            }
        }

        // Rejected before it is logged, so a logger that throws cannot undo the rejection. On error the
        // service's own exception rides along as the inner one rather than being dropped.
        private void RejectAwaitable(DecoratorInvocationContext<T> context, string how, object awaitable)
        {
            string outcome = context.Phase switch
            {
                DecoratorPhase.Start => "the call was rejected and the method did not run",
                DecoratorPhase.End => "the method's result was discarded and the call rejected",
                _ => "the call was rejected; the method's own exception is the inner exception"
            };
            string message = $"{context.Phase.ToString().ToUpperInvariant()} handler for method {context.MethodName} on type of Decorator<{typeof(T).Name}> {how} a {Awaitable.Describe(awaitable.GetType())}; handlers run synchronously, so {outcome}";
            context.Reject(context.Exception == null ? new DecoratorException(message) : new DecoratorException(message, context.Exception));
            try
            {
                this._logger?.Error(message);
            }
            catch (Exception)
            {
                // The rejection stands whether or not it could be logged.
            }
        }

        private bool TryConvert<R>(DecoratorInvocationContext<T> context, out R? value)
        {
            object? candidate = context.Result;
            if (candidate is R typed)
            {
                value = typed;
                return true;
            }

            value = default;
            if (candidate == null)
            {
                return true;
            }

            this._logger?.Error("{0} handler for method {1} on type of Decorator<{2}> supplied a {3} where a {4} is required; the value was ignored", context.Phase.ToString().ToUpperInvariant(), context.MethodName, typeof(T).Name, candidate.GetType().Name, typeof(R).Name);
            return false;
        }

        private void Raise(EventHandler<DecoratorEventArgs<T>>? handlers, DecoratorInvocationContext<T> context, object? result)
        {
            if (handlers == null)
            {
                return;
            }

            try
            {
                handlers.Invoke(this, new DecoratorEventArgs<T>(this)
                {
                    MethodName = context.MethodName,
                    Args = context.Args,
                    Result = result,
                    Exception = context.Exception
                });
            }
            catch (Exception ex)
            {
                this._logger?.Error("Exception raising {0} event for method {1} on type of Decorator<{2}>: {3}", ex, context.Phase.ToString().ToUpperInvariant(), context.MethodName, typeof(T).Name, ex.Message);
            }
        }

        private readonly record struct MethodCacheKey(Type Type, string MethodName);

        private readonly record struct ImplementationKey(Type Type, MethodInfo Method);
    }
}
