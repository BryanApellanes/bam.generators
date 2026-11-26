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

    /// <summary>An interface a <c>Decorator&lt;IDeleteService, …&gt;</c> does not serve.</summary>
    public interface IPurgeService
    {
        string Purge(string key);
    }

    /// <summary>
    /// Implements <see cref="IDeleteService"/> and, explicitly, <see cref="IPurgeService"/>, so a call by name to
    /// <c>Purge</c> resolves to a method of an interface the pair does not serve (bam.generators#36 review).
    /// </summary>
    public class ForeignExplicitService : IDeleteService, IPurgeService
    {
        public string Delete(string key)
        {
            return "delete:" + key;
        }

        string IPurgeService.Purge(string key)
        {
            return "purge:" + key;
        }
    }
}
