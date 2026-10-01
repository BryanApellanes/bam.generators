using System.Collections.Concurrent;
using System.Reflection;

namespace Bam.Generators.Decorators
{
    /// <summary>
    /// Recognises the types a handler must not hand back: anything <c>await</c> would accept. Handlers run
    /// synchronously, so whatever such a value would decide comes too late.
    /// </summary>
    internal static class Awaitable
    {
        private static readonly ConcurrentDictionary<Type, bool> _known = new ConcurrentDictionary<Type, bool>();

        /// <summary>
        /// Gets a value indicating whether <paramref name="type"/> is <c>Task</c>, <c>Task&lt;T&gt;</c>,
        /// <c>ValueTask</c>, <c>ValueTask&lt;T&gt;</c>, or anything else with a public parameterless
        /// <c>GetAwaiter()</c>, which is what <c>ConfigureAwait</c>, <c>Task.Yield</c> and custom awaitables
        /// return. Cached per type.
        /// </summary>
        public static bool Is(Type type)
        {
            return _known.GetOrAdd(type, static candidate =>
            {
                if (typeof(Task).IsAssignableFrom(candidate) || candidate == typeof(ValueTask))
                {
                    return true;
                }

                if (candidate.IsGenericType && candidate.GetGenericTypeDefinition() == typeof(ValueTask<>))
                {
                    return true;
                }

                return candidate.GetMethod("GetAwaiter", BindingFlags.Public | BindingFlags.Instance, Type.EmptyTypes) != null;
            });
        }

        /// <summary>
        /// Names <paramref name="type"/> the way a handler author would recognise it: <c>Task</c> or
        /// <c>Task&lt;string&gt;</c> for any task subclass, the compiler's state-machine box included.
        /// </summary>
        public static string Describe(Type type)
        {
            if (typeof(Task).IsAssignableFrom(type))
            {
                Type? candidate = type;
                while (candidate != null)
                {
                    if (candidate.IsGenericType && candidate.GetGenericTypeDefinition() == typeof(Task<>))
                    {
                        return $"Task<{candidate.GetGenericArguments()[0].Name}>";
                    }

                    candidate = candidate.BaseType;
                }

                return "Task";
            }

            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ValueTask<>))
            {
                return $"ValueTask<{type.GetGenericArguments()[0].Name}>";
            }

            return type.Name;
        }
    }
}
