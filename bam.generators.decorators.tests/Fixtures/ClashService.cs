namespace Bam.Generators.Decorators.Tests.Fixtures
{
    public interface ICounted
    {
        int Count { get; }
    }

    public interface ICounter
    {
        int Count();

        string Describe();
    }

    public interface IDescribed
    {
        string Describe();
    }

    /// <summary>
    /// Inherits members that cannot all be public members of one class: a property and a method both named
    /// <c>Count</c>, and <c>Describe()</c> declared identically by two interfaces. It has no decorator generated
    /// ahead of time, so decorating it proves the generated source for these clashes compiles.
    /// </summary>
    public interface IClashService : ICounted, ICounter, IDescribed
    {
    }

    public class ClashService : IClashService
    {
        public int Count => 7;

        int ICounter.Count() => 11;

        public string Describe() => "clash";
    }
}
