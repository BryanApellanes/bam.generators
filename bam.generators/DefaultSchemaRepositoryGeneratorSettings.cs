using Bam.Data;
using Bam.Data.Schema;
using Bam.CoreServices;
using Bam.Data.Repositories;
using Bam.Data.Schema;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bam.Generators
{
    public class DefaultSchemaRepositoryGeneratorSettings : SchemaRepositoryGeneratorSettings
    {
        public DefaultSchemaRepositoryGeneratorSettings(IDaoRepoGenerationConfig daoConfig) : base(ServiceRegistry.Get<IDaoCodeWriter>(), ServiceRegistry.Get<IDaoTargetStreamResolver>(),ServiceRegistry.Get<IWrapperGenerator>())
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
