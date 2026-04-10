using Bam.DependencyInjection;
using Bam.Test;
using Bam.Generators.Ux;

namespace Bam.Generators.Ux.Tests.Unit
{
    [UnitTestMenu("UxGenerationConfig should", Selector = "ugcut")]
    public class UxGenerationConfigShould : UnitTestMenuContainer
    {
        public UxGenerationConfigShould(ServiceRegistry serviceRegistry) : base(serviceRegistry)
        {
        }

        [UnitTest]
        public void ReturnDefaultsWhenFileDoesNotExist()
        {
            When.A<UxGenerationConfig>("returns defaults for missing file",
                () => UxGenerationConfig.ReadFile("nonexistent.json"),
                (config) => config)
            .TheTest
            .ShouldPass(because =>
            {
                UxGenerationConfig config = (UxGenerationConfig)because.Result;
                because.ItsTrue("template path defaults to .", config.TemplatePath == ".");
                because.ItsTrue("output directory defaults to ./output", config.OutputDirectory == "./output");
                because.ItsTrue("site model path defaults to ./site.json", config.SiteModelPath == "./site.json");
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest]
        public void RoundTripWriteAndRead()
        {
            string tempFile = Path.Combine(Path.GetTempPath(), $"bam-ux-config-{Guid.NewGuid():N}.json");

            When.A<UxGenerationConfig>("writes and reads config file",
                () =>
                {
                    var config = new UxGenerationConfig
                    {
                        TemplatePath = "/custom/templates",
                        OutputDirectory = "/custom/output",
                        SiteModelPath = "/custom/model.json"
                    };
                    config.WriteFile(tempFile);
                    return UxGenerationConfig.ReadFile(tempFile);
                },
                (config) => config)
            .TheTest
            .ShouldPass(because =>
            {
                UxGenerationConfig config = (UxGenerationConfig)because.Result;
                because.ItsTrue("template path round trips", config.TemplatePath == "/custom/templates");
                because.ItsTrue("output directory round trips", config.OutputDirectory == "/custom/output");
                because.ItsTrue("site model path round trips", config.SiteModelPath == "/custom/model.json");

                if (File.Exists(tempFile)) File.Delete(tempFile);
            })
            .SoBeHappy()
            .UnlessItFailed();
        }
    }
}
