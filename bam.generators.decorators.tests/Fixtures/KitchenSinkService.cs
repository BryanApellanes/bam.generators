namespace Bam.Generators.Decorators.Tests.Fixtures
{
    /// <summary>A base interface, so the generator has inherited members to discover.</summary>
    public interface INamedService
    {
        string Name { get; set; }
    }

    /// <summary>
    /// Exercises every member shape the generator handles: properties, an indexer, an event, overloads,
    /// nullable annotations, the four async return shapes, by-ref parameters, a generic method, a method whose
    /// name collides with a decorator base member, and a keyword-named parameter.
    /// </summary>
    public interface IKitchenSinkService : INamedService
    {
        event EventHandler? Changed;

        int Count { get; }

        string this[int index] { get; set; }

        int Add(int a, int b);

        double Add(double a, double b);

        void Reset();

        string? Find(string? key);

        Task<int> AddAsync(int a, int b);

        Task ResetAsync();

        ValueTask<string> DescribeAsync(string subject);

        ValueTask FlushAsync();

        bool TryParse(string text, out int value);

        TItem Echo<TItem>(TItem item) where TItem : class;

        string Fail(string message);

        Task<string> FailAsync(string message);

        string Invoke(string @event);
    }

    public class KitchenSinkService : IKitchenSinkService
    {
        private readonly Dictionary<int, string> _items = new Dictionary<int, string>();

        public event EventHandler? Changed;

        public string Name { get; set; } = "sink";

        public int Count { get; private set; }

        public int Flushes { get; private set; }

        public string this[int index]
        {
            get => _items.TryGetValue(index, out string? value) ? value : string.Empty;
            set => _items[index] = value;
        }

        public int Add(int a, int b)
        {
            Count++;
            return a + b;
        }

        public double Add(double a, double b)
        {
            Count++;
            return a + b;
        }

        public void Reset()
        {
            Count = 0;
            Changed?.Invoke(this, EventArgs.Empty);
        }

        public string? Find(string? key)
        {
            return key == null ? null : "found:" + key;
        }

        public async Task<int> AddAsync(int a, int b)
        {
            await Task.Yield();
            Count++;
            return a + b;
        }

        public async Task ResetAsync()
        {
            await Task.Yield();
            Count = 0;
        }

        public async ValueTask<string> DescribeAsync(string subject)
        {
            await Task.Yield();
            return "about " + subject;
        }

        public async ValueTask FlushAsync()
        {
            await Task.Yield();
            Flushes++;
        }

        public bool TryParse(string text, out int value)
        {
            return int.TryParse(text, out value);
        }

        public TItem Echo<TItem>(TItem item) where TItem : class
        {
            return item;
        }

        public string Fail(string message)
        {
            throw new InvalidOperationException(message);
        }

        public async Task<string> FailAsync(string message)
        {
            await Task.Yield();
            throw new InvalidOperationException(message);
        }

        public string Invoke(string @event)
        {
            return "invoked:" + @event;
        }
    }
}
