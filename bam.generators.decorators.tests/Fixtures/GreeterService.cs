namespace Bam.Generators.Decorators.Tests.Fixtures
{
    /// <summary>
    /// A service with NO decorator generated ahead of time, so decorating it exercises the
    /// generate-and-compile-at-runtime path of <see cref="DecoratorTypeResolver"/>.
    /// </summary>
    public interface IGreeterService
    {
        string Greet(string name);
    }

    public class GreeterService : IGreeterService
    {
        public string Greet(string name) => $"Hello, {name}";
    }

    /// <summary>A second service that happens to share a method name with <see cref="IEchoService"/>.</summary>
    public interface IShoutService
    {
        string Message(string message);
    }

    public class ShoutService : IShoutService
    {
        public string Message(string message) => message.ToUpperInvariant();
    }
}
