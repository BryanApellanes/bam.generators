using Bam.Console;
using Bam.Data.Schema;
using Bam.Generators.Tests.TestClasses;
using Bam.Data.Repositories;
using Bam.DependencyInjection;
using Bam.Test;
using Bam.Services;

namespace Bam.Generators.Tests.Unit
{
    [UnitTestMenu("SchemaGenerator Should", Selector = "sgs")]
    public class SchemaProviderShould : UnitTestMenuContainer
    {
        public SchemaProviderShould(ServiceRegistry serviceRegistry) : base(serviceRegistry)
        {
            Configure(svcRegistry =>
            {
                svcRegistry
                    .For<ISchemaTempPathProvider>().Use<SchemaTempPathProvider>()
                    .For<ITypeTableNameProvider>().Use<DaoSuffixTypeTableNameProvider>()
                    .For<SchemaProvider>().Use<SchemaProvider>();

                svcRegistry
                    .For<SchemaProvider>().Use(
                        new SchemaProvider(svcRegistry.Get<ITypeTableNameProvider>(), serviceRegistry.Get<ISchemaTempPathProvider>())
                    );
            });
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
    }
}
