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
        /// return. A type reflection cannot answer for is treated as awaitable. Definite answers are cached
        /// per type; a failed lookup is not, so it is asked again next time.
        /// </summary>
        public static bool Is(Type type)
        {
            try
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
            catch (Exception)
            {
                // A type reflection cannot answer for (an ambiguous match, a type that fails to load) is
                // treated as awaitable, so the check fails closed rather than open. The factory threw, so
                // nothing was cached: if the type becomes answerable later, it is answered then.
                return true;
            }
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
                        // A plain Task is a Task<VoidTaskResult> underneath; nobody wrote that.
                        Type result = candidate.GetGenericArguments()[0];
                        return result.Name == "VoidTaskResult" ? "Task" : $"Task<{result.Name}>";
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
