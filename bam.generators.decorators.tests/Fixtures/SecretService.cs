namespace Bam.Generators.Decorators.Tests.Fixtures
{
    /// <summary>
    /// A public interface whose only implementation is internal, as is common behind a public contract. Generated
    /// code cannot name <see cref="SecretService"/>, so the interface is decorated alone (bam.generators#16):
    /// <c>ISecretServiceDecorator</c>, typed <c>Decorator&lt;ISecretService, ISecretService&gt;</c>. Its method
    /// names are unique among the fixtures so its generated hooks never overload another decorator's.
    /// </summary>
    public interface ISecretService
    {
        string Reveal(string key);
    }

    internal sealed class SecretService : ISecretService
    {
        public int Reveals { get; private set; }

        public string Reveal(string key)
        {
            Reveals++;
            return "secret:" + key;
        }
    }
}
