namespace Bam.Generators.Tests.TestClasses
{
    public class TestShed
    {
        public TestShed()
        {
            this.TestTools = new List<TestTool>();
        }

        public ulong Id { get; set; }
        public string Name { get; set; } = null!;

        public virtual List<TestTool> TestTools { get; set; }
    }
}
