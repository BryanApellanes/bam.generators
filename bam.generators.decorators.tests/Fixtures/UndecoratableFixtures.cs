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

    public class InitOnlyService : IInitOnlyService
    {
        public string Value { get; init; } = string.Empty;
    }

    /// <summary>An open generic service; only a closed construction of it can be decorated.</summary>
    public interface IBoxService<TItem>
    {
        TItem Unbox();
    }

    public class BoxService<TItem> : IBoxService<TItem>
    {
        private readonly TItem _item;

        public BoxService(TItem item)
        {
            _item = item;
        }

        public TItem Unbox() => _item;
    }
}
