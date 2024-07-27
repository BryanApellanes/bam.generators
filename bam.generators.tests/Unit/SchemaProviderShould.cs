using Bam.Console;
using Bam.Data.Schema;
using Bam.Generators.Tests.TestClasses;
using Bam;
using Bam.CoreServices;
using Bam.Data.Repositories;
using Bam.Test;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bam.Generators.Tests.Unit
{
    [UnitTestMenu("SchemaGenerator Should", Selector = "sgs")]
    public class SchemaProviderShould : UnitTestMenuContainer
    {
        public SchemaProviderShould(ServiceRegistry serviceRegistry) : base(serviceRegistry)
        {
        }

        [Test]
        public void ShouldGenerateTypeSchema()
        {
            string testName = 32.RandomLetters();
            SchemaProvider schemaGenerator = Get<SchemaProvider>();
            TypeSchema typeSchema = schemaGenerator.CreateTypeSchema(testName, typeof(TestPerson));
            typeSchema.Name.ShouldBe(testName);
            typeSchema.Tables.Count.ShouldBeEqualTo(3);

            Message.PrintLine(typeSchema.ToString(), ConsoleColor.DarkYellow);
        }

        public override ServiceRegistry Configure(ServiceRegistry serviceRegistry)
        {
            serviceRegistry = serviceRegistry
                .For<ISchemaTempPathProvider>().Use<SchemaTempPathProvider>()
                .For<ITypeTableNameProvider>().Use<DaoSuffixTypeTableNameProvider>()
                .For<SchemaProvider>().Use<SchemaProvider>();

            return serviceRegistry
                .For<SchemaProvider>().Use(
                    new SchemaProvider(serviceRegistry.Get<ITypeTableNameProvider>(), serviceRegistry.Get<ISchemaTempPathProvider>())
                );
        }
    }
}
