using Amazon.Runtime.Internal.Util;
using Bam.Application;
using Bam.Console;
using Bam.Data.Schema;
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
    [UnitTestMenu("DefaultSchemaRepositoryGenerator Should", Selector = "srgut")]
    public class DefaultSchemaRepositoryGeneratorShould : UnitTestMenuContainer
    {
        public DefaultSchemaRepositoryGeneratorShould(ServiceRegistry serviceRegistry) : base(serviceRegistry)
        {
        }

        [UnitTest]
        public void ShouldGenerateSchemaRepository()
        {
            DefaultSchemaRepositoryGenerator generator = new DefaultSchemaRepositoryGenerator(new ConsoleLogger());
            generator.GenerateSource();
        }
    }
}
