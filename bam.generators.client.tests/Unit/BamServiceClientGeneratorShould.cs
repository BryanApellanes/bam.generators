using Bam.DependencyInjection;
using Bam.Generators.Client.Tests.Fixtures;
using Bam.Test;

namespace Bam.Generators.Client.Tests.Unit
{
    [UnitTestMenu("BamServiceClientGenerator should", Selector = "bscgs")]
    public class BamServiceClientGeneratorShould : UnitTestMenuContainer
    {
        public BamServiceClientGeneratorShould(ServiceRegistry serviceRegistry) : base(serviceRegistry)
        {
        }

        [UnitTest]
        public void ReturnSourceForAServiceType()
        {
            string source = string.Empty;
            When.A<BamServiceClientGenerator>("gets source for a service type",
                () => new BamServiceClientGenerator(),
                (generator) =>
                {
                    source = generator.GetSource(typeof(EchoService), GenerationMode.Subclass);
                    return generator;
                })
            .TheTest
            .ShouldPass(because =>
            {
                because.ItsTrue("the source is not empty", () => !string.IsNullOrWhiteSpace(source));
                because.ItsTrue("the source declares the client class", () => source.Contains("class EchoServiceClient"));
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest]
        public void WriteAClientSourceFileToDisk()
        {
            string outputDir = Path.Combine(Path.GetTempPath(), "bgc-" + Guid.NewGuid().ToString("N"));
            string expectedFile = Path.Combine(outputDir, "EchoServiceClient.cs");
            string written = string.Empty;

            When.A<BamServiceClientGenerator>("writes a client source file",
                () => new BamServiceClientGenerator().AddServiceType(typeof(EchoService), GenerationMode.Subclass),
                (generator) =>
                {
                    generator.WriteSource(outputDir);
                    if (File.Exists(expectedFile))
                    {
                        written = File.ReadAllText(expectedFile);
                    }
                    return generator;
                })
            .TheTest
            .ShouldPass(because =>
            {
                because.ItsTrue("the expected file was created", () => File.Exists(expectedFile));
                because.ItsTrue("the file contains the generated client", () => written.Contains("class EchoServiceClient"));
            })
            .SoBeHappy()
            .UnlessItFailed();
        }
    }
}
