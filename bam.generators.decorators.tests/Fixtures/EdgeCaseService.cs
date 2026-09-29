using System.Collections.Specialized;

namespace Bam.Generators.Decorators.Tests.Fixtures
{
    /// <summary>A generic type with nested types, which are named level by level in generated source.</summary>
    public class Wrapper<TOuter>
    {
        public class Item
        {
            public TOuter? Value { get; set; }
        }

        public class Pair<TInner>
        {
            public TOuter? First { get; set; }

            public TInner? Second { get; set; }
        }
    }

    public class EdgeCaseBase
    {
    }

    /// <summary>
    /// The shapes that are easy to get wrong in generated source: nullable generic parameters under each kind
    /// of constraint, members and parameters named with C# keywords, overloads that differ only by
    /// <c>object</c> and <c>string</c>, and types nested in generic types.
    /// </summary>
    public interface IEdgeCaseService
    {
        string @event { get; set; }

        TItem? Locate<TItem>(string key) where TItem : class;

        TValue? Maybe<TValue>(TValue? fallback);

        TNumber Twice<TNumber>(TNumber number) where TNumber : struct;

        TBase Same<TBase>(TBase item) where TBase : EdgeCaseBase;

        string @default(string @class);

        string Log(object value);

        string Log(string? value);

        Dictionary<string, int>.KeyCollection Keys();

        Wrapper<int>.Item Wrap(Wrapper<string>.Pair<bool> pair);
    }

    public class EdgeCaseService : IEdgeCaseService
    {
        public string @event { get; set; } = "none";

        public TItem? Locate<TItem>(string key) where TItem : class
        {
            return key as TItem;
        }

        public TValue? Maybe<TValue>(TValue? fallback)
        {
            return fallback;
        }

        public TNumber Twice<TNumber>(TNumber number) where TNumber : struct
        {
            return number;
        }

        public TBase Same<TBase>(TBase item) where TBase : EdgeCaseBase
        {
            return item;
        }

        public string @default(string @class)
        {
            return "default:" + @class;
        }

        public string Log(object value)
        {
            return "object:" + value;
        }

        public string Log(string? value)
        {
            return "string:" + (value ?? "null");
        }

        public Dictionary<string, int>.KeyCollection Keys()
        {
            return new Dictionary<string, int> { { "one", 1 } }.Keys;
        }

        public Wrapper<int>.Item Wrap(Wrapper<string>.Pair<bool> pair)
        {
            return new Wrapper<int>.Item { Value = pair.Second ? 1 : 0 };
        }
    }

    /// <summary>
    /// Takes its base class from an assembly that nothing in its signature mentions
    /// (<c>System.Collections.Specialized</c>). A compiler given only the assemblies of the types a decorator
    /// names cannot compile one for it.
    /// </summary>
    public interface IElsewhereService
    {
        string Here();
    }

    public class ElsewhereService : NameValueCollection, IElsewhereService
    {
        public string Here() => "here";
    }

    /// <summary>Inherits an interface from another assembly (<c>System.ObjectModel</c>).</summary>
    public interface IObservedService : System.ComponentModel.INotifyPropertyChanging
    {
        string Observe();
    }

    public class ObservedService : IObservedService
    {
        public event System.ComponentModel.PropertyChangingEventHandler? PropertyChanging;

        public string Observe()
        {
            PropertyChanging?.Invoke(this, new System.ComponentModel.PropertyChangingEventArgs(nameof(Observe)));
            return "observed";
        }
    }

    /// <summary>Counts its own constructions, so tests can tell a transient service from a single instance.</summary>
    public interface ICounterService
    {
        int Id { get; }

        int Next();
    }

    public class CounterService : ICounterService
    {
        private static int _constructed;
        private int _count;

        public CounterService()
        {
            Id = Interlocked.Increment(ref _constructed);
        }

        public static int Constructed => _constructed;

        public int Id { get; }

        public int Next()
        {
            return ++_count;
        }
    }
}
