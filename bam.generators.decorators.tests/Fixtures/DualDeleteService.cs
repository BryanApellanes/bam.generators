namespace Bam.Generators.Decorators.Tests.Fixtures
{
    /// <summary>Marks a service interface member a guard must check (bam.generators#35).</summary>
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class AdminOnlyAttribute : Attribute
    {
    }

    /// <summary>A service whose one member is guarded on the interface.</summary>
    public interface IDeleteService
    {
        [AdminOnly]
        string Delete(string key);
    }

    /// <summary>
    /// Implements <see cref="IDeleteService.Delete"/> explicitly and also has a public, unguarded
    /// <c>Delete</c> the interface does not declare, so a call by name resolves to a method that implements no
    /// member of the service interface (bam.generators#35).
    /// </summary>
    public class DualDeleteService : IDeleteService
    {
        string IDeleteService.Delete(string key)
        {
            return "interface:" + key;
        }

        public string Delete(string key)
        {
            return "public:" + key;
        }
    }
}
