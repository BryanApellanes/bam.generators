using Bam.Data;
using Bam.Data.Schema;
using Bam.Data.Repositories;
using Bam.DependencyInjection;
using Bam.Services;

namespace Bam.Generators
{
    /// <summary>
    /// Settings for <see cref="HandlebarsSchemaRepositoryGenerator"/> that resolves its dependencies
    /// (code writer, stream resolver, wrapper generator) from a default service registry configured
    /// for Handlebars-based code generation.
    /// </summary>
    public class HandlebarsSchemaRepositoryGeneratorSettings : SchemaRepositoryGeneratorSettings
    {
        /// <summary>
        /// Initializes a new instance with dependencies resolved from a default service registry.
        /// </summary>
        /// <param name="daoConfig">The DAO repository generation configuration.</param>
        public HandlebarsSchemaRepositoryGeneratorSettings(IDaoRepoGenerationConfig daoConfig) : base(ServiceRegistry!.Get<IDaoCodeWriter>(), ServiceRegistry!.Get<IDaoTargetStreamResolver>(),ServiceRegistry!.Get<IWrapperGenerator>())
        {
            this.DaoRepoGenerationConfig = daoConfig;
        }

        static ServiceRegistry? _serviceRegistry;
        static object _serviceRegistryLock = new object();
        private static ServiceRegistry? ServiceRegistry
        {
            get
            {
                return _serviceRegistryLock.DoubleCheckLock(ref _serviceRegistry, () =>
                {
                    return new ServiceRegistry()
                        .For<IDaoCodeWriter>().Use<HandlebarsCSharpDaoCodeWriter>()
                        .For<ISchemaProvider>().Use<SchemaProvider>()
                        .For<IDaoTargetStreamResolver>().Use<FsDaoTargetStreamResolver>()
                        .For<IWrapperGenerator>().Use<HandlebarsWrapperGenerator>();
                });
            }
        }
    }
}
