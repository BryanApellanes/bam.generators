namespace Bam.Generators.Decorators.Tests.Fixtures
{
    public class EchoService : IEchoService
    {
        public int Calls { get; private set; }

        public string Message(string message)
        {
            Calls++;
            return message;
        }
    }
}
