using Bam.Console;
using Bam.DependencyInjection;
using Bam.Services;
using Bam.Test;

namespace Bam.Generators.Tests.Unit
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
            When.A<HandlebarsSchemaRepositoryGenerator>("generates source",
                () =>
                {
                    string configPath = RuntimeSettings.GetEntryAssemblyDirectoryFilePathFor("DaoRepoGenerationConfig.yaml");
                    DaoRepoGenerationConfig config = DaoRepoGenerationConfig.ReadFrom(configPath);
                    return new HandlebarsSchemaRepositoryGenerator(config, new ConsoleLogger());
                },
                (generator) =>
                {
                    generator.GenerateSource();
                    return generator;
                })
            .TheTest
            .ShouldPass(because =>
            {
                because.TheResult.IsNotNull();
            })
            .SoBeHappy()
            .UnlessItFailed();
        }
    }
}
