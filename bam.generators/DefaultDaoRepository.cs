using Bam.Data.Schema;
using Bam.CoreServices;
using Bam.Data;
using Bam.Data.Repositories;
using Bam.Data.Schema;
using Bam.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bam.Generators
{
    public class DefaultDaoRepository : DaoRepository
    {
        public DefaultDaoRepository(IDatabase? database = null, ILogger? logger = null) : base(ServiceRegistry.Get<ISchemaProvider>(), ServiceRegistry.Get<IDaoGenerator>(), ServiceRegistry.Get<IWrapperGenerator>())
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
