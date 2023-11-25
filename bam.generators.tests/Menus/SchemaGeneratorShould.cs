using Bam.Data.Schema;
using Bam.Generators.Tests.TestClasses;
using Bam.Net;
using Bam.Net.CommandLine;
using Bam.Net.CoreServices;
using Bam.Net.Data.Repositories;
using Bam.Testing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bam.Generators.Tests
{
    [UnitTestMenu("SchemaGenerator Should", Selector = "sgs")]
    public class SchemaGeneratorShould : UnitTestMenuContainer
    {
        public SchemaGeneratorShould(ServiceRegistry serviceRegistry) : base(serviceRegistry)
        {
        }

        [Test]
        public void ShouldGenerateTypeSchema()
        {
            string testName = 32.RandomLetters();
            SchemaGenerator schemaGenerator = Get<SchemaGenerator>();
            TypeSchema typeSchema = schemaGenerator.CreateTypeSchema(testName, typeof(TestPerson));
            typeSchema.Name.ShouldEqual(testName);
            typeSchema.Tables.Count.ShouldBeEqualTo(3);

            Message.PrintLine(typeSchema.ToString(), ConsoleColor.DarkYellow);
        }

        public override ServiceRegistry Configure(ServiceRegistry serviceRegistry)
        {
            serviceRegistry = serviceRegistry
                .For<ITypeSchemaTempPathProvider>().Use<TypeSchemaTempPathProvider>()
                .For<ITypeTableNameProvider>().Use<DaoSuffixTypeTableNameProvider>()
                .For<SchemaGenerator>().Use<SchemaGenerator>();

            return serviceRegistry
                .For<SchemaGenerator>().Use(
                    new SchemaGenerator(serviceRegistry.Get<ITypeTableNameProvider>(), serviceRegistry.Get<ITypeSchemaTempPathProvider>())
                );
        }
    }
}
