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

        [UnitTest]
        public void ShouldAnnotateNullableMembersInGeneratedSource()
        {
            When.A<HandlebarsSchemaRepositoryGenerator>("generates source for types with a foreign key and child collections",
                () =>
                {
                    string configPath = RuntimeSettings.GetEntryAssemblyDirectoryFilePathFor("DaoRepoGenerationConfig.yaml");
                    DaoRepoGenerationConfig config = DaoRepoGenerationConfig.ReadFrom(configPath);
                    return new HandlebarsSchemaRepositoryGenerator(config, new ConsoleLogger());
                },
                (generator) =>
                {
                    DirectoryInfo writeTo = new DirectoryInfo(Path.Combine(Path.GetTempPath(), $"bam-generators-nullable-{Guid.NewGuid():N}"));
                    try
                    {
                        generator.GenerateSource(writeTo.FullName);
                        FileInfo[] files = writeTo.GetFiles("*.cs", SearchOption.AllDirectories);
                        string source = string.Join(Environment.NewLine, files.Select(file => File.ReadAllText(file.FullName)));
                        return new GeneratedSourceOutcome(files.Length, source);
                    }
                    finally
                    {
                        // Reading and deleting right after generation also pins that the generator closed
                        // every file it wrote (bam.data.repositories#9).
                        writeTo.Refresh();
                        if (writeTo.Exists)
                        {
                            writeTo.Delete(true);
                        }
                    }
                })
            .TheTest
            .ShouldPass<GeneratedSourceOutcome>((because, _, outcome) =>
            {
                string source = outcome.Source;
                because.ItsTrue($"source files were generated ({outcome.FileCount})", outcome.FileCount > 0);
                because.ItsTrue("GetOne...Where returns a nullable type", source.Contains("TestCar? GetOneTestCarWhere("));
                because.ItsTrue("One...Where returns a nullable type", source.Contains("TestCar? OneTestCarWhere("));
                because.ItsTrue("no One...Where method returns a non-nullable type",
                    !System.Text.RegularExpressions.Regex.IsMatch(source, @"public [\w\.]+ (Get)?One\w+Where\("));
                because.ItsTrue("the dao foreign key backing field is nullable", source.Contains("TestPerson? _testPersonOfTestPersonId;"));
                because.ItsTrue("the wrapper parent backing field is nullable", source.Contains("TestPerson? _testPerson;"));
                because.ItsTrue("the wrapper parent is read with a nullable cast", source.Contains("TestPerson?)DaoRepository.GetParentPropertyOfChild("));
                because.ItsTrue("the wrapper child collection backing field is nullable", source.Contains("TestCar>? _testCars;"));
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        private sealed record GeneratedSourceOutcome(int FileCount, string Source);
    }
}
