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
                        new SchemaProvider(svcRegistry.Get<ITypeTableNameProvider>(), svcRegistry.Get<ISchemaTempPathProvider>())
                    );
            });
        }

        [Test]
        public void GenerateTypeSchema()
        {
            string testName = 32.RandomLetters();

            When.A<SchemaProvider>("creates a TypeSchema",
                () => Get<SchemaProvider>(),
                (schemaGenerator) => schemaGenerator.CreateTypeSchema(testName, typeof(TestPerson)))
            .TheTest
            .ShouldPass(because =>
            {
                because.TheResult.IsNotNull()
                    .As<TypeSchema>("Name equals test name", ts => testName.Equals(ts?.Name))
                    .As<TypeSchema>("has 3 tables", ts => ts?.Tables.Count == 3);
            })
            .SoBeHappy()
            .UnlessItFailed();
        }
    }
}
