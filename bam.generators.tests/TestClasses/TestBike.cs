namespace Bam.Generators.Tests.TestClasses
{
    /// <summary>
    /// A child whose parent is optional: the parent property is declared nullable.
    /// </summary>
    public class TestBike
    {
        public TestBike() { }
        public ulong Id { get; set; }
        public string Name { get; set; } = null!;

        public ulong? TestGarageId { get; set; }

        public virtual TestGarage? TestGarage { get; set; }
    }
}
