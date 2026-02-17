namespace Bam.Generators.Tests.TestClasses
{
    public class TestCar
    {
        public TestCar() { }
        public ulong Id { get; set; }
        public string Make { get; set; } = null!;
        public string Model { get; set; } = null!;

        public ulong TestPersonId { get; set; }

        public virtual TestPerson TestPerson { get; set; } = null!;
    }
}
