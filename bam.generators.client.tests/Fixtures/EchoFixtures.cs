using Bam.Server;

namespace Bam.Generators.Client.Tests.Fixtures
{
    /// <summary>Interface used to exercise Interface generation mode.</summary>
    public interface IEchoService
    {
        string Echo(string message);
    }

    /// <summary>A [WebService] with virtual sync, async, and void methods — valid for Subclass mode.</summary>
    [WebService]
    public class EchoService : IEchoService
    {
        public virtual string Echo(string message) => message;

        public virtual global::System.Threading.Tasks.Task<int> AddAsync(int a, int b) =>
            global::System.Threading.Tasks.Task.FromResult(a + b);

        public virtual void Ping()
        {
        }
    }

    /// <summary>A [WebService] whose method is NOT virtual — Subclass mode must reject it.</summary>
    [WebService]
    public class SealedEchoService
    {
        public string Echo(string message) => message;
    }

    /// <summary>A plain type with no [WebService] — generation must reject it.</summary>
    public class NotAWebService
    {
        public virtual void Foo()
        {
        }
    }
}
