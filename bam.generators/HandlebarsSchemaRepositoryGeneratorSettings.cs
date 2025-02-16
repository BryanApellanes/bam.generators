using Bam.Data;
using Bam.Data.Schema;
using Bam.Data.Repositories;
using Bam.DependencyInjection;
using Bam.Services;

namespace Bam.Generators
{
    public class HandlebarsSchemaRepositoryGeneratorSettings : SchemaRepositoryGeneratorSettings
    {
        public HandlebarsSchemaRepositoryGeneratorSettings(IDaoRepoGenerationConfig daoConfig) : base(ServiceRegistry.Get<IDaoCodeWriter>(), ServiceRegistry.Get<IDaoTargetStreamResolver>(),ServiceRegistry.Get<IWrapperGenerator>())
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
