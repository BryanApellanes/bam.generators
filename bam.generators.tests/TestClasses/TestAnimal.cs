namespace Bam.Generators.Tests.TestClasses
{
    public class TestAnimal
    {
        public TestAnimal() { }

        public ulong Id { get; set; }
        public string Name { get; set; }

        public virtual List<TestPerson> Owners { get; set; }
    }
}
