using Amazon.Runtime.Internal.Util;
using Bam.Data.Schema;
using Bam.Net.CommandLine;
using Bam.Net.CoreServices;
using Bam.Net.Data.Repositories;
using Bam.Services;
using Bam.Testing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bam.Generators.Tests
{
    [UnitTestMenu("DaoRepository Unit Tests", Selector = "drut")]
    public class SchemaRepositoryGeneratorShould : UnitTestMenuContainer
    {
        public SchemaRepositoryGeneratorShould(ServiceRegistry serviceRegistry) : base(serviceRegistry)
        {
        }

        [UnitTest]
        public void ShouldGenerateSchemaRepository()
        {

        }

        public override ServiceRegistry Configure(ServiceRegistry serviceRegistry)
        {
            return serviceRegistry
                .For<ILogger>().Use<ConsoleLogger>();
        }
    }
}
