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

    /// <summary>
    /// Decorated only by the test that plants a template in the working directory, so its decorator is
    /// certain to be compiled there rather than found already loaded.
    /// </summary>
    public interface IPingService
    {
        string Ping();
    }

    public class PingService : IPingService
    {
        public string Ping() => "pong";
    }

    /// <summary>Decorated only by the test that checks no template directory is created.</summary>
    public interface IPongService
    {
        string Pong();
    }

    public class PongService : IPongService
    {
        public string Pong() => "ping";
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
