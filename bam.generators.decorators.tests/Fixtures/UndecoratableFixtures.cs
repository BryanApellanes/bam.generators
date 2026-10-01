namespace Bam.Generators.Decorators.Tests.Fixtures
{
    /// <summary>Does not implement <see cref="IEchoService"/> — generation must reject the pair.</summary>
    public class NotAnEchoService
    {
        public string Message(string message) => message;
    }

    /// <summary>Not visible outside this assembly — generated code could not reference it.</summary>
    internal interface IHiddenService
    {
        void Hide();
    }

    internal class HiddenService : IHiddenService
    {
        public void Hide()
        {
        }
    }

    /// <summary>Declares an init-only property, which a decorator cannot forward.</summary>
    public interface IInitOnlyService
    {
        string Value { get; init; }
    }

    /// <summary>Pointer-typed members: generated code is not unsafe, so the model refuses them up front.</summary>
    public unsafe interface IPointerService
    {
        int* Address();
    }

    public unsafe class PointerService : IPointerService
    {
        public int* Address()
        {
            return null;
        }
    }

    /// <summary>A function pointer: IsPointer is false on it, IsFunctionPointer is true.</summary>
    public unsafe interface IFunctionPointerService
    {
        delegate*<int, void> Callback();
    }

    public unsafe class FunctionPointerService : IFunctionPointerService
    {
        public delegate*<int, void> Callback()
        {
            return null;
        }
    }

    /// <summary>A pointer hidden inside a generic argument.</summary>
    public unsafe interface IPointerArgumentService
    {
        List<nint> Addresses(List<int*[]> pointers);
    }

    public unsafe class PointerArgumentService : IPointerArgumentService
    {
        public List<nint> Addresses(List<int*[]> pointers)
        {
            return new List<nint>();
        }
    }

    /// <summary>
    /// A method whose result is itself awaitable gets no typed Func hook: a handler could never hand such a
    /// result back. Its sibling with a plain result keeps one.
    /// </summary>
    public interface INestedTaskService
    {
        Task<Task<int>> Nested();

        Task<int> Plain();
    }

    public class NestedTaskService : INestedTaskService
    {
        public Task<Task<int>> Nested()
        {
            return Task.FromResult(Task.FromResult(1));
        }

        public Task<int> Plain()
        {
            return Task.FromResult(1);
        }
    }

    public class InitOnlyService : IInitOnlyService
    {
        public string Value { get; init; } = string.Empty;
    }

    /// <summary>
    /// An open generic service; only a closed construction of it can be decorated. Its members carry
    /// <c>T?</c> in every position the renderer has to read from the open type: bare, in a task, in a list.
    /// </summary>
    public interface IBoxService<TItem>
    {
        TItem Unbox();

        TItem? Peek();

        Task<TItem?> UnboxAsync();

        List<TItem?> All();
    }

    public class BoxService<TItem> : IBoxService<TItem>
    {
        private readonly TItem _item;

        public BoxService(TItem item)
        {
            _item = item;
        }

        public TItem Unbox() => _item;

        public TItem? Peek() => _item;

        public Task<TItem?> UnboxAsync() => Task.FromResult<TItem?>(_item);

        public List<TItem?> All() => new List<TItem?> { _item };
    }

    /// <summary>
    /// <c>T?</c> on generic methods, in every position: bare, in a task, a value task, a list, an array and a
    /// delegate, under the constraints reflection cannot decide nullability for.
    /// </summary>
    public interface INullableShapesService
    {
        Task<T?> FindAsync<T>(string key) where T : new();

        ValueTask<T?> PeekAsync<T>(string key);

        Task<List<T?>> AllAsync<T>() where T : notnull;

        T?[] Some<T>(int count);

        Func<string?, T?> Finder<T>();

        Task<string?> NameAsync();
    }

    public class NullableShapesService : INullableShapesService
    {
        public Task<T?> FindAsync<T>(string key) where T : new() => Task.FromResult<T?>(default);

        public ValueTask<T?> PeekAsync<T>(string key) => new ValueTask<T?>(default(T));

        public Task<List<T?>> AllAsync<T>() where T : notnull => Task.FromResult(new List<T?>());

        public T?[] Some<T>(int count) => new T?[count];

        public Func<string?, T?> Finder<T>() => key => default;

        public Task<string?> NameAsync() => Task.FromResult<string?>(null);
    }

    /// <summary>A second implementation of <see cref="IEchoService"/>, to decorate the interface under two types.</summary>
    public class LoudEchoService : IEchoService
    {
        public string Message(string message) => message.ToUpperInvariant() + "!";
    }
}
