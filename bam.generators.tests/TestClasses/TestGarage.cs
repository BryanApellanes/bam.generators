namespace Bam.Generators.Tests.TestClasses
{
    public class TestGarage
    {
        public TestGarage()
        {
            this.TestBikes = new List<TestBike>();
        }

        public ulong Id { get; set; }
        public string Name { get; set; } = null!;

        public virtual List<TestBike> TestBikes { get; set; }
    }
}
