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
            string configPath = RuntimeSettings.GetEntryAssemblyDirectoryFilePathFor("DaoRepoGenerationConfig.yaml");
            DaoRepoGenerationConfig config = DaoRepoGenerationConfig.ReadFrom(configPath);
            HandlebarsSchemaRepositoryGenerator generator = new HandlebarsSchemaRepositoryGenerator(config, new ConsoleLogger());
            generator.GenerateSource();
        }
    }
}
