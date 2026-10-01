using System.Runtime.CompilerServices;

namespace Bam.Generators.Decorators.Tests.Fixtures
{
    /// <summary>
    /// A type reflection cannot answer for: two GetAwaiter methods make the lookup throw
    /// <see cref="System.Reflection.AmbiguousMatchException"/>, so the awaitable check has to fail closed.
    /// </summary>
    public class AmbiguousAwaitable
    {
        public TaskAwaiter GetAwaiter()
        {
            return Task.CompletedTask.GetAwaiter();
        }

        public TaskAwaiter GetAwaiter<T>()
        {
            return Task.CompletedTask.GetAwaiter();
        }
    }
}
