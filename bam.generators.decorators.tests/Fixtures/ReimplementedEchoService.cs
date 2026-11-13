namespace Bam.Generators.Decorators.Tests.Fixtures
{
    /// <summary>
    /// An <see cref="EchoService"/> that re-implements <see cref="IEchoService"/> with a method hiding the
    /// base one, so the interface map of the runtime type points at a method <see cref="EchoService"/> does
    /// not declare (bam.generators#24).
    /// </summary>
    public class ReimplementedEchoService : EchoService, IEchoService
    {
        public new string Message(string message)
        {
            return message.ToUpperInvariant();
        }
    }

    /// <summary>A second interface over the same shape, for an implementation queued under two interfaces.</summary>
    public interface IShout
    {
        string Shout(string message);
    }

    /// <summary>One implementation of two service interfaces.</summary>
    public class EchoShout : IEchoService, IShout
    {
        public string Message(string message)
        {
            return message;
        }

        public string Shout(string message)
        {
            return message.ToUpperInvariant();
        }
    }
}
