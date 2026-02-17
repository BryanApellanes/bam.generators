using Bam.Data.Schema;
using Bam.Data;
using Bam.Data.Repositories;
using Bam.DependencyInjection;
using Bam.Logging;
using Bam.Services;

namespace Bam.Generators
{
    /// <summary>
    /// A DaoRepository implementation that uses a default service registry to resolve
    /// its schema provider, DAO generator, and wrapper generator dependencies.
    /// </summary>
    public class DefaultDaoRepository : DaoRepository
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DefaultDaoRepository"/> class, resolving
        /// dependencies from a default service registry that uses Handlebars-based code generation.
        /// </summary>
        /// <param name="database">An optional database instance. If provided, overrides the default database.</param>
        /// <param name="logger">An optional logger instance. If provided, overrides the default logger.</param>
        public DefaultDaoRepository(IDatabase? database = null, ILogger? logger = null) : base(ServiceRegistry!.Get<ISchemaProvider>(), ServiceRegistry!.Get<IDaoGenerator>(), ServiceRegistry!.Get<IWrapperGenerator>())
        {
            if(database != null)
            {
                this.Database = database;
            }

            if(logger != null)
            {
                this.Logger = logger;
            }
        }

        static ServiceRegistry? _serviceRegistry;
        static readonly object _serviceRegistryLock = new object();
        private static ServiceRegistry? ServiceRegistry
        {
            get
            {
                return _serviceRegistryLock.DoubleCheckLock(ref _serviceRegistry, () => new ServiceRegistry()
                    .For<IDaoCodeWriter>().Use<HandlebarsCSharpDaoCodeWriter>()
                    .For<ISchemaProvider>().Use<SchemaProvider>()
                    .For<IDaoGenerator>().Use<DaoGenerator>()
                    .For<IWrapperGenerator>().Use<HandlebarsWrapperGenerator>());
            }
        }
    }
}
