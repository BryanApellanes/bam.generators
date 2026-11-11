namespace Bam.Generators.Tests.TestClasses
{
    /// <summary>
    /// A child that has the foreign key id but no property that references its parent.
    /// </summary>
    public class TestTool
    {
        public TestTool() { }
        public ulong Id { get; set; }
        public string Name { get; set; } = null!;

        public ulong TestShedId { get; set; }
    }
}
